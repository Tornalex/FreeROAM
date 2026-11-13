using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomConnectionService
{
    public const string ConnectionsFolder = RoomRegistrationService.RegistryFolder + "/Connections";
    public const string CreateUndoName = "Create Room Connection";
    public const string UpdateUndoName = "Update Room Connection";
    public const string DeleteUndoName = "Delete Room Connection";
    private static readonly Dictionary<ConnectionUndoSentinel, ConnectionUndoRecord> ConnectionUndoRecords =
        new Dictionary<ConnectionUndoSentinel, ConnectionUndoRecord>();

    static RoomConnectionService()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    // Optional test seam for deterministic save-failure/rollback coverage.
    // Production callers leave this null and use EditorSceneManager.SaveScene.
    internal static Func<Scene, bool> SaveDoorSceneOverrideForTests;

    internal static void ClearUndoRecordsForConnectionNamePrefix(string namePrefix)
    {
        if (string.IsNullOrEmpty(namePrefix))
            return;

        List<ConnectionUndoSentinel> remove = new List<ConnectionUndoSentinel>();
        foreach (KeyValuePair<ConnectionUndoSentinel, ConnectionUndoRecord> pair in ConnectionUndoRecords)
        {
            if (pair.Value != null && pair.Value.AssetName.StartsWith(namePrefix, StringComparison.Ordinal))
                remove.Add(pair.Key);
        }

        foreach (ConnectionUndoSentinel sentinel in remove)
        {
            Undo.ClearUndo(sentinel);
            ConnectionUndoRecords.Remove(sentinel);
            if (sentinel != null)
                UnityEngine.Object.DestroyImmediate(sentinel);
        }
    }

    public static bool CreateConnection(
        RoomDefinition roomA,
        string doorAId,
        RoomDefinition roomB,
        string doorBId,
        RoomConnectionDirection direction,
        string connectionAssetName,
        out string message)
    {
        return CreateConnection(
            roomA,
            doorAId,
            roomB,
            doorBId,
            direction,
            connectionAssetName,
            out _,
            out message);
    }

    public static bool CreateConnection(
        RoomDefinition roomA,
        string doorAId,
        RoomDefinition roomB,
        string doorBId,
        RoomConnectionDirection direction,
        string connectionAssetName,
        out RoomConnection createdConnection,
        out string message)
    {
        createdConnection = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room connections cannot be created in Play Mode.";
            return false;
        }

        if (roomA == null || roomB == null)
        {
            message = "Both endpoints must reference a RoomDefinition.";
            return false;
        }

        if (roomA == roomB || string.Equals(roomA.RoomId, roomB.RoomId, StringComparison.Ordinal))
        {
            message = "Self-connections are not supported in the MVP. Select two different rooms.";
            return false;
        }

        if (string.Equals(roomA.SceneGuid, roomB.SceneGuid, StringComparison.Ordinal))
        {
            message = "Both endpoints resolve to the same scene. Select doors from different registered scenes.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomA.RoomId) || string.IsNullOrWhiteSpace(roomB.RoomId))
        {
            message = "Both rooms must have a registered room ID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(doorAId) || string.IsNullOrWhiteSpace(doorBId))
        {
            message = "Both endpoints must have a non-empty stable doorId.";
            return false;
        }

        if (direction != RoomConnectionDirection.Bidirectional && direction != RoomConnectionDirection.OneWay)
        {
            message = $"Invalid connection direction value ({(int)direction}).";
            return false;
        }

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null)
        {
            message = "RoomRegistry is missing. Register rooms before creating connections.";
            return false;
        }

        if (registry.Rooms == null)
        {
            message = "RoomRegistry has no room list.";
            return false;
        }

        if (!ContainsDefinition(registry, roomA) || !ContainsDefinition(registry, roomB))
        {
            message = "Both endpoint rooms must be present in RoomRegistry.";
            return false;
        }

        if (!ValidateRegisteredRoomIdentity(registry, roomA, out message) ||
            !ValidateRegisteredRoomIdentity(registry, roomB, out message))
        {
            return false;
        }

        List<DoorHandle> handles = new List<DoorHandle>();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        RoomConnection connection = null;
        DoorHandle handleA = null;
        DoorHandle handleB = null;
        bool created = false;
        int undoGroup = -1;

        try
        {
            if (!TryGetDoor(roomA, doorAId, handles, out handleA, out message))
                return false;

            if (!TryGetDoor(roomB, doorBId, handles, out handleB, out message))
                return false;

            if (handleA.Door == handleB.Door)
            {
                message = "Endpoint A and endpoint B resolve to the same door.";
                return false;
            }

            if (handleA.Door.Connection != null)
            {
                message = $"Door '{doorAId}' in room '{roomA.RoomId}' is already connected to '{handleA.Door.Connection.name}'.";
                return false;
            }

            if (handleB.Door.Connection != null)
            {
                message = $"Door '{doorBId}' in room '{roomB.RoomId}' is already connected to '{handleB.Door.Connection.name}'.";
                return false;
            }

            if (TryFindConnectionUsingEndpoint(roomA.RoomId, doorAId, out RoomConnection existingA))
            {
                message = $"Door '{doorAId}' in room '{roomA.RoomId}' is already listed by connection '{existingA.name}'.";
                return false;
            }

            if (TryFindConnectionUsingEndpoint(roomB.RoomId, doorBId, out RoomConnection existingB))
            {
                message = $"Door '{doorBId}' in room '{roomB.RoomId}' is already listed by connection '{existingB.name}'.";
                return false;
            }

            EnsureFolder(ConnectionsFolder);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(CreateUndoName);
            Undo.RecordObject(handleA.Door, CreateUndoName);
            Undo.RecordObject(handleB.Door, CreateUndoName);

            connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.Assign(roomA.RoomId, doorAId, roomB.RoomId, doorBId, direction);

            string finalName = string.IsNullOrWhiteSpace(connectionAssetName)
                ? $"Conn_{roomA.RoomId}_{doorAId}_to_{roomB.RoomId}_{doorBId}"
                : connectionAssetName;

            finalName = SanitizeFileName(finalName);
            if (finalName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                finalName = finalName.Substring(0, finalName.Length - ".asset".Length);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ConnectionsFolder}/{finalName}.asset");
            AssetDatabase.CreateAsset(connection, assetPath);
            Undo.RegisterCreatedObjectUndo(connection, CreateUndoName);

            if (!SetBacklink(handleA.Door, connection) || !SetBacklink(handleB.Door, connection))
            {
                RollbackAndDiscardUndo(connection, handleA, handleB, undoGroup);
                undoGroup = -1;
                message = "Could not assign the connection backlink to both doors.";
                return false;
            }

            if (!SaveDoorScene(handleA) || !SaveDoorScene(handleB))
            {
                RollbackAndDiscardUndo(connection, handleA, handleB, undoGroup);
                undoGroup = -1;
                message = "Could not save one of the scenes that contain the connected doors.";
                return false;
            }

            AssetDatabase.SaveAssets();
            Undo.FlushUndoRecordObjects();
            RoomValidationReport report = RoomConnectionValidator.Validate();
            message =
                $"Created connection '{connection.name}' ({direction}) between " +
                $"'{roomA.RoomId}/{doorAId}' and '{roomB.RoomId}/{doorBId}'. " +
                $"Validation: {report.ErrorCount} error(s), {report.WarningCount} warning(s).";

            Undo.CollapseUndoOperations(undoGroup);
            created = true;
            createdConnection = connection;

            return true;
        }
        catch (Exception exception)
        {
            if (connection != null && !created)
            {
                RollbackAndDiscardUndo(connection, handleA, handleB, undoGroup);
                undoGroup = -1;
            }

            createdConnection = null;
            message = "Connection creation failed: " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            CloseOpenedScenes(handles, originalActiveScene);
        }
    }

    public static bool UpdateConnection(
        RoomConnection connection,
        RoomDefinition roomA,
        string doorAId,
        RoomDefinition roomB,
        string doorBId,
        RoomConnectionDirection direction,
        out string message)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room connections cannot be modified in Play Mode.";
            return false;
        }

        if (connection == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(connection)))
        {
            message = "Select a persistent RoomConnection asset before modifying it.";
            return false;
        }

        if (!ValidateRequestedEndpoints(roomA, doorAId, roomB, doorBId, direction, out RoomRegistry registry, out message))
            return false;

        List<DoorHandle> handles = new List<DoorHandle>();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        DoorHandle handleA = null;
        DoorHandle handleB = null;
        Dictionary<RoomTransition, RoomConnection> backlinkSnapshot =
            new Dictionary<RoomTransition, RoomConnection>();
        string oldARoomId = connection.EndpointARoomId;
        string oldADoorId = connection.EndpointADoorId;
        string oldBRoomId = connection.EndpointBRoomId;
        string oldBDoorId = connection.EndpointBDoorId;
        RoomConnectionDirection oldDirection = connection.Direction;
        bool mutated = false;
        bool scenesClosed = false;
        ConnectionUndoRecord undoRecord = null;

        try
        {
            if (!TryGetDoor(roomA, doorAId, handles, out handleA, out message) ||
                !TryGetDoor(roomB, doorBId, handles, out handleB, out message))
            {
                return false;
            }

            if (ReferenceEquals(handleA.Door, handleB.Door))
            {
                message = "Endpoint A and endpoint B resolve to the same door.";
                return false;
            }

            if (!CanUseEndpoint(handleA.Door, connection, roomA.RoomId, doorAId, out message) ||
                !CanUseEndpoint(handleB.Door, connection, roomB.RoomId, doorBId, out message))
            {
                return false;
            }

            if (!FindBacklinkedDoors(registry, connection, handles, backlinkSnapshot, out message))
                return false;

            CaptureBacklinkSnapshot(handleA.Door, backlinkSnapshot);
            CaptureBacklinkSnapshot(handleB.Door, backlinkSnapshot);

            undoRecord = CreateUpdateUndoRecord(
                connection,
                oldARoomId,
                oldADoorId,
                oldBRoomId,
                oldBDoorId,
                oldDirection,
                roomA,
                doorAId,
                roomB,
                doorBId,
                direction,
                backlinkSnapshot,
                handleA.Door,
                handleB.Door);

            foreach (RoomTransition previousDoor in backlinkSnapshot.Keys)
            {
                if (previousDoor != null && previousDoor.Connection == connection && !SetBacklink(previousDoor, null))
                    throw new InvalidOperationException($"Could not clear the old backlink on door '{previousDoor.name}'.");
            }

            SetConnectionDataSerialized(connection, roomA.RoomId, doorAId, roomB.RoomId, doorBId, direction);
            if (!SetBacklink(handleA.Door, connection) || !SetBacklink(handleB.Door, connection))
                throw new InvalidOperationException("Could not assign the updated connection backlink to both doors.");
            mutated = true;

            if (!SaveTouchedScenes(handles, backlinkSnapshot.Keys, handleA.Door, handleB.Door, out message))
                throw new InvalidOperationException(message);

            EditorUtility.SetDirty(connection);
            AssetDatabase.SaveAssets();
            CloseOpenedScenes(handles, originalActiveScene);
            scenesClosed = true;
            RegisterUndoRecord(undoRecord, UpdateUndoName);
            message = $"Updated connection '{connection.name}' ({direction}) between '{roomA.RoomId}/{doorAId}' and '{roomB.RoomId}/{doorBId}'.";
            return true;
        }
        catch (Exception exception)
        {
            if (undoRecord != null)
                DiscardUndoRecord(undoRecord);

            if (mutated || backlinkSnapshot.Count > 0)
            {
                connection.Assign(oldARoomId, oldADoorId, oldBRoomId, oldBDoorId, oldDirection);
                RestoreBacklinks(backlinkSnapshot);
                SaveTouchedScenes(handles, backlinkSnapshot.Keys, handleA != null ? handleA.Door : null,
                    handleB != null ? handleB.Door : null, out _);
                AssetDatabase.SaveAssets();
            }

            message = "Connection update failed and was rolled back: " + exception.Message;
            return false;
        }
        finally
        {
            if (!scenesClosed)
                CloseOpenedScenes(handles, originalActiveScene);
        }
    }

    public static bool DeleteConnection(RoomConnection connection, out string message)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room connections cannot be deleted in Play Mode.";
            return false;
        }

        if (connection == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(connection)))
        {
            message = "Select a persistent RoomConnection asset before deleting it.";
            return false;
        }

        string connectionName = connection.name;
        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null || registry.Rooms == null)
        {
            message = "RoomRegistry is missing or invalid; registered backlinks cannot be checked safely.";
            return false;
        }

        List<DoorHandle> handles = new List<DoorHandle>();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Dictionary<RoomTransition, RoomConnection> backlinkSnapshot =
            new Dictionary<RoomTransition, RoomConnection>();
        bool assetDestroyed = false;
        bool scenesClosed = false;
        ConnectionUndoRecord undoRecord = null;

        try
        {
            if (!FindBacklinkedDoors(registry, connection, handles, backlinkSnapshot, out message))
                return false;

            undoRecord = CreateDeleteUndoRecord(connection, backlinkSnapshot);

            foreach (RoomTransition door in backlinkSnapshot.Keys)
            {
                if (door != null && door.Connection == connection && !SetBacklink(door, null))
                    throw new InvalidOperationException($"Could not clear the backlink on door '{door.name}'.");
            }

            if (!SaveTouchedScenes(handles, backlinkSnapshot.Keys, null, null, out message))
                throw new InvalidOperationException(message);

            string assetPath = AssetDatabase.GetAssetPath(connection);
            if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.DeleteAsset(assetPath))
                throw new InvalidOperationException($"Could not delete connection asset '{assetPath}'.");
            assetDestroyed = true;
            AssetDatabase.SaveAssets();
            CloseOpenedScenes(handles, originalActiveScene);
            scenesClosed = true;
            RegisterUndoRecord(undoRecord, DeleteUndoName);
            message = $"Deleted connection '{connectionName}' and cleared {backlinkSnapshot.Count} registered door backlink(s).";
            return true;
        }
        catch (Exception exception)
        {
            if (undoRecord != null)
                DiscardUndoRecord(undoRecord);

            RoomConnection rollbackConnection = assetDestroyed && undoRecord != null
                ? RestoreConnectionAsset(undoRecord)
                : connection;
            if (rollbackConnection != null)
                RestoreBacklinksTo(backlinkSnapshot.Keys, rollbackConnection);
            SaveTouchedScenes(handles, backlinkSnapshot.Keys, null, null, out _);
            AssetDatabase.SaveAssets();
            message = "Connection deletion failed and was rolled back: " + exception.Message;
            return false;
        }
        finally
        {
            if (!scenesClosed)
                CloseOpenedScenes(handles, originalActiveScene);
        }
    }

    private static bool ValidateRequestedEndpoints(
        RoomDefinition roomA,
        string doorAId,
        RoomDefinition roomB,
        string doorBId,
        RoomConnectionDirection direction,
        out RoomRegistry registry,
        out string message)
    {
        registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (roomA == null || roomB == null)
        {
            message = "Both endpoints must reference a RoomDefinition.";
            return false;
        }

        if (roomA == roomB || string.Equals(roomA.RoomId, roomB.RoomId, StringComparison.Ordinal))
        {
            message = "Self-connections are not supported. Select two different rooms.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(doorAId) || string.IsNullOrWhiteSpace(doorBId))
        {
            message = "Both endpoints must have a non-empty stable DoorId.";
            return false;
        }

        if (direction != RoomConnectionDirection.Bidirectional && direction != RoomConnectionDirection.OneWay)
        {
            message = $"Invalid connection direction value ({(int)direction}).";
            return false;
        }

        if (registry == null || registry.Rooms == null ||
            !ContainsDefinition(registry, roomA) || !ContainsDefinition(registry, roomB))
        {
            message = "Both endpoint rooms must be registered in RoomRegistry.";
            return false;
        }

        if (!ValidateRegisteredRoomIdentity(registry, roomA, out message) ||
            !ValidateRegisteredRoomIdentity(registry, roomB, out message))
        {
            return false;
        }

        message = null;
        return true;
    }

    private static bool CanUseEndpoint(
        RoomTransition door,
        RoomConnection connection,
        string roomId,
        string doorId,
        out string message)
    {
        if (door.Connection != null && door.Connection != connection)
        {
            message = $"Door '{doorId}' in room '{roomId}' is already connected to '{door.Connection.name}'.";
            return false;
        }

        if (TryFindOtherConnectionUsingEndpoint(roomId, doorId, connection, out RoomConnection existing))
        {
            message = $"Door '{doorId}' in room '{roomId}' is already listed by connection '{existing.name}'.";
            return false;
        }

        message = null;
        return true;
    }

    private static bool TryFindOtherConnectionUsingEndpoint(
        string roomId,
        string doorId,
        RoomConnection ignoredConnection,
        out RoomConnection found)
    {
        found = null;
        foreach (string guid in AssetDatabase.FindAssets("t:RoomConnection"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            RoomConnection candidate = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
            if (candidate == null || candidate == ignoredConnection)
                continue;

            if (EndpointMatches(candidate.EndpointARoomId, candidate.EndpointADoorId, roomId, doorId) ||
                EndpointMatches(candidate.EndpointBRoomId, candidate.EndpointBDoorId, roomId, doorId))
            {
                found = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool FindBacklinkedDoors(
        RoomRegistry registry,
        RoomConnection connection,
        List<DoorHandle> handles,
        Dictionary<RoomTransition, RoomConnection> backlinks,
        out string message)
    {
        HashSet<string> scannedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition == null)
                continue;

            string scenePath = AssetDatabase.GUIDToAssetPath(definition.SceneGuid);
            if (string.IsNullOrEmpty(scenePath))
            {
                if (string.Equals(definition.RoomId, connection.EndpointARoomId, StringComparison.Ordinal) ||
                    string.Equals(definition.RoomId, connection.EndpointBRoomId, StringComparison.Ordinal))
                {
                    message = $"Could not resolve the scene for connection endpoint room '{definition.RoomId}'.";
                    return false;
                }
                continue;
            }

            if (!scannedPaths.Add(scenePath))
                continue;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedByOperation = !scene.IsValid() || !scene.isLoaded;
            if (openedByOperation)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            else if (scene.isDirty)
            {
                message = $"Scene '{scene.name}' has unsaved changes. Save it before modifying a connection.";
                return false;
            }

            DoorHandle handle = new DoorHandle(scene, openedByOperation);
            handles.Add(handle);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RoomTransition door in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                {
                    if (door == null || door.Connection != connection)
                        continue;

                    handle.Door = door;
                    if (!backlinks.ContainsKey(door))
                        backlinks.Add(door, connection);
                }
            }
        }

        message = null;
        return true;
    }

    private static void CaptureBacklinkSnapshot(
        RoomTransition door,
        Dictionary<RoomTransition, RoomConnection> backlinks)
    {
        if (door != null && !backlinks.ContainsKey(door))
            backlinks.Add(door, door.Connection);
    }

    private static bool SaveTouchedScenes(
        List<DoorHandle> handles,
        IEnumerable<RoomTransition> touchedDoors,
        RoomTransition extraA,
        RoomTransition extraB,
        out string message)
    {
        HashSet<Scene> savedScenes = new HashSet<Scene>();
        HashSet<RoomTransition> touched = new HashSet<RoomTransition>();
        if (touchedDoors != null)
            foreach (RoomTransition door in touchedDoors)
                if (door != null) touched.Add(door);
        if (extraA != null) touched.Add(extraA);
        if (extraB != null) touched.Add(extraB);

        foreach (DoorHandle handle in handles)
        {
            if (handle == null || handle.Door == null || !touched.Contains(handle.Door) ||
                !savedScenes.Add(handle.Scene))
            {
                continue;
            }

            if (!SaveDoorScene(handle))
            {
                message = $"Could not save scene '{handle.Scene.path}' while updating the connection.";
                return false;
            }
        }

        message = null;
        return true;
    }

    private static void RestoreBacklinks(
        Dictionary<RoomTransition, RoomConnection> backlinks)
    {
        foreach (KeyValuePair<RoomTransition, RoomConnection> pair in backlinks)
        {
            if (pair.Key != null)
                SetBacklink(pair.Key, pair.Value);
        }
    }

    private static void RestoreBacklinksTo(
        IEnumerable<RoomTransition> doors,
        RoomConnection connection)
    {
        if (doors == null || connection == null)
            return;

        foreach (RoomTransition door in doors)
        {
            if (door != null)
                SetBacklink(door, connection);
        }
    }

    private static void SetConnectionDataSerialized(
        RoomConnection connection,
        string roomAId,
        string doorAId,
        string roomBId,
        string doorBId,
        RoomConnectionDirection direction)
    {
        SerializedObject serialized = new SerializedObject(connection);
        serialized.FindProperty("endpointARoomId").stringValue = roomAId;
        serialized.FindProperty("endpointADoorId").stringValue = doorAId;
        serialized.FindProperty("endpointBRoomId").stringValue = roomBId;
        serialized.FindProperty("endpointBDoorId").stringValue = doorBId;
        serialized.FindProperty("direction").enumValueIndex = (int)direction;
        serialized.ApplyModifiedProperties();
    }

    private static ConnectionUndoRecord CreateUpdateUndoRecord(
        RoomConnection connection,
        string oldARoomId,
        string oldADoorId,
        string oldBRoomId,
        string oldBDoorId,
        RoomConnectionDirection oldDirection,
        RoomDefinition newRoomA,
        string newDoorAId,
        RoomDefinition newRoomB,
        string newDoorBId,
        RoomConnectionDirection newDirection,
        Dictionary<RoomTransition, RoomConnection> backlinkSnapshot,
        RoomTransition newDoorA,
        RoomTransition newDoorB)
    {
        ConnectionUndoRecord record = CreateBaseUndoRecord(connection);
        record.Before = new ConnectionState(oldARoomId, oldADoorId, oldBRoomId, oldBDoorId, oldDirection);
        record.After = new ConnectionState(
            newRoomA.RoomId,
            newDoorAId,
            newRoomB.RoomId,
            newDoorBId,
            newDirection);

        foreach (KeyValuePair<RoomTransition, RoomConnection> pair in backlinkSnapshot)
        {
            AddDoorAddress(record.AffectedDoors, CaptureDoorAddress(pair.Key));
            if (pair.Value == connection)
                AddDoorAddress(record.BeforeBacklinks, CaptureDoorAddress(pair.Key));
        }

        ConnectionDoorAddress addressA = CaptureDoorAddress(newDoorA);
        ConnectionDoorAddress addressB = CaptureDoorAddress(newDoorB);
        AddDoorAddress(record.AffectedDoors, addressA);
        AddDoorAddress(record.AffectedDoors, addressB);
        AddDoorAddress(record.AfterBacklinks, addressA);
        AddDoorAddress(record.AfterBacklinks, addressB);
        return record;
    }

    private static ConnectionUndoRecord CreateDeleteUndoRecord(
        RoomConnection connection,
        Dictionary<RoomTransition, RoomConnection> backlinkSnapshot)
    {
        ConnectionUndoRecord record = CreateBaseUndoRecord(connection);
        record.IsDelete = true;
        record.Before = new ConnectionState(
            connection.EndpointARoomId,
            connection.EndpointADoorId,
            connection.EndpointBRoomId,
            connection.EndpointBDoorId,
            connection.Direction);
        record.After = record.Before;

        foreach (KeyValuePair<RoomTransition, RoomConnection> pair in backlinkSnapshot)
        {
            ConnectionDoorAddress address = CaptureDoorAddress(pair.Key);
            AddDoorAddress(record.AffectedDoors, address);
            if (pair.Value == connection)
                AddDoorAddress(record.BeforeBacklinks, address);
        }

        return record;
    }

    private static ConnectionUndoRecord CreateBaseUndoRecord(RoomConnection connection)
    {
        string assetPath = AssetDatabase.GetAssetPath(connection);
        string metaPath = assetPath + ".meta";
        return new ConnectionUndoRecord
        {
            AssetPath = assetPath,
            AssetGuid = AssetDatabase.AssetPathToGUID(assetPath),
            AssetMeta = File.Exists(metaPath) ? File.ReadAllText(metaPath) : null,
            AssetName = connection.name,
            Sentinel = null,
            LastApplied = true
        };
    }

    private static ConnectionDoorAddress CaptureDoorAddress(RoomTransition door)
    {
        if (door == null)
            return null;

        return new ConnectionDoorAddress(
            door.gameObject.scene.path,
            door.DoorId,
            door.gameObject.name,
            BuildDoorHierarchyPath(door.transform));
    }

    private static void AddDoorAddress(List<ConnectionDoorAddress> addresses, ConnectionDoorAddress address)
    {
        if (address == null || string.IsNullOrEmpty(address.ScenePath))
            return;

        foreach (ConnectionDoorAddress existing in addresses)
        {
            if (string.Equals(existing.Key, address.Key, StringComparison.Ordinal))
                return;
        }

        addresses.Add(address);
    }

    private static string BuildDoorHierarchyPath(Transform transform)
    {
        List<string> parts = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            parts.Add(current.GetSiblingIndex() + ":" + current.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void RegisterUndoRecord(ConnectionUndoRecord record, string undoName)
    {
        ConnectionUndoSentinel sentinel = ScriptableObject.CreateInstance<ConnectionUndoSentinel>();
        sentinel.name = "RoomConnectionUndo_" + Path.GetFileNameWithoutExtension(record.AssetPath);
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        Undo.RegisterCompleteObjectUndo(sentinel, undoName);
        sentinel.SetApplied(true);
        EditorUtility.SetDirty(sentinel);
        Undo.FlushUndoRecordObjects();
        record.Sentinel = sentinel;
        record.LastApplied = true;
        ConnectionUndoRecords.Add(sentinel, record);
    }

    private static void DiscardUndoRecord(ConnectionUndoRecord record)
    {
        if (record == null || record.Sentinel == null)
            return;

        Undo.ClearUndo(record.Sentinel);
        ConnectionUndoRecords.Remove(record.Sentinel);
        UnityEngine.Object.DestroyImmediate(record.Sentinel);
        record.Sentinel = null;
    }

    private static void OnUndoRedoPerformed()
    {
        ReconcileUndoRedo();
    }

    internal static void ReconcileUndoRedo()
    {
        List<KeyValuePair<ConnectionUndoSentinel, ConnectionUndoRecord>> records =
            new List<KeyValuePair<ConnectionUndoSentinel, ConnectionUndoRecord>>(ConnectionUndoRecords);
        foreach (KeyValuePair<ConnectionUndoSentinel, ConnectionUndoRecord> pair in records)
        {
            ConnectionUndoSentinel sentinel = pair.Key;
            ConnectionUndoRecord record = pair.Value;
            if (sentinel == null || record == null || sentinel.IsApplied == record.LastApplied)
                continue;

            if (ApplyUndoRecord(record, sentinel.IsApplied, out string error))
                record.LastApplied = sentinel.IsApplied;
            else
                Debug.LogError("[RoomConnectionService] Could not restore a graph-authored connection Undo state: " + error);
        }
    }

    private static bool ApplyUndoRecord(ConnectionUndoRecord record, bool isApplied, out string error)
    {
        error = null;
        RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(record.AssetPath);

        if (record.IsDelete && isApplied)
        {
            if (connection == null)
            {
                error = $"Deleted connection asset '{record.AssetPath}' is already missing during Redo.";
                return false;
            }

            if (!ApplyBacklinkState(record, connection, new List<ConnectionDoorAddress>(), out error))
                return false;

            return AssetDatabase.DeleteAsset(record.AssetPath);
        }

        if (record.IsDelete && connection == null)
        {
            connection = RestoreConnectionAsset(record);
            if (connection == null)
            {
                error = $"Could not restore connection asset '{record.AssetPath}'.";
                return false;
            }
        }

        if (connection == null)
        {
            error = $"Connection asset '{record.AssetPath}' is missing during Undo/Redo.";
            return false;
        }

        ConnectionState state = record.IsDelete || !isApplied ? record.Before : record.After;
        connection.Assign(state.EndpointARoomId, state.EndpointADoorId,
            state.EndpointBRoomId, state.EndpointBDoorId, state.Direction);
        EditorUtility.SetDirty(connection);

        List<ConnectionDoorAddress> desired = record.IsDelete
            ? record.BeforeBacklinks
            : isApplied ? record.AfterBacklinks : record.BeforeBacklinks;
        if (!ApplyBacklinkState(record, connection, desired, out error))
            return false;

        AssetDatabase.SaveAssets();
        return true;
    }

    private static RoomConnection RestoreConnectionAsset(ConnectionUndoRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.AssetPath))
            return null;

        RoomConnection existing = AssetDatabase.LoadAssetAtPath<RoomConnection>(record.AssetPath);
        if (existing != null)
            return existing;

        string metaPath = record.AssetPath + ".meta";
        if (!string.IsNullOrEmpty(record.AssetMeta))
            File.WriteAllText(metaPath, record.AssetMeta);

        ConnectionState state = record.Before;
        RoomConnection restored = ScriptableObject.CreateInstance<RoomConnection>();
        restored.name = record.AssetName;
        restored.Assign(state.EndpointARoomId, state.EndpointADoorId,
            state.EndpointBRoomId, state.EndpointBDoorId, state.Direction);
        AssetDatabase.CreateAsset(restored, record.AssetPath);
        if (!string.IsNullOrEmpty(record.AssetMeta))
            File.WriteAllText(metaPath, record.AssetMeta);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        restored = AssetDatabase.LoadAssetAtPath<RoomConnection>(record.AssetPath);
        if (restored == null ||
            (!string.IsNullOrEmpty(record.AssetGuid) &&
             !string.Equals(AssetDatabase.AssetPathToGUID(record.AssetPath), record.AssetGuid, StringComparison.Ordinal)))
        {
            Debug.LogError($"[RoomConnectionService] Restored connection asset '{record.AssetPath}' did not retain its original GUID '{record.AssetGuid}'.");
            return null;
        }

        return restored;
    }

    private static bool ApplyBacklinkState(
        ConnectionUndoRecord record,
        RoomConnection connection,
        List<ConnectionDoorAddress> desiredBacklinks,
        out string error)
    {
        error = null;
        HashSet<string> desiredKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConnectionDoorAddress address in desiredBacklinks)
            desiredKeys.Add(address.Key);

        List<ConnectionDoorAddress> allAddresses = new List<ConnectionDoorAddress>();
        foreach (ConnectionDoorAddress address in record.AffectedDoors)
            AddDoorAddress(allAddresses, address);
        foreach (ConnectionDoorAddress address in desiredBacklinks)
            AddDoorAddress(allAddresses, address);

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Dictionary<string, DoorHandle> handlesByPath = new Dictionary<string, DoorHandle>(StringComparer.Ordinal);
        Dictionary<string, RoomTransition> doorsByAddress =
            new Dictionary<string, RoomTransition>(StringComparer.Ordinal);
        HashSet<Scene> changedScenes = new HashSet<Scene>();

        try
        {
            foreach (ConnectionDoorAddress address in allAddresses)
            {
                if (!handlesByPath.TryGetValue(address.ScenePath, out DoorHandle handle))
                {
                    Scene scene = SceneManager.GetSceneByPath(address.ScenePath);
                    bool openedHere = !scene.IsValid() || !scene.isLoaded;
                    if (openedHere)
                        scene = EditorSceneManager.OpenScene(address.ScenePath, OpenSceneMode.Additive);
                    else if (scene.isDirty)
                    {
                        error = $"Scene '{scene.path}' has unsaved changes; save it before applying connection Undo/Redo.";
                        return false;
                    }

                    handle = new DoorHandle(scene, openedHere);
                    handlesByPath.Add(address.ScenePath, handle);
                }

                RoomTransition door = FindDoorByAddress(handle.Scene, address);
                if (door == null)
                {
                    error = $"Could not resolve DoorId '{address.DoorId}' in '{address.ScenePath}' while applying Undo/Redo.";
                    return false;
                }
                doorsByAddress[address.Key] = door;
            }

            foreach (ConnectionDoorAddress address in allAddresses)
            {
                RoomTransition door = doorsByAddress[address.Key];
                if (door.Connection == connection && !desiredKeys.Contains(address.Key))
                {
                    if (!SetBacklink(door, null))
                    {
                        error = $"Could not clear connection backlink on '{address.DoorId}'.";
                        return false;
                    }
                    changedScenes.Add(door.gameObject.scene);
                }
            }

            foreach (ConnectionDoorAddress address in desiredBacklinks)
            {
                RoomTransition door = doorsByAddress[address.Key];
                if (door.Connection != null && door.Connection != connection)
                {
                    error = $"Door '{address.DoorId}' is now linked to another connection; Undo/Redo was not applied.";
                    return false;
                }
                if (!SetBacklink(door, connection))
                {
                    error = $"Could not restore connection backlink on '{address.DoorId}'.";
                    return false;
                }
                changedScenes.Add(door.gameObject.scene);
            }

            foreach (Scene changedScene in changedScenes)
            {
                EditorSceneManager.MarkSceneDirty(changedScene);
                if (!EditorSceneManager.SaveScene(changedScene))
                {
                    error = $"Could not save scene '{changedScene.path}' while applying connection Undo/Redo.";
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            foreach (DoorHandle handle in handlesByPath.Values)
            {
                if (handle.OpenedByOperation && handle.Scene.IsValid() && handle.Scene.isLoaded)
                    EditorSceneManager.CloseScene(handle.Scene, removeScene: true);
            }
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded &&
                SceneManager.GetActiveScene() != originalActiveScene)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }
        }
    }

    private static RoomTransition FindDoorByAddress(Scene scene, ConnectionDoorAddress address)
    {
        RoomTransition hierarchyMatch = null;
        RoomTransition idMatch = null;
        int idMatches = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RoomTransition door in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
            {
                if (string.Equals(BuildDoorHierarchyPath(door.transform), address.HierarchyPath, StringComparison.Ordinal))
                    hierarchyMatch = door;

                if (string.Equals(door.DoorId, address.DoorId, StringComparison.Ordinal) &&
                    string.Equals(door.gameObject.name, address.GameObjectName, StringComparison.Ordinal))
                {
                    idMatch = door;
                    idMatches++;
                }
            }
        }

        return hierarchyMatch ?? (idMatches == 1 ? idMatch : null);
    }

    private static bool TryGetDoor(
        RoomDefinition room,
        string doorId,
        List<DoorHandle> handles,
        out DoorHandle handle,
        out string message)
    {
        handle = null;

        string scenePath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
        if (string.IsNullOrEmpty(scenePath))
        {
            message = $"Could not resolve the scene path for room '{room.RoomId}'.";
            return false;
        }

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;

        if (alreadyLoaded && scene.isDirty)
        {
            message = $"Scene '{scene.name}' has unsaved changes. Save it before creating a connection.";
            return false;
        }

        bool openedByOperation = false;

        if (!alreadyLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            openedByOperation = true;
        }

        handle = new DoorHandle(scene, openedByOperation);
        handles.Add(handle);

        RoomTransition match = null;
        int matches = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
            {
                if (!string.Equals(transition.DoorId, doorId, StringComparison.Ordinal))
                    continue;

                match = transition;
                matches++;
            }
        }

        if (matches == 0)
        {
            message = $"No door with stable doorId '{doorId}' was found in room '{room.RoomId}'.";
            return false;
        }

        if (matches > 1)
        {
            message = $"Door ID '{doorId}' is duplicated in room '{room.RoomId}'.";
            return false;
        }

        handle.Door = match;
        message = null;
        return true;
    }

    private static bool SetBacklink(RoomTransition door, RoomConnection connection)
    {
        SerializedObject serializedDoor = new SerializedObject(door);
        SerializedProperty connectionProperty = serializedDoor.FindProperty("connection");

        if (connectionProperty == null)
            return false;

        connectionProperty.objectReferenceValue = connection;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        if (PrefabUtility.IsPartOfPrefabInstance(door))
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);

        return true;
    }

    private static void ClearBacklink(RoomTransition door)
    {
        if (door == null)
            return;

        SerializedObject serializedDoor = new SerializedObject(door);
        SerializedProperty connectionProperty = serializedDoor.FindProperty("connection");

        if (connectionProperty == null)
            return;

        connectionProperty.objectReferenceValue = null;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        if (PrefabUtility.IsPartOfPrefabInstance(door))
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
    }

    private static bool SaveDoorScene(DoorHandle handle)
    {
        if (handle == null || handle.Door == null)
            return false;

        EditorSceneManager.MarkSceneDirty(handle.Scene);
        if (SaveDoorSceneOverrideForTests != null)
            return SaveDoorSceneOverrideForTests(handle.Scene);

        return EditorSceneManager.SaveScene(handle.Scene);
    }

    private static void Rollback(RoomConnection connection, DoorHandle handleA, DoorHandle handleB)
    {
        if (handleA != null && handleA.Door != null)
            ClearAndRestoreScene(handleA);

        if (handleB != null && handleB.Door != null)
            ClearAndRestoreScene(handleB);

        if (connection != null)
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(connection);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RoomConnectionService] Could not delete connection asset during rollback: {exception.Message}");
            }
        }
    }

    private static void ClearAndRestoreScene(DoorHandle handle)
    {
        try
        {
            ClearBacklink(handle.Door);

            if (!handle.Scene.IsValid() || !handle.Scene.isLoaded)
                return;

            // The operation may already have saved this scene before another
            // endpoint failed. Persist the cleared backlink in either case.
            EditorSceneManager.MarkSceneDirty(handle.Scene);
            if (!EditorSceneManager.SaveScene(handle.Scene))
                Debug.LogError($"[RoomConnectionService] Could not restore scene '{handle.Scene.name}' after rollback.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RoomConnectionService] Rollback of scene '{handle.Scene.name}' failed: {exception.Message}");
        }
    }

    private static void RollbackAndDiscardUndo(
        RoomConnection connection,
        DoorHandle handleA,
        DoorHandle handleB,
        int undoGroup)
    {
        Rollback(connection, handleA, handleB);

        if (undoGroup < 0)
            return;

        try
        {
            Undo.RevertAllDownToGroup(undoGroup);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RoomConnectionService] Could not discard failed connection Undo records: {exception.Message}");
        }
    }

    private static void CloseOpenedScenes(List<DoorHandle> handles, Scene originalActiveScene)
    {
        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            SceneManager.SetActiveScene(originalActiveScene);

        foreach (DoorHandle handle in handles)
        {
            if (handle.OpenedByOperation && handle.Scene.IsValid() && handle.Scene.isLoaded)
                EditorSceneManager.CloseScene(handle.Scene, removeScene: true);
        }
    }

    private static bool ContainsDefinition(RoomRegistry registry, RoomDefinition definition)
    {
        foreach (RoomDefinition room in registry.Rooms)
        {
            if (room == definition)
                return true;
        }

        return false;
    }

    private static bool ValidateRegisteredRoomIdentity(
        RoomRegistry registry,
        RoomDefinition definition,
        out string message)
    {
        int roomIdMatches = 0;
        int sceneGuidMatches = 0;

        foreach (RoomDefinition registered in registry.Rooms)
        {
            if (registered == null)
                continue;

            if (string.Equals(registered.RoomId, definition.RoomId, StringComparison.Ordinal))
                roomIdMatches++;

            if (string.Equals(registered.SceneGuid, definition.SceneGuid, StringComparison.Ordinal))
                sceneGuidMatches++;
        }

        if (roomIdMatches != 1)
        {
            message = $"Room ID '{definition.RoomId}' is not unique in RoomRegistry.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.SceneGuid) || sceneGuidMatches != 1)
        {
            message = $"The scene GUID for room '{definition.RoomId}' is missing or is not unique in RoomRegistry.";
            return false;
        }

        message = null;
        return true;
    }

    private static bool TryFindConnectionUsingEndpoint(string roomId, string doorId, out RoomConnection found)
    {
        found = null;

        string[] guids = AssetDatabase.FindAssets("t:RoomConnection");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
            if (connection == null)
                continue;

            if (EndpointMatches(connection.EndpointARoomId, connection.EndpointADoorId, roomId, doorId) ||
                EndpointMatches(connection.EndpointBRoomId, connection.EndpointBDoorId, roomId, doorId))
            {
                found = connection;
                return true;
            }
        }

        return false;
    }

    private static bool EndpointMatches(string candidateRoomId, string candidateDoorId, string roomId, string doorId)
    {
        return string.Equals(candidateRoomId, roomId, StringComparison.Ordinal) &&
               string.Equals(candidateDoorId, doorId, StringComparison.Ordinal);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent))
            return;

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }

    private static string SanitizeFileName(string fileName)
    {
        StringBuilder builder = new StringBuilder(fileName.Length);

        foreach (char character in fileName)
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0
                ? '_'
                : character);
        }

        return builder.ToString();
    }

    private sealed class DoorHandle
    {
        public DoorHandle(Scene scene, bool openedByOperation)
        {
            Scene = scene;
            OpenedByOperation = openedByOperation;
        }

        public Scene Scene { get; }
        public bool OpenedByOperation { get; }
        public RoomTransition Door { get; set; }
    }

    private sealed class ConnectionUndoRecord
    {
        public ConnectionUndoSentinel Sentinel;
        public string AssetPath;
        public string AssetGuid;
        public string AssetMeta;
        public string AssetName;
        public bool IsDelete;
        public bool LastApplied;
        public ConnectionState Before;
        public ConnectionState After;
        public List<ConnectionDoorAddress> AffectedDoors = new List<ConnectionDoorAddress>();
        public List<ConnectionDoorAddress> BeforeBacklinks = new List<ConnectionDoorAddress>();
        public List<ConnectionDoorAddress> AfterBacklinks = new List<ConnectionDoorAddress>();
    }

    private readonly struct ConnectionState
    {
        public ConnectionState(
            string endpointARoomId,
            string endpointADoorId,
            string endpointBRoomId,
            string endpointBDoorId,
            RoomConnectionDirection direction)
        {
            EndpointARoomId = endpointARoomId;
            EndpointADoorId = endpointADoorId;
            EndpointBRoomId = endpointBRoomId;
            EndpointBDoorId = endpointBDoorId;
            Direction = direction;
        }

        public string EndpointARoomId { get; }
        public string EndpointADoorId { get; }
        public string EndpointBRoomId { get; }
        public string EndpointBDoorId { get; }
        public RoomConnectionDirection Direction { get; }
    }

    private sealed class ConnectionDoorAddress
    {
        public ConnectionDoorAddress(string scenePath, string doorId, string gameObjectName, string hierarchyPath)
        {
            ScenePath = scenePath;
            DoorId = doorId;
            GameObjectName = gameObjectName;
            HierarchyPath = hierarchyPath;
            Key = scenePath + "\u001e" + hierarchyPath;
        }

        public string ScenePath { get; }
        public string DoorId { get; }
        public string GameObjectName { get; }
        public string HierarchyPath { get; }
        public string Key { get; }
    }

    private sealed class ConnectionUndoSentinel : ScriptableObject
    {
        [SerializeField] private bool isApplied;
        public bool IsApplied => isApplied;
        public void SetApplied(bool value) => isApplied = value;
    }
}
