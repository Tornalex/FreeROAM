using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Phase 6.6.3 integration coverage for one successful connected room
    /// transition. The test uses the real Master scene, GameSceneManager,
    /// RuntimeRoomCatalog, RoomConnectionResolver and room scenes.
    /// </summary>
    public class RoomTransitionPlayModeTests : PlayModeTestBase
    {
        private const string MasterScenePath = "Assets/Scenes/Master.unity";
        private const string SourceScenePath =
            "Assets/Scenes/Rooms/Re-Entry Module/Re-EntryModule_WakeRoom.unity";
        private const string DestinationScenePath =
            "Assets/Scenes/Rooms/Re-Entry Module/Re-EntryModule_Corridor.unity";

        private const string SourceRoomId = "room_0cd7bdc0537c4f4bb6937de388e8858d";
        private const string DestinationRoomId = "room_f4a2fbe688fb4506af039512474bd0f7";
        private const string SourceDoorId = "WakeRoom_Right";
        private const string DestinationDoorId = "Re-EntryModule_Corridor_Left";
        private const string ConnectionAssetName =
            "Conn_room_0cd7bdc0537c4f4bb6937de388e8858d_WakeRoom_Right_to_room_f4a2fbe688fb4506af039512474bd0f7_Re-EntryModule_Corridor_Left";

        private const float TransitionTimeoutSeconds = 30f;
        private const float PlayerRelocationTolerance = 0.001f;
        private const float PlayerSettleHorizontalTolerance = 0.05f;

        // After the transition lock clears, control and gravity resume while the
        // production routine waits 0.1s of realtime. The player therefore falls
        // a short distance from the spawn point before the test observes it.
        private const float PlayerSettleVerticalTolerance = 0.6f;

        private const string ManagerTypeName = "GameSceneManager";
        private const string DoorTypeName = "RoomTransition";
        private const string PlayerTypeName = "Player";
        private const string CatalogTypeName = "RuntimeRoomCatalog";
        private const string ConnectionResolverTypeName = "RoomConnectionResolver";
        private const string IdentityResolverTypeName = "RoomIdentityResolver";
        private const string AbilityManagerTypeName = "SpearWander.Abilities.AbilityManager";
        private const string CheckpointManagerTypeName = "CheckpointManager";

        [UnityTest]
        public IEnumerator ConnectedTransitionLoadsDestinationAndUnloadsSource()
        {
            Type managerType = ReflectionTestUtilities.ResolveRuntimeType(ManagerTypeName);
            Type doorType = ReflectionTestUtilities.ResolveRuntimeType(DoorTypeName);
            Type playerType = ReflectionTestUtilities.ResolveRuntimeType(PlayerTypeName);
            Type catalogType = ReflectionTestUtilities.ResolveRuntimeType(CatalogTypeName);
            Type connectionResolverType =
                ReflectionTestUtilities.ResolveRuntimeType(ConnectionResolverTypeName);
            Type identityResolverType =
                ReflectionTestUtilities.ResolveRuntimeType(IdentityResolverTypeName);

            Type controlReasonType = playerType.GetNestedType("ControlReason");
            Assert.That(controlReasonType, Is.Not.Null, "Player.ControlReason was not found.");
            object transitionControlReason = Enum.Parse(controlReasonType, "Transition");

            object preExistingManager = GetManager(managerType);
            bool managerExisted = IsAlive(preExistingManager);
            bool masterExisted = Fixture.IsSceneLoaded(MasterScenePath);
            object preExistingAbilityManager =
                GetStaticInstance(AbilityManagerTypeName);
            object preExistingCheckpointManager =
                GetStaticInstance(CheckpointManagerTypeName);

            object manager = null;
            Scene sourceScene = default;
            Scene destinationScene = default;
            Scene masterScene = default;
            bool masterLoadedByTest = false;
            bool destinationAdopted = false;
            bool relocationObserved = false;
            Vector3 observedRelocation = default;
            Vector3 observedFinalPosition = default;
            bool playerCaptured = false;

            Transform playerTransform = null;
            object player = null;
            Rigidbody2D playerBody = null;
            Vector3 previousPlayerPosition = default;
            Quaternion previousPlayerRotation = default;
            RigidbodyType2D previousBodyType = default;
            Vector2 previousVelocity = default;
            float previousAngularVelocity = 0f;
            bool previousSimulated = true;
            RigidbodyConstraints2D previousConstraints = default;
            bool previousHadControl = true;
            bool previousHadTransitionRequest = false;

            object cameraConfiner = null;
            Collider2D previousCameraBounds = null;
            CanvasGroup transitionCanvasGroup = null;
            float previousCanvasAlpha = 0f;
            float previousTimeScale = Time.timeScale;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            PlayModeLogCapture logCapture = null;

            try
            {
                // The real Master scene loads Yarn Spinner, whose input
                // advancer throws a known batchmode-only NullReferenceException.
                // Failing messages are asserted manually below so unrelated
                // environment exceptions cannot hide transition errors.
                LogAssert.ignoreFailingMessages = true;
                logCapture = Fixture.StartLogCapture();

                Assert.That(
                    Fixture.IsSceneLoaded(SourceScenePath),
                    Is.False,
                    $"Source scene '{SourceScenePath}' was already loaded before the test.");
                Assert.That(
                    Fixture.IsSceneLoaded(DestinationScenePath),
                    Is.False,
                    $"Destination scene '{DestinationScenePath}' was already loaded before the test.");

                if (!masterExisted)
                {
                    Assert.That(
                        managerExisted,
                        Is.False,
                        "A live GameSceneManager exists without the Master scene. " +
                        "This test will not replace a live manager.");
                }

                yield return Fixture.LoadSceneAdditive(
                    SourceScenePath,
                    scene => sourceScene = scene);

                Assert.That(sourceScene.IsValid(), Is.True);
                Assert.That(sourceScene.isLoaded, Is.True);
                Assert.That(sourceScene.path, Is.EqualTo(SourceScenePath));

                object sourceRoom = ResolveRoomIdentity(
                    identityResolverType,
                    SourceScenePath,
                    SourceRoomId);
                object destinationRoom = ResolveRoomIdentity(
                    identityResolverType,
                    DestinationScenePath,
                    DestinationRoomId);

                if (!masterExisted)
                {
                    // Yarn Spinner's input advancer throws a known batchmode
                    // NullReferenceException. It is unrelated to room
                    // transitions, so disable that input component as soon as
                    // the Master scene is loaded.
                    yield return Fixture.LoadSceneAdditive(
                        MasterScenePath,
                        scene =>
                        {
                            masterScene = scene;
                            DisableYarnLineAdvancerInput(scene);
                        });
                    yield return null;
                    masterLoadedByTest = true;

                    float managerDeadline = Time.realtimeSinceStartup + TransitionTimeoutSeconds;
                    while (Time.realtimeSinceStartup < managerDeadline)
                    {
                        manager = GetManager(managerType);
                        if (IsAlive(manager) &&
                            string.Equals(
                                ReflectionTestUtilities.GetInstanceField<string>(
                                    manager,
                                    "_currentRoomScene"),
                                sourceScene.name,
                                StringComparison.Ordinal))
                        {
                            break;
                        }

                        yield return null;
                    }

                    Assert.That(
                        IsAlive(manager),
                        Is.True,
                        "The Master scene did not create a live GameSceneManager.");
                    Assert.That(
                        ReflectionTestUtilities.GetInstanceField<string>(
                            manager,
                            "_currentRoomScene"),
                        Is.EqualTo(sourceScene.name),
                        "GameSceneManager.Start did not select the loaded source room.");
                }
                else
                {
                    masterScene = Fixture.GetLoadedScene(MasterScenePath);
                    DisableYarnLineAdvancerInput(masterScene);

                    float managerDeadline = Time.realtimeSinceStartup + TransitionTimeoutSeconds;
                    while (Time.realtimeSinceStartup < managerDeadline)
                    {
                        manager = GetManager(managerType);
                        if (IsAlive(manager))
                            break;

                        yield return null;
                    }

                    Assert.That(
                        IsAlive(manager),
                        Is.True,
                        "The pre-loaded Master scene does not contain a live GameSceneManager.");

                    // A pre-existing manager already ran Start. Put it on the
                    // source room without replacing the manager or its player.
                    ReflectionTestUtilities.SetInstanceField(
                        manager,
                        "_currentRoomScene",
                        sourceScene.name);
                    ReflectionTestUtilities.SetInstanceField(
                        manager,
                        "_currentRoomDefinition",
                        sourceRoom);
                    ReflectionTestUtilities.SetInstanceField(manager, "_targetDoorID", null);
                    ReflectionTestUtilities.SetInstanceField(manager, "_isTransitioning", false);
                    Assert.That(
                        SceneManager.SetActiveScene(sourceScene),
                        Is.True,
                        "Could not make the source scene active for the pre-existing manager.");
                }

                Assert.That(IsAlive(manager), Is.True);
                Assert.That(
                    Fixture.IsSceneLoaded(MasterScenePath),
                    Is.True,
                    "The Master scene must remain loaded for the persistent player and camera.");
                masterScene = Fixture.GetLoadedScene(MasterScenePath);

                Assert.That(
                    SceneManager.GetActiveScene().handle,
                    Is.EqualTo(sourceScene.handle),
                    "The source room must be active before the transition is requested.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(manager, "CurrentRoom"),
                    Is.EqualTo(sourceScene.name));
                object currentSourceDefinition =
                    ReflectionTestUtilities.GetProperty(manager, "CurrentRoomDefinition");
                Assert.That(currentSourceDefinition, Is.SameAs(sourceRoom));

                playerTransform =
                    ReflectionTestUtilities.GetInstanceField<Transform>(manager, "playerTransform");
                Assert.That(
                    playerTransform,
                    Is.Not.Null,
                    "GameSceneManager.playerTransform is not assigned.");
                Assert.That(
                    playerTransform.gameObject.scene.handle,
                    Is.EqualTo(masterScene.handle),
                    "The persistent player must live in the Master scene.");

                player = playerTransform.GetComponent(playerType);
                Assert.That(player, Is.Not.Null, "The persistent player has no Player component.");
                playerBody = playerTransform.GetComponent<Rigidbody2D>();
                Assert.That(
                    playerBody,
                    Is.Not.Null,
                    "The persistent player has no Rigidbody2D.");

                cameraConfiner =
                    ReflectionTestUtilities.GetInstanceField(manager, "cameraConfiner");
                Assert.That(
                    cameraConfiner,
                    Is.Not.Null,
                    "GameSceneManager.cameraConfiner is not assigned.");
                previousCameraBounds =
                    ReflectionTestUtilities.GetInstanceField<Collider2D>(
                        cameraConfiner,
                        "BoundingShape2D");

                transitionCanvasGroup =
                    ReflectionTestUtilities.GetInstanceField<CanvasGroup>(
                        manager,
                        "transitionCanvasGroup");
                Assert.That(
                    transitionCanvasGroup,
                    Is.Not.Null,
                    "GameSceneManager.transitionCanvasGroup is not assigned.");
                previousCanvasAlpha = transitionCanvasGroup.alpha;

                previousPlayerPosition = playerTransform.position;
                previousPlayerRotation = playerTransform.rotation;
                previousBodyType = playerBody.bodyType;
                previousVelocity = playerBody.linearVelocity;
                previousAngularVelocity = playerBody.angularVelocity;
                previousSimulated = playerBody.simulated;
                previousConstraints = playerBody.constraints;
                previousHadControl =
                    ReflectionTestUtilities.GetProperty<bool>(player, "HasControl");
                previousHadTransitionRequest = ReflectionTestUtilities.InvokeInstance<bool>(
                    player,
                    "HasControlRequest",
                    transitionControlReason);
                playerCaptured = true;

                Component sourceDoor = FindUniqueDoor(
                    doorType,
                    sourceScene,
                    SourceDoorId,
                    "source");

                object sourceConnection = ReflectionTestUtilities.GetProperty(
                    sourceDoor,
                    "Connection");
                Assert.That(
                    sourceConnection,
                    Is.Not.Null,
                    $"Source door '{SourceDoorId}' has no RoomConnection.");

                AssertConnection(
                    sourceConnection,
                    SourceRoomId,
                    SourceDoorId,
                    DestinationRoomId,
                    DestinationDoorId);

                Transform sourceSpawn =
                    ReflectionTestUtilities.GetProperty<Transform>(sourceDoor, "SpawnPoint");
                Assert.That(sourceSpawn, Is.Not.Null, "The source door has no SpawnPoint.");
                Assert.That(
                    sourceSpawn.gameObject.scene.handle,
                    Is.EqualTo(sourceScene.handle),
                    "The source door SpawnPoint is not in the source scene.");

                object catalog =
                    ReflectionTestUtilities.InvokeStatic(catalogType, "LoadDefault");
                object resolution = ReflectionTestUtilities.InvokeStatic(
                    connectionResolverType,
                    "Resolve",
                    sourceDoor,
                    catalog);

                Assert.That(
                    GetStatus(resolution),
                    Is.EqualTo("DestinationValidationPending"),
                    "The source door did not resolve to a pending connected destination: " +
                    GetMessage(resolution));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(
                        resolution,
                        "DestinationScenePath"),
                    Is.EqualTo(DestinationScenePath));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(
                        resolution,
                        "DestinationDoorId"),
                    Is.EqualTo(DestinationDoorId));
                object resolvedDestinationRoom =
                    ReflectionTestUtilities.GetProperty(resolution, "DestinationRoom");
                Assert.That(resolvedDestinationRoom, Is.SameAs(destinationRoom));
                Assert.That(
                    ReflectionTestUtilities.GetProperty(resolution, "Connection"),
                    Is.SameAs(sourceConnection));

                // This is the same public request route used by
                // RoomTransition.OnTriggerEnter2D.
                ReflectionTestUtilities.InvokeInstance(
                    manager,
                    "RequestRoomTransition",
                    sourceDoor);

                Assert.That(
                    IsTransitioning(manager),
                    Is.True,
                    "RequestRoomTransition did not start the connected transition.");

                float transitionDeadline =
                    Time.realtimeSinceStartup + TransitionTimeoutSeconds;
                bool timedOut = false;

                while (IsTransitioning(manager))
                {
                    if (Time.realtimeSinceStartup >= transitionDeadline)
                    {
                        timedOut = true;
                        break;
                    }

                    if (!destinationAdopted &&
                        Fixture.IsSceneLoaded(DestinationScenePath))
                    {
                        destinationScene = Fixture.GetLoadedScene(DestinationScenePath);
                        Fixture.TakeOwnership(destinationScene);
                        destinationAdopted = true;
                    }

                    if (destinationAdopted &&
                        !relocationObserved &&
                        SceneManager.GetActiveScene().handle == destinationScene.handle)
                    {
                        relocationObserved = true;
                        observedRelocation = playerTransform.position;
                    }

                    yield return null;
                }

                if (!destinationAdopted &&
                    Fixture.IsSceneLoaded(DestinationScenePath))
                {
                    destinationScene = Fixture.GetLoadedScene(DestinationScenePath);
                    Fixture.TakeOwnership(destinationScene);
                    destinationAdopted = true;
                }

                Assert.That(
                    timedOut,
                    Is.False,
                    $"The connected transition did not complete within {TransitionTimeoutSeconds} seconds.");
                Assert.That(
                    IsTransitioning(manager),
                    Is.False,
                    "The connected transition lock was not cleared.");
                Assert.That(
                    destinationAdopted,
                    Is.True,
                    $"Destination scene '{DestinationScenePath}' was never loaded.");
                Assert.That(destinationScene.IsValid(), Is.True);
                Assert.That(destinationScene.isLoaded, Is.True);
                Assert.That(destinationScene.path, Is.EqualTo(DestinationScenePath));

                observedFinalPosition = playerTransform.position;

                Assert.That(
                    CountLoadedScenes(DestinationScenePath),
                    Is.EqualTo(1),
                    "The destination scene must be loaded exactly once.");
                Assert.That(
                    Fixture.IsSceneLoaded(SourceScenePath),
                    Is.False,
                    "The source scene remained loaded after the committed transition.");
                Assert.That(
                    sourceScene.isLoaded,
                    Is.False,
                    "The source scene handle still reports itself as loaded.");
                Assert.That(
                    SceneManager.GetSceneByPath(SourceScenePath).isLoaded,
                    Is.False,
                    "SceneManager still reports the source scene as loaded.");

                Assert.That(
                    SceneManager.GetActiveScene().handle,
                    Is.EqualTo(destinationScene.handle),
                    "The destination scene is not active.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(manager, "CurrentRoom"),
                    Is.EqualTo(destinationScene.name));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(manager, "CurrentRoomId"),
                    Is.EqualTo(DestinationRoomId));

                object currentDestinationDefinition =
                    ReflectionTestUtilities.GetProperty(manager, "CurrentRoomDefinition");
                Assert.That(
                    currentDestinationDefinition,
                    Is.SameAs(destinationRoom),
                    "CurrentRoomDefinition does not refer to the destination RoomDefinition.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(
                        currentDestinationDefinition,
                        "SceneName"),
                    Is.EqualTo(destinationScene.name));

                Component destinationDoor = FindUniqueDoor(
                    doorType,
                    destinationScene,
                    DestinationDoorId,
                    "destination");

                Transform destinationSpawn =
                    ReflectionTestUtilities.GetProperty<Transform>(
                        destinationDoor,
                        "SpawnPoint");
                Assert.That(
                    destinationSpawn,
                    Is.Not.Null,
                    "The destination door has no SpawnPoint.");
                Assert.That(
                    destinationSpawn.gameObject.scene.handle,
                    Is.EqualTo(destinationScene.handle),
                    "The destination door SpawnPoint is not in the destination scene.");

                object destinationConnection =
                    ReflectionTestUtilities.GetProperty(
                        destinationDoor,
                        "Connection");
                Assert.That(
                    destinationConnection,
                    Is.SameAs(sourceConnection),
                    "The destination door does not reference the source connection asset.");

                Assert.That(
                    relocationObserved,
                    Is.True,
                    "The player relocation frame at the destination spawn was not observed.");
                AssertPlayerPosition(
                    "player relocation",
                    observedRelocation,
                    destinationSpawn.position,
                    PlayerRelocationTolerance);

                object validatedResolution = ReflectionTestUtilities.InvokeStatic(
                    connectionResolverType,
                    "ValidateDestinationBacklink",
                    resolution,
                    DestinationScenePath,
                    CreateDoorArray(doorType, destinationScene));
                Assert.That(
                    GetStatus(validatedResolution),
                    Is.EqualTo("Success"),
                    "The destination door backlink did not validate: " +
                    GetMessage(validatedResolution));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(
                        validatedResolution,
                        "DestinationDoorId"),
                    Is.EqualTo(DestinationDoorId));

                AssertPlayerSettledPosition(
                    "final player position",
                    observedFinalPosition,
                    destinationSpawn.position);

                Assert.That(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        player,
                        "HasControlRequest",
                        transitionControlReason),
                    Is.EqualTo(previousHadTransitionRequest),
                    "The player's Transition control request was not restored.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<bool>(player, "HasControl"),
                    Is.EqualTo(previousHadControl),
                    "Player control was not restored.");
                Assert.That(
                    playerBody.bodyType,
                    Is.EqualTo(previousBodyType),
                    "The player Rigidbody2D body type was not restored.");
                Assert.That(
                    Time.timeScale,
                    Is.EqualTo(previousTimeScale).Within(0.0001f),
                    "Time.timeScale was not restored.");

                Collider2D destinationBounds = FindUniqueCameraBounds(destinationScene);
                Collider2D activeBounds =
                    ReflectionTestUtilities.GetInstanceField<Collider2D>(
                        cameraConfiner,
                        "BoundingShape2D");
                Assert.That(
                    activeBounds,
                    Is.Not.Null,
                    "The camera confiner has no bounding shape after the transition.");
                Assert.That(
                    activeBounds,
                    Is.SameAs(destinationBounds),
                    "The camera confiner is not bound to the destination CameraBounds collider.");
                Assert.That(
                    activeBounds.CompareTag("CameraBounds"),
                    Is.True,
                    "The assigned camera bounds collider is not tagged CameraBounds.");

                TestContext.WriteLine(
                    "[RoomTransition] Source scene: " + SourceScenePath);
                TestContext.WriteLine(
                    "[RoomTransition] Destination scene: " + DestinationScenePath);
                TestContext.WriteLine(
                    "[RoomTransition] Source room/door: " +
                    SourceRoomId + "/" + SourceDoorId);
                TestContext.WriteLine(
                    "[RoomTransition] Destination room/door: " +
                    DestinationRoomId + "/" + DestinationDoorId);
                TestContext.WriteLine(
                    "[RoomTransition] Connection: " + ConnectionAssetName +
                    " direction=" + GetPropertyText(sourceConnection, "Direction"));
                TestContext.WriteLine(
                    "[RoomTransition] Trigger: GameSceneManager.RequestRoomTransition(RoomTransition)");
                TestContext.WriteLine(
                    "[RoomTransition] Expected destination SpawnPoint: " +
                    FormatVector(destinationSpawn.position));
                TestContext.WriteLine(
                    "[RoomTransition] Observed relocation position: " +
                    FormatVector(observedRelocation));
                TestContext.WriteLine(
                    "[RoomTransition] Observed final position: " +
                    FormatVector(observedFinalPosition));
                TestContext.WriteLine(
                    "[RoomTransition] Relocation delta: " +
                    FormatVector(observedRelocation - destinationSpawn.position) +
                    ", final delta: " +
                    FormatVector(observedFinalPosition - destinationSpawn.position));

                IReadOnlyList<PlayModeLogCapture.Entry> entries = logCapture.Entries;
                List<string> transitionErrors = new List<string>();
                int warningCount = 0;
                int additiveLightOverlapErrors = 0;
                int knownYarnExceptions = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    PlayModeLogCapture.Entry entry = entries[i];
                    if (entry.Type == LogType.Error ||
                        entry.Type == LogType.Exception ||
                        entry.Type == LogType.Assert)
                    {
                        if (IsKnownYarnInputException(entry))
                        {
                            knownYarnExceptions++;
                        }
                        else if (IsAdditiveLightOverlapError(entry))
                        {
                            // Both rooms are loaded at the same time while the
                            // destination is activated, so their global 2D
                            // lights briefly overlap. This is scene content
                            // noise from the intentional additive overlap, not
                            // a transition pipeline failure.
                            additiveLightOverlapErrors++;
                        }
                        else
                        {
                            transitionErrors.Add(entry.Type + ": " + entry.Message);
                        }
                    }

                    if (entry.Type == LogType.Warning)
                        warningCount++;
                }

                TestContext.WriteLine(
                    "[RoomTransition] Log entries for setup + transition: " +
                    entries.Count + ", warnings=" + warningCount +
                    ", known additive light-overlap errors=" +
                    additiveLightOverlapErrors +
                    ", known Yarn input exceptions=" + knownYarnExceptions);
                for (int i = 0; i < entries.Count; i++)
                {
                    PlayModeLogCapture.Entry entry = entries[i];
                    if (entry.Type != LogType.Log)
                    {
                        TestContext.WriteLine(
                            "[RoomTransition][Log][" + entry.Type + "] " + entry.Message);
                    }
                }

                Assert.That(
                    transitionErrors,
                    Is.Empty,
                    "The connected transition logged errors:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, transitionErrors));
                Assert.That(
                    transitionCanvasGroup.alpha,
                    Is.EqualTo(0f).Within(0.0001f),
                    "The transition fade did not return to a transparent state.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;

                if (!destinationAdopted &&
                    Fixture.IsSceneLoaded(DestinationScenePath))
                {
                    try
                    {
                        destinationScene = Fixture.GetLoadedScene(DestinationScenePath);
                        Fixture.TakeOwnership(destinationScene);
                        destinationAdopted = true;
                    }
                    catch (Exception)
                    {
                        // Fixture cleanup still protects all fixture-owned scenes.
                    }
                }

                if (IsAlive(manager))
                {
                    if (IsTransitioning(manager) || !managerExisted)
                    {
                        try
                        {
                            ReflectionTestUtilities.InvokeInstance(manager, "StopAllCoroutines");
                            ReflectionTestUtilities.SetInstanceField(
                                manager,
                                "_isTransitioning",
                                false);
                        }
                        catch (Exception)
                        {
                            // Best-effort cleanup of a failed transition.
                        }
                    }

                    if (!managerExisted)
                    {
                        DestroyGameObject(manager);
                    }
                    else if (playerCaptured && IsAlive(player))
                    {
                        try
                        {
                            playerTransform.SetPositionAndRotation(
                                previousPlayerPosition,
                                previousPlayerRotation);
                            if (playerBody != null)
                            {
                                playerBody.bodyType = previousBodyType;
                                playerBody.linearVelocity = previousVelocity;
                                playerBody.angularVelocity = previousAngularVelocity;
                                playerBody.simulated = previousSimulated;
                                playerBody.constraints = previousConstraints;
                            }

                            if (previousHadTransitionRequest)
                                ReflectionTestUtilities.InvokeInstance(
                                    player,
                                    "AddControlRequest",
                                    transitionControlReason);
                            else
                                ReflectionTestUtilities.InvokeInstance(
                                    player,
                                    "RemoveControlRequest",
                                    transitionControlReason);

                            Physics2D.SyncTransforms();
                        }
                        catch (Exception)
                        {
                            // Best-effort restoration of the pre-existing player.
                        }
                    }

                    if (cameraConfiner != null)
                    {
                        try
                        {
                            ReflectionTestUtilities.SetInstanceField(
                                cameraConfiner,
                                "BoundingShape2D",
                                previousCameraBounds);
                            ReflectionTestUtilities.InvokeInstance(
                                cameraConfiner,
                                "InvalidateBoundingShapeCache");
                        }
                        catch (Exception)
                        {
                            // Best-effort camera cleanup; the fixture restores
                            // manager fields even if the camera cannot be reset.
                        }
                    }

                    if (transitionCanvasGroup != null)
                        transitionCanvasGroup.alpha = previousCanvasAlpha;
                }

                if (masterLoadedByTest)
                {
                    DestroyCreatedPersistentManager(
                        AbilityManagerTypeName,
                        preExistingAbilityManager);
                    DestroyCreatedPersistentManager(
                        CheckpointManagerTypeName,
                        preExistingCheckpointManager);
                }

                if (logCapture != null)
                {
                    try
                    {
                        Fixture.StopLogCapture();
                    }
                    catch (Exception)
                    {
                        // The fixture also attempts to stop capture during cleanup.
                    }
                }

                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        private static void DisableYarnLineAdvancerInput(Scene masterScene)
        {
            // Yarn declares KeyCodes inside the Yarn.Unity.LineAdvancerInput
            // namespace, so its full name uses a dot rather than a nested-type
            // plus. Disable every instance to be robust to DontDestroyOnLoad
            // objects as well as scene hierarchy placement.
            Type inputType =
                FindLoadedType("Yarn.Unity.LineAdvancerInput.KeyCodes");
            if (inputType == null)
                return;

            UnityEngine.Object[] components =
                UnityEngine.Object.FindObjectsByType(
                    inputType,
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                Behaviour behaviour = components[i] as Behaviour;
                if (behaviour != null)
                    behaviour.enabled = false;
            }

            foreach (GameObject root in masterScene.GetRootGameObjects())
            {
                Component[] sceneComponents = root.GetComponentsInChildren(
                    inputType,
                    includeInactive: true);
                for (int i = 0; i < sceneComponents.Length; i++)
                {
                    Behaviour behaviour = sceneComponents[i] as Behaviour;
                    if (behaviour != null)
                        behaviour.enabled = false;
                }
            }
        }

        private static Type FindLoadedType(string fullName)
        {
            System.Reflection.Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, throwOnError: false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static object GetManager(Type managerType)
        {
            return ReflectionTestUtilities.GetStaticProperty(managerType, "Instance");
        }

        private static object GetStaticInstance(string typeName)
        {
            Type type = ReflectionTestUtilities.ResolveRuntimeType(typeName);
            return ReflectionTestUtilities.GetStaticProperty(type, "Instance");
        }

        private static bool IsAlive(object unityObject)
        {
            if (unityObject == null)
                return false;

            UnityEngine.Object candidate = unityObject as UnityEngine.Object;
            return candidate != null;
        }

        private static bool IsTransitioning(object manager)
        {
            return ReflectionTestUtilities.GetInstanceField<bool>(
                manager,
                "_isTransitioning");
        }

        private static object ResolveRoomIdentity(
            Type identityResolverType,
            string scenePath,
            string expectedRoomId)
        {
            object result = ReflectionTestUtilities.InvokeStatic(
                identityResolverType,
                "ResolveByScenePath",
                scenePath);

            Assert.That(
                GetStatus(result),
                Is.EqualTo("Success"),
                $"Room identity did not resolve for '{scenePath}': " + GetMessage(result));

            object room = ReflectionTestUtilities.GetProperty(result, "Room");
            Assert.That(room, Is.Not.Null);
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(room, "RoomId"),
                Is.EqualTo(expectedRoomId));
            return room;
        }

        private static string GetStatus(object result)
        {
            return ReflectionTestUtilities.GetProperty(result, "Status").ToString();
        }

        private static string GetMessage(object result)
        {
            return ReflectionTestUtilities.GetProperty<string>(result, "Message");
        }

        private static string GetPropertyText(object target, string propertyName)
        {
            return ReflectionTestUtilities.GetProperty(target, propertyName).ToString();
        }

        private static void AssertConnection(
            object connection,
            string expectedARoomId,
            string expectedADoorId,
            string expectedBRoomId,
            string expectedBDoorId)
        {
            UnityEngine.Object connectionObject = connection as UnityEngine.Object;
            Assert.That(connectionObject, Is.Not.Null);
            Assert.That(connectionObject.name, Is.EqualTo(ConnectionAssetName));
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    connection,
                    "EndpointARoomId"),
                Is.EqualTo(expectedARoomId));
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    connection,
                    "EndpointADoorId"),
                Is.EqualTo(expectedADoorId));
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    connection,
                    "EndpointBRoomId"),
                Is.EqualTo(expectedBRoomId));
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    connection,
                    "EndpointBDoorId"),
                Is.EqualTo(expectedBDoorId));
            Assert.That(
                GetPropertyText(connection, "Direction"),
                Is.EqualTo("Bidirectional"));
        }

        private static Component FindUniqueDoor(
            Type doorType,
            Scene scene,
            string doorId,
            string role)
        {
            List<Component> matches = new List<Component>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component[] components = root.GetComponentsInChildren(
                    doorType,
                    includeInactive: true);
                for (int i = 0; i < components.Length; i++)
                {
                    Component candidate = components[i];
                    if (candidate == null)
                        continue;

                    if (string.Equals(
                            ReflectionTestUtilities.GetProperty<string>(
                                candidate,
                                "DoorId"),
                            doorId,
                            StringComparison.Ordinal))
                    {
                        matches.Add(candidate);
                    }
                }
            }

            Assert.That(
                matches,
                Has.Count.EqualTo(1),
                $"Expected exactly one {role} door with DoorId '{doorId}'.");
            return matches[0];
        }

        private static Array CreateDoorArray(Type doorType, Scene scene)
        {
            List<Component> doors = new List<Component>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component[] components = root.GetComponentsInChildren(
                    doorType,
                    includeInactive: true);
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null)
                        doors.Add(components[i]);
                }
            }

            Array result = Array.CreateInstance(doorType, doors.Count);
            for (int i = 0; i < doors.Count; i++)
                result.SetValue(doors[i], i);
            return result;
        }

        private static Collider2D FindUniqueCameraBounds(Scene scene)
        {
            List<Collider2D> matches = new List<Collider2D>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(
                    includeInactive: true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null || !candidate.CompareTag("CameraBounds"))
                        continue;

                    Collider2D collider = candidate.GetComponent<Collider2D>();
                    if (collider != null)
                        matches.Add(collider);
                }
            }

            Assert.That(
                matches,
                Has.Count.EqualTo(1),
                $"Expected exactly one CameraBounds collider in '{scene.path}'.");
            return matches[0];
        }

        private static int CountLoadedScenes(string scenePath)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded &&
                    string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertPlayerPosition(
            string label,
            Vector3 observed,
            Vector3 expected,
            float tolerance)
        {
            Vector3 delta = observed - expected;
            Assert.That(
                Mathf.Abs(delta.x),
                Is.LessThanOrEqualTo(tolerance),
                $"{label} X differs from the destination SpawnPoint by {delta.x}. " +
                $"Observed={FormatVector(observed)}, expected={FormatVector(expected)}.");
            Assert.That(
                Mathf.Abs(delta.y),
                Is.LessThanOrEqualTo(tolerance),
                $"{label} Y differs from the destination SpawnPoint by {delta.y}. " +
                $"Observed={FormatVector(observed)}, expected={FormatVector(expected)}.");
            Assert.That(
                Mathf.Abs(delta.z),
                Is.LessThanOrEqualTo(tolerance),
                $"{label} Z differs from the destination SpawnPoint by {delta.z}. " +
                $"Observed={FormatVector(observed)}, expected={FormatVector(expected)}.");
        }

        private static void AssertPlayerSettledPosition(
            string label,
            Vector3 observed,
            Vector3 expected)
        {
            Vector3 delta = observed - expected;
            Assert.That(
                Mathf.Abs(delta.x),
                Is.LessThanOrEqualTo(PlayerSettleHorizontalTolerance),
                $"{label} X drifted from the destination SpawnPoint by {delta.x}. " +
                $"Observed={FormatVector(observed)}, expected={FormatVector(expected)}.");
            Assert.That(
                Mathf.Abs(delta.z),
                Is.LessThanOrEqualTo(PlayerSettleHorizontalTolerance),
                $"{label} Z drifted from the destination SpawnPoint by {delta.z}. " +
                $"Observed={FormatVector(observed)}, expected={FormatVector(expected)}.");
            Assert.That(
                Mathf.Abs(delta.y),
                Is.LessThanOrEqualTo(PlayerSettleVerticalTolerance),
                $"{label} Y differs from the destination SpawnPoint by {delta.y}. " +
                $"Observed={FormatVector(observed)}, expected={FormatVector(expected)}.");
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F4") + ", " +
                   value.y.ToString("F4") + ", " +
                   value.z.ToString("F4") + ")";
        }

        private static bool Contains(string value, string fragment)
        {
            return value != null &&
                   value.IndexOf(fragment, StringComparison.Ordinal) >= 0;
        }

        private static bool IsKnownYarnInputException(PlayModeLogCapture.Entry entry)
        {
            return entry.Type == LogType.Exception &&
                   Contains(entry.Message, "NullReferenceException") &&
                   Contains(
                       entry.StackTrace,
                       "Yarn.Unity.InputSystemAvailability.GetKeyDown");
        }

        private static bool IsAdditiveLightOverlapError(PlayModeLogCapture.Entry entry)
        {
            return entry.Type == LogType.Error &&
                   Contains(entry.Message, "More than one global light on layer ") &&
                   Contains(
                       entry.Message,
                       " for light blend style index 0");
        }

        private static void DestroyCreatedPersistentManager(
            string typeName,
            object preExistingInstance)
        {
            if (IsAlive(preExistingInstance))
                return;

            Type type = ReflectionTestUtilities.ResolveRuntimeType(typeName);
            object current = ReflectionTestUtilities.GetStaticProperty(type, "Instance");
            if (!IsAlive(current))
                return;

            DestroyGameObject(current);
        }

        private static void DestroyGameObject(object component)
        {
            Component unityComponent = component as Component;
            if (unityComponent != null)
                UnityEngine.Object.Destroy(unityComponent.gameObject);
        }
    }
}
