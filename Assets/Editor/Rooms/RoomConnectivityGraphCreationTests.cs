using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 5.2 connection-authoring tests. These follow the repository's custom
/// editor test-runner convention and do not require a separate test assembly.
/// Run: Tools/Project Spear/Rooms/Tests/Run Connection Creation Tests
/// Batch: -executeMethod RoomConnectivityGraphCreationTests.RunAllBatch
/// </summary>
public static class RoomConnectivityGraphCreationTests
{
    private sealed class TestFailure : Exception
    {
        public TestFailure(string message) : base(message) { }
    }

    private sealed class ModelFixture : IDisposable
    {
        public RoomConnectivityGraphModel Model;
        public RoomGraphNode Alpha;
        public RoomGraphNode Beta;
        public RoomGraphDoor AlphaDoor;
        public RoomGraphDoor BetaDoor;

        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        public ModelFixture()
        {
            Model = new RoomConnectivityGraphModel { RegistryAvailable = true };
            RoomDefinition alpha = Definition("test-alpha", "Test Alpha", "scene-a");
            RoomDefinition beta = Definition("test-beta", "Test Beta", "scene-b");
            Alpha = new RoomGraphNode(alpha, "Assets/Tests/A.unity");
            Beta = new RoomGraphNode(beta, "Assets/Tests/B.unity");
            AlphaDoor = Door(alpha, "door-a", "Door A");
            BetaDoor = Door(beta, "door-b", "Door B");
            Alpha.Doors.Add(AlphaDoor);
            Beta.Doors.Add(BetaDoor);
            Model.Nodes.Add(Alpha);
            Model.Nodes.Add(Beta);
        }

        private RoomDefinition Definition(string id, string display, string scene)
        {
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            definition.name = id;
            definition.AssignIdentity(id, display, scene, "guid-" + id);
            _objects.Add(definition);
            return definition;
        }

        private static RoomGraphDoor Door(RoomDefinition room, string id, string name)
        {
            return new RoomGraphDoor(
                room,
                "Assets/Tests/" + room.SceneName + ".unity",
                id,
                string.Empty,
                name,
                true,
                null);
        }

        public void Dispose()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }
        }
    }

    private sealed class ServiceFixture : IDisposable
    {
        private readonly string _folderPath;
        private readonly string _connectionNamePrefix;
        private readonly RoomRegistry _registry;
        private readonly List<RoomDefinition> _definitions = new List<RoomDefinition>();
        private readonly Dictionary<RoomDefinition, string> _doorIdsByRoom =
            new Dictionary<RoomDefinition, string>();
        private readonly Scene _originalActiveScene;
        private readonly EditorBuildSettingsScene[] _originalBuildScenes;
        private readonly string _token;
        private bool _registered;

        public readonly RoomDefinition RoomA;
        public readonly RoomDefinition RoomB;
        public readonly string DoorAId;
        public readonly string DoorBId;
        public string DoorCId { get; private set; }
        public string ConnectionNamePrefix => _connectionNamePrefix;

        public ServiceFixture()
        {
            string token = Guid.NewGuid().ToString("N");
            _token = token;
            _folderPath = "Assets/__RoomConnectionCreationTests_" + token;
            _connectionNamePrefix = "P52_" + token;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folderPath));

            _registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
            if (_registry == null || _registry.Rooms == null)
                throw new TestFailure("RoomRegistry is required for service integration tests.");

            _originalActiveScene = SceneManager.GetActiveScene();
            _originalBuildScenes = (EditorBuildSettingsScene[])EditorBuildSettings.scenes.Clone();
            string roomAId = "p52_test_" + token + "_a";
            string roomBId = "p52_test_" + token + "_b";
            DoorAId = "p52_door_" + token + "_a";
            DoorBId = "p52_door_" + token + "_b";

            RoomA = CreateRoom(roomAId, "A", DoorAId);
            RoomB = CreateRoom(roomBId, "B", DoorBId);

            if (!_registry.Add(RoomA) || !_registry.Add(RoomB))
                throw new TestFailure("Could not register temporary room definitions for service tests.");

            _registered = true;
            EnsureBuildScenes(RoomA, RoomB);
            EditorUtility.SetDirty(_registry);
            AssetDatabase.SaveAssets();
        }

        private RoomDefinition CreateRoom(string roomId, string suffix, string doorId)
        {
            string scenePath = _folderPath + "/Room_" + suffix + ".unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject doorObject = new GameObject("Door_" + suffix);
            SceneManager.MoveGameObjectToScene(doorObject, scene);
            RoomTransition transition = doorObject.AddComponent<RoomTransition>();
            SerializedObject serialized = new SerializedObject(transition);
            serialized.FindProperty("doorId").stringValue = doorId;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new TestFailure("Could not save a temporary room scene.");

            if (_originalActiveScene.IsValid() && _originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(_originalActiveScene);
            EditorSceneManager.CloseScene(scene, removeScene: true);

            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            definition.name = "Room_" + suffix;
            definition.AssignIdentity(
                roomId,
                "P52 Test Room " + suffix,
                "Room_" + suffix,
                AssetDatabase.AssetPathToGUID(scenePath));
            string definitionPath = _folderPath + "/Room_" + suffix + ".asset";
            AssetDatabase.CreateAsset(definition, definitionPath);
            _definitions.Add(definition);
            _doorIdsByRoom[definition] = doorId;
            return definition;
        }

        public RoomDefinition AddRoomC()
        {
            DoorCId = "p52_door_" + _token + "_c";
            RoomDefinition room = CreateRoom("p52_test_" + _token + "_c", "C", DoorCId);
            if (!_registry.Add(room))
                throw new TestFailure("Could not register the third temporary room for endpoint-edit tests.");

            EnsureBuildScenes(room);
            EditorUtility.SetDirty(_registry);
            AssetDatabase.SaveAssets();
            return room;
        }

        public void ClearDoorUndo(RoomDefinition room, string doorId)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (RoomTransition door in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                    {
                        if (string.Equals(door.DoorId, doorId, StringComparison.Ordinal))
                            Undo.ClearUndo(door);
                    }
                }
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        public void Dispose()
        {
            RoomConnectionService.ClearUndoRecordsForConnectionNamePrefix(_connectionNamePrefix);
            List<RoomConnection> testConnections = FindConnections(_connectionNamePrefix);
            foreach (RoomConnection connection in testConnections)
            {
                if (connection != null)
                    Undo.ClearUndo(connection);
            }

            foreach (KeyValuePair<RoomDefinition, string> endpoint in _doorIdsByRoom)
                ClearTestBacklink(endpoint.Key, endpoint.Value);

            foreach (RoomDefinition definition in _definitions)
            {
                if (definition != null)
                    _registry.Remove(definition);
            }

            if (_registered)
            {
                EditorUtility.SetDirty(_registry);
                AssetDatabase.SaveAssets();
            }

            foreach (RoomConnection connection in testConnections)
            {
                if (connection == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(connection);
                if (!string.IsNullOrEmpty(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }

            // Also remove test-prefixed files that Unity could not load as a
            // RoomConnection after an interrupted Undo/Redo test operation.
            string testAssetPrefix = _connectionNamePrefix + "_";
            foreach (string assetPath in Directory.GetFiles(
                         RoomConnectionService.ConnectionsFolder,
                         testAssetPrefix + "*.asset"))
            {
                AssetDatabase.DeleteAsset(assetPath.Replace('\\', '/'));
            }

            AssetDatabase.DeleteAsset(_folderPath);
            EditorBuildSettings.scenes = _originalBuildScenes;
            AssetDatabase.Refresh();

            if (_originalActiveScene.IsValid() && _originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(_originalActiveScene);
        }

        private static void EnsureBuildScenes(params RoomDefinition[] definitions)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (RoomDefinition definition in definitions)
            {
                string path = definition != null
                    ? AssetDatabase.GUIDToAssetPath(definition.SceneGuid)
                    : null;
                if (string.IsNullOrEmpty(path))
                    continue;

                int found = scenes.FindIndex(scene => string.Equals(scene.path, path, StringComparison.Ordinal));
                if (found >= 0)
                {
                    if (!scenes[found].enabled)
                        scenes[found] = new EditorBuildSettingsScene(path, true);
                }
                else
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ClearTestBacklink(RoomDefinition room, string doorId)
        {
            if (room == null)
                return;

            string scenePath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
            if (string.IsNullOrEmpty(scenePath))
                return;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(true))
                    {
                        if (!string.Equals(transition.DoorId, doorId, StringComparison.Ordinal))
                            continue;

                        Undo.ClearUndo(transition);
                        SerializedObject serialized = new SerializedObject(transition);
                        serialized.FindProperty("connection").objectReferenceValue = null;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/Project Spear/Rooms/Tests/Run Connection Creation Tests")]
    public static void RunAllMenu()
    {
        RunAll(true);
    }

    public static void RunAllBatch()
    {
        int failures = RunAll(true);
        if (failures > 0)
            throw new Exception($"{failures} room connection creation test(s) failed.");
    }

    public static int RunAll(bool writeReport)
    {
        List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>
        {
            Test("Starting and cancelling the workflow", StartingAndCancelling),
            Test("Selecting a valid source door", SelectingValidSource),
            Test("Selecting a valid destination door", SelectingValidDestination),
            Test("Rejecting the same door for both endpoints", RejectingSameDoor),
            Test("Rejecting a door already connected", RejectingConnectedDoor),
            Test("Going back preserves valid endpoints and mode", GoingBackPreservesSelections),
            Test("Bidirectional service creation and Undo/Redo", BidirectionalCreationUndoRedo),
            Test("OneWay service creation preserves A-to-B semantics", OneWayCreation),
            Test("Connection endpoint and direction modification supports Undo/Redo", ConnectionModificationUndoRedo),
            Test("Connection deletion clears backlinks and supports Undo/Redo", ConnectionDeletionUndoRedo),
            Test("Room removal without connections preserves its definition and supports Undo/Redo", RoomRemovalWithoutConnectionsUndoRedo),
            Test("Room removal deletes only incident connections and supports Undo/Redo", RoomRemovalWithConnectionsUndoRedo),
            Test("Missing RoomDefinition registration removal is safe and cataloged", MissingRoomRegistrationRemovalIsSafe),
            Test("Rejecting stale endpoints without partial creation", RejectingStaleEndpoint),
            Test("Partial-save failure rolls back asset and both backlinks", PartialSaveFailureRollsBack),
            Test("Graph rebuild contains the created connection", GraphRefreshAfterCreation),
            Test("Existing navigation and layout suites remain green", ExistingGraphSuitesRemainGreen)
        };

        int passed = 0;
        List<string> failures = new List<string>();
        foreach (KeyValuePair<string, Action> test in tests)
        {
            try
            {
                test.Value();
                passed++;
                Debug.Log("[RoomGraphCreationTests] PASS " + test.Key);
            }
            catch (Exception exception)
            {
                failures.Add(test.Key + ": " + exception.Message);
                Debug.LogError("[RoomGraphCreationTests] FAIL " + test.Key + ": " + exception);
            }
        }

        string summary =
            $"Room Connectivity Graph creation tests: {passed}/{tests.Count} passed, {failures.Count} failed.";
        if (failures.Count == 0)
            Debug.Log("[RoomGraphCreationTests] " + summary);
        else
            Debug.LogError("[RoomGraphCreationTests] " + summary + " " + string.Join(" | ", failures));

        if (writeReport)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string reportPath = Path.Combine(projectRoot, "Temp", "RoomConnectivityGraphCreationTests.log");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, summary + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        return failures.Count;
    }

    private static void StartingAndCancelling()
    {
        RoomConnectionCreationWorkflow workflow = new RoomConnectionCreationWorkflow();
        Check(!workflow.IsActive, "The workflow must start inactive.");
        workflow.Start();
        Check(workflow.IsActive && workflow.Step == RoomConnectionCreationStep.SelectSource,
            "Start must enter the source-selection step.");
        Check(workflow.Source == null && workflow.Destination == null,
            "Starting must not silently select endpoints.");
        workflow.Cancel();
        Check(!workflow.IsActive && workflow.Source == null && workflow.Destination == null,
            "Cancel must clear transient endpoint selections.");
    }

    private static void SelectingValidSource()
    {
        using (ModelFixture fixture = new ModelFixture())
        {
            RoomConnectionCreationWorkflow workflow = new RoomConnectionCreationWorkflow();
            workflow.Start();
            string reason = RoomConnectionCreationWorkflow.GetIneligibilityReason(
                fixture.Model, fixture.Alpha, fixture.AlphaDoor);
            Check(reason == null, "A valid unconnected door was rejected: " + reason);
            Check(workflow.ChooseSource(fixture.AlphaDoor, out string error), error);
            Check(ReferenceEquals(workflow.Source, fixture.AlphaDoor), "The selected door was not retained as A.");
            Check(workflow.Step == RoomConnectionCreationStep.SelectDestination,
                "Selecting source must advance to destination selection.");
        }
    }

    private static void SelectingValidDestination()
    {
        using (ModelFixture fixture = new ModelFixture())
        {
            RoomConnectionCreationWorkflow workflow = new RoomConnectionCreationWorkflow();
            workflow.Start();
            Check(workflow.ChooseSource(fixture.AlphaDoor, out string sourceError), sourceError);
            string reason = RoomConnectionCreationWorkflow.GetIneligibilityReason(
                fixture.Model, fixture.Beta, fixture.BetaDoor, fixture.AlphaDoor);
            Check(reason == null, "A valid destination was rejected: " + reason);
            Check(workflow.ChooseDestination(fixture.BetaDoor, out string destinationError), destinationError);
            Check(ReferenceEquals(workflow.Destination, fixture.BetaDoor), "The selected door was not retained as B.");
            Check(workflow.Step == RoomConnectionCreationStep.SelectMode,
                "Selecting destination must advance to mode selection.");
        }
    }

    private static void RejectingSameDoor()
    {
        using (ModelFixture fixture = new ModelFixture())
        {
            RoomConnectionCreationWorkflow workflow = new RoomConnectionCreationWorkflow();
            workflow.Start();
            Check(workflow.ChooseSource(fixture.AlphaDoor, out _), "Could not select source.");
            Check(!workflow.ChooseDestination(fixture.AlphaDoor, out string error),
                "The same door was accepted as both endpoints.");
            Check(error.Contains("different door"), "The rejection did not explain the identical endpoint.");
            Check(workflow.Step == RoomConnectionCreationStep.SelectDestination &&
                  workflow.Destination == null,
                "A rejected endpoint must not advance or be retained.");
        }
    }

    private static void RejectingConnectedDoor()
    {
        using (ModelFixture fixture = new ModelFixture())
        {
            RoomConnection existing = ScriptableObject.CreateInstance<RoomConnection>();
            try
            {
                existing.name = "AlreadyConnected";
                existing.Assign("test-alpha", "door-a", "test-beta", "door-b", RoomConnectionDirection.Bidirectional);
                fixture.AlphaDoor.ReferencingEdges.Add(new RoomGraphEdge(existing, "test.asset"));
                string reason = RoomConnectionCreationWorkflow.GetIneligibilityReason(
                    fixture.Model, fixture.Alpha, fixture.AlphaDoor);
                Check(reason != null && reason.Contains("AlreadyConnected"),
                    "The topology connection was not explained as the reason the door is ineligible.");

                fixture.AlphaDoor.ReferencingEdges.Clear();
                RoomGraphDoor backlinked = new RoomGraphDoor(
                    fixture.AlphaDoor.Room,
                    fixture.AlphaDoor.ScenePath,
                    fixture.AlphaDoor.StableDoorId,
                    string.Empty,
                    fixture.AlphaDoor.GameObjectName,
                    true,
                    existing);
                reason = RoomConnectionCreationWorkflow.GetIneligibilityReason(
                    fixture.Model, fixture.Alpha, backlinked);
                Check(reason != null && reason.Contains("backlink"),
                    "A scene backlink to an existing connection was not rejected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }
    }

    private static void GoingBackPreservesSelections()
    {
        using (ModelFixture fixture = new ModelFixture())
        {
            RoomConnectionCreationWorkflow workflow = new RoomConnectionCreationWorkflow();
            workflow.Start();
            Check(workflow.ChooseSource(fixture.AlphaDoor, out _), "Could not select source.");
            Check(workflow.ChooseDestination(fixture.BetaDoor, out _), "Could not select destination.");
            Check(workflow.SetDirection(RoomConnectionDirection.OneWay), "Could not set OneWay mode.");
            Check(workflow.ContinueToConfirmation(out string error), error);

            Check(workflow.GoBack() && workflow.Step == RoomConnectionCreationStep.SelectMode,
                "Back from confirmation did not return to mode selection.");
            Check(workflow.GoBack() && workflow.Step == RoomConnectionCreationStep.SelectDestination,
                "Back from mode did not return to destination selection.");
            Check(workflow.GoBack() && workflow.Step == RoomConnectionCreationStep.SelectSource,
                "Back from destination did not return to source selection.");
            Check(ReferenceEquals(workflow.Source, fixture.AlphaDoor) &&
                  ReferenceEquals(workflow.Destination, fixture.BetaDoor),
                "Going back discarded valid endpoints.");
            Check(workflow.Direction == RoomConnectionDirection.OneWay,
                "Going back discarded the selected mode.");
        }
    }

    private static void BidirectionalCreationUndoRedo()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            bool created = RoomConnectionService.CreateConnection(
                fixture.RoomA,
                fixture.DoorAId,
                fixture.RoomB,
                fixture.DoorBId,
                RoomConnectionDirection.Bidirectional,
                fixture.ConnectionNamePrefix + "_Bidirectional",
                out RoomConnection connection,
                out string message);

            Check(created, "The service rejected valid endpoints: " + message);
            Check(connection != null, "The service did not return the created asset.");
            Check(connection.EndpointARoomId == fixture.RoomA.RoomId &&
                  connection.EndpointADoorId == fixture.DoorAId,
                "Endpoint A does not preserve the selected source.");
            Check(connection.EndpointBRoomId == fixture.RoomB.RoomId &&
                  connection.EndpointBDoorId == fixture.DoorBId,
                "Endpoint B does not preserve the selected destination.");
            Check(connection.Direction == RoomConnectionDirection.Bidirectional,
                "The created connection is not bidirectional.");

            string assetPath = AssetDatabase.GetAssetPath(connection);
            Undo.PerformUndo();
            Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath) == null,
                "Undo did not remove the created connection asset.");
            Check(ReadBacklink(fixture.RoomA, fixture.DoorAId) == null &&
                  ReadBacklink(fixture.RoomB, fixture.DoorBId) == null,
                "Undo did not clear both saved door backlinks.");

            Undo.PerformRedo();
            RoomConnection redone = AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath);
            Check(redone != null && redone.Direction == RoomConnectionDirection.Bidirectional,
                "Redo did not restore the connection asset.");
            Check(ReferenceEquals(ReadBacklink(fixture.RoomA, fixture.DoorAId), redone) &&
                  ReferenceEquals(ReadBacklink(fixture.RoomB, fixture.DoorBId), redone),
                "Redo did not restore both door backlinks.");
        }
    }

    private static void OneWayCreation()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            bool created = RoomConnectionService.CreateConnection(
                fixture.RoomA,
                fixture.DoorAId,
                fixture.RoomB,
                fixture.DoorBId,
                RoomConnectionDirection.OneWay,
                fixture.ConnectionNamePrefix + "_OneWay",
                out RoomConnection connection,
                out string message);

            Check(created, "The service rejected valid endpoints: " + message);
            Check(connection.EndpointARoomId == fixture.RoomA.RoomId &&
                  connection.EndpointADoorId == fixture.DoorAId,
                "Endpoint A must remain the OneWay source.");
            Check(connection.EndpointBRoomId == fixture.RoomB.RoomId &&
                  connection.EndpointBDoorId == fixture.DoorBId,
                "Endpoint B must remain the OneWay destination.");
            Check(connection.Direction == RoomConnectionDirection.OneWay,
                "The OneWay mode was not saved.");
        }
    }

    private static void ConnectionModificationUndoRedo()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            RoomDefinition roomC = fixture.AddRoomC();
            bool created = RoomConnectionService.CreateConnection(
                fixture.RoomA,
                fixture.DoorAId,
                fixture.RoomB,
                fixture.DoorBId,
                RoomConnectionDirection.Bidirectional,
                fixture.ConnectionNamePrefix + "_Modify",
                out RoomConnection connection,
                out string message);
            Check(created && connection != null, "Could not create the connection for modification: " + message);

            string assetPath = AssetDatabase.GetAssetPath(connection);
            // The create operation is covered in its own test. Clear only its
            // test-object Undo records so this case isolates modification Undo.
            Undo.ClearUndo(connection);
            fixture.ClearDoorUndo(fixture.RoomA, fixture.DoorAId);
            fixture.ClearDoorUndo(fixture.RoomB, fixture.DoorBId);
            fixture.ClearDoorUndo(roomC, fixture.DoorCId);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            bool updated = RoomConnectionService.UpdateConnection(
                connection,
                fixture.RoomA,
                fixture.DoorAId,
                roomC,
                fixture.DoorCId,
                RoomConnectionDirection.OneWay,
                out message);
            Check(updated, "Could not rewire the connection: " + message);
            Check(AssetDatabase.GetAssetPath(connection) == assetPath,
                "Modifying a connection replaced its persistent asset identity.");
            Check(connection.EndpointARoomId == fixture.RoomA.RoomId &&
                  connection.EndpointADoorId == fixture.DoorAId &&
                  connection.EndpointBRoomId == roomC.RoomId &&
                  connection.EndpointBDoorId == fixture.DoorCId &&
                  connection.Direction == RoomConnectionDirection.OneWay,
                "The modified endpoints or direction were not persisted.");

            // Exercise the Undo action before scene-discovery helpers open and
            // close scenes; those editor scene operations can add their own
            // unrelated Undo groups in the synchronous custom harness.
            Undo.PerformUndo();
            RoomConnection undone = AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath);
            Check(undone != null && undone.Direction == RoomConnectionDirection.Bidirectional &&
                  undone.EndpointBRoomId == fixture.RoomB.RoomId && undone.EndpointBDoorId == fixture.DoorBId,
                "Undo did not restore the original connection endpoints and direction.");
            Check(ReadBacklink(fixture.RoomA, fixture.DoorAId) == undone &&
                  ReadBacklink(fixture.RoomB, fixture.DoorBId) == undone &&
                  ReadBacklink(roomC, fixture.DoorCId) == null,
                "Undo did not restore the original door backlinks.");

            Undo.PerformRedo();
            RoomConnection redone = AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath);
            Check(redone != null && redone.Direction == RoomConnectionDirection.OneWay &&
                  redone.EndpointBRoomId == roomC.RoomId && redone.EndpointBDoorId == fixture.DoorCId,
                "Redo did not restore the modified connection.");
            Check(ReadBacklink(fixture.RoomA, fixture.DoorAId) == redone &&
                  ReadBacklink(fixture.RoomB, fixture.DoorBId) == null &&
                  ReadBacklink(roomC, fixture.DoorCId) == redone,
                "Redo did not restore the modified door backlinks.");
        }
    }

    private static void ConnectionDeletionUndoRedo()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            bool created = RoomConnectionService.CreateConnection(
                fixture.RoomA,
                fixture.DoorAId,
                fixture.RoomB,
                fixture.DoorBId,
                RoomConnectionDirection.Bidirectional,
                fixture.ConnectionNamePrefix + "_Delete",
                out RoomConnection connection,
                out string message);
            Check(created && connection != null, "Could not create the connection for deletion: " + message);

            string assetPath = AssetDatabase.GetAssetPath(connection);
            // Isolate deletion Undo from the asset-creation operation already
            // covered by BidirectionalCreationUndoRedo.
            Undo.ClearUndo(connection);
            fixture.ClearDoorUndo(fixture.RoomA, fixture.DoorAId);
            fixture.ClearDoorUndo(fixture.RoomB, fixture.DoorBId);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            bool deleted = RoomConnectionService.DeleteConnection(connection, out message);
            Check(deleted, "Could not delete the connection: " + message);
            Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath) == null,
                "Connection deletion left the asset in place.");

            Undo.PerformUndo();
            RoomConnection undone = AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath);
            Check(undone != null, "Undo did not restore the deleted connection asset.");
            Check(ReadBacklink(fixture.RoomA, fixture.DoorAId) == undone &&
                  ReadBacklink(fixture.RoomB, fixture.DoorBId) == undone,
                "Undo did not restore both deleted-connection backlinks.");

            Undo.PerformRedo();
            Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(assetPath) == null,
                "Redo did not delete the connection asset again.");
            Check(ReadBacklink(fixture.RoomA, fixture.DoorAId) == null &&
                  ReadBacklink(fixture.RoomB, fixture.DoorBId) == null,
                "Redo did not clear both door backlinks again.");
        }
    }

    private static void RoomRemovalWithoutConnectionsUndoRedo()
    {
        string unrelatedConnectionPath = FindFirstPersistentConnectionPath();
        try
        {
            using (ServiceFixture fixture = new ServiceFixture())
            {
                RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
                int roomIndex = FindRegistryIndex(registry, fixture.RoomA);
                string definitionPath = AssetDatabase.GetAssetPath(fixture.RoomA);
                string scenePath = AssetDatabase.GUIDToAssetPath(fixture.RoomA.SceneGuid);
                Check(RoomRemovalService.GetConnectionsForRoom(fixture.RoomA.RoomId).Count == 0,
                    "The no-connection fixture unexpectedly has an incident connection.");

                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                bool removed = RoomRemovalService.RemoveRoomRegistration(
                    fixture.RoomA,
                    fixture.RoomA.RoomId,
                    fixture.RoomA.SceneGuid,
                    roomIndex,
                    out string message);
                Check(removed, "Could not remove an unconnected room: " + message);
                Check(!RegistryContains(registry, fixture.RoomA),
                    "The room registration remained after removal.");
                Check(AssetDatabase.LoadAssetAtPath<RoomDefinition>(definitionPath) == fixture.RoomA,
                    "Removing registration deleted the RoomDefinition asset.");
                Check(File.Exists(scenePath), "Removing registration deleted the Unity scene.");
                Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(unrelatedConnectionPath) != null,
                    "Removing an unconnected room changed an unrelated connection asset.");
                Check(RuntimeRoomCatalogGenerator.ValidateForBuild().IsValid,
                    "RuntimeRoomCatalog was not synchronized after removing an unconnected room.");

                Undo.PerformUndo();
                RoomConnectionService.ReconcileUndoRedo();
                Check(RegistryContains(registry, fixture.RoomA),
                    "Undo did not restore the room registration.");
                Check(RoomRegistrationService.SynchronizeRuntimeCatalog().IsValid,
                    "RuntimeRoomCatalog was not synchronized after room-removal Undo.");
                Check(AssetDatabase.LoadAssetAtPath<RoomDefinition>(definitionPath) == fixture.RoomA &&
                      File.Exists(scenePath),
                    "Undo did not preserve the scene and RoomDefinition asset.");

                Undo.PerformRedo();
                RoomConnectionService.ReconcileUndoRedo();
                Check(!RegistryContains(registry, fixture.RoomA),
                    "Redo did not remove the room registration again.");
                Check(RoomRegistrationService.SynchronizeRuntimeCatalog().IsValid,
                    "RuntimeRoomCatalog was not synchronized after room-removal Redo.");
                Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(unrelatedConnectionPath) != null,
                    "Redo changed an unrelated connection asset.");
            }
        }
        finally
        {
            RuntimeRoomCatalogValidationReport report = RuntimeRoomCatalogGenerator.GenerateOrUpdate();
            if (!report.IsValid)
                throw new TestFailure("Could not restore RuntimeRoomCatalog after room-removal fixture: " + report.FormatSummary());
        }
    }

    private static void RoomRemovalWithConnectionsUndoRedo()
    {
        string unrelatedConnectionPath = FindFirstPersistentConnectionPath();
        try
        {
            using (ServiceFixture fixture = new ServiceFixture())
            {
                RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
                bool created = RoomConnectionService.CreateConnection(
                    fixture.RoomA,
                    fixture.DoorAId,
                    fixture.RoomB,
                    fixture.DoorBId,
                    RoomConnectionDirection.OneWay,
                    fixture.ConnectionNamePrefix + "_RoomRemoval",
                    out RoomConnection connection,
                    out string createMessage);
                Check(created, "Could not create the room-removal fixture connection: " + createMessage);

                string connectionPath = AssetDatabase.GetAssetPath(connection);
                string connectionGuid = AssetDatabase.AssetPathToGUID(connectionPath);
                string definitionPath = AssetDatabase.GetAssetPath(fixture.RoomA);
                string scenePath = AssetDatabase.GUIDToAssetPath(fixture.RoomA.SceneGuid);
                int roomIndex = FindRegistryIndex(registry, fixture.RoomA);

                // Separate the setup connection creation from the full room
                // removal Undo group, matching distinct Graph UI operations.
                Undo.ClearUndo(connection);
                fixture.ClearDoorUndo(fixture.RoomA, fixture.DoorAId);
                fixture.ClearDoorUndo(fixture.RoomB, fixture.DoorBId);
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();

                bool removed = RoomRemovalService.RemoveRoomRegistration(
                    fixture.RoomA,
                    fixture.RoomA.RoomId,
                    fixture.RoomA.SceneGuid,
                    roomIndex,
                    out string removeMessage);
                Check(removed, "Could not remove a connected room: " + removeMessage);
                Check(!RegistryContains(registry, fixture.RoomA),
                    "The connected room registration remained after removal.");
                Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(connectionPath) == null,
                    "Room removal did not delete its incident connection asset.");
                Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(unrelatedConnectionPath) != null,
                    "Room removal deleted an unrelated RoomConnection asset.");
                Check(AssetDatabase.LoadAssetAtPath<RoomDefinition>(definitionPath) == fixture.RoomA &&
                      File.Exists(scenePath),
                    "Room removal deleted the RoomDefinition or Unity scene.");
                Check(RoomRemovalService.GetConnectionsForRoom(fixture.RoomB.RoomId).Count == 0,
                    "The removed connection still appears in the other room's topology.");
                Check(RuntimeRoomCatalogGenerator.ValidateForBuild().IsValid,
                    "RuntimeRoomCatalog was not synchronized after removing a connected room.");

                Undo.PerformUndo();
                RoomConnectionService.ReconcileUndoRedo();
                RoomRegistrationService.SynchronizeRuntimeCatalog();
                RoomConnection restored = AssetDatabase.LoadAssetAtPath<RoomConnection>(connectionPath);
                Check(RegistryContains(registry, fixture.RoomA) && restored != null,
                    "Undo did not restore both the room registration and incident connection.");
                Check(string.Equals(AssetDatabase.AssetPathToGUID(connectionPath), connectionGuid, StringComparison.Ordinal),
                    "Undo did not preserve the RoomConnection asset GUID.");
                Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(unrelatedConnectionPath) != null,
                    "Undo changed an unrelated RoomConnection asset.");
                Check(RuntimeRoomCatalogGenerator.ValidateForBuild().IsValid,
                    "RuntimeRoomCatalog did not match the restored RoomRegistry after Undo.");

                Undo.PerformRedo();
                RoomConnectionService.ReconcileUndoRedo();
                RoomRegistrationService.SynchronizeRuntimeCatalog();
                Check(!RegistryContains(registry, fixture.RoomA) &&
                      AssetDatabase.LoadAssetAtPath<RoomConnection>(connectionPath) == null,
                    "Redo did not remove the room and its incident connection again.");
                Check(AssetDatabase.LoadAssetAtPath<RoomConnection>(unrelatedConnectionPath) != null,
                    "Redo changed an unrelated RoomConnection asset.");
            }
        }
        finally
        {
            RuntimeRoomCatalogValidationReport report = RuntimeRoomCatalogGenerator.GenerateOrUpdate();
            if (!report.IsValid)
                throw new TestFailure("Could not restore RuntimeRoomCatalog after connected-room fixture: " + report.FormatSummary());
        }
    }

    private static void MissingRoomRegistrationRemovalIsSafe()
    {
        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        Check(registry != null, "RoomRegistry is required for invalid registration removal coverage.");
        int initialCount = registry.Rooms.Count;
        int missingEntryIndex = initialCount;
        int connectionCount = FindConnections(string.Empty).Count;

        try
        {
            Undo.RecordObject(registry, "Add Missing RoomDefinition Test Entry");
            SerializedObject serialized = new SerializedObject(registry);
            SerializedProperty rooms = serialized.FindProperty("rooms");
            rooms.InsertArrayElementAtIndex(rooms.arraySize);
            rooms.GetArrayElementAtIndex(rooms.arraySize - 1).objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            bool removed = RoomRemovalService.RemoveRoomRegistration(
                null,
                null,
                null,
                missingEntryIndex,
                out string message);
            Check(removed, "Could not remove an invalid registry entry: " + message);
            Check(registry.Rooms.Count == initialCount,
                "Invalid registration removal did not remove exactly the null RoomDefinition slot.");
            Check(FindConnections(string.Empty).Count == connectionCount,
                "Removing an invalid registration changed a RoomConnection asset.");
            Check(RuntimeRoomCatalogGenerator.ValidateForBuild().IsValid,
                "RuntimeRoomCatalog was not regenerated after removing the invalid registry entry.");

            Undo.PerformUndo();
            Check(registry.Rooms.Count == initialCount + 1 && registry.Rooms[missingEntryIndex] == null,
                "Undo did not restore the invalid null registration slot.");
            Check(!RuntimeRoomCatalogGenerator.ValidateForBuild().IsValid,
                "The invalid null registration was silently omitted from the validation result after Undo.");

            Undo.PerformRedo();
            Check(registry.Rooms.Count == initialCount,
                "Redo did not remove the invalid registry slot again.");
            Check(RuntimeRoomCatalogGenerator.ValidateForBuild().IsValid,
                "RuntimeRoomCatalog did not validate after invalid-registration Redo.");
        }
        finally
        {
            if (registry.Rooms.Count > initialCount &&
                missingEntryIndex < registry.Rooms.Count &&
                registry.Rooms[missingEntryIndex] == null)
            {
                registry.RemoveMissingAt(missingEntryIndex);
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            RuntimeRoomCatalogGenerator.GenerateOrUpdate();
        }
    }

    private static int FindRegistryIndex(RoomRegistry registry, RoomDefinition definition)
    {
        for (int i = 0; i < registry.Rooms.Count; i++)
            if (registry.Rooms[i] == definition)
                return i;
        return -1;
    }

    private static bool RegistryContains(RoomRegistry registry, RoomDefinition definition)
    {
        return FindRegistryIndex(registry, definition) >= 0;
    }

    private static string FindFirstPersistentConnectionPath()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RoomConnection"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RoomConnection connection = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
            if (connection != null && !connection.name.StartsWith("P52_", StringComparison.Ordinal))
            {
                return path;
            }
        }

        throw new TestFailure("No existing connection asset is available to verify unrelated-connection preservation.");
    }

    private static void RejectingStaleEndpoint()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            RoomDefinition unregistered = ScriptableObject.CreateInstance<RoomDefinition>();
            unregistered.name = "Unregistered_Test_Room";
            unregistered.AssignIdentity("p52_unregistered", "Unregistered", "Missing", "missing-guid");
            try
            {
                bool created = RoomConnectionService.CreateConnection(
                    unregistered,
                    "stale-door",
                    fixture.RoomB,
                    fixture.DoorBId,
                    RoomConnectionDirection.Bidirectional,
                    fixture.ConnectionNamePrefix + "_Stale",
                    out RoomConnection connection,
                    out string message);

                Check(!created && connection == null, "The service accepted a stale unregistered endpoint.");
                Check(message.Contains("RoomRegistry"), "The stale endpoint error was not actionable.");
                Check(FindConnections(fixture.ConnectionNamePrefix + "_Stale").Count == 0,
                    "A failed preflight left a partial RoomConnection asset.");
                Check(ReadBacklink(fixture.RoomB, fixture.DoorBId) == null,
                    "A failed preflight changed the valid endpoint backlink.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unregistered);
            }
        }
    }

    private static void PartialSaveFailureRollsBack()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            int saveAttempts = 0;
            RoomConnectionService.SaveDoorSceneOverrideForTests = scene =>
            {
                saveAttempts++;
                return saveAttempts == 2 ? false : EditorSceneManager.SaveScene(scene);
            };

            try
            {
                bool created = RoomConnectionService.CreateConnection(
                    fixture.RoomA,
                    fixture.DoorAId,
                    fixture.RoomB,
                    fixture.DoorBId,
                    RoomConnectionDirection.Bidirectional,
                    fixture.ConnectionNamePrefix + "_Rollback",
                    out RoomConnection connection,
                    out string message);

                Check(!created && connection == null,
                    "The injected second-scene save failure did not fail creation.");
                Check(saveAttempts == 2, "The service did not attempt to save both endpoint scenes.");
                Check(message.Contains("Could not save"), "The save failure was not clearly reported.");
                Check(FindConnections(fixture.ConnectionNamePrefix + "_Rollback").Count == 0,
                    "Rollback left the partially created connection asset behind.");
                Check(ReadBacklink(fixture.RoomA, fixture.DoorAId) == null &&
                      ReadBacklink(fixture.RoomB, fixture.DoorBId) == null,
                    "Rollback did not clear both endpoint backlinks.");
            }
            finally
            {
                RoomConnectionService.SaveDoorSceneOverrideForTests = null;
            }
        }
    }

    private static void GraphRefreshAfterCreation()
    {
        using (ServiceFixture fixture = new ServiceFixture())
        {
            bool created = RoomConnectionService.CreateConnection(
                fixture.RoomA,
                fixture.DoorAId,
                fixture.RoomB,
                fixture.DoorBId,
                RoomConnectionDirection.OneWay,
                fixture.ConnectionNamePrefix + "_Graph_Refresh",
                out RoomConnection connection,
                out string message);
            Check(created, "Could not create the test edge: " + message);

            RoomConnectivityGraphModel refreshed = RoomConnectivityGraphService.Build();
            RoomGraphEdge edge = refreshed.FindEdge(connection);
            Check(refreshed.Validation != null, "The rebuilt graph did not rerun validation.");
            Check(edge != null, "A rebuilt graph did not include the new RoomConnection asset.");
            Check(edge.EndpointsResolved && edge.IsValid && edge.Direction == RoomConnectionDirection.OneWay,
                "The rebuilt graph did not resolve the created endpoints and direction.");
            Check(ReferenceEquals(edge.SourceNode.Definition, fixture.RoomA) &&
                  ReferenceEquals(edge.TargetNode.Definition, fixture.RoomB),
                "The rebuilt graph reversed or changed the endpoint semantics.");
        }
    }

    private static void ExistingGraphSuitesRemainGreen()
    {
        Check(RoomConnectivityGraphNavigationTests.RunAll(false) == 0,
            "The Phase 5.1A navigation/filter suite reported failures.");
        Check(RoomConnectivityGraphLayoutTests.RunAll(false) == 0,
            "The Phase 5.1B layout suite reported failures.");
    }

    private static RoomConnection ReadBacklink(RoomDefinition room, string doorId)
    {
        List<DiscoveredDoor> doors = RoomDiscoveryService.DiscoverDoors(room, out string message);
        if (doors == null)
            throw new TestFailure("Could not read a test door backlink: " + message);

        foreach (DiscoveredDoor door in doors)
        {
            if (string.Equals(door.StableDoorId, doorId, StringComparison.Ordinal))
                return door.Connection;
        }

        throw new TestFailure("Could not resolve test door '" + doorId + "'.");
    }

    private static List<RoomConnection> FindConnections(string namePart)
    {
        List<RoomConnection> matches = new List<RoomConnection>();
        foreach (string guid in AssetDatabase.FindAssets("t:RoomConnection"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
            if (connection != null && connection.name.Contains(namePart))
                matches.Add(connection);
        }

        return matches;
    }

    private static KeyValuePair<string, Action> Test(string name, Action action)
    {
        return new KeyValuePair<string, Action>(name, action);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new TestFailure(message);
    }
}
