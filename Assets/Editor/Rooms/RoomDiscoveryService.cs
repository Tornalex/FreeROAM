using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct DiscoveredDoor
{
    public DiscoveredDoor(
        string doorID,
        string stableDoorId,
        string gameObjectName,
        bool isActive,
        RoomConnection connection)
        : this(
            doorID,
            stableDoorId,
            gameObjectName,
            isActive,
            connection,
            new Vector2(0.5f, 0.5f),
            false,
            Vector2.zero)
    {
    }

    public DiscoveredDoor(
        string doorID,
        string stableDoorId,
        string gameObjectName,
        bool isActive,
        RoomConnection connection,
        Vector2 normalizedScenePosition,
        bool hasSpatialData,
        Vector2 scenePosition)
        : this(
            doorID,
            stableDoorId,
            gameObjectName,
            isActive,
            connection,
            normalizedScenePosition,
            hasSpatialData,
            scenePosition,
            null,
            null)
    {
    }

    public DiscoveredDoor(
        string doorID,
        string stableDoorId,
        string gameObjectName,
        bool isActive,
        RoomConnection connection,
        Vector2 normalizedScenePosition,
        bool hasSpatialData,
        Vector2 scenePosition,
        string targetSceneName,
        string targetDoorID)
    {
        DoorID = doorID;
        StableDoorId = stableDoorId;
        GameObjectName = gameObjectName;
        IsActive = isActive;
        TargetSceneName = targetSceneName;
        TargetDoorID = targetDoorID;
        Connection = connection;
        NormalizedScenePosition = normalizedScenePosition;
        HasSpatialData = hasSpatialData;
        ScenePosition = scenePosition;
    }

    public string DoorID { get; }
    public string StableDoorId { get; }
    public string GameObjectName { get; }
    public bool IsActive { get; }
    public string TargetSceneName { get; }
    public string TargetDoorID { get; }
    public RoomConnection Connection { get; }
    public Vector2 NormalizedScenePosition { get; }
    public bool HasSpatialData { get; }
    public Vector2 ScenePosition { get; }
}

internal readonly struct DoorDiscoveryResult
{
    public DoorDiscoveryResult(List<DiscoveredDoor> doors, string message)
    {
        Doors = doors;
        Message = message;
    }

    public List<DiscoveredDoor> Doors { get; }
    public string Message { get; }
}

public static class RoomDiscoveryService
{
    [MenuItem("Tools/Project Spear/Rooms/Discover Doors in Registered Scenes")]
    public static void DiscoverAllMenu()
    {
        if (!RoomRegistrationService.TryGetRegistry(out RoomRegistry registry, out string error))
        {
            Debug.LogWarning($"[RoomDiscovery] {error}");
            return;
        }

        int roomCount = 0;
        int doorCount = 0;

        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition == null)
                continue;

            roomCount++;

            List<DiscoveredDoor> doors = DiscoverDoors(definition, out string message);
            if (doors == null)
            {
                Debug.LogWarning($"[RoomDiscovery] {message}");
                continue;
            }

            doorCount += doors.Count;
            Debug.Log($"[RoomDiscovery] Scene '{definition.SceneName}' (room '{definition.RoomId}'): {doors.Count} door(s).");

            foreach (DiscoveredDoor door in doors)
            {
                Debug.Log(
                    $"[RoomDiscovery]   Door '{door.DoorID}' (stable '{door.StableDoorId}', object '{door.GameObjectName}', " +
                    $"{(door.IsActive ? "active" : "inactive")}) -> " +
                    $"'{door.TargetSceneName}' / '{door.TargetDoorID}', " +
                    $"connection '{(door.Connection != null ? door.Connection.name : "none")}'");
            }
        }

        Debug.Log($"[RoomDiscovery] Scanned {roomCount} registered scene(s), found {doorCount} door(s).");
    }

    /// <summary>
    /// Reads RoomTransition components from the registered scene without modifying or saving it.
    /// The scene is opened additively when it is not already loaded, then closed again.
    /// </summary>
    public static List<DiscoveredDoor> DiscoverDoors(RoomDefinition definition, out string message)
    {
        if (definition == null)
        {
            message = "Cannot discover doors for a null RoomDefinition.";
            return null;
        }

        string scenePath = AssetDatabase.GUIDToAssetPath(definition.SceneGuid);
        if (string.IsNullOrEmpty(scenePath))
        {
            message = $"Could not resolve a scene path for room '{definition.RoomId}' (GUID '{definition.SceneGuid}').";
            return null;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedByThisPass = !scene.IsValid() || !scene.isLoaded;

        try
        {
            if (openedByThisPass)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            List<RoomTransition> transitions = new List<RoomTransition>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                    transitions.Add(transition);
            }

            bool hasRoomBounds = TryGetRoomBounds(scene, out Bounds roomBounds);
            Dictionary<RoomTransition, Vector2> fallbackPositions = hasRoomBounds
                ? null
                : BuildDeterministicFallbackPositions(transitions);
            RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);

            List<DiscoveredDoor> doors = new List<DiscoveredDoor>(transitions.Count);
            foreach (RoomTransition transition in transitions)
            {
                Vector2 normalizedPosition = hasRoomBounds
                    ? NormalizePosition(transition.transform.position, roomBounds)
                    : fallbackPositions[transition];
                GetTarget(
                    definition,
                    transition,
                    registry,
                    out string targetSceneName,
                    out string targetDoorID);

                doors.Add(new DiscoveredDoor(
                    transition.DoorID,
                    transition.DoorId,
                    transition.gameObject.name,
                    transition.gameObject.activeInHierarchy,
                    transition.Connection,
                    normalizedPosition,
                    hasRoomBounds,
                    new Vector2(transition.transform.position.x, transition.transform.position.y),
                    targetSceneName,
                    targetDoorID));
            }

            message = null;
            return doors;
        }
        catch (Exception exception)
        {
            message = $"Could not discover doors in '{scenePath}': {exception.Message}";
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);

            if (openedByThisPass && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    private static void GetTarget(
        RoomDefinition sourceRoom,
        RoomTransition sourceDoor,
        RoomRegistry registry,
        out string targetSceneName,
        out string targetDoorID)
    {
        targetSceneName = null;
        targetDoorID = null;

        RoomConnection connection = sourceDoor != null ? sourceDoor.Connection : null;
        if (sourceRoom == null || connection == null)
            return;

        bool matchesEndpointA =
            string.Equals(connection.EndpointARoomId, sourceRoom.RoomId, StringComparison.Ordinal) &&
            string.Equals(connection.EndpointADoorId, sourceDoor.DoorId, StringComparison.Ordinal);
        bool matchesEndpointB =
            string.Equals(connection.EndpointBRoomId, sourceRoom.RoomId, StringComparison.Ordinal) &&
            string.Equals(connection.EndpointBDoorId, sourceDoor.DoorId, StringComparison.Ordinal);

        if (matchesEndpointA == matchesEndpointB)
            return;

        string targetRoomId = matchesEndpointA
            ? connection.EndpointBRoomId
            : connection.EndpointARoomId;
        targetDoorID = matchesEndpointA
            ? connection.EndpointBDoorId
            : connection.EndpointADoorId;

        if (registry == null)
            return;

        int targetRoomMatches = 0;
        foreach (RoomDefinition room in registry.Rooms)
        {
            if (room == null || !string.Equals(room.RoomId, targetRoomId, StringComparison.Ordinal))
                continue;

            targetSceneName = room.SceneName;
            targetRoomMatches++;
        }

        if (targetRoomMatches != 1)
            targetSceneName = null;
    }

    private static bool TryGetRoomBounds(Scene scene, out Bounds bounds)
    {
        bounds = default;
        if (TryGetCameraBounds(scene, out bounds))
            return true;

        bool initialized = false;

        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                Encapsulate(renderer.bounds, ref bounds, ref initialized);
            }

            foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(includeInactive: true))
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                    continue;

                Encapsulate(collider.bounds, ref bounds, ref initialized);
            }
        }

        return initialized && IsUsableBounds(bounds);
    }

    private static bool TryGetCameraBounds(Scene scene, out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(includeInactive: true))
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    !collider.gameObject.CompareTag("CameraBounds"))
                {
                    continue;
                }

                Encapsulate(collider.bounds, ref bounds, ref initialized);
            }
        }

        return initialized && IsUsableBounds(bounds);
    }

    private static void Encapsulate(Bounds candidate, ref Bounds bounds, ref bool initialized)
    {
        if (!IsFinite(candidate.min) || !IsFinite(candidate.max))
            return;

        if (!initialized)
        {
            bounds = candidate;
            initialized = true;
            return;
        }

        bounds.Encapsulate(candidate);
    }

    private static bool IsUsableBounds(Bounds bounds)
    {
        return IsFinite(bounds.min) && IsFinite(bounds.max) &&
               (bounds.size.x > 0.001f || bounds.size.y > 0.001f);
    }

    private static Vector2 NormalizePosition(Vector3 position, Bounds bounds)
    {
        float x = bounds.size.x > 0.001f
            ? Mathf.InverseLerp(bounds.min.x, bounds.max.x, position.x)
            : 0.5f;
        float y = bounds.size.y > 0.001f
            ? Mathf.InverseLerp(bounds.min.y, bounds.max.y, position.y)
            : 0.5f;

        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    private static Dictionary<RoomTransition, Vector2> BuildDeterministicFallbackPositions(
        List<RoomTransition> transitions)
    {
        List<RoomTransition> sorted = new List<RoomTransition>(transitions);
        sorted.Sort((left, right) =>
        {
            int id = string.Compare(left.DoorId, right.DoorId, StringComparison.Ordinal);
            if (id != 0)
                return id;

            return string.Compare(
                BuildHierarchyPath(left.transform),
                BuildHierarchyPath(right.transform),
                StringComparison.Ordinal);
        });

        Dictionary<RoomTransition, Vector2> result = new Dictionary<RoomTransition, Vector2>();
        for (int i = 0; i < sorted.Count; i++)
            result[sorted[i]] = GetFallbackPosition(i, sorted.Count);

        return result;
    }

    private static Vector2 GetFallbackPosition(int index, int count)
    {
        if (count <= 0)
            return new Vector2(0.5f, 0.5f);

        float perimeterPosition = (index + 0.5f) * 4f / count;
        int side = Mathf.Min(3, Mathf.FloorToInt(perimeterPosition));
        float along = perimeterPosition - side;

        switch (side)
        {
            case 0: return new Vector2(along, 1f);
            case 1: return new Vector2(1f, 1f - along);
            case 2: return new Vector2(1f - along, 0f);
            default: return new Vector2(0f, along);
        }
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        List<string> parts = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            parts.Add(current.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
