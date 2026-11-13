using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Focused coverage for per-door RoomTransition activation modes. The
    /// tests use transient objects only: no room scene, Master scene or asset
    /// is loaded or modified.
    /// </summary>
    public class RoomTransitionActivationModePlayModeTests : PlayModeTestBase
    {
        private const string ManagerTypeName = "GameSceneManager";
        private const string DoorTypeName = "RoomTransition";
        private const string InteractableTypeName = "RoomTransitionInteractable";
        private const string ActivationModeTypeName = "RoomTransitionActivationMode";
        private const string PlayerTypeName = "Player";
        private const string PlayerInteractTypeName = "PlayerInteract";
        private const string ConnectionTypeName = "RoomConnection";

        private const string RejectedRequestMessage =
            "Room transition request rejected; another transition is already in progress.";

        private sealed class ActivationModeHarness : IDisposable
        {
            private readonly PlayModeTestFixture _fixture;
            private readonly Type _managerType;
            private readonly object _previousManager;

            public readonly object Manager;
            public readonly PlayModeLogCapture Log;
            public readonly Type PlayerType;
            public readonly Type PlayerInteractType;
            public readonly Type ConnectionType;

            public ActivationModeHarness(PlayModeTestFixture fixture)
            {
                _fixture = fixture;

                _managerType = ReflectionTestUtilities.ResolveRuntimeType(ManagerTypeName);
                Type doorType = ReflectionTestUtilities.ResolveRuntimeType(DoorTypeName);
                Type interactableType =
                    ReflectionTestUtilities.ResolveRuntimeType(InteractableTypeName);
                Type activationModeType =
                    ReflectionTestUtilities.ResolveRuntimeType(ActivationModeTypeName);

                PlayerType = ReflectionTestUtilities.ResolveRuntimeType(PlayerTypeName);
                PlayerInteractType =
                    ReflectionTestUtilities.ResolveRuntimeType(PlayerInteractTypeName);
                ConnectionType =
                    ReflectionTestUtilities.ResolveRuntimeType(ConnectionTypeName);

                DoorType = doorType;
                InteractableType = interactableType;
                ActivationModeType = activationModeType;

                _previousManager =
                    ReflectionTestUtilities.GetStaticProperty(_managerType, "Instance");

                GameObject managerObject =
                    fixture.CreateGameObject("ActivationModeTestManager");
                managerObject.SetActive(false);
                Manager = managerObject.AddComponent(_managerType);

                // The manager's own guard is the production behavior under
                // test: requests made while it is transitioning are rejected.
                ReflectionTestUtilities.SetInstanceField(
                    Manager,
                    "_isTransitioning",
                    true);
                ReflectionTestUtilities.SetStaticField(
                    _managerType,
                    "<Instance>k__BackingField",
                    Manager);

                Log = fixture.StartLogCapture();
            }

            public Type DoorType { get; }

            public Type InteractableType { get; }

            public Type ActivationModeType { get; }

            public Component CreateDoor(
                string name,
                string activationMode,
                object connection = null)
            {
                GameObject doorObject = _fixture.CreateGameObject(name);
                doorObject.SetActive(false);

                Component door = doorObject.AddComponent(DoorType);
                if (!string.IsNullOrEmpty(activationMode))
                {
                    ReflectionTestUtilities.SetInstanceField(
                        door,
                        "activationMode",
                        Enum.Parse(ActivationModeType, activationMode));
                }

                if (connection != null)
                    ReflectionTestUtilities.SetInstanceField(door, "connection", connection);

                doorObject.AddComponent<BoxCollider2D>().isTrigger = true;
                doorObject.SetActive(true);
                return door;
            }

            public GameObject CreatePlayerCollider()
            {
                GameObject player = _fixture.CreateGameObject("ActivationModeTestPlayer");
                player.tag = "Player";
                player.AddComponent<BoxCollider2D>();
                return player;
            }

            public Component GetInteractable(Component door)
            {
                return door != null ? door.GetComponent(InteractableType) : null;
            }

            public object GetActivationMode(Component door)
            {
                return ReflectionTestUtilities.GetProperty(door, "ActivationMode");
            }

            public int CountRejectedRequests()
            {
                return Log.Find(RejectedRequestMessage).Count;
            }

            public void Dispose()
            {
                ReflectionTestUtilities.SetStaticField(
                    _managerType,
                    "<Instance>k__BackingField",
                    _previousManager);
            }
        }

        [UnityTest]
        public IEnumerator AutomaticModeRequestsTransitionOnTriggerEntry()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                // No explicit mode: the serialized field default must be Automatic.
                Component door = harness.CreateDoor("AutomaticDoor", null);
                GameObject player = harness.CreatePlayerCollider();
                Collider2D playerCollider = player.GetComponent<Collider2D>();

                Assert.That(
                    harness.GetActivationMode(door).ToString(),
                    Is.EqualTo("Automatic"));

                harness.Log.Clear();
                ReflectionTestUtilities.InvokeInstance(
                    door,
                    "OnTriggerEnter2D",
                    playerCollider);

                Assert.That(
                    harness.CountRejectedRequests(),
                    Is.EqualTo(1),
                    "Automatic mode did not forward trigger entry to " +
                    "GameSceneManager.RequestRoomTransition.");
                Assert.That(
                    harness.GetInteractable(door),
                    Is.Null,
                    "Automatic doors must not register an interaction target.");

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator InteractModeDoesNotRequestTransitionOnTriggerEntry()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                Component door = harness.CreateDoor("InteractDoor", "Interact");
                GameObject player = harness.CreatePlayerCollider();
                Collider2D playerCollider = player.GetComponent<Collider2D>();

                Assert.That(
                    harness.GetInteractable(door),
                    Is.Not.Null,
                    "Interact mode must expose an IInteractable target.");

                harness.Log.Clear();
                ReflectionTestUtilities.InvokeInstance(
                    door,
                    "OnTriggerEnter2D",
                    playerCollider);

                Assert.That(
                    harness.CountRejectedRequests(),
                    Is.EqualTo(0),
                    "Interact mode must not request a transition from trigger entry alone.");

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator InteractModeRequestsTransitionAfterPlayerInteraction()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                Component door = harness.CreateDoor("InteractDoor", "Interact");
                GameObject player = harness.CreatePlayerCollider();
                Collider2D playerCollider = player.GetComponent<Collider2D>();

                harness.Log.Clear();
                ReflectionTestUtilities.InvokeInstance(
                    door,
                    "OnTriggerEnter2D",
                    playerCollider);
                Assert.That(
                    harness.CountRejectedRequests(),
                    Is.EqualTo(0),
                    "Trigger entry must not request the transition.");

                Component interactable = harness.GetInteractable(door);
                Assert.That(interactable, Is.Not.Null);

                // This is the same call PlayerInteract makes on Interact input.
                ReflectionTestUtilities.InvokeInstance(
                    interactable,
                    "Interact",
                    (object)null);

                Assert.That(
                    harness.CountRejectedRequests(),
                    Is.EqualTo(1),
                    "A valid interaction did not forward to " +
                    "GameSceneManager.RequestRoomTransition.");

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LeavingInteractionRangeRemovesDoorAndHidesPrompt()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                Component door = harness.CreateDoor("InteractDoor", "Interact");
                Collider2D doorCollider = door.GetComponent<Collider2D>();
                Assert.That(doorCollider, Is.Not.Null);

                GameObject playerObject =
                    Fixture.CreateGameObject("ActivationModePlayerInteract");
                playerObject.SetActive(false);
                Component player = playerObject.AddComponent(harness.PlayerType);
                Component playerInteract =
                    playerObject.AddComponent(harness.PlayerInteractType);
                ReflectionTestUtilities.SetInstanceField(
                    playerInteract,
                    "_player",
                    player);

                GameObject promptRoot =
                    Fixture.CreateGameObject("ActivationModePromptRoot");
                promptRoot.SetActive(false);
                ReflectionTestUtilities.SetInstanceField(
                    playerInteract,
                    "_interactPromptRoot",
                    promptRoot);

                ReflectionTestUtilities.InvokeInstance(
                    playerInteract,
                    "OnTriggerEnter2D",
                    doorCollider);

                IList interactablesInRange =
                    (IList)ReflectionTestUtilities.GetInstanceField(
                        playerInteract,
                        "_interactablesInRange");
                Assert.That(
                    interactablesInRange.Count,
                    Is.EqualTo(1),
                    "Entering the door trigger did not add the door interaction target.");
                Assert.That(
                    promptRoot.activeSelf,
                    Is.True,
                    "Entering the door trigger did not show the interaction prompt.");

                ReflectionTestUtilities.InvokeInstance(
                    playerInteract,
                    "OnTriggerExit2D",
                    doorCollider);

                Assert.That(
                    interactablesInRange.Count,
                    Is.EqualTo(0),
                    "Leaving the door trigger did not remove the interaction target.");
                Assert.That(
                    promptRoot.activeSelf,
                    Is.False,
                    "Leaving the door trigger did not hide the interaction prompt.");

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ConnectionEndpointsCanUseDifferentActivationModes()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                ScriptableObject connection =
                    ScriptableObject.CreateInstance(harness.ConnectionType);
                try
                {
                    Component endpointA =
                        harness.CreateDoor("EndpointA", "Automatic", connection);
                    Component endpointB =
                        harness.CreateDoor("EndpointB", "Interact", connection);

                    Assert.That(
                        ReflectionTestUtilities.GetProperty(endpointA, "Connection"),
                        Is.SameAs(connection),
                        "Endpoint A does not reference the shared connection.");
                    Assert.That(
                        ReflectionTestUtilities.GetProperty(endpointB, "Connection"),
                        Is.SameAs(connection),
                        "Endpoint B does not reference the shared connection.");
                    Assert.That(
                        harness.GetActivationMode(endpointA).ToString(),
                        Is.EqualTo("Automatic"));
                    Assert.That(
                        harness.GetActivationMode(endpointB).ToString(),
                        Is.EqualTo("Interact"));
                    Assert.That(
                        harness.GetInteractable(endpointA),
                        Is.Null,
                        "Automatic endpoint unexpectedly exposes an interaction target.");
                    Assert.That(
                        harness.GetInteractable(endpointB),
                        Is.Not.Null,
                        "Interact endpoint does not expose an interaction target.");
                }
                finally
                {
                    UnityEngine.Object.Destroy(connection);
                }

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ConnectionlessDoorWithEmptyTargetIsNoOp()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                // The legacy route is selected only when the door has no
                // RoomConnection. An unconfigured legacy door must remain a
                // no-op and must not start or lock a transition.
                ReflectionTestUtilities.SetInstanceField(
                    harness.Manager,
                    "_isTransitioning",
                    false);

                Component door = harness.CreateDoor("LegacyDoor", null);
                harness.Log.Clear();

                ReflectionTestUtilities.InvokeInstance(
                    harness.Manager,
                    "RequestRoomTransition",
                    door);

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<bool>(
                        harness.Manager,
                        "_isTransitioning"),
                    Is.False,
                    "An unconfigured connection-less door started a transition.");
                Assert.That(
                    harness.Log.Find("Connected transition").Count,
                    Is.EqualTo(0),
                    "A connection-less door used the connected transition route.");
                Assert.That(
                    harness.CountRejectedRequests(),
                    Is.EqualTo(0),
                    "An unconfigured connection-less door was not a no-op.");

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RepeatedInteractionsDuringActiveTransitionAreIgnored()
        {
            using (ActivationModeHarness harness = new ActivationModeHarness(Fixture))
            {
                Component door = harness.CreateDoor("InteractDoor", "Interact");
                Component interactable = harness.GetInteractable(door);
                Assert.That(interactable, Is.Not.Null);

                harness.Log.Clear();
                ReflectionTestUtilities.InvokeInstance(
                    interactable,
                    "Interact",
                    (object)null);
                ReflectionTestUtilities.InvokeInstance(
                    interactable,
                    "Interact",
                    (object)null);

                Assert.That(
                    harness.CountRejectedRequests(),
                    Is.EqualTo(2),
                    "Each repeated interaction was not rejected by the active transition guard.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<bool>(
                        harness.Manager,
                        "_isTransitioning"),
                    Is.True,
                    "A repeated interaction cleared the transition lock.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<string>(
                        harness.Manager,
                        "_currentRoomScene"),
                    Is.Null,
                    "A repeated interaction changed the current room.");

                yield return null;
            }
        }
    }
}
