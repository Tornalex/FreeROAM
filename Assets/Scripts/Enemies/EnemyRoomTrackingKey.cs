using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves the runtime key used by <see cref="DeadEnemyTracker"/>.
/// Registered scenes use their stable RoomId; unregistered scenes keep the
/// legacy scene-name fallback. Catalog infrastructure failures are reported
/// once per scene and then fall back to the legacy key.
/// </summary>
public static class EnemyRoomTrackingKey
{
    private static readonly HashSet<string> CatalogFailureScenes = new HashSet<string>();

    /// <summary>
    /// Resolves the tracking key from the enemy's own scene. The scene path is
    /// the only identity source for registered rooms; the current room is used
    /// only by the legacy fallback.
    /// </summary>
    public static string ResolveFromScene(
        string scenePath,
        string sceneName,
        string currentRoom,
        bool hasRoomManager)
    {
        // Persistent or unnamed scenes keep their legacy fallback even if a
        // path happens to be registered; they are not owned rooms.
        if (IsLegacyOnlyScene(sceneName))
            return ResolveLegacyKey(sceneName, currentRoom, hasRoomManager);

        RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(scenePath);
        return Resolve(
            scenePath,
            sceneName,
            currentRoom,
            hasRoomManager,
            identity.Status,
            identity.Room,
            identity.Message);
    }

    /// <summary>
    /// Maps a resolver status to the tracking key. Separated from
    /// <see cref="ResolveFromScene"/> so every status can be exercised without
    /// touching the real Resources catalog.
    /// </summary>
    public static string Resolve(
        string scenePath,
        string sceneName,
        string currentRoom,
        bool hasRoomManager,
        RoomIdentityStatus status,
        RoomDefinition room,
        string message)
    {
        if (status == RoomIdentityStatus.Success && room != null)
            return room.RoomId;

        if (status == RoomIdentityStatus.CatalogMissing ||
            status == RoomIdentityStatus.CatalogInvalid ||
            status == RoomIdentityStatus.CatalogUnavailable)
        {
            ReportCatalogFailure(scenePath, sceneName, status, message);
        }

        return ResolveLegacyKey(sceneName, currentRoom, hasRoomManager);
    }

    /// <summary>
    /// Existing scene-name compatibility fallback. When the manager exists,
    /// objects in an ordinary scene keep the scene name; objects in Master,
    /// DontDestroyOnLoad, or scenes without a usable name use the manager's
    /// currently committed scene name. Without a manager the legacy behavior
    /// is the literal "Unknown" key.
    /// </summary>
    public static string ResolveLegacyKey(
        string owningSceneName,
        string currentRoom,
        bool hasRoomManager)
    {
        if (!hasRoomManager)
            return "Unknown";

        if (!string.IsNullOrEmpty(owningSceneName) &&
            owningSceneName != "DontDestroyOnLoad" &&
            owningSceneName != "Master")
        {
            return owningSceneName;
        }

        return currentRoom;
    }

    private static bool IsLegacyOnlyScene(string sceneName)
    {
        return string.IsNullOrEmpty(sceneName) ||
               sceneName == "DontDestroyOnLoad" ||
               sceneName == "Master";
    }

    private static void ReportCatalogFailure(
        string scenePath,
        string sceneName,
        RoomIdentityStatus status,
        string message)
    {
        string sceneLabel = !string.IsNullOrEmpty(scenePath)
            ? scenePath
            : (!string.IsNullOrEmpty(sceneName) ? sceneName : "<unknown scene>");

        // One diagnostic per scene per process/domain, so a broken catalog
        // cannot flood the Console with one warning per enemy.
        if (!CatalogFailureScenes.Add(sceneLabel))
            return;

        Debug.LogWarning(
            $"[EnemyRoomTrackingKey] Could not resolve room identity for '{sceneLabel}' " +
            $"({status}); falling back to the legacy scene-name key. {message}");
    }
}
