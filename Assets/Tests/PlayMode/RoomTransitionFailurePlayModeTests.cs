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
    /// Phase 6.6.4 integration coverage for connected transition failure and
    /// rollback. Failures are induced only through runtime-only state on
    /// instantiated scene components or through a transient runtime
    /// RoomConnection instance; no project asset is modified.
    /// </summary>
    public class RoomTransitionFailurePlayModeTests : PlayModeTestBase
    {
        private const string MasterScenePath = "Assets/Scenes/Master.unity";
        private const string SourceScenePath =
            "Assets/Scenes/Rooms/Re-Entry Module/Re-EntryModule_WakeRoom.unity";
        private const string DestinationScenePath =
            "Assets/Scenes/Rooms/Re-Entry Module/Re-EntryModule_Corridor.unity";

        private const string SourceRoomId = "room_0cd7bdc0537c4f4bb6937de388e8858d";
        private const string DestinationRoomId = "room_f4a2fbe688fb4506af039512474bd0f7";
        private const string SourceDoorId = "Re-EntryModule_WakeRoom_Right";
        private const string DestinationDoorId = "Re-EntryModule_Corridor_Left";

        private const float TransitionTimeoutSeconds = 30f;
        private const float PlayerPositionTolerance = 0.05f;
        private const float CameraPositionTolerance = 0.1f;

        private const string ManagerTypeName = "GameSceneManager";
        private const string DoorTypeName = "RoomTransition";
        private const string PlayerTypeName = "Player";
        private const string IdentityResolverTypeName = "RoomIdentityResolver";
        private const string ConnectionTypeName = "RoomConnection";
        private const string ConnectionDirectionTypeName = "RoomConnectionDirection";
        private const string AbilityManagerTypeName = "SpearWander.Abilities.AbilityManager";
        private const string CheckpointManagerTypeName = "CheckpointManager";

        private const string TransitionControlReasonName = "Transition";

        private static class FaultStages
        {
            public const string SourceValidation = "source-validation";
            public const string SnapshotCapture = "snapshot-capture";
            public const string FreezeControlSetup = "freeze-control-setup";
            public const string FadeOut = "fade-out";
            public const string DestinationLoadStart = "destination-load-start";
            public const string DestinationValidation = "destination-validation";
            public const string RelocationBeforeMutation = "player-relocation-before-mutation";
            public const string RelocationAfterMutation = "player-relocation-after-mutation";
            public const string DestinationActivation = "destination-activation";
            public const string DestinationCameraSetup = "destination-camera-setup";
            public const string PreCommit = "pre-commit";
            public const string RollbackPlayerTransform = "rollback-player-transform";
            public const string RollbackCamera = "rollback-camera";
            public const string RollbackDestinationUnload = "rollback-destination-unload";
            public const string RollbackFade = "rollback-fade";
            public const string RollbackTimescale = "rollback-timescale";
            public const string RollbackTransitionLock = "rollback-transition-lock";
            public const string PostCommitSourceUnload = "post-commit-source-unload";
            public const string PostCommitSourceUnloadWait = "post-commit-source-unload-wait";
            public const string PostCommitFadeIn = "post-commit-fade-in";
            public const string PostCommitControlRestore = "post-commit-control-restore";
            public const string PostCommitTimescaleRestore = "post-commit-timescale-restore";
        }

        /// <summary>
        /// Mutable state for one failure scenario. It is a class so setup and
        /// assertions can share it across iterator methods.
        /// </summary>
        private sealed class FailureScenario
        {
            public Type ManagerType;
            public Type DoorType;
            public Type PlayerType;
            public Type IdentityResolverType;
            public object TransitionControlReason;

            public object Manager;
            public bool ManagerExisted;
            public bool MasterLoadedByTest;
            public object PreExistingAbilityManager;
            public object PreExistingCheckpointManager;

            public Scene SourceScene;
            public Scene MasterScene;
            public Component SourceDoor;
            public object SourceConnection;
            public object SourceRoom;

            public Transform PlayerTransform;
            public object Player;
            public Rigidbody2D PlayerBody;
            public Vector3 PlayerPosition;
            public Quaternion PlayerRotation;
            public RigidbodyType2D BodyType;
            public Vector2 Velocity;
            public float AngularVelocity;
            public bool Simulated;
            public RigidbodyConstraints2D Constraints;
            public bool HadControl;
            public bool HadTransitionControl;

            public object CameraConfiner;
            public Collider2D CameraBounds;
            public Vector3 CameraPosition;
            public Quaternion CameraRotation;

            public CanvasGroup Canvas;
            public float CanvasAlpha;

            public string ManagerRoomScene;
            public object ManagerRoomDefinition;
            public string ManagerTargetDoorId;
            public float TimeScale;

            public PlayModeLogCapture Log;
            public bool PreviousIgnoreFailingMessages;
            public bool Captured;

            public Scene DestinationScene;
            public Component DestinationDoor;
            public object DestinationOriginalConnection;
            public bool DestinationSpawnPointMutated;
            public Transform DestinationOriginalSpawnPoint;
            public ScriptableObject TransientConnection;

            public Action<string> FaultInjector;
            public bool FaultInjectorInstalled;
            public bool TimedOut;
        }

        [UnityTest]
        public IEnumerator MissingDestinationBacklinkRollsBackToSource()
        {
            FailureScenario scenario = new FailureScenario();
            bool destinationObserved = false;

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler =
                    (scene, mode) =>
                {
                    if (!string.Equals(scene.path, DestinationScenePath, StringComparison.Ordinal))
                        return;

                    destinationObserved = true;
                    scenario.DestinationScene = scene;
                    scenario.DestinationDoor = FindUniqueDoor(
                        scenario.DoorType,
                        scene,
                        DestinationDoorId,
                        "destination");
                    scenario.DestinationOriginalConnection =
                        ReflectionTestUtilities.GetProperty(
                            scenario.DestinationDoor,
                            "Connection");

                    // Runtime-only removal of the destination backlink. The
                    // production resolver must reject the destination before
                    // the transition commits.
                    ReflectionTestUtilities.SetInstanceField(
                        scenario.DestinationDoor,
                        "connection",
                        null);
                };

                SceneManager.sceneLoaded += handler;
                try
                {
                    TriggerTransition(scenario);
                    AssertTransitionStarted(scenario);
                    yield return WaitForTransitionEnd(scenario);
                }
                finally
                {
                    SceneManager.sceneLoaded -= handler;
                }

                Assert.That(
                    scenario.TimedOut,
                    Is.False,
                    $"The failed transition did not settle within {TransitionTimeoutSeconds} seconds.");

                Assert.That(
                    destinationObserved,
                    Is.True,
                    "The destination scene never loaded, so the backlink failure was not exercised.");

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "destination validation",
                    expectedRollbackFragment: "does not backlink to connection",
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[FailureRollback] Scenario: missing destination backlink");
                TestContext.WriteLine(
                    "[FailureRollback] Destination was loaded then rolled back; source remains active.");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator MissingDestinationSpawnPointRollsBackToSource()
        {
            FailureScenario scenario = new FailureScenario();
            bool destinationObserved = false;

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler =
                    (scene, mode) =>
                {
                    if (!string.Equals(scene.path, DestinationScenePath, StringComparison.Ordinal))
                        return;

                    destinationObserved = true;
                    scenario.DestinationScene = scene;
                    scenario.DestinationDoor = FindUniqueDoor(
                        scenario.DoorType,
                        scene,
                        DestinationDoorId,
                        "destination");
                    scenario.DestinationOriginalSpawnPoint =
                        ReflectionTestUtilities.GetProperty<Transform>(
                            scenario.DestinationDoor,
                            "SpawnPoint");

                    // Runtime-only removal of the destination SpawnPoint. The
                    // backlink stays valid so the failure is specifically the
                    // spawn validation stage.
                    scenario.DestinationSpawnPointMutated = true;
                    ReflectionTestUtilities.SetInstanceField(
                        scenario.DestinationDoor,
                        "_spawnPoint",
                        null);
                };

                SceneManager.sceneLoaded += handler;
                try
                {
                    TriggerTransition(scenario);
                    AssertTransitionStarted(scenario);
                    yield return WaitForTransitionEnd(scenario);
                }
                finally
                {
                    SceneManager.sceneLoaded -= handler;
                }

                Assert.That(
                    scenario.TimedOut,
                    Is.False,
                    $"The failed transition did not settle within {TransitionTimeoutSeconds} seconds.");

                Assert.That(
                    destinationObserved,
                    Is.True,
                    "The destination scene never loaded, so the SpawnPoint failure was not exercised.");

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "destination validation",
                    expectedRollbackFragment: "has no valid SpawnPoint in the destination scene",
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[FailureRollback] Scenario: missing destination SpawnPoint");
                TestContext.WriteLine(
                    "[FailureRollback] Destination was loaded then rolled back; source remains active.");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator UnregisteredDestinationRoomFailsAtSourceValidation()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                Type connectionType =
                    ReflectionTestUtilities.ResolveRuntimeType(ConnectionTypeName);
                Type directionType =
                    ReflectionTestUtilities.ResolveRuntimeType(ConnectionDirectionTypeName);

                // A transient, runtime-only RoomConnection that points to a
                // destination RoomId absent from the runtime catalog. No asset
                // and no catalog entry is modified.
                scenario.TransientConnection =
                    ScriptableObject.CreateInstance(connectionType);
                ReflectionTestUtilities.SetInstanceField(
                    scenario.TransientConnection,
                    "endpointARoomId",
                    SourceRoomId);
                ReflectionTestUtilities.SetInstanceField(
                    scenario.TransientConnection,
                    "endpointADoorId",
                    SourceDoorId);
                ReflectionTestUtilities.SetInstanceField(
                    scenario.TransientConnection,
                    "endpointBRoomId",
                    "room_unregistered_phase_6_6_4");
                ReflectionTestUtilities.SetInstanceField(
                    scenario.TransientConnection,
                    "endpointBDoorId",
                    "UnregisteredDoor");
                ReflectionTestUtilities.SetInstanceField(
                    scenario.TransientConnection,
                    "direction",
                    Enum.Parse(directionType, "Bidirectional"));

                ReflectionTestUtilities.SetInstanceField(
                    scenario.SourceDoor,
                    "connection",
                    scenario.TransientConnection);

                TriggerTransition(scenario);
                yield return null;

                Assert.That(
                    IsTransitioning(scenario.Manager),
                    Is.False,
                    "The rejected transition must not keep the transition lock.");
                Assert.That(
                    CountLoadedScenes(DestinationScenePath),
                    Is.EqualTo(0),
                    "An unregistered destination must not load a scene.");

                AssertSourceStateUnchanged(scenario);
                Vector3 drift =
                    scenario.PlayerTransform.position - scenario.PlayerPosition;
                Assert.That(
                    Mathf.Abs(drift.x),
                    Is.LessThanOrEqualTo(PlayerPositionTolerance),
                    "The rejected transition moved the player horizontally.");
                Assert.That(
                    Mathf.Abs(drift.z),
                    Is.LessThanOrEqualTo(PlayerPositionTolerance),
                    "The rejected transition moved the player horizontally.");
                Assert.That(
                    Mathf.Abs(drift.y),
                    Is.LessThanOrEqualTo(0.6f),
                    "The rejected transition moved the player vertically beyond normal gravity settle.");
                Assert.That(
                    scenario.PlayerBody.bodyType,
                    Is.EqualTo(scenario.BodyType),
                    "The rejected transition changed the player Rigidbody2D body type.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<bool>(
                        scenario.Player,
                        "HasControl"),
                    Is.EqualTo(scenario.HadControl),
                    "The rejected transition changed player control.");
                Assert.That(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        scenario.Player,
                        "HasControlRequest",
                        scenario.TransitionControlReason),
                    Is.EqualTo(scenario.HadTransitionControl),
                    "The rejected transition changed the player's Transition control request.");
                Assert.That(
                    Time.timeScale,
                    Is.EqualTo(scenario.TimeScale).Within(0.0001f),
                    "The rejected transition changed Time.timeScale.");
                Assert.That(
                    scenario.Canvas.alpha,
                    Is.EqualTo(scenario.CanvasAlpha).Within(0.0001f),
                    "The rejected transition changed the transition fade alpha.");
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 1,
                    expectedRollbackErrors: 0,
                    expectedSourceValidationFragment: "InvalidDestinationRoom");

                TestContext.WriteLine(
                    "[FailureRollback] Scenario: unregistered destination RoomId");
                TestContext.WriteLine(
                    "[FailureRollback] Transition was rejected at source validation; no scene load or freeze occurred.");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator SourceValidationExceptionPreservesPreTransitionState()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.SourceValidation);

                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationNotLoaded(
                    "A source validation failure must not load the destination.");
                Assert.That(
                    scenario.Canvas.alpha,
                    Is.EqualTo(scenario.CanvasAlpha).Within(0.0001f),
                    "A source validation failure must not change the fade alpha.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 1,
                    expectedRollbackErrors: 0,
                    expectedSourceValidationFragment:
                        "Injected failure at " + FaultStages.SourceValidation);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: source validation exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator SnapshotCaptureExceptionFailsAtSourceValidation()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.SnapshotCapture);

                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationNotLoaded(
                    "A snapshot failure must not load the destination.");
                Assert.That(
                    scenario.Canvas.alpha,
                    Is.EqualTo(scenario.CanvasAlpha).Within(0.0001f),
                    "A snapshot failure must not change the fade alpha.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 1,
                    expectedRollbackErrors: 0,
                    expectedSourceValidationFragment:
                        "Injected failure at " + FaultStages.SnapshotCapture);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: snapshot capture exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator FreezeControlSetupFailureRollsBack()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.FreezeControlSetup);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "freeze/control setup",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.FreezeControlSetup,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: freeze/control setup exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator FadeOutFailureRollsBackBeforeDestinationLoad()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.FadeOut);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "fade-out",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.FadeOut,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: fade-out exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator DestinationLoadStartFailureRollsBack()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.DestinationLoadStart);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "destination load",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.DestinationLoadStart,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: destination load start exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator DestinationValidationExceptionRollsBackAndUnloadsDestination()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.DestinationValidation);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "destination validation",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.DestinationValidation,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: destination validation exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator RelocationBeforeMutationFailureRollsBack()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.RelocationBeforeMutation);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "player relocation",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.RelocationBeforeMutation,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: relocation exception before mutation");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator RelocationAfterPartialMutationFailureRollsBack()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.RelocationAfterMutation);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "player relocation",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.RelocationAfterMutation,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: relocation exception after partial mutation");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator DestinationActivationFailureRollsBack()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.DestinationActivation);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "destination activation and camera setup",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.DestinationActivation,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: destination activation exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator DestinationCameraSetupFailureRestoresActiveScene()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.DestinationCameraSetup);

                AssertRollbackInvariants(scenario);
                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "destination activation and camera setup",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.DestinationCameraSetup,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: destination camera setup exception");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator PlayerTransformRollbackFailureDoesNotStopCleanup()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PreCommit,
                    FaultStages.RollbackPlayerTransform);

                CaptureLoadedDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationNotLoaded(
                    "A failed player transform restoration must not prevent destination cleanup.");
                Assert.That(
                    Vector3.Distance(
                        scenario.PlayerTransform.position,
                        scenario.PlayerPosition),
                    Is.GreaterThan(0.1f),
                    "The injected player transform restoration failure was not observable.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "pre-commit",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.PreCommit,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "Rollback step failed (player transform restoration)"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: player transform rollback failure");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator CameraRollbackFailureDoesNotStopCleanup()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PreCommit,
                    FaultStages.RollbackCamera);

                CaptureLoadedDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationNotLoaded(
                    "A failed camera restoration must not prevent destination cleanup.");

                Collider2D activeBounds =
                    ReflectionTestUtilities.GetInstanceField<Collider2D>(
                        scenario.CameraConfiner,
                        "BoundingShape2D");
                Assert.That(
                    activeBounds,
                    Is.Not.SameAs(scenario.CameraBounds),
                    "The injected camera restoration failure was not observable.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "pre-commit",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.PreCommit,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "Rollback step failed (camera bounds restoration)"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: camera rollback failure");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator DestinationUnloadRollbackFailureDoesNotStopControlRestore()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PreCommit,
                    FaultStages.RollbackDestinationUnload);

                CaptureLoadedDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationLoadedOnce(
                    "The injected destination unload failure must leave the destination loaded.");
                Assert.That(
                    scenario.PlayerBody.bodyType,
                    Is.EqualTo(scenario.BodyType),
                    "Rigidbody restoration did not continue after the unload failure.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "pre-commit",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.PreCommit,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "Rollback step failed (destination scene cleanup"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: destination unload rollback failure");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator FadeRollbackFailureDoesNotStopCleanup()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PreCommit,
                    FaultStages.RollbackFade);

                CaptureLoadedDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationNotLoaded(
                    "A failed fade restoration must not prevent destination cleanup.");
                Assert.That(
                    scenario.Canvas.alpha,
                    Is.Not.EqualTo(scenario.CanvasAlpha).Within(0.0001f),
                    "The injected fade restoration failure was not observable.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "pre-commit",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.PreCommit,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "Rollback step failed (fade state restoration)"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: fade rollback failure");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator TimeScaleAndLockRollbackFailuresAreRescuedByFinalCleanup()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PreCommit,
                    FaultStages.RollbackTimescale,
                    FaultStages.RollbackTransitionLock);

                CaptureLoadedDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationNotLoaded(
                    "A failed time scale or lock restoration must not prevent destination cleanup.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "pre-commit",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.PreCommit,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "Rollback step failed (time scale restoration)",
                        "Rollback step failed (transition lock release)"
                    },
                    expectedAdditionalErrorCount: 2);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: time scale and lock rollback failures");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator MultipleRollbackFailuresStillRestoreControlTimeAndLock()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PreCommit,
                    FaultStages.RollbackPlayerTransform,
                    FaultStages.RollbackCamera,
                    FaultStages.RollbackDestinationUnload,
                    FaultStages.RollbackFade);

                CaptureLoadedDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                AssertSourceStateUnchanged(scenario);
                AssertSourceLoadedAndActive(scenario);
                AssertDestinationLoadedOnce(
                    "The injected destination unload failure must leave the destination loaded.");
                Assert.That(
                    Vector3.Distance(
                        scenario.PlayerTransform.position,
                        scenario.PlayerPosition),
                    Is.GreaterThan(0.1f),
                    "The injected player transform restoration failure was not observable.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: "pre-commit",
                    expectedRollbackFragment:
                        "Injected failure at " + FaultStages.PreCommit,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 1,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "Rollback step failed (player transform restoration)",
                        "Rollback step failed (camera bounds restoration)",
                        "Rollback step failed (destination scene cleanup",
                        "Rollback step failed (fade state restoration)"
                    },
                    expectedAdditionalErrorCount: 4);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: multiple rollback failures");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator PostCommitSourceUnloadFailureDoesNotRollback()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PostCommitSourceUnload);

                CaptureLoadedDestination(scenario);
                AssertCommittedToDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                Assert.That(
                    Fixture.IsSceneLoaded(SourceScenePath),
                    Is.True,
                    "An injected source unload failure should leave the source loaded.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<string>(
                        scenario.Manager,
                        "CurrentRoom"),
                    Is.EqualTo(scenario.DestinationScene.name),
                    "A post-commit source unload failure must not roll back the room identity.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 0,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "source unload failed"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: post-commit source unload failure");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator PostCommitFadeInFailureStillRestoresControl()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PostCommitFadeIn);

                CaptureLoadedDestination(scenario);
                AssertCommittedToDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                Assert.That(
                    Fixture.IsSceneLoaded(SourceScenePath),
                    Is.False,
                    "The source scene should have unloaded after commit.");
                Assert.That(
                    scenario.Canvas.alpha,
                    Is.GreaterThan(0.5f),
                    "The injected fade-in failure was not observable.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 0,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "fade-in failed"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: post-commit fade-in failure");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator PostCommitControlAndTimeFailuresAreRescuedByFinalCleanup()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PostCommitFadeIn,
                    FaultStages.PostCommitControlRestore,
                    FaultStages.PostCommitTimescaleRestore);

                CaptureLoadedDestination(scenario);
                AssertCommittedToDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                Assert.That(
                    scenario.PlayerBody.bodyType,
                    Is.EqualTo(scenario.BodyType),
                    "Final cleanup did not restore the player body type.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 0,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "fade-in failed",
                        "player control restoration failed",
                        "Time.timeScale restoration failed"
                    },
                    expectedAdditionalErrorCount: 3);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: post-commit control and time failures");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        [UnityTest]
        public IEnumerator PostCommitSourceUnloadFailureAfterStartIsReported()
        {
            FailureScenario scenario = new FailureScenario();

            try
            {
                yield return PrepareScenario(scenario);
                AssertDestinationNotLoadedBeforeTest();

                yield return TriggerFaultedTransition(
                    scenario,
                    FaultStages.PostCommitSourceUnloadWait);

                CaptureLoadedDestination(scenario);
                AssertCommittedToDestination(scenario);
                AssertRestoredControlTimeAndLock(scenario);
                Assert.That(
                    Fixture.IsSceneLoaded(SourceScenePath),
                    Is.False,
                    "The asynchronous source unload should still complete after the reported fault.");

                AssertExpectedLogs(
                    scenario,
                    expectedRollbackStage: null,
                    expectedRollbackFragment: null,
                    expectedSourceValidationErrors: 0,
                    expectedRollbackErrors: 0,
                    expectedAdditionalErrorFragments: new[]
                    {
                        "source unload failed after the unload operation started"
                    },
                    expectedAdditionalErrorCount: 1);

                TestContext.WriteLine(
                    "[ExceptionSafety] Scenario: post-commit source unload failure after start");
            }
            finally
            {
                CleanupScenario(scenario);
            }
        }

        private IEnumerator PrepareScenario(FailureScenario scenario)
        {
            scenario.ManagerType =
                ReflectionTestUtilities.ResolveRuntimeType(ManagerTypeName);
            scenario.DoorType =
                ReflectionTestUtilities.ResolveRuntimeType(DoorTypeName);
            scenario.PlayerType =
                ReflectionTestUtilities.ResolveRuntimeType(PlayerTypeName);
            scenario.IdentityResolverType =
                ReflectionTestUtilities.ResolveRuntimeType(IdentityResolverTypeName);

            Type controlReasonType = scenario.PlayerType.GetNestedType(
                "ControlReason");
            Assert.That(
                controlReasonType,
                Is.Not.Null,
                "Player.ControlReason was not found.");
            scenario.TransitionControlReason =
                Enum.Parse(controlReasonType, TransitionControlReasonName);

            scenario.Manager = GetManager(scenario.ManagerType);
            scenario.ManagerExisted = IsAlive(scenario.Manager);
            bool masterExisted = Fixture.IsSceneLoaded(MasterScenePath);

            // A persistent manager without Master can be left over by the
            // PlayMode test host. It is only usable when its scene-owned player
            // and camera references are still alive. Replace an orphaned
            // manager before loading Master so its duplicate-instance guard
            // cannot discard the valid manager created by that scene.
            if (!masterExisted && scenario.ManagerExisted)
            {
                Transform existingPlayer =
                    ReflectionTestUtilities.GetInstanceField<Transform>(
                        scenario.Manager,
                        "playerTransform");
                Component existingCamera =
                    ReflectionTestUtilities.GetInstanceField<Component>(
                        scenario.Manager,
                        "cameraConfiner");

                if (existingPlayer == null || existingCamera == null)
                {
                    DestroyGameObject(scenario.Manager);
                    yield return null;
                    scenario.Manager = null;
                    scenario.ManagerExisted = false;
                }
            }

            Assert.That(
                Fixture.IsSceneLoaded(SourceScenePath),
                Is.False,
                $"Source scene '{SourceScenePath}' was already loaded before the test.");

            scenario.PreExistingAbilityManager =
                GetStaticInstance(AbilityManagerTypeName);
            scenario.PreExistingCheckpointManager =
                GetStaticInstance(CheckpointManagerTypeName);

            // The real Master scene loads Yarn Spinner, whose input advancer
            // throws a known batchmode-only NullReferenceException. Failing
            // messages are asserted manually instead.
            scenario.PreviousIgnoreFailingMessages =
                LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            scenario.Log = Fixture.StartLogCapture();

            yield return Fixture.LoadSceneAdditive(
                SourceScenePath,
                scene => scenario.SourceScene = scene);

            Assert.That(scenario.SourceScene.IsValid(), Is.True);
            Assert.That(scenario.SourceScene.isLoaded, Is.True);

            scenario.SourceRoom = ResolveRoomIdentity(
                scenario.IdentityResolverType,
                SourceScenePath,
                SourceRoomId);

            if (!masterExisted)
            {
                yield return Fixture.LoadSceneAdditive(
                    MasterScenePath,
                    scene =>
                    {
                        scenario.MasterScene = scene;
                        DisableYarnLineAdvancerInput();
                    });
                yield return null;
                scenario.MasterLoadedByTest = true;

                float deadline =
                    Time.realtimeSinceStartup + TransitionTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    scenario.Manager = GetManager(scenario.ManagerType);
                    if (IsAlive(scenario.Manager) &&
                        string.Equals(
                            ReflectionTestUtilities.GetInstanceField<string>(
                                scenario.Manager,
                                "_currentRoomScene"),
                            scenario.SourceScene.name,
                            StringComparison.Ordinal))
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(
                    IsAlive(scenario.Manager),
                    Is.True,
                    "The Master scene did not create a live GameSceneManager.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<string>(
                        scenario.Manager,
                        "_currentRoomScene"),
                    Is.EqualTo(scenario.SourceScene.name),
                    "GameSceneManager.Start did not select the loaded source room.");
            }
            else
            {
                scenario.MasterScene = Fixture.GetLoadedScene(MasterScenePath);

                float deadline =
                    Time.realtimeSinceStartup + TransitionTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    scenario.Manager = GetManager(scenario.ManagerType);
                    if (IsAlive(scenario.Manager))
                        break;

                    yield return null;
                }

                Assert.That(
                    IsAlive(scenario.Manager),
                    Is.True,
                    "The pre-loaded Master scene has no live GameSceneManager.");

            }

            Assert.That(IsAlive(scenario.Manager), Is.True);
            // A live manager can outlive Master in DontDestroyOnLoad. Whether
            // this setup created Master or reused an existing manager, put the
            // manager and active scene in the exact source-room state needed
            // by this scenario.
            ReflectionTestUtilities.SetInstanceField(
                scenario.Manager,
                "_currentRoomScene",
                scenario.SourceScene.name);
            ReflectionTestUtilities.SetInstanceField(
                scenario.Manager,
                "_currentRoomDefinition",
                scenario.SourceRoom);
            ReflectionTestUtilities.SetInstanceField(
                scenario.Manager,
                "_targetDoorID",
                null);
            ReflectionTestUtilities.SetInstanceField(
                scenario.Manager,
                "_isTransitioning",
                false);
            if (SceneManager.GetActiveScene() != scenario.SourceScene)
            {
                Assert.That(
                    SceneManager.SetActiveScene(scenario.SourceScene),
                    Is.True,
                    "Could not make the source scene active for the failure scenario. " +
                    $"Source path='{scenario.SourceScene.path}', " +
                    $"valid={scenario.SourceScene.IsValid()}, " +
                    $"loaded={scenario.SourceScene.isLoaded}, " +
                    $"active path='{SceneManager.GetActiveScene().path}'.");
            }

            scenario.MasterScene = Fixture.GetLoadedScene(MasterScenePath);
            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(scenario.SourceScene.handle),
                "The source room must be active before the failure scenario starts.");

            scenario.SourceDoor = FindUniqueDoor(
                scenario.DoorType,
                scenario.SourceScene,
                SourceDoorId,
                "source");
            scenario.SourceConnection =
                ReflectionTestUtilities.GetProperty(
                    scenario.SourceDoor,
                    "Connection");
            Assert.That(
                scenario.SourceConnection,
                Is.Not.Null,
                $"Source door '{SourceDoorId}' has no RoomConnection.");

            CaptureSnapshot(scenario);
        }

        private static void CaptureSnapshot(FailureScenario scenario)
        {
            scenario.TimeScale = Time.timeScale;
            scenario.ManagerRoomScene =
                ReflectionTestUtilities.GetInstanceField<string>(
                    scenario.Manager,
                    "_currentRoomScene");
            scenario.ManagerRoomDefinition =
                ReflectionTestUtilities.GetInstanceField(
                    scenario.Manager,
                    "_currentRoomDefinition");
            scenario.ManagerTargetDoorId =
                ReflectionTestUtilities.GetInstanceField<string>(
                    scenario.Manager,
                    "_targetDoorID");

            scenario.PlayerTransform =
                ReflectionTestUtilities.GetInstanceField<Transform>(
                    scenario.Manager,
                    "playerTransform");
            Assert.That(
                scenario.PlayerTransform,
                Is.Not.Null,
                "GameSceneManager.playerTransform is not assigned.");
            scenario.Player =
                scenario.PlayerTransform.GetComponent(scenario.PlayerType);
            Assert.That(
                scenario.Player,
                Is.Not.Null,
                "The persistent player has no Player component.");
            scenario.PlayerBody =
                scenario.PlayerTransform.GetComponent<Rigidbody2D>();
            Assert.That(
                scenario.PlayerBody,
                Is.Not.Null,
                "The persistent player has no Rigidbody2D.");

            scenario.PlayerPosition = scenario.PlayerTransform.position;
            scenario.PlayerRotation = scenario.PlayerTransform.rotation;
            scenario.BodyType = scenario.PlayerBody.bodyType;
            scenario.Velocity = scenario.PlayerBody.linearVelocity;
            scenario.AngularVelocity = scenario.PlayerBody.angularVelocity;
            scenario.Simulated = scenario.PlayerBody.simulated;
            scenario.Constraints = scenario.PlayerBody.constraints;
            scenario.HadControl =
                ReflectionTestUtilities.GetProperty<bool>(
                    scenario.Player,
                    "HasControl");
            scenario.HadTransitionControl =
                ReflectionTestUtilities.InvokeInstance<bool>(
                    scenario.Player,
                    "HasControlRequest",
                    scenario.TransitionControlReason);

            scenario.CameraConfiner =
                ReflectionTestUtilities.GetInstanceField(
                    scenario.Manager,
                    "cameraConfiner");
            Assert.That(
                scenario.CameraConfiner,
                Is.Not.Null,
                "GameSceneManager.cameraConfiner is not assigned.");
            scenario.CameraBounds =
                ReflectionTestUtilities.GetInstanceField<Collider2D>(
                    scenario.CameraConfiner,
                    "BoundingShape2D");
            Component cameraComponent = scenario.CameraConfiner as Component;
            Assert.That(cameraComponent, Is.Not.Null);
            scenario.CameraPosition = cameraComponent.transform.position;
            scenario.CameraRotation = cameraComponent.transform.rotation;

            scenario.Canvas =
                ReflectionTestUtilities.GetInstanceField<CanvasGroup>(
                    scenario.Manager,
                    "transitionCanvasGroup");
            Assert.That(
                scenario.Canvas,
                Is.Not.Null,
                "GameSceneManager.transitionCanvasGroup is not assigned.");
            scenario.CanvasAlpha = scenario.Canvas.alpha;

            scenario.Captured = true;
        }

        private static void TriggerTransition(FailureScenario scenario)
        {
            // Same public request route used by RoomTransition.OnTriggerEnter2D.
            ReflectionTestUtilities.InvokeInstance(
                scenario.Manager,
                "RequestRoomTransition",
                scenario.SourceDoor);
        }

        private static void AssertTransitionStarted(FailureScenario scenario)
        {
            Assert.That(
                IsTransitioning(scenario.Manager),
                Is.True,
                "RequestRoomTransition did not start the connected transition.");
        }

        private static void InstallFaultInjector(
            FailureScenario scenario,
            params string[] stages)
        {
            scenario.FaultInjector = ThrowOnStages(stages);
            scenario.FaultInjectorInstalled = true;
            ReflectionTestUtilities.SetStaticField(
                scenario.ManagerType,
                "_connectedTransitionFaultInjector",
                scenario.FaultInjector);
        }

        private static void ClearFaultInjector(FailureScenario scenario)
        {
            if (scenario == null || scenario.ManagerType == null)
                return;

            ReflectionTestUtilities.SetStaticField(
                scenario.ManagerType,
                "_connectedTransitionFaultInjector",
                null);
            scenario.FaultInjectorInstalled = false;
            scenario.FaultInjector = null;
        }

        private static Action<string> ThrowOnStages(params string[] stages)
        {
            HashSet<string> failingStages =
                new HashSet<string>(stages ?? Array.Empty<string>(), StringComparer.Ordinal);
            return stage =>
            {
                if (failingStages.Contains(stage))
                    throw new InvalidOperationException("Injected failure at " + stage);
            };
        }

        private static IEnumerator TriggerFaultedTransition(
            FailureScenario scenario,
            params string[] faultStages)
        {
            InstallFaultInjector(scenario, faultStages);
            TriggerTransition(scenario);

            // Some source-validation and snapshot failures complete
            // synchronously before StartCoroutine returns, so the lock may
            // already be clear here. Waiting is valid for both cases.
            yield return WaitForTransitionEnd(scenario);
            Assert.That(
                scenario.TimedOut,
                Is.False,
                $"The faulted transition did not settle within {TransitionTimeoutSeconds} seconds.");
        }

        private void CaptureLoadedDestination(FailureScenario scenario)
        {
            if (!Fixture.IsSceneLoaded(DestinationScenePath))
                return;

            scenario.DestinationScene = Fixture.GetLoadedScene(DestinationScenePath);
        }

        private static void AssertDestinationNotLoaded(string context)
        {
            Assert.That(
                CountLoadedScenes(DestinationScenePath),
                Is.EqualTo(0),
                context);
        }

        private static void AssertDestinationLoadedOnce(string context)
        {
            Assert.That(
                CountLoadedScenes(DestinationScenePath),
                Is.EqualTo(1),
                context);
        }

        private static void AssertRestoredControlTimeAndLock(FailureScenario scenario)
        {
            Assert.That(
                IsTransitioning(scenario.Manager),
                Is.False,
                "The transition lock was not released.");
            Assert.That(
                Time.timeScale,
                Is.EqualTo(scenario.TimeScale).Within(0.0001f),
                "Time.timeScale was not restored.");
            Assert.That(
                ReflectionTestUtilities.GetProperty<bool>(
                    scenario.Player,
                    "HasControl"),
                Is.EqualTo(scenario.HadControl),
                "Player control was not restored.");
            Assert.That(
                ReflectionTestUtilities.InvokeInstance<bool>(
                    scenario.Player,
                    "HasControlRequest",
                    scenario.TransitionControlReason),
                Is.EqualTo(scenario.HadTransitionControl),
                "The player's Transition control request was not restored.");
        }

        private void AssertSourceLoadedAndActive(FailureScenario scenario)
        {
            Assert.That(
                Fixture.IsSceneLoaded(SourceScenePath),
                Is.True,
                "The source scene is no longer loaded.");
            Assert.That(
                scenario.SourceScene.isLoaded,
                Is.True,
                "The source scene handle reports unloaded.");
            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(scenario.SourceScene.handle),
                "The source scene is not active.");
        }

        private void AssertCommittedToDestination(FailureScenario scenario)
        {
            Assert.That(
                IsTransitioning(scenario.Manager),
                Is.False,
                "The committed transition did not release the transition lock.");
            Assert.That(
                scenario.DestinationScene.IsValid(),
                Is.True,
                "The destination scene handle is invalid.");
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    scenario.Manager,
                    "CurrentRoom"),
                Is.EqualTo(scenario.DestinationScene.name),
                "The manager did not commit the destination room.");
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    scenario.Manager,
                    "CurrentRoomId"),
                Is.EqualTo(DestinationRoomId),
                "The manager did not commit the destination room identity.");
            AssertDestinationLoadedOnce(
                "The destination scene must remain loaded after commit.");
        }

        private static IEnumerator WaitForTransitionEnd(FailureScenario scenario)
        {
            float deadline =
                Time.realtimeSinceStartup + TransitionTimeoutSeconds;
            while (IsTransitioning(scenario.Manager))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    scenario.TimedOut = true;
                    yield break;
                }

                yield return null;
            }
        }

        private void AssertDestinationNotLoadedBeforeTest()
        {
            Assert.That(
                Fixture.IsSceneLoaded(DestinationScenePath),
                Is.False,
                $"Destination scene '{DestinationScenePath}' was already loaded before the test.");
        }

        private void AssertRollbackInvariants(FailureScenario scenario)
        {
            Assert.That(
                IsTransitioning(scenario.Manager),
                Is.False,
                "The transition lock was not cleared by rollback.");
            Assert.That(scenario.TimedOut, Is.False);

            Assert.That(
                Fixture.IsSceneLoaded(SourceScenePath),
                Is.True,
                "The source scene is no longer loaded after rollback.");
            Assert.That(
                scenario.SourceScene.isLoaded,
                Is.True,
                "The source scene handle reports unloaded after rollback.");
            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(scenario.SourceScene.handle),
                "The source scene is not active after rollback.");
            Assert.That(
                CountLoadedScenes(DestinationScenePath),
                Is.EqualTo(0),
                "The rolled-back destination scene remained loaded.");

            AssertSourceStateUnchanged(scenario);

            Assert.That(
                Vector3.Distance(
                    scenario.PlayerTransform.position,
                    scenario.PlayerPosition),
                Is.LessThanOrEqualTo(PlayerPositionTolerance),
                $"Player was not restored to its original position. Observed={FormatVector(scenario.PlayerTransform.position)}, " +
                $"expected={FormatVector(scenario.PlayerPosition)}.");
            Assert.That(
                scenario.PlayerBody.bodyType,
                Is.EqualTo(scenario.BodyType),
                "Player Rigidbody2D body type was not restored.");
            Assert.That(
                scenario.PlayerBody.simulated,
                Is.EqualTo(scenario.Simulated),
                "Player Rigidbody2D simulated flag was not restored.");
            Assert.That(
                scenario.PlayerBody.constraints,
                Is.EqualTo(scenario.Constraints),
                "Player Rigidbody2D constraints were not restored.");
            Assert.That(
                Vector2.Distance(
                    scenario.PlayerBody.linearVelocity,
                    scenario.Velocity),
                Is.LessThanOrEqualTo(0.5f),
                $"Player Rigidbody2D velocity was not restored. Observed={scenario.PlayerBody.linearVelocity}, " +
                $"expected={scenario.Velocity}.");
            Assert.That(
                Mathf.Abs(
                    scenario.PlayerBody.angularVelocity - scenario.AngularVelocity),
                Is.LessThanOrEqualTo(0.5f),
                $"Player Rigidbody2D angular velocity was not restored. Observed={scenario.PlayerBody.angularVelocity}, " +
                $"expected={scenario.AngularVelocity}.");
            Assert.That(
                ReflectionTestUtilities.GetProperty<bool>(
                    scenario.Player,
                    "HasControl"),
                Is.EqualTo(scenario.HadControl),
                "Player control was not restored by rollback.");
            Assert.That(
                ReflectionTestUtilities.InvokeInstance<bool>(
                    scenario.Player,
                    "HasControlRequest",
                    scenario.TransitionControlReason),
                Is.EqualTo(scenario.HadTransitionControl),
                "The player's Transition control request was not restored by rollback.");

            Assert.That(
                Time.timeScale,
                Is.EqualTo(scenario.TimeScale).Within(0.0001f),
                "Time.timeScale was not restored by rollback.");

            Collider2D activeBounds =
                ReflectionTestUtilities.GetInstanceField<Collider2D>(
                    scenario.CameraConfiner,
                    "BoundingShape2D");
            Assert.That(
                activeBounds,
                Is.SameAs(scenario.CameraBounds),
                "The camera confiner was not restored to the source room bounds.");
            Component cameraComponent = scenario.CameraConfiner as Component;
            Assert.That(
                Vector3.Distance(
                    cameraComponent.transform.position,
                    scenario.CameraPosition),
                Is.LessThanOrEqualTo(CameraPositionTolerance),
                $"Camera position was not restored. Observed={FormatVector(cameraComponent.transform.position)}, " +
                $"expected={FormatVector(scenario.CameraPosition)}.");
            Assert.That(
                Quaternion.Angle(
                    cameraComponent.transform.rotation,
                    scenario.CameraRotation),
                Is.LessThanOrEqualTo(0.5f),
                "Camera rotation was not restored.");

            Assert.That(
                scenario.Canvas.alpha,
                Is.EqualTo(scenario.CanvasAlpha).Within(0.0001f),
                "The transition fade did not return to its original alpha.");
        }

        private static void AssertSourceStateUnchanged(FailureScenario scenario)
        {
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    scenario.Manager,
                    "CurrentRoom"),
                Is.EqualTo(scenario.ManagerRoomScene),
                "CurrentRoom no longer identifies the source room.");
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(
                    scenario.Manager,
                    "CurrentRoomId"),
                Is.EqualTo(SourceRoomId),
                "CurrentRoomId no longer identifies the source room.");
            Assert.That(
                ReflectionTestUtilities.GetProperty(
                    scenario.Manager,
                    "CurrentRoomDefinition"),
                Is.SameAs(scenario.ManagerRoomDefinition),
                "CurrentRoomDefinition no longer identifies the source room.");
            Assert.That(
                ReflectionTestUtilities.GetInstanceField<string>(
                    scenario.Manager,
                    "_targetDoorID"),
                Is.EqualTo(scenario.ManagerTargetDoorId),
                "The manager target door id was not restored.");
        }

        private static void AssertExpectedLogs(
            FailureScenario scenario,
            string expectedRollbackStage,
            string expectedRollbackFragment,
            int expectedSourceValidationErrors,
            int expectedRollbackErrors,
            string expectedSourceValidationFragment = null,
            string[] expectedAdditionalErrorFragments = null,
            int expectedAdditionalErrorCount = -1)
        {
            IReadOnlyList<PlayModeLogCapture.Entry> entries =
                scenario.Log.Entries;
            List<string> unexpected = new List<string>();
            List<string> rollbackMessages = new List<string>();
            List<string> sourceValidationMessages = new List<string>();
            List<string> additionalMessages = new List<string>();
            int knownYarnExceptions = 0;
            int additiveLightOverlapErrors = 0;
            int warningCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                PlayModeLogCapture.Entry entry = entries[i];
                if (entry.Type == LogType.Warning)
                    warningCount++;

                if (entry.Type != LogType.Error &&
                    entry.Type != LogType.Exception &&
                    entry.Type != LogType.Assert)
                {
                    continue;
                }

                if (IsKnownYarnInputException(entry))
                {
                    knownYarnExceptions++;
                }
                else if (IsAdditiveLightOverlapError(entry))
                {
                    additiveLightOverlapErrors++;
                }
                else if (Contains(
                             entry.Message,
                             "[GameSceneManager] Connected transition rolled back at "))
                {
                    rollbackMessages.Add(entry.Message);
                }
                else if (Contains(
                             entry.Message,
                             "[GameSceneManager] Connected transition failed at source validation"))
                {
                    sourceValidationMessages.Add(entry.Message);
                }
                else if (MatchesAny(
                             entry.Message,
                             expectedAdditionalErrorFragments))
                {
                    additionalMessages.Add(entry.Message);
                }
                else
                {
                    unexpected.Add(entry.Type + ": " + entry.Message);
                }
            }

            TestContext.WriteLine(
                "[FailureRollback] Log entries=" + entries.Count +
                ", warnings=" + warningCount +
                ", known Yarn exceptions=" + knownYarnExceptions +
                ", known light-overlap errors=" + additiveLightOverlapErrors +
                ", rollback errors=" + rollbackMessages.Count +
                ", source-validation errors=" + sourceValidationMessages.Count +
                ", additional expected errors=" + additionalMessages.Count);

            Assert.That(
                rollbackMessages,
                Has.Count.EqualTo(expectedRollbackErrors),
                "Unexpected number of rollback error logs.");
            Assert.That(
                sourceValidationMessages,
                Has.Count.EqualTo(expectedSourceValidationErrors),
                "Unexpected number of source-validation error logs.");

            for (int i = 0; i < rollbackMessages.Count; i++)
            {
                Assert.That(
                    rollbackMessages[i],
                    Does.Contain("rolled back at " + expectedRollbackStage),
                    "Rollback reported an unexpected stage: " + rollbackMessages[i]);
                Assert.That(
                    rollbackMessages[i],
                    Does.Contain(expectedRollbackFragment),
                    "Rollback reported an unexpected reason: " + rollbackMessages[i]);
            }

            if (!string.IsNullOrEmpty(expectedSourceValidationFragment))
            {
                for (int i = 0; i < sourceValidationMessages.Count; i++)
                {
                    Assert.That(
                        sourceValidationMessages[i],
                        Does.Contain(expectedSourceValidationFragment),
                        "Source validation reported an unexpected reason: " +
                        sourceValidationMessages[i]);
                }
            }

            if (expectedAdditionalErrorCount >= 0)
            {
                Assert.That(
                    additionalMessages,
                    Has.Count.EqualTo(expectedAdditionalErrorCount),
                    "Unexpected number of additional expected error logs.");
            }

            if (expectedAdditionalErrorFragments != null)
            {
                for (int i = 0; i < expectedAdditionalErrorFragments.Length; i++)
                {
                    string fragment = expectedAdditionalErrorFragments[i];
                    Assert.That(
                        MatchesAny(string.Join("\n", additionalMessages), new[] { fragment }),
                        Is.True,
                        "Expected additional error was not logged: " + fragment);
                }
            }

            Assert.That(
                unexpected,
                Is.Empty,
                "The failure scenario logged unexpected errors:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, unexpected));
        }

        private static bool MatchesAny(string message, string[] fragments)
        {
            if (fragments == null || string.IsNullOrEmpty(message))
                return false;

            for (int i = 0; i < fragments.Length; i++)
            {
                if (Contains(message, fragments[i]))
                    return true;
            }

            return false;
        }

        private void CleanupScenario(FailureScenario scenario)
        {
            try
            {
                ClearFaultInjector(scenario);
            }
            catch (Exception)
            {
                // Best-effort removal of the test-only fault injection hook.
            }

            try
            {
                if (scenario.SourceDoor != null && scenario.SourceConnection != null)
                {
                    ReflectionTestUtilities.SetInstanceField(
                        scenario.SourceDoor,
                        "connection",
                        scenario.SourceConnection);
                }
            }
            catch (Exception)
            {
                // Best-effort restoration of a mutated source door.
            }

            if (scenario.TransientConnection != null)
                UnityEngine.Object.Destroy(scenario.TransientConnection);

            if (scenario.DestinationDoor != null &&
                IsAlive(scenario.DestinationDoor))
            {
                try
                {
                    if (scenario.DestinationOriginalConnection != null)
                    {
                        ReflectionTestUtilities.SetInstanceField(
                            scenario.DestinationDoor,
                            "connection",
                            scenario.DestinationOriginalConnection);
                    }

                    if (scenario.DestinationSpawnPointMutated)
                    {
                        ReflectionTestUtilities.SetInstanceField(
                            scenario.DestinationDoor,
                            "_spawnPoint",
                            scenario.DestinationOriginalSpawnPoint);
                    }
                }
                catch (Exception)
                {
                    // Best-effort restoration of a mutated destination door.
                }
            }

            if (scenario.Captured)
                Time.timeScale = scenario.TimeScale;

            if (scenario.DestinationScene.IsValid() &&
                scenario.DestinationScene.isLoaded)
            {
                try
                {
                    Fixture.TakeOwnership(scenario.DestinationScene);
                }
                catch (Exception)
                {
                    // Fixture cleanup still protects every fixture-owned scene.
                }
            }

            // A failed assertion may abort a test before it captured the
            // destination handle. Adopt any remaining exact-path instance so
            // cleanup cannot leave it loaded for the next test.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (!candidate.IsValid() || !candidate.isLoaded ||
                    !string.Equals(candidate.path, DestinationScenePath, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    Fixture.TakeOwnership(candidate);
                }
                catch (Exception)
                {
                    // Fixture cleanup still protects every fixture-owned scene.
                }
            }

            if (IsAlive(scenario.Manager))
            {
                if (!scenario.ManagerExisted)
                {
                    try
                    {
                        ReflectionTestUtilities.InvokeInstance(
                            scenario.Manager,
                            "StopAllCoroutines");
                        ReflectionTestUtilities.SetInstanceField(
                            scenario.Manager,
                            "_isTransitioning",
                            false);
                    }
                    catch (Exception)
                    {
                        // Best-effort cleanup of a failed transition.
                    }

                    DestroyGameObject(scenario.Manager);
                }
                else
                {
                    if (IsTransitioning(scenario.Manager))
                    {
                        try
                        {
                            ReflectionTestUtilities.InvokeInstance(
                                scenario.Manager,
                                "StopAllCoroutines");
                            ReflectionTestUtilities.SetInstanceField(
                                scenario.Manager,
                                "_isTransitioning",
                                false);
                        }
                        catch (Exception)
                        {
                            // Best-effort cleanup of a failed transition.
                        }
                    }

                    if (scenario.Captured && IsAlive(scenario.Player))
                        RestorePreExistingPlayer(scenario);
                }
            }

            if (scenario.MasterLoadedByTest)
            {
                DestroyCreatedPersistentManager(
                    AbilityManagerTypeName,
                    scenario.PreExistingAbilityManager);
                DestroyCreatedPersistentManager(
                    CheckpointManagerTypeName,
                    scenario.PreExistingCheckpointManager);
            }

            if (scenario.Log != null)
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

            LogAssert.ignoreFailingMessages =
                scenario.PreviousIgnoreFailingMessages;
        }

        private static void RestorePreExistingPlayer(FailureScenario scenario)
        {
            try
            {
                scenario.PlayerTransform.SetPositionAndRotation(
                    scenario.PlayerPosition,
                    scenario.PlayerRotation);
                if (scenario.PlayerBody != null)
                {
                    scenario.PlayerBody.bodyType = scenario.BodyType;
                    scenario.PlayerBody.linearVelocity = scenario.Velocity;
                    scenario.PlayerBody.angularVelocity = scenario.AngularVelocity;
                    scenario.PlayerBody.simulated = scenario.Simulated;
                    scenario.PlayerBody.constraints = scenario.Constraints;
                }

                if (scenario.HadTransitionControl)
                    ReflectionTestUtilities.InvokeInstance(
                        scenario.Player,
                        "AddControlRequest",
                        scenario.TransitionControlReason);
                else
                    ReflectionTestUtilities.InvokeInstance(
                        scenario.Player,
                        "RemoveControlRequest",
                        scenario.TransitionControlReason);

                if (scenario.CameraConfiner != null)
                {
                    ReflectionTestUtilities.SetInstanceField(
                        scenario.CameraConfiner,
                        "BoundingShape2D",
                        scenario.CameraBounds);
                    ReflectionTestUtilities.InvokeInstance(
                        scenario.CameraConfiner,
                        "InvalidateBoundingShapeCache");
                    Component cameraComponent =
                        scenario.CameraConfiner as Component;
                    if (cameraComponent != null)
                    {
                        cameraComponent.transform.SetPositionAndRotation(
                            scenario.CameraPosition,
                            scenario.CameraRotation);
                    }
                }

                if (scenario.Canvas != null)
                    scenario.Canvas.alpha = scenario.CanvasAlpha;

                Physics2D.SyncTransforms();
            }
            catch (Exception)
            {
                // Best-effort restoration of the pre-existing player.
            }
        }

        private static void DisableYarnLineAdvancerInput()
        {
            // Yarn declares KeyCodes inside the Yarn.Unity.LineAdvancerInput
            // namespace. Disable every instance so the known batchmode input
            // NullReferenceException does not repeat every frame.
            Type inputType = FindLoadedType(
                "Yarn.Unity.LineAdvancerInput.KeyCodes");
            if (inputType == null)
                return;

            UnityEngine.Object[] components =
                UnityEngine.Object.FindObjectsByType(
                    inputType,
                    FindObjectsInactive.Include);
            for (int i = 0; i < components.Length; i++)
            {
                Behaviour behaviour = components[i] as Behaviour;
                if (behaviour != null)
                    behaviour.enabled = false;
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
            return ReflectionTestUtilities.GetStaticProperty(
                managerType,
                "Instance");
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
                ReflectionTestUtilities.GetProperty(result, "Status").ToString(),
                Is.EqualTo("Success"),
                $"Room identity did not resolve for '{scenePath}': " +
                ReflectionTestUtilities.GetProperty<string>(result, "Message"));

            object room = ReflectionTestUtilities.GetProperty(result, "Room");
            Assert.That(room, Is.Not.Null);
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(room, "RoomId"),
                Is.EqualTo(expectedRoomId));
            return room;
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

        private static int CountLoadedScenes(string scenePath)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded &&
                    string.Equals(
                        scene.path,
                        scenePath,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
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

        private static bool IsKnownYarnInputException(
            PlayModeLogCapture.Entry entry)
        {
            return entry.Type == LogType.Exception &&
                   Contains(entry.Message, "NullReferenceException") &&
                   Contains(
                       entry.StackTrace,
                       "Yarn.Unity.InputSystemAvailability.GetKeyDown");
        }

        private static bool IsAdditiveLightOverlapError(
            PlayModeLogCapture.Entry entry)
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
            object current =
                ReflectionTestUtilities.GetStaticProperty(type, "Instance");
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
