using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Removes room registrations authored through the Room Connectivity Graph.
/// Scene assets and RoomDefinition assets are deliberately preserved; only the
/// registry membership, incident connection assets/backlinks and derived
/// RuntimeRoomCatalog entries are changed.
/// </summary>
public static class RoomRemovalService
{
    public const string RemoveUndoName = "Remove Room Registration";

    public static List<RoomConnection> GetConnectionsForRoom(string roomId)
    {
        List<RoomConnection> result = new List<RoomConnection>();
        if (string.IsNullOrWhiteSpace(roomId))
            return result;

        List<string> paths = GetConnectionAssetPaths();
        foreach (string path in paths)
        {
            RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
            if (connection == null)
                continue;

            if (string.Equals(connection.EndpointARoomId, roomId, StringComparison.Ordinal) ||
                string.Equals(connection.EndpointBRoomId, roomId, StringComparison.Ordinal))
            {
                result.Add(connection);
            }
        }

        return result;
    }

    public static bool RemoveRoomRegistration(
        RoomDefinition definition,
        string stableRoomId,
        string sceneGuid,
        int registryIndex,
        out string message)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room registrations cannot be removed in Play Mode.";
            return false;
        }

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null || registry.Rooms == null)
        {
            message = $"RoomRegistry is missing or invalid at '{RoomRegistrationService.RegistryPath}'.";
            return false;
        }

        int selectedIndex;
        string roomId;
        if (definition == null)
        {
            if (registryIndex < 0 || registryIndex >= registry.Rooms.Count || registry.Rooms[registryIndex] != null)
            {
                message = "The selected missing RoomDefinition registry entry is stale. Refresh the graph and select it again.";
                return false;
            }

            selectedIndex = registryIndex;
            roomId = null;
        }
        else
        {
            if (!TryResolveRegistration(registry, definition, stableRoomId, sceneGuid,
                    out selectedIndex, out roomId, out message))
            {
                return false;
            }
        }

        List<RoomConnection> incidentConnections = GetConnectionsForRoom(roomId);
        int undoGroup = -1;
        bool removedRegistryEntry = false;

        try
        {
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(RemoveUndoName);

            // Connections are removed through the existing transactional
            // service so its per-asset undo records also restore backlinks.
            foreach (RoomConnection connection in incidentConnections)
            {
                if (!RoomConnectionService.DeleteConnection(connection, out message))
                    throw new InvalidOperationException(message);
            }

            // Record the registry snapshot only after the connection deletions.
            // Their Undo flushes would otherwise finalize the registry record
            // before the room is actually removed, breaking Redo.
            Undo.RegisterCompleteObjectUndo(registry, RemoveUndoName);

            bool removed = definition == null
                ? registry.RemoveAt(selectedIndex)
                : registry.Remove(definition);
            if (!removed)
                throw new InvalidOperationException("The selected room registration changed before it could be removed.");

            removedRegistryEntry = true;
            EditorUtility.SetDirty(registry);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssets();

            RuntimeRoomCatalogValidationReport catalogReport = RuntimeRoomCatalogGenerator.GenerateOrUpdate();
            if (!catalogReport.IsValid)
            {
                string detail = catalogReport.Issues.Count > 0
                    ? catalogReport.Issues[0].Message
                    : "RuntimeRoomCatalog generation failed.";
                throw new InvalidOperationException(detail);
            }

            Undo.SetCurrentGroupName(RemoveUndoName);
            Undo.CollapseUndoOperations(undoGroup);
            message = definition == null
                ? $"Removed invalid RoomRegistry entry {registryIndex}; no RoomDefinition or scene asset was deleted."
                : $"Removed room registration '{roomId}', deleted {incidentConnections.Count} incident connection(s), and regenerated RuntimeRoomCatalog. The scene and RoomDefinition asset were preserved.";
            return true;
        }
        catch (Exception exception)
        {
            if (undoGroup >= 0)
            {
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    RoomConnectionService.ReconcileUndoRedo();
                }
                catch (Exception rollbackException)
                {
                    Debug.LogError("[RoomRemoval] Undo rollback failed: " + rollbackException.Message);
                }
            }

            if (removedRegistryEntry && definition == null && registryIndex >= registry.Rooms.Count)
            {
                registry.RestoreMissingAt(registryIndex);
            }
            else if (removedRegistryEntry && definition != null && !ContainsDefinition(registry, definition))
            {
                registry.Insert(registryIndex, definition);
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            RuntimeRoomCatalogValidationReport restoreReport = RoomRegistrationService.SynchronizeRuntimeCatalog();
            message = "Room removal failed and was rolled back: " + exception.Message;
            if (!restoreReport.IsValid)
                message += " RuntimeRoomCatalog also needs repair: " + restoreReport.Issues[0].Message;
            return false;
        }
    }

    private static bool TryResolveRegistration(
        RoomRegistry registry,
        RoomDefinition requestedDefinition,
        string stableRoomId,
        string sceneGuid,
        out int index,
        out string roomId,
        out string message)
    {
        index = -1;
        roomId = null;
        List<int> matches = new List<int>();
        for (int i = 0; i < registry.Rooms.Count; i++)
        {
            RoomDefinition candidate = registry.Rooms[i];
            if (candidate == requestedDefinition)
            {
                matches.Add(i);
                continue;
            }

            bool idMatches = !string.IsNullOrWhiteSpace(stableRoomId) &&
                             string.Equals(candidate != null ? candidate.RoomId : null,
                                 stableRoomId, StringComparison.Ordinal);
            bool sceneMatches = !string.IsNullOrWhiteSpace(sceneGuid) && candidate != null &&
                                string.Equals(candidate.SceneGuid, sceneGuid, StringComparison.Ordinal);
            if (idMatches || sceneMatches)
                matches.Add(i);
        }

        if (matches.Count != 1)
        {
            message = matches.Count == 0
                ? "The selected room is no longer registered. Refresh the graph and try again."
                : "The selected RoomId or scene GUID is duplicated in RoomRegistry; removal was cancelled to protect unrelated rooms.";
            return false;
        }

        index = matches[0];
        RoomDefinition selected = registry.Rooms[index];
        if (selected == null)
        {
            message = "The selected room registration has a missing RoomDefinition reference.";
            return false;
        }

        roomId = selected.RoomId;
        if (string.IsNullOrWhiteSpace(roomId))
        {
            message = "The selected RoomDefinition has no stable RoomId; removal was cancelled rather than guessing which connections to delete.";
            return false;
        }

        message = null;
        return true;
    }

    private static List<string> GetConnectionAssetPaths()
    {
        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:RoomConnection"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static bool ContainsDefinition(RoomRegistry registry, RoomDefinition definition)
    {
        foreach (RoomDefinition candidate in registry.Rooms)
            if (candidate == definition)
                return true;
        return false;
    }
}
