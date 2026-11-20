using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine; // Cinemachine 3 (Unity 6)
using SpearWander.Abilities;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Configurazione Iniziale")]
    //[SerializeField] private string firstRoomSceneName = "Stanza_01_Tutorial";
    //[SerializeField] private string firstRoomDoorID = "Spawn_Iniziale";

    [Header("Telecamera")]
    [SerializeField] private CinemachineConfiner2D cameraConfiner;
    [SerializeField] private CameraManager cameraManager;

    [Header("Interfaccia Transizione (Game Feel)")]
    [SerializeField] private CanvasGroup transitionCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.4f;

    private string _currentRoomScene;
    private RoomDefinition _currentRoomDefinition;
    private string _targetDoorID;
    private bool _isTransitioning;

    // Narrow test-only hook. It is null in production and never changes a
    // result unless a test installs a delegate that throws for a given stage.
    private static Action<string> _connectedTransitionFaultInjector = null;

    private const float MAX_DELTA_TIME = 0.033f;

    private sealed class TransitionStepResult
    {
        public bool Failed { get; private set; }
        public string Error { get; private set; }

        public void Reset()
        {
            Failed = false;
            Error = null;
        }

        public void Fail(Exception exception)
        {
            Failed = true;
            Error = exception != null ? exception.Message : "Unknown failure.";
        }
    }

    private sealed class ConnectedTransitionSnapshot
    {
        public Scene SourceScene;
        public Scene PreviousActiveScene;
        public string CurrentRoomScene;
        public RoomDefinition CurrentRoomDefinition;
        public string TargetDoorId;
        public float TimeScale;
        public float FadeAlpha;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        public Player Player;
        public bool HadTransitionControlRequest;
        public Rigidbody2D PlayerRigidbody;
        public RigidbodyType2D RigidbodyBodyType;
        public Vector2 RigidbodyVelocity;
        public float RigidbodyAngularVelocity;
        public bool RigidbodySimulated;
        public RigidbodyConstraints2D RigidbodyConstraints;
        public Collider2D[] PlayerColliders;
        public bool[] PlayerColliderEnabled;
        public Collider2D PreviousCameraBounds;
        public CameraManager.CameraStateSnapshot CameraManagerState;
        public bool HasCameraManagerState;
        public Transform CameraTransform;
        public Vector3 CameraPosition;
        public Quaternion CameraRotation;
    }

    public string CurrentRoom => _currentRoomScene;
    public RoomDefinition CurrentRoomDefinition => _currentRoomDefinition;
    public string CurrentRoomId => _currentRoomDefinition != null ? _currentRoomDefinition.RoomId : null;

    /// <summary>
    /// Resolves the connection assigned to a source door without starting a
    /// scene transition or changing current-room/player state.
    /// </summary>
    public RoomConnectionResolutionResult ResolveConnectedRoomTransition(RoomTransition sourceDoor)
    {
        return ResolveConnectedRoomTransitionWithCatalogLoader(
            sourceDoor,
            RuntimeRoomCatalog.LoadDefault);
    }

    private static RoomConnectionResolutionResult ResolveConnectedRoomTransitionWithCatalogLoader(
        RoomTransition sourceDoor,
        Func<RuntimeRoomCatalog> catalogLoader)
    {
        if (sourceDoor == null)
            return RoomConnectionResolver.Resolve(null, null);

        RuntimeRoomCatalog catalog;
        try
        {
            catalog = catalogLoader != null ? catalogLoader() : null;
        }
        catch (InvalidOperationException exception)
        {
            return new RoomConnectionResolutionResult(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                "RuntimeRoomCatalog could not be loaded: " + exception.Message);
        }

        return RoomConnectionResolver.Resolve(sourceDoor, catalog);
    }

    /// <summary>
    /// Routes a door request to the connected transaction when it owns a
    /// RoomConnection; connection-less doors retain the legacy route.
    /// </summary>
    public void RequestRoomTransition(RoomTransition sourceDoor)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("[GameSceneManager] Room transition request rejected; another transition is already in progress.", this);
            return;
        }

        if (sourceDoor == null)
        {
            Debug.LogError("[GameSceneManager] Room transition request has no source door.", this);
            return;
        }

        if (UsesConnectedTransition(sourceDoor))
        {
            StartCoroutine(ConnectedTransitionRoutine(sourceDoor));
            return;
        }
    }

    private static bool UsesConnectedTransition(RoomTransition sourceDoor)
    {
        return sourceDoor != null && sourceDoor.Connection != null;
    }

    private IEnumerator ConnectedTransitionRoutine(RoomTransition sourceDoor)
    {
        _isTransitioning = true;

        // Captured before any mutation so the synchronous cleanup path can
        // restore the original simulation speed even if snapshot capture fails.
        float originalTimeScale = Time.timeScale;

        ConnectedTransitionSnapshot snapshot = null;
        RoomConnectionResolutionResult resolution = default;
        Scene sourceScene = default;
        Scene destinationScene = default;
        string destinationScenePath = null;
        bool destinationLoadStarted = false;
        bool committed = false;
        string failedStage = "source validation";
        string failureReason = null;

        // Exceptions cannot be caught around a yield in a C# iterator. Each
        // synchronous stage is therefore guarded locally, and this outer
        // try/finally provides a final synchronous cleanup path for every exit
        // (completion, yield break, or an exception that reaches the iterator).
        // Unity does not run the finally block if this coroutine is forcibly
        // stopped with StopCoroutine/StopAllCoroutines or if the manager is
        // destroyed; that limitation cannot be avoided from managed code.
        try
        {
            try
            {
                InjectConnectedTransitionFault("source-validation");
                resolution = ResolveConnectedRoomTransition(sourceDoor);
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason == null &&
                (resolution.Status != RoomConnectionResolutionStatus.DestinationValidationPending ||
                 resolution.DestinationRoom == null ||
                 string.IsNullOrEmpty(resolution.DestinationScenePath) ||
                 string.IsNullOrEmpty(resolution.DestinationDoorId)))
            {
                failureReason = $"{resolution.Status}: {resolution.Message}";
            }

            if (failureReason == null)
            {
                sourceScene = sourceDoor.gameObject.scene;
                if (!sourceScene.IsValid() || !sourceScene.isLoaded ||
                    !IsCurrentSourceRoom(sourceScene, resolution))
                {
                    failureReason =
                        $"door '{resolution.SourceDoorId}' is not in the manager's loaded current room.";
                }
            }

            if (failureReason != null)
            {
                Debug.LogError(
                    $"[GameSceneManager] Connected transition failed at source validation: " +
                    failureReason, this);
                yield break;
            }

            destinationScenePath = resolution.DestinationScenePath;

            try
            {
                InjectConnectedTransitionFault("snapshot-capture");
                snapshot = CaptureConnectedTransitionSnapshot(sourceScene);
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (snapshot == null)
            {
                if (failureReason == null)
                    failureReason = "player or source state is unavailable.";

                Debug.LogError(
                    $"[GameSceneManager] Connected transition failed at source validation: " +
                    failureReason, this);
                yield break;
            }

            failedStage = "freeze/control setup";
            try
            {
                InjectConnectedTransitionFault("freeze-control-setup");

                // Keep room simulation from advancing while two room scenes overlap.
                Time.timeScale = 0f;
                SetConnectedPlayerControl(snapshot, false);
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            failedStage = "fade-out";
            TransitionStepResult fadeOutResult = new TransitionStepResult();
            yield return StartCoroutine(FadeToAlphaGuarded(
                1f, "fade-out", fadeOutResult, fadeOutDuration));
            if (fadeOutResult.Failed)
                failureReason = fadeOutResult.Error;

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            failedStage = "destination load";
            AsyncOperation loadOperation = null;
            try
            {
                if (HasLoadedScenePath(destinationScenePath))
                {
                    failureReason = $"Destination scene '{destinationScenePath}' is already loaded.";
                }
                else
                {
                    InjectConnectedTransitionFault("destination-load-start");
                    destinationLoadStarted = true;
                    if (!TryBeginAdditiveLoad(
                            destinationScenePath,
                            out loadOperation,
                            out failureReason))
                    {
                        // The source is intentionally kept loaded until commit.
                    }
                }
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason == null && loadOperation != null)
            {
                bool loadWaitFailed = false;
                while (true)
                {
                    bool isDone = false;
                    try
                    {
                        isDone = loadOperation.isDone;
                    }
                    catch (Exception exception)
                    {
                        failureReason = exception.Message;
                        loadWaitFailed = true;
                    }

                    if (loadWaitFailed || isDone)
                        break;

                    yield return null;
                }
            }

            if (failureReason == null)
            {
                try
                {
                    if (!TryGetUniqueLoadedScene(
                            destinationScenePath,
                            out destinationScene,
                            out string sceneError))
                    {
                        failureReason = sceneError;
                    }
                }
                catch (Exception exception)
                {
                    failureReason = exception.Message;
                }
            }

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            failedStage = "destination validation";
            Transform spawnPoint = null;
            try
            {
                InjectConnectedTransitionFault("destination-validation");
                if (!TryValidateConnectedDestination(
                        destinationScene,
                        resolution,
                        out _,
                        out spawnPoint,
                        out failureReason))
                {
                    // failureReason was populated by the validation helper.
                }
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            failedStage = "player relocation";
            try
            {
                InjectConnectedTransitionFault("player-relocation-before-mutation");
                playerTransform.position = spawnPoint.position;
                InjectConnectedTransitionFault("player-relocation-after-mutation");

                if (snapshot.PlayerRigidbody != null)
                {
                    snapshot.PlayerRigidbody.bodyType = snapshot.RigidbodyBodyType;
                    snapshot.PlayerRigidbody.linearVelocity = Vector2.zero;
                    snapshot.PlayerRigidbody.angularVelocity = snapshot.RigidbodyAngularVelocity;
                }

                Physics2D.SyncTransforms();
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            failedStage = "destination activation and camera setup";
            try
            {
                InjectConnectedTransitionFault("destination-activation");
                if (!SceneManager.SetActiveScene(destinationScene))
                {
                    failureReason =
                        $"Unity refused to make destination scene '{destinationScene.path}' active.";
                }
                else
                {
                    InjectConnectedTransitionFault("destination-camera-setup");
                    if (!TrySetConnectedCameraBounds(destinationScene, out failureReason))
                    {
                        // Keep the source room and restore the prior camera state.
                    }
                    else
                    {
                        PositionCameraAtPlayer();
                    }
                }
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            // Last pre-commit checkpoint. A failure here exercises rollback
            // after relocation, destination activation and camera assignment.
            failedStage = "pre-commit";
            try
            {
                InjectConnectedTransitionFault("pre-commit");
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
            }

            if (failureReason != null)
            {
                yield return StartCoroutine(RollbackConnectedTransition(
                    snapshot,
                    destinationScene,
                    destinationScenePath,
                    destinationLoadStarted,
                    failedStage,
                    failureReason));
                yield break;
            }

            // Commit boundary: after this point the destination room is
            // authoritative and rollback must never run.
            _currentRoomScene = destinationScene.name;
            _currentRoomDefinition = resolution.DestinationRoom;
            committed = true;

            PostCommitSourceUnload(sourceScene, out bool sourceUnloadFailed, out string unloadError,
                out AsyncOperation unloadOperation);

            if (sourceUnloadFailed && unloadError != null)
                Debug.LogError(unloadError, this);

            if (unloadOperation != null)
            {
                try
                {
                    InjectConnectedTransitionFault("post-commit-source-unload-wait");
                }
                catch (Exception exception)
                {
                    sourceUnloadFailed = true;
                    Debug.LogError(
                        "[GameSceneManager] Connected transition committed, but source unload failed " +
                        $"after the unload operation started: {exception.Message}", this);
                }

                bool waitFailed = false;
                while (true)
                {
                    bool isDone = false;
                    try
                    {
                        isDone = unloadOperation.isDone;
                    }
                    catch (Exception exception)
                    {
                        waitFailed = true;
                        sourceUnloadFailed = true;
                        Debug.LogError(
                            "[GameSceneManager] Connected transition committed, but source unload failed " +
                            $"while waiting for the operation: {exception.Message}", this);
                    }

                    if (waitFailed || isDone)
                        break;

                    yield return null;
                }
            }

            if (unloadOperation == null && !sourceUnloadFailed &&
                sourceScene.IsValid() && sourceScene.isLoaded)
            {
                Debug.LogError(
                    "[GameSceneManager] Connected transition committed, but source scene " +
                    $"'{sourceScene.path}' could not be unloaded.", this);
                sourceUnloadFailed = true;
            }
            else if (unloadOperation != null && !sourceUnloadFailed &&
                     sourceScene.IsValid() && sourceScene.isLoaded)
            {
                Debug.LogError(
                    "[GameSceneManager] Connected transition committed, but source scene " +
                    $"'{sourceScene.path}' remained loaded after the unload operation completed.", this);
                sourceUnloadFailed = true;
            }

            failedStage = "fade-in";
            TransitionStepResult fadeInResult = new TransitionStepResult();
            yield return StartCoroutine(FadeToAlphaGuarded(
                0f, "post-commit-fade-in", fadeInResult, fadeInDuration));
            if (fadeInResult.Failed)
            {
                Debug.LogError(
                    "[GameSceneManager] Connected transition committed, but fade-in failed: " +
                    fadeInResult.Error, this);
            }

            try
            {
                InjectConnectedTransitionFault("post-commit-control-restore");
                SetConnectedPlayerControl(snapshot, true);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[GameSceneManager] Connected transition committed, but player control " +
                    $"restoration failed: {exception.Message}", this);
            }

            try
            {
                InjectConnectedTransitionFault("post-commit-timescale-restore");
                Time.timeScale = snapshot.TimeScale;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[GameSceneManager] Connected transition committed, but Time.timeScale " +
                    $"restoration failed: {exception.Message}", this);
            }

            if (sourceUnloadFailed && sourceScene.IsValid() && sourceScene.isLoaded)
            {
                Debug.LogWarning(
                    "[GameSceneManager] Connected transition committed with the source scene still " +
                    "loaded; two room instances may be active.", this);
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }
        finally
        {
            // Final synchronous cleanup path. Every step is best-effort and
            // independent so the transition lock is always released.
            if (snapshot != null && snapshot.Player != null &&
                !snapshot.HadTransitionControlRequest)
            {
                try
                {
                    snapshot.Player.RemoveControlRequest(Player.ControlReason.Transition);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[GameSceneManager] Connected transition {(committed ? "post-commit" : "pre-commit")} " +
                        $"cleanup could not restore player control: {exception.Message}", this);
                }
            }

            if (snapshot != null && snapshot.PlayerRigidbody != null)
            {
                try
                {
                    if (snapshot.PlayerRigidbody.bodyType == RigidbodyType2D.Kinematic &&
                        snapshot.RigidbodyBodyType != RigidbodyType2D.Kinematic)
                    {
                        snapshot.PlayerRigidbody.bodyType = snapshot.RigidbodyBodyType;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[GameSceneManager] Connected transition {(committed ? "post-commit" : "pre-commit")} " +
                        $"cleanup could not restore the player body type: {exception.Message}", this);
                }
            }

            try
            {
                Time.timeScale = snapshot != null ? snapshot.TimeScale : originalTimeScale;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[GameSceneManager] Connected transition {(committed ? "post-commit" : "pre-commit")} " +
                    $"cleanup could not restore Time.timeScale: {exception.Message}", this);
            }

            _isTransitioning = false;
        }
    }

    private static void InjectConnectedTransitionFault(string stage)
    {
        Action<string> injector = _connectedTransitionFaultInjector;
        if (injector != null)
            injector(stage);
    }

    /// <summary>
    /// Starts the source unload after commit. A null operation is reported as a
    /// failure; an operation that has started is awaited by the caller so that
    /// a post-start failure can be detected by checking the scene afterwards.
    /// </summary>
    private void PostCommitSourceUnload(
        Scene sourceScene,
        out bool failed,
        out string error,
        out AsyncOperation operation)
    {
        failed = false;
        error = null;
        operation = null;

        try
        {
            InjectConnectedTransitionFault("post-commit-source-unload");
            operation = SceneManager.UnloadSceneAsync(sourceScene);
        }
        catch (Exception exception)
        {
            failed = true;
            error =
                "[GameSceneManager] Connected transition committed, but source unload failed: " +
                exception.Message;
            return;
        }

        if (operation == null)
        {
            failed = true;
            error =
                "[GameSceneManager] Connected transition committed, but source scene " +
                $"'{sourceScene.path}' could not be unloaded.";
        }
    }

    private bool IsCurrentSourceRoom(Scene sourceScene, RoomConnectionResolutionResult resolution)
    {
        if (string.IsNullOrEmpty(_currentRoomScene) || sourceScene.name != _currentRoomScene)
            return false;

        return _currentRoomDefinition == null ||
               string.Equals(_currentRoomDefinition.RoomId, resolution.SourceRoom.RoomId, StringComparison.Ordinal);
    }

    private ConnectedTransitionSnapshot CaptureConnectedTransitionSnapshot(Scene sourceScene)
    {
        if (playerTransform == null)
            return null;

        Player player = playerTransform.GetComponent<Player>();
        if (player == null)
            return null;

        ConnectedTransitionSnapshot snapshot = new ConnectedTransitionSnapshot
        {
            SourceScene = sourceScene,
            PreviousActiveScene = SceneManager.GetActiveScene(),
            CurrentRoomScene = _currentRoomScene,
            CurrentRoomDefinition = _currentRoomDefinition,
            TargetDoorId = _targetDoorID,
            TimeScale = Time.timeScale,
            FadeAlpha = transitionCanvasGroup != null ? transitionCanvasGroup.alpha : 0f,
            PlayerPosition = playerTransform.position,
            PlayerRotation = playerTransform.rotation,
            Player = player,
            HadTransitionControlRequest = player.HasControlRequest(Player.ControlReason.Transition),
            PlayerRigidbody = playerTransform.GetComponent<Rigidbody2D>(),
            PlayerColliders = playerTransform.GetComponentsInChildren<Collider2D>(includeInactive: true),
            PreviousCameraBounds = cameraConfiner != null ? cameraConfiner.BoundingShape2D : null
        };

        if (cameraManager != null)
        {
            snapshot.CameraManagerState = cameraManager.CaptureState();
            snapshot.HasCameraManagerState = true;
        }

        snapshot.PlayerColliderEnabled = new bool[snapshot.PlayerColliders.Length];
        for (int i = 0; i < snapshot.PlayerColliders.Length; i++)
            snapshot.PlayerColliderEnabled[i] = snapshot.PlayerColliders[i] != null && snapshot.PlayerColliders[i].enabled;

        if (snapshot.PlayerRigidbody != null)
        {
            snapshot.RigidbodyBodyType = snapshot.PlayerRigidbody.bodyType;
            snapshot.RigidbodyVelocity = snapshot.PlayerRigidbody.linearVelocity;
            snapshot.RigidbodyAngularVelocity = snapshot.PlayerRigidbody.angularVelocity;
            snapshot.RigidbodySimulated = snapshot.PlayerRigidbody.simulated;
            snapshot.RigidbodyConstraints = snapshot.PlayerRigidbody.constraints;
        }

        if (cameraConfiner != null)
        {
            CinemachineCamera camera = cameraConfiner.GetComponent<CinemachineCamera>();
            if (camera != null)
            {
                snapshot.CameraTransform = camera.transform;
                snapshot.CameraPosition = camera.transform.position;
                snapshot.CameraRotation = camera.transform.rotation;
            }
        }

        return snapshot;
    }

    private static bool TryBeginAdditiveLoad(string scenePath, out AsyncOperation operation, out string error)
    {
        operation = null;
        error = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (operation == null)
            {
                error = $"Unity could not start loading destination scene '{scenePath}'.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not load destination scene '{scenePath}': {exception.Message}";
            return false;
        }
    }

    private static bool HasLoadedScenePath(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TryGetUniqueLoadedScene(string scenePath, out Scene scene, out string error)
    {
        scene = default;
        error = null;
        int matches = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (candidate.isLoaded && string.Equals(candidate.path, scenePath, StringComparison.Ordinal))
            {
                scene = candidate;
                matches++;
            }
        }

        if (matches == 1)
            return true;

        error = matches == 0
            ? $"Destination scene '{scenePath}' did not become loaded."
            : $"Destination scene path '{scenePath}' is loaded {matches} times; a unique destination is required.";
        scene = default;
        return false;
    }

    private static bool TryValidateConnectedDestination(
        Scene destinationScene,
        RoomConnectionResolutionResult resolution,
        out RoomTransition destinationDoor,
        out Transform spawnPoint,
        out string error)
    {
        destinationDoor = null;
        spawnPoint = null;
        error = null;
        if (!destinationScene.IsValid() || !destinationScene.isLoaded ||
            (!string.IsNullOrEmpty(destinationScene.path) &&
             !string.Equals(destinationScene.path, resolution.DestinationScenePath, StringComparison.Ordinal)))
        {
            error = "The loaded destination scene is invalid or does not match the catalog scene path.";
            return false;
        }

        List<RoomTransition> doors = new List<RoomTransition>();
        foreach (GameObject root in destinationScene.GetRootGameObjects())
            doors.AddRange(root.GetComponentsInChildren<RoomTransition>(includeInactive: true));

        RoomConnectionResolutionResult validated = RoomConnectionResolver.ValidateDestinationBacklink(
            resolution,
            resolution.DestinationScenePath,
            doors);
        if (validated.Status != RoomConnectionResolutionStatus.Success)
        {
            error = $"{validated.Status}: {validated.Message}";
            return false;
        }

        int matches = 0;
        foreach (RoomTransition candidate in doors)
        {
            if (candidate != null && string.Equals(candidate.DoorId, resolution.DestinationDoorId, StringComparison.Ordinal))
            {
                destinationDoor = candidate;
                matches++;
            }
        }

        if (matches != 1 || destinationDoor == null)
        {
            error = $"Destination doorId '{resolution.DestinationDoorId}' must match exactly one door; found {matches}.";
            destinationDoor = null;
            return false;
        }

        spawnPoint = destinationDoor.SpawnPoint;
        if (spawnPoint == null || spawnPoint.gameObject.scene != destinationScene)
        {
            error = $"Destination door '{resolution.DestinationDoorId}' has no valid SpawnPoint in the destination scene.";
            spawnPoint = null;
            return false;
        }

        return true;
    }

    private bool TrySetConnectedCameraBounds(Scene destinationScene, out string error)
    {
        error = null;

        Collider2D bounds = null;
        int matches = 0;
        foreach (GameObject root in destinationScene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (!child.CompareTag("CameraBounds"))
                    continue;

                Collider2D candidate = child.GetComponent<Collider2D>();
                if (candidate == null)
                    continue;

                bounds = candidate;
                matches++;
            }
        }

        if (matches != 1)
        {
            error = $"Destination scene '{destinationScene.path}' must contain exactly one CameraBounds collider; found {matches}.";
            return false;
        }

        RoomCameraSettings roomSettings = bounds.GetComponent<RoomCameraSettings>();
        if (cameraManager != null)
        {
            bool applied = roomSettings != null
                ? cameraManager.ApplyRoomSettings(roomSettings)
                : cameraManager.SetCameraBounds(bounds);
            if (!applied)
            {
                error = $"Could not apply gameplay camera configuration for '{destinationScene.path}'.";
                return false;
            }

            return true;
        }

        if (cameraConfiner != null)
        {
            cameraConfiner.BoundingShape2D = bounds;
            cameraConfiner.InvalidateBoundingShapeCache();
        }

        return true;
    }

    private void PositionCameraAtPlayer()
    {
        if (playerTransform == null)
            return;

        if (cameraManager != null)
        {
            cameraManager.ForceFollowingCameraPosition(playerTransform.position);
            if (cameraConfiner != null)
                cameraConfiner.InvalidateBoundingShapeCache();
            return;
        }

        if (cameraConfiner == null)
            return;

        CinemachineCamera camera = cameraConfiner.GetComponent<CinemachineCamera>();
        if (camera == null)
            return;

        Vector3 targetPosition = new Vector3(
            playerTransform.position.x,
            playerTransform.position.y,
            camera.transform.position.z);
        camera.transform.position = targetPosition;
        camera.ForceCameraPosition(targetPosition, camera.transform.rotation);
        cameraConfiner.InvalidateBoundingShapeCache();
    }

    private void SetConnectedPlayerControl(ConnectedTransitionSnapshot snapshot, bool restoreMovement)
    {
        if (snapshot.PlayerRigidbody != null)
        {
            if (restoreMovement)
            {
                snapshot.PlayerRigidbody.bodyType = snapshot.RigidbodyBodyType;
                snapshot.PlayerRigidbody.linearVelocity = Vector2.zero;
                snapshot.PlayerRigidbody.angularVelocity = snapshot.RigidbodyAngularVelocity;
            }
            else
            {
                snapshot.PlayerRigidbody.linearVelocity = Vector2.zero;
                snapshot.PlayerRigidbody.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        if (!snapshot.HadTransitionControlRequest)
        {
            if (restoreMovement)
                snapshot.Player.RemoveControlRequest(Player.ControlReason.Transition);
            else
                snapshot.Player.AddControlRequest(Player.ControlReason.Transition);
        }
    }

    /// <summary>
    /// Restores the source room as far as possible. Every step is independently
    /// guarded: one failed restoration is logged with its step name and never
    /// prevents the remaining cleanup from running. The original failure was
    /// already logged by the caller and remains the primary diagnostic.
    /// </summary>
    private IEnumerator RollbackConnectedTransition(
        ConnectedTransitionSnapshot snapshot,
        Scene destinationScene,
        string destinationScenePath,
        bool destinationLoadStarted,
        string failedStage,
        string failureReason)
    {
        Debug.LogError($"[GameSceneManager] Connected transition rolled back at {failedStage}: {failureReason}", this);

        // A freeze/control setup failure can reach rollback before the
        // transition has paused the simulation. Pause immediately so physics
        // cannot continue changing the captured player state while cleanup
        // awaits scene unloads and fade restoration.
        Time.timeScale = 0f;

        TryRunRollbackStep(
            "room identity restoration",
            "rollback-room-identity",
            () =>
            {
                _currentRoomScene = snapshot.CurrentRoomScene;
                _currentRoomDefinition = snapshot.CurrentRoomDefinition;
                _targetDoorID = snapshot.TargetDoorId;
            });

        if (playerTransform != null)
        {
            TryRunRollbackStep(
                "player transform restoration",
                "rollback-player-transform",
                () => playerTransform.SetPositionAndRotation(
                    snapshot.PlayerPosition,
                    snapshot.PlayerRotation));

            TryRunRollbackStep(
                "collider restoration",
                "rollback-collider",
                () =>
                {
                    if (snapshot.PlayerColliders == null || snapshot.PlayerColliderEnabled == null)
                        return;

                    for (int i = 0; i < snapshot.PlayerColliders.Length; i++)
                    {
                        if (snapshot.PlayerColliders[i] != null)
                        {
                            snapshot.PlayerColliders[i].enabled =
                                snapshot.PlayerColliderEnabled[i];
                        }
                    }
                });
        }

        TryRunRollbackStep(
            "rigidbody restoration",
            "rollback-rigidbody",
            () => RestoreRigidbodySnapshot(snapshot));

        TryRunRollbackStep(
            "physics synchronization",
            "rollback-physics-sync",
            () => Physics2D.SyncTransforms());

        TryRunRollbackStep(
            "active scene restoration",
            "rollback-active-scene",
            () =>
            {
                if (snapshot.PreviousActiveScene.IsValid() &&
                    snapshot.PreviousActiveScene.isLoaded &&
                    SceneManager.GetActiveScene() != snapshot.PreviousActiveScene &&
                    !SceneManager.SetActiveScene(snapshot.PreviousActiveScene))
                {
                    throw new InvalidOperationException(
                        "Unity refused to restore the previous active scene.");
                }
            });

        TryRunRollbackStep(
            "camera bounds restoration",
            "rollback-camera",
            () => RestoreCameraBoundsSnapshot(snapshot));

        TryRunRollbackStep(
            "camera transform restoration",
            "rollback-camera-transform",
            () => RestoreCameraTransformSnapshot(snapshot));

        if (destinationLoadStarted)
        {
            yield return StartCoroutine(UnloadDestinationScenesForRollback(
                destinationScene,
                destinationScenePath,
                snapshot.SourceScene));
        }

        TransitionStepResult fadeResult = new TransitionStepResult();
        yield return StartCoroutine(FadeToAlphaGuarded(
            snapshot.FadeAlpha, "rollback-fade", fadeResult));
        if (fadeResult.Failed)
        {
            Debug.LogError(
                "[GameSceneManager] Rollback step failed (fade state restoration): " +
                fadeResult.Error, this);
        }

        TryRunRollbackStep(
            "player control restoration",
            "rollback-control",
            () =>
            {
                if (snapshot.Player != null && !snapshot.HadTransitionControlRequest)
                    snapshot.Player.RemoveControlRequest(Player.ControlReason.Transition);
            });

        TryRunRollbackStep(
            "time scale restoration",
            "rollback-timescale",
            () => Time.timeScale = snapshot.TimeScale);

        TryRunRollbackStep(
            "transition lock release",
            "rollback-transition-lock",
            () => _isTransitioning = false);
    }

    private bool TryRunRollbackStep(
        string stepName,
        string faultStage,
        Action step)
    {
        try
        {
            InjectConnectedTransitionFault(faultStage);
            step();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[GameSceneManager] Rollback step failed ({stepName}): {exception.Message}",
                this);
            return false;
        }
    }

    /// <summary>
    /// Unloads only loaded scenes that match the exact destination path (or the
    /// specific scene handle obtained for this transition). The source scene is
    /// never unloaded. Multiple destination instances are all cleaned up.
    /// </summary>
    private IEnumerator UnloadDestinationScenesForRollback(
        Scene destinationScene,
        string destinationScenePath,
        Scene sourceScene)
    {
        List<Scene> scenes = new List<Scene>();
        bool collectionFailed = false;
        try
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (!candidate.IsValid() || !candidate.isLoaded)
                    continue;

                if (sourceScene.IsValid() && candidate.handle == sourceScene.handle)
                    continue;

                bool exactPathMatch = !string.IsNullOrEmpty(destinationScenePath) &&
                    string.Equals(
                        candidate.path,
                        destinationScenePath,
                        StringComparison.Ordinal);
                bool primaryHandleMatch = destinationScene.IsValid() &&
                    candidate.handle == destinationScene.handle;

                if (exactPathMatch || primaryHandleMatch)
                    scenes.Add(candidate);
            }
        }
        catch (Exception exception)
        {
            collectionFailed = true;
            Debug.LogError(
                "[GameSceneManager] Rollback step failed (destination scene cleanup): " +
                $"could not enumerate loaded scenes: {exception.Message}", this);
        }

        if (collectionFailed)
            yield break;

        for (int i = 0; i < scenes.Count; i++)
        {
            Scene scene = scenes[i];
            AsyncOperation unloadOperation = null;
            try
            {
                InjectConnectedTransitionFault("rollback-destination-unload");
                unloadOperation = SceneManager.UnloadSceneAsync(scene);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[GameSceneManager] Rollback step failed (destination scene cleanup of " +
                    $"'{scene.path}'): {exception.Message}", this);
                continue;
            }

            if (unloadOperation == null)
            {
                Debug.LogError(
                    "[GameSceneManager] Rollback step failed (destination scene cleanup of " +
                    $"'{scene.path}'): Unity returned no unload operation.", this);
                continue;
            }

            bool waitFailed = false;
            while (true)
            {
                bool isDone = false;
                try
                {
                    isDone = unloadOperation.isDone;
                }
                catch (Exception exception)
                {
                    waitFailed = true;
                    Debug.LogError(
                        "[GameSceneManager] Rollback step failed (destination scene cleanup of " +
                        $"'{scene.path}'): the unload operation failed while being awaited: " +
                        exception.Message, this);
                }

                if (waitFailed || isDone)
                    break;

                yield return null;
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                Debug.LogError(
                    "[GameSceneManager] Rollback step failed (destination scene cleanup of " +
                    $"'{scene.path}'): the scene remained loaded.", this);
            }
        }
    }

    private IEnumerator FadeToAlphaGuarded(
        float targetAlpha,
        string faultStage,
        TransitionStepResult result,
        float durationOverride = -1f)
    {
        result.Reset();

        CanvasGroup group = null;
        float startAlpha = 0f;
        float duration = 0f;
        bool skip = false;
        bool failed = false;

        try
        {
            InjectConnectedTransitionFault(faultStage);
            group = transitionCanvasGroup;
            if (group == null)
            {
                skip = true;
            }
            else
            {
                startAlpha = group.alpha;
                duration = durationOverride >= 0f
                    ? durationOverride
                    : (targetAlpha > startAlpha ? fadeOutDuration : fadeInDuration);
                if (duration <= 0f)
                {
                    group.alpha = targetAlpha;
                    skip = true;
                }
            }
        }
        catch (Exception exception)
        {
            result.Fail(exception);
            failed = true;
        }

        if (failed || skip)
            yield break;

        float time = 0f;
        while (time < duration)
        {
            bool frameFailed = false;
            try
            {
                time += Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            }
            catch (Exception exception)
            {
                result.Fail(exception);
                frameFailed = true;
            }

            if (frameFailed)
                yield break;

            yield return null;
        }

        try
        {
            group.alpha = targetAlpha;
        }
        catch (Exception exception)
        {
            result.Fail(exception);
        }
    }

    private static void RestoreRigidbodySnapshot(ConnectedTransitionSnapshot snapshot)
    {
        Rigidbody2D rb = snapshot.PlayerRigidbody;
        if (rb == null)
            return;

        rb.bodyType = snapshot.RigidbodyBodyType;
        rb.constraints = snapshot.RigidbodyConstraints;
        rb.simulated = snapshot.RigidbodySimulated;
        rb.linearVelocity = snapshot.RigidbodyVelocity;
        rb.angularVelocity = snapshot.RigidbodyAngularVelocity;
    }

    private void RestoreCameraBoundsSnapshot(ConnectedTransitionSnapshot snapshot)
    {
        if (cameraManager != null && snapshot.HasCameraManagerState)
        {
            cameraManager.RestoreState(snapshot.CameraManagerState);
            return;
        }

        if (cameraConfiner == null)
            return;

        cameraConfiner.BoundingShape2D = snapshot.PreviousCameraBounds;
        cameraConfiner.InvalidateBoundingShapeCache();
    }

    private void RestoreCameraTransformSnapshot(ConnectedTransitionSnapshot snapshot)
    {
        if (snapshot.CameraTransform == null)
            return;

        snapshot.CameraTransform.SetPositionAndRotation(
            snapshot.CameraPosition,
            snapshot.CameraRotation);
        CinemachineCamera camera = snapshot.CameraTransform.GetComponent<CinemachineCamera>();
        if (camera != null)
            camera.ForceCameraPosition(snapshot.CameraPosition, snapshot.CameraRotation);
        if (cameraConfiner != null)
            cameraConfiner.InvalidateBoundingShapeCache();
    }

    private IEnumerator FadeToAlpha(float targetAlpha)
    {
        if (transitionCanvasGroup == null)
            yield break;

        float startAlpha = transitionCanvasGroup.alpha;
        float duration = targetAlpha > startAlpha ? fadeOutDuration : fadeInDuration;
        if (duration <= 0f)
        {
            transitionCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }
        transitionCanvasGroup.alpha = targetAlpha;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (transitionCanvasGroup != null) transitionCanvasGroup.alpha = 0f;

        // After DontDestroyOnLoad, gameObject.scene is "DontDestroyOnLoad".
        // Check by scene count: if only 1 real scene (non-DontDestroyOnLoad), load the initial room.
        int realSceneCount = 0;
        Scene realScene = default;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "DontDestroyOnLoad")
            {
                realSceneCount++;
                realScene = s;
            }
        }

        if (realSceneCount > 1)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.name != "DontDestroyOnLoad" && s != SceneManager.GetActiveScene())
                {
                    _currentRoomScene = s.name;
                    _currentRoomDefinition = ResolveRoomDefinition(s);
                    SceneManager.SetActiveScene(s);
                    break;
                }
            }
        }
        else if (realSceneCount == 1)
        {
            if (realScene.name != "Master")
            {
                _currentRoomScene = realScene.name;
                _currentRoomDefinition = ResolveRoomDefinition(realScene);
            }
            //else
                //LoadInitialRoom(firstRoomSceneName, firstRoomDoorID);
        }
    }

    public void TransitionToRoom(string sceneName, string targetDoorID)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[GameSceneManager] Transizione rifiutata. Una transizione è già in corso!");
            return;
        }
        StartCoroutine(TransitionRoutine(sceneName, targetDoorID));
    }

    private IEnumerator TransitionRoutine(string sceneName, string targetDoorID)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameSceneManager] TransitionToRoom called with an empty scene name.");
            yield break;
        }

        _isTransitioning = true;
        _targetDoorID = targetDoorID;

        SetPlayerControl(false);

        yield return StartCoroutine(FadeToBlack());

        if (!string.IsNullOrEmpty(_currentRoomScene))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(_currentRoomScene);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone) yield return null;
            }
        }

        _currentRoomScene = sceneName;
        _currentRoomDefinition = null;
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(_currentRoomScene, LoadSceneMode.Additive);

        if (loadOp == null)
        {
            Debug.LogError($"[GameSceneManager] Could not load room scene '{_currentRoomScene}'. Is it added to the build settings?");
            SetPlayerControl(true);
            _isTransitioning = false;
            yield break;
        }

        while (!loadOp.isDone) yield return null;

        Scene newScene = SceneManager.GetSceneByName(_currentRoomScene);
        SceneManager.SetActiveScene(newScene);
        _currentRoomDefinition = ResolveRoomDefinition(newScene);

        Physics2D.SyncTransforms();
        yield return null;

        FocusTargetDoor();
        UpdateCameraBounds();

        PositionCameraAtPlayer();

        yield return null;
        yield return null;
        yield return null;

        yield return StartCoroutine(FadeFromBlack());

        SetPlayerControl(true);

        yield return new WaitForSecondsRealtime(0.1f);

        _isTransitioning = false;
    }

    public void ReloadCurrentRoom(bool resetState = true, Vector3? overrideSpawn = null)
    {
        if (_isTransitioning) return;
        if (string.IsNullOrEmpty(_currentRoomScene)) return;

        string targetScene = _currentRoomScene;

        if (!overrideSpawn.HasValue &&
            CheckpointManager.Instance != null &&
            CheckpointManager.Instance.HasCheckpoint)
        {
            var (room, pos) = CheckpointManager.Instance.GetCheckpoint();
            overrideSpawn = pos;
            targetScene = room;
        }

        if (!overrideSpawn.HasValue)
            //targetScene = firstRoomSceneName;

        StartCoroutine(ReloadRoomRoutine(targetScene, overrideSpawn, resetState));
    }

    private IEnumerator ReloadRoomRoutine(string targetScene, Vector3? overrideSpawn = null, bool resetState = true)
    {
        _isTransitioning = true;

        Player player = playerTransform.GetComponent<Player>();
        if (resetState && player != null) player.ResetState();

        SetPlayerControl(false);

        yield return StartCoroutine(FadeToBlack());

        if (!string.IsNullOrEmpty(_currentRoomScene))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(_currentRoomScene);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone) yield return null;
            }
        }

        _currentRoomScene = targetScene;
        _currentRoomDefinition = null;
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(_currentRoomScene, LoadSceneMode.Additive);

        if (loadOp == null)
        {
            Debug.LogError($"[GameSceneManager] Could not load room scene '{_currentRoomScene}'. Is it added to the build settings?");
            SetPlayerControl(true);
            _isTransitioning = false;
            yield break;
        }

        while (!loadOp.isDone) yield return null;

        Scene newScene = SceneManager.GetSceneByName(_currentRoomScene);
        SceneManager.SetActiveScene(newScene);
        _currentRoomDefinition = ResolveRoomDefinition(newScene);

        Physics2D.SyncTransforms();
        yield return null;

        if (overrideSpawn.HasValue)
        {
            playerTransform.position = overrideSpawn.Value;
            Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        else
        {
            FocusSpawnDoor();
        }
        UpdateCameraBounds();

        PositionCameraAtPlayer();

        yield return null;
        yield return null;
        yield return null;

        if (player != null) player.RemoveControlRequest(Player.ControlReason.Death);

        // Restore abilities after scene reload when the player state was not reset.
        if (!resetState && player != null && AbilityManager.Instance != null)
        {
            AbilityManager.Instance.RestoreAbilities(player);
        }

        yield return StartCoroutine(FadeFromBlack());

        SetPlayerControl(true);

        yield return new WaitForSecondsRealtime(0.1f);

        _isTransitioning = false;
    }

    private void FocusSpawnDoor()
    {
        RoomTransition[] doors = FindObjectsByType<RoomTransition>(FindObjectsInactive.Exclude);
        if (doors.Length == 0) return;

        foreach (RoomTransition door in doors)
        {
            //if (door.DoorID == firstRoomDoorID)
            {
                //if (playerTransform != null && door.SpawnPoint != null)
                {
                    //playerTransform.position = door.SpawnPoint.position;
                    //Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
                    //if (rb != null) rb.linearVelocity = Vector2.zero;
                }
                //return;
            }
        }

        RoomTransition first = doors[0];
        if (playerTransform != null && first.SpawnPoint != null)
        {
            playerTransform.position = first.SpawnPoint.position;
            Rigidbody2D rb2 = playerTransform.GetComponent<Rigidbody2D>();
            if (rb2 != null) rb2.linearVelocity = Vector2.zero;
        }
    }

    public void LoadInitialRoom(string sceneName, string targetDoorID)
    {
        StartCoroutine(TransitionRoutine(sceneName, targetDoorID));
    }

    private void SetPlayerControl(bool hasControl)
    {
        if (playerTransform == null) return;

        Player player = playerTransform.GetComponent<Player>();
        if (player == null) return;

        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (!hasControl)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            player.AddControlRequest(Player.ControlReason.Transition);
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            player.RemoveControlRequest(Player.ControlReason.Transition);
        }
    }

    public IEnumerator FadeToBlack()
    {
        if (transitionCanvasGroup == null) yield break;
        float startAlpha = transitionCanvasGroup.alpha;
        float time = 0;

        while (time < fadeOutDuration)
        {
            float safeDelta = Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
            time += safeDelta;
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, time / fadeOutDuration);
            yield return null;
        }
        transitionCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeFromBlack()
    {
        if (transitionCanvasGroup == null) yield break;
        float startAlpha = transitionCanvasGroup.alpha;
        float time = 0;

        while (time < fadeInDuration)
        {
            float safeDelta = Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
            time += safeDelta;
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, time / fadeInDuration);
            yield return null;
        }
        transitionCanvasGroup.alpha = 0f;
    }

    private void FocusTargetDoor()
    {
        if (playerTransform == null) return;
        RoomTransition[] doors = FindObjectsByType<RoomTransition>(FindObjectsInactive.Exclude);
        foreach (RoomTransition door in doors)
        {
            if (door.DoorID == _targetDoorID)
            {
                if (door.SpawnPoint == null)
                {
                    Debug.LogWarning($"[GameSceneManager] Door '{_targetDoorID}' has no spawn point assigned.");
                    return;
                }

                playerTransform.position = door.SpawnPoint.position;
                Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }
        }
        Debug.LogWarning($"[GameSceneManager] Porta target '{_targetDoorID}' non trovata nella stanza '{_currentRoomScene}'.");
    }

    private void UpdateCameraBounds()
    {
        GameObject boundsObj = GameObject.FindGameObjectWithTag("CameraBounds");
        if (boundsObj != null)
        {
            Collider2D boundsCollider = boundsObj.GetComponent<Collider2D>();
            if (boundsCollider != null)
            {
                RoomCameraSettings roomSettings =
                    boundsObj.GetComponent<RoomCameraSettings>();
                if (cameraManager != null)
                {
                    if (roomSettings != null)
                        cameraManager.ApplyRoomSettings(roomSettings);
                    else
                        cameraManager.SetCameraBounds(boundsCollider);
                }
                else if (cameraConfiner != null)
                {
                    cameraConfiner.BoundingShape2D = boundsCollider;
                    cameraConfiner.InvalidateBoundingShapeCache();
                }
            }
        }
    }

    private static RoomDefinition ResolveRoomDefinition(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            return null;

        RoomIdentityResult result = RoomIdentityResolver.ResolveByScenePath(scene.path);
        if (result.IsSuccess)
            return result.Room;

        if (result.Status == RoomIdentityStatus.CatalogMissing ||
            result.Status == RoomIdentityStatus.CatalogInvalid ||
            result.Status == RoomIdentityStatus.CatalogUnavailable)
        {
            Debug.LogWarning($"[GameSceneManager] Could not resolve room identity for '{scene.path}': {result.Message}");
        }

        return null;
    }
}
