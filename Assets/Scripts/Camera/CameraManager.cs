using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Selects between the three gameplay cameras. CinemachineBrain remains
/// responsible for evaluating the active camera and blending between cameras.
/// </summary>
public sealed class CameraManager : MonoBehaviour
{
    private const int ActivePriority = 10;
    private const int SecondaryPriority = 9;
    private const int TertiaryPriority = 8;

    [Header("Fixed Camera")]
    [Tooltip("Time used to move between two Fixed Camera anchors while Fixed is already active.")]
    [SerializeField, Min(0f)] private float fixedAnchorTransitionDuration = 0.25f;

    [Header("Gameplay Cameras")]
    [SerializeField] private CinemachineCamera explorationCamera;
    [SerializeField] private CinemachineCamera corridorCamera;
    [SerializeField] private CinemachineCamera fixedCamera;

    private sealed class CameraSwitchRequest
    {
        public CameraSwitchTrigger Trigger;
        public RoomCameraType CameraType;
        public Transform FixedCameraAnchor;
        public ulong SceneHandle;
    }

    private readonly List<CameraSwitchRequest> _cameraSwitchRequests =
        new List<CameraSwitchRequest>();
    private readonly HashSet<ulong> _cameraSwitchSceneHandles =
        new HashSet<ulong>();

    private RoomCameraType _activeCameraType = RoomCameraType.Exploration;
    private bool _hasValidCameras;

    private bool _isFixedCameraTransitionActive;
    private Vector3 _fixedTransitionStartPosition;
    private Quaternion _fixedTransitionStartRotation;
    private Vector3 _fixedTransitionTargetPosition;
    private Quaternion _fixedTransitionTargetRotation;
    private float _fixedTransitionDuration;
    private float _fixedTransitionElapsed;

    public RoomCameraType ActiveCameraType => _activeCameraType;

    public readonly struct CameraStateSnapshot
    {
        public readonly RoomCameraType ActiveCameraType;
        public readonly Collider2D CameraBounds;
        public readonly Vector3 ActiveCameraPosition;
        public readonly Quaternion ActiveCameraRotation;
        public readonly Vector3 FixedCameraPosition;
        public readonly Quaternion FixedCameraRotation;

        public CameraStateSnapshot(
            RoomCameraType activeCameraType,
            Collider2D cameraBounds,
            Vector3 activeCameraPosition,
            Quaternion activeCameraRotation,
            Vector3 fixedCameraPosition,
            Quaternion fixedCameraRotation)
        {
            ActiveCameraType = activeCameraType;
            CameraBounds = cameraBounds;
            ActiveCameraPosition = activeCameraPosition;
            ActiveCameraRotation = activeCameraRotation;
            FixedCameraPosition = fixedCameraPosition;
            FixedCameraRotation = fixedCameraRotation;
        }
    }

    private void Awake()
    {
        if (!HasAllCameras())
        {
            Debug.LogError("[CameraManager] All three gameplay cameras must be assigned.", this);
            enabled = false;
            return;
        }

        _hasValidCameras = true;
        SetCameraPriorities(RoomCameraType.Exploration);
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        if (_hasValidCameras)
        {
            RemoveUnloadedSwitchScenes();
            ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        CancelFixedCameraTransition();
    }

    private void FixedUpdate()
    {
        if (RemoveInvalidSwitchRequests())
            ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);

        UpdateFixedCameraTransition();
    }

    /// <summary>
    /// Selects a gameplay camera without changing the Dialogue Camera.
    /// Active CameraSwitchTriggers take precedence over direct/debug selection.
    /// </summary>
    public bool SelectCamera(RoomCameraType cameraType)
    {
        if (!_hasValidCameras || !TryGetCamera(cameraType, out _))
            return false;

        if (_cameraSwitchRequests.Count > 0)
            return ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);

        if (cameraType != RoomCameraType.Fixed)
            CancelFixedCameraTransition();

        return SetCameraPriorities(cameraType);
    }

    /// <summary>
    /// Registers a room-local camera selection request. Requests are resolved
    /// in LIFO order, with the newest active trigger taking precedence.
    /// </summary>
    public bool RegisterCameraSwitchTrigger(
        CameraSwitchTrigger trigger,
        RoomCameraType cameraType,
        Transform fixedCameraAnchor)
    {
        if (!_hasValidCameras ||
            trigger == null ||
            !trigger.isActiveAndEnabled)
        {
            return false;
        }

        Scene triggerScene = trigger.gameObject.scene;
        if (!triggerScene.IsValid() || !triggerScene.isLoaded)
            return false;

        if (cameraType == RoomCameraType.Fixed &&
            (fixedCameraAnchor == null ||
             !fixedCameraAnchor.gameObject.scene.IsValid() ||
             fixedCameraAnchor.gameObject.scene.handle.GetRawData() !=
             triggerScene.handle.GetRawData()))
        {
            return false;
        }

        int existingIndex = FindSwitchRequest(trigger);
        if (existingIndex >= 0)
        {
            _cameraSwitchSceneHandles.Add(triggerScene.handle.GetRawData());
            CameraSwitchRequest existing = _cameraSwitchRequests[existingIndex];
            existing.CameraType = cameraType;
            existing.FixedCameraAnchor = cameraType == RoomCameraType.Fixed
                ? fixedCameraAnchor
                : null;
            existing.SceneHandle = triggerScene.handle.GetRawData();

            if (existingIndex == _cameraSwitchRequests.Count - 1)
                return ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);

            return true;
        }

        _cameraSwitchSceneHandles.Add(triggerScene.handle.GetRawData());
        _cameraSwitchRequests.Add(new CameraSwitchRequest
        {
            Trigger = trigger,
            CameraType = cameraType,
            FixedCameraAnchor = cameraType == RoomCameraType.Fixed
                ? fixedCameraAnchor
                : null,
            SceneHandle = triggerScene.handle.GetRawData()
        });

        return ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);
    }

    /// <summary>Removes a trigger request. Repeated calls are harmless.</summary>
    public void UnregisterCameraSwitchTrigger(CameraSwitchTrigger trigger)
    {
        if (object.ReferenceEquals(trigger, null))
            return;

        int index = FindSwitchRequest(trigger);
        if (index < 0)
            return;

        bool wasActive = index == _cameraSwitchRequests.Count - 1;
        _cameraSwitchRequests.RemoveAt(index);

        if (wasActive)
            ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);
    }

    /// <summary>
    /// Assigns the same room boundary to each gameplay camera's independent
    /// Confiner2D and invalidates every corresponding shape cache.
    /// </summary>
    public bool SetCameraBounds(Collider2D bounds)
    {
        if (!TryGetConfiner(explorationCamera, out CinemachineConfiner2D explorationConfiner) ||
            !TryGetConfiner(corridorCamera, out CinemachineConfiner2D corridorConfiner) ||
            !TryGetConfiner(fixedCamera, out CinemachineConfiner2D fixedConfiner))
        {
            Debug.LogError(
                "[CameraManager] Each gameplay camera must have its own CinemachineConfiner2D.",
                this);
            return false;
        }

        AssignBounds(explorationConfiner, bounds);
        AssignBounds(corridorConfiner, bounds);
        AssignBounds(fixedConfiner, bounds);
        return true;
    }

    /// <summary>Applies a room's bounds and requested gameplay camera configuration.</summary>
    public bool ApplyRoomSettings(RoomCameraSettings settings)
    {
        if (settings == null || settings.CameraBounds == null)
            return false;

        if (!SetCameraBounds(settings.CameraBounds))
            return false;

        // CameraSwitchTrigger owns gameplay camera selection. Room settings
        // still supply bounds, but cannot override the Exploration fallback.
        if (_cameraSwitchRequests.Count > 0)
            return ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);

        CancelFixedCameraTransition();
        return SetCameraPriorities(RoomCameraType.Exploration);
    }

    /// <summary>
    /// Force-positions a following gameplay camera after a room spawn. A fixed
    /// camera intentionally remains at its authored room anchor.
    /// </summary>
    public void ForceFollowingCameraPosition(Vector3 targetPosition)
    {
        if (_activeCameraType == RoomCameraType.Fixed ||
            !TryGetCamera(_activeCameraType, out CinemachineCamera camera))
        {
            return;
        }

        Vector3 cameraPosition = new Vector3(
            targetPosition.x,
            targetPosition.y,
            camera.transform.position.z);
        camera.transform.position = cameraPosition;
        camera.ForceCameraPosition(cameraPosition, camera.transform.rotation);
    }

    public CameraStateSnapshot CaptureState()
    {
        Collider2D bounds = null;
        if (TryGetConfiner(explorationCamera, out CinemachineConfiner2D confiner))
            bounds = confiner.BoundingShape2D;

        return new CameraStateSnapshot(
            _activeCameraType,
            bounds,
            TryGetCamera(_activeCameraType, out CinemachineCamera activeCamera)
                ? activeCamera.transform.position
                : Vector3.zero,
            activeCamera != null ? activeCamera.transform.rotation : Quaternion.identity,
            fixedCamera != null ? fixedCamera.transform.position : Vector3.zero,
            fixedCamera != null ? fixedCamera.transform.rotation : Quaternion.identity);
    }

    public void RestoreState(CameraStateSnapshot snapshot)
    {
        CancelFixedCameraTransition();

        if (TryGetCamera(snapshot.ActiveCameraType, out CinemachineCamera activeCamera))
        {
            activeCamera.transform.SetPositionAndRotation(
                snapshot.ActiveCameraPosition,
                snapshot.ActiveCameraRotation);
            activeCamera.ForceCameraPosition(
                snapshot.ActiveCameraPosition,
                snapshot.ActiveCameraRotation);
        }

        if (fixedCamera != null)
        {
            fixedCamera.transform.SetPositionAndRotation(
                snapshot.FixedCameraPosition,
                snapshot.FixedCameraRotation);
            fixedCamera.ForceCameraPosition(
                snapshot.FixedCameraPosition,
                snapshot.FixedCameraRotation);
        }

        SetCameraBounds(snapshot.CameraBounds);
        if (_cameraSwitchRequests.Count > 0)
            ReapplySwitchStackOrFallback(forceFollowingCameraPosition: false);
        else
            SetCameraPriorities(snapshot.ActiveCameraType);
    }

    private void PositionFixedCamera(Transform anchor)
    {
        fixedCamera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        fixedCamera.ForceCameraPosition(anchor.position, anchor.rotation);
    }

    private bool SetCameraPriorities(RoomCameraType cameraType)
    {
        if (!TryGetCamera(cameraType, out CinemachineCamera selected))
            return false;

        CinemachineCamera first;
        CinemachineCamera second;
        switch (cameraType)
        {
            case RoomCameraType.Exploration:
                first = corridorCamera;
                second = fixedCamera;
                break;
            case RoomCameraType.Corridor:
                first = explorationCamera;
                second = fixedCamera;
                break;
            case RoomCameraType.Fixed:
                first = explorationCamera;
                second = corridorCamera;
                break;
            default:
                return false;
        }

        selected.Priority = ActivePriority;
        first.Priority = SecondaryPriority;
        second.Priority = TertiaryPriority;
        _activeCameraType = cameraType;
        return true;
    }

    private bool ReapplySwitchStackOrFallback(bool forceFollowingCameraPosition)
    {
        RemoveInvalidSwitchRequests();

        bool applied;
        if (_cameraSwitchRequests.Count == 0)
        {
            CancelFixedCameraTransition();
            applied = SetCameraPriorities(RoomCameraType.Exploration);
        }
        else
        {
            CameraSwitchRequest activeRequest =
                _cameraSwitchRequests[_cameraSwitchRequests.Count - 1];
            applied = ApplySwitchRequest(activeRequest);
        }

        if (applied && forceFollowingCameraPosition)
            ForceActiveFollowingCameraPosition();

        return applied;
    }

    private bool ApplySwitchRequest(CameraSwitchRequest request)
    {
        if (request.CameraType == RoomCameraType.Fixed)
        {
            Transform anchor = request.FixedCameraAnchor;
            if (anchor == null)
                return false;

            if (_activeCameraType == RoomCameraType.Fixed)
                BeginFixedCameraTransition(anchor.position, anchor.rotation);
            else
            {
                CancelFixedCameraTransition();
                PositionFixedCamera(anchor);
            }
        }
        else
        {
            CancelFixedCameraTransition();
        }

        return SetCameraPriorities(request.CameraType);
    }

    private void BeginFixedCameraTransition(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (fixedCamera == null)
            return;

        if (_isFixedCameraTransitionActive &&
            (_fixedTransitionTargetPosition - targetPosition).sqrMagnitude <= 0.000001f &&
            Quaternion.Angle(_fixedTransitionTargetRotation, targetRotation) <= 0.01f)
        {
            return;
        }

        Vector3 currentPosition = fixedCamera.transform.position;
        Quaternion currentRotation = fixedCamera.transform.rotation;
        if ((currentPosition - targetPosition).sqrMagnitude <= 0.000001f &&
            Quaternion.Angle(currentRotation, targetRotation) <= 0.01f)
        {
            CancelFixedCameraTransition();
            PositionFixedCamera(targetPosition, targetRotation);
            return;
        }

        if (fixedAnchorTransitionDuration <= 0f)
        {
            CancelFixedCameraTransition();
            PositionFixedCamera(targetPosition, targetRotation);
            return;
        }

        _fixedTransitionStartPosition = currentPosition;
        _fixedTransitionStartRotation = currentRotation;
        _fixedTransitionTargetPosition = targetPosition;
        _fixedTransitionTargetRotation = targetRotation;
        _fixedTransitionDuration = fixedAnchorTransitionDuration;
        _fixedTransitionElapsed = 0f;
        _isFixedCameraTransitionActive = true;
    }

    private void UpdateFixedCameraTransition()
    {
        if (!_isFixedCameraTransitionActive)
            return;

        if (fixedCamera == null || _activeCameraType != RoomCameraType.Fixed)
        {
            CancelFixedCameraTransition();
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        _fixedTransitionElapsed = Mathf.Min(
            _fixedTransitionElapsed + deltaTime,
            _fixedTransitionDuration);

        float normalizedTime = _fixedTransitionElapsed / _fixedTransitionDuration;
        float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
        Vector3 position = Vector3.Lerp(
            _fixedTransitionStartPosition,
            _fixedTransitionTargetPosition,
            easedTime);
        Quaternion rotation = Quaternion.Slerp(
            _fixedTransitionStartRotation,
            _fixedTransitionTargetRotation,
            easedTime);

        if (_fixedTransitionElapsed >= _fixedTransitionDuration)
        {
            position = _fixedTransitionTargetPosition;
            rotation = _fixedTransitionTargetRotation;
            _isFixedCameraTransitionActive = false;
        }

        PositionFixedCamera(position, rotation);
    }

    private void CancelFixedCameraTransition()
    {
        _isFixedCameraTransitionActive = false;
        _fixedTransitionElapsed = 0f;
    }

    private void ForceActiveFollowingCameraPosition()
    {
        if (_activeCameraType == RoomCameraType.Fixed ||
            !TryGetCamera(_activeCameraType, out CinemachineCamera camera) ||
            camera.Follow == null)
        {
            return;
        }

        ForceFollowingCameraPosition(camera.Follow.position);
    }

    private void HandleSceneUnloaded(Scene unloadedScene)
    {
        bool sceneHadSwitchTriggers =
            _cameraSwitchSceneHandles.Remove(unloadedScene.handle.GetRawData());
        bool removedRequest = false;
        for (int i = _cameraSwitchRequests.Count - 1; i >= 0; i--)
        {
            if (_cameraSwitchRequests[i].SceneHandle == unloadedScene.handle.GetRawData())
            {
                _cameraSwitchRequests.RemoveAt(i);
                removedRequest = true;
            }
        }

        if (!sceneHadSwitchTriggers && !removedRequest)
            return;

        // Room transitions can relocate the Player before the source scene is
        // unloaded. Re-evaluate the remaining trigger stack and align a
        // following camera to the Player before the transition fade completes.
        ReapplySwitchStackOrFallback(forceFollowingCameraPosition: true);
    }

    private void RemoveUnloadedSwitchScenes()
    {
        List<ulong> unloadedHandles = null;
        foreach (ulong sceneHandle in _cameraSwitchSceneHandles)
        {
            if (IsSceneHandleLoaded(sceneHandle))
                continue;

            unloadedHandles ??= new List<ulong>();
            unloadedHandles.Add(sceneHandle);
        }

        if (unloadedHandles == null)
            return;

        for (int i = 0; i < unloadedHandles.Count; i++)
            _cameraSwitchSceneHandles.Remove(unloadedHandles[i]);
    }

    private int FindSwitchRequest(CameraSwitchTrigger trigger)
    {
        for (int i = 0; i < _cameraSwitchRequests.Count; i++)
        {
            if (object.ReferenceEquals(_cameraSwitchRequests[i].Trigger, trigger))
                return i;
        }

        return -1;
    }

    private bool RemoveInvalidSwitchRequests()
    {
        bool removedAny = false;
        for (int i = _cameraSwitchRequests.Count - 1; i >= 0; i--)
        {
            if (IsValidSwitchRequest(_cameraSwitchRequests[i]))
                continue;

            _cameraSwitchRequests.RemoveAt(i);
            removedAny = true;
        }

        return removedAny;
    }

    private static bool IsValidSwitchRequest(CameraSwitchRequest request)
    {
        CameraSwitchTrigger trigger = request.Trigger;
        if (trigger == null || !trigger.isActiveAndEnabled)
            return false;

        Scene triggerScene = trigger.gameObject.scene;
        if (!triggerScene.IsValid() ||
            !triggerScene.isLoaded ||
            triggerScene.handle.GetRawData() != request.SceneHandle)
        {
            return false;
        }

        if (request.CameraType != RoomCameraType.Fixed)
            return true;

        Transform anchor = request.FixedCameraAnchor;
        return anchor != null &&
               anchor.gameObject.scene.IsValid() &&
               anchor.gameObject.scene.isLoaded &&
               anchor.gameObject.scene.handle.GetRawData() == request.SceneHandle;
    }

    private static bool IsSceneHandleLoaded(ulong sceneHandle)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.handle.GetRawData() == sceneHandle)
                return true;
        }

        return false;
    }

    private void PositionFixedCamera(Vector3 position, Quaternion rotation)
    {
        fixedCamera.transform.SetPositionAndRotation(position, rotation);
        fixedCamera.ForceCameraPosition(position, rotation);
    }

    private bool HasAllCameras()
    {
        return explorationCamera != null &&
               corridorCamera != null &&
               fixedCamera != null &&
               explorationCamera != corridorCamera &&
               explorationCamera != fixedCamera &&
               corridorCamera != fixedCamera;
    }

    private bool TryGetCamera(RoomCameraType cameraType, out CinemachineCamera camera)
    {
        camera = cameraType switch
        {
            RoomCameraType.Exploration => explorationCamera,
            RoomCameraType.Corridor => corridorCamera,
            RoomCameraType.Fixed => fixedCamera,
            _ => null
        };

        if (camera != null)
            return true;

        Debug.LogError($"[CameraManager] No gameplay camera is assigned for {cameraType}.", this);
        return false;
    }

    private static bool TryGetConfiner(
        CinemachineCamera camera,
        out CinemachineConfiner2D confiner)
    {
        confiner = camera != null ? camera.GetComponent<CinemachineConfiner2D>() : null;
        return confiner != null;
    }

    private static void AssignBounds(CinemachineConfiner2D confiner, Collider2D bounds)
    {
        confiner.BoundingShape2D = bounds;
        confiner.InvalidateBoundingShapeCache();
    }
}
