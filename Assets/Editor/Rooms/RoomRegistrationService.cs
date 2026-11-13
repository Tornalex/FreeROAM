using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomRegistrationService
{
    public const string RegistryFolder = "Assets/Data/Rooms";
    public const string RegistryPath = RegistryFolder + "/RoomRegistry.asset";
    public const string DefinitionsFolder = RegistryFolder + "/Definitions";
    public const string RegisterSceneUndoName = "Register Room Scene";
    private const string MasterSceneName = "Master";
    private static readonly Dictionary<string, RegistrationUndoEntry> CreatedRegistrationUndoEntries =
        new Dictionary<string, RegistrationUndoEntry>(StringComparer.Ordinal);

    static RoomRegistrationService()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    [MenuItem("Tools/Project Spear/Rooms/Register Open Scene…")]
    public static void RegisterOpenSceneMenu()
    {
        bool registered = RegisterOpenScene(out string message);

        if (registered)
            Debug.Log(message);
        else
            Debug.LogWarning(message);

        EditorUtility.DisplayDialog("Room Registration", message, "OK");
    }

    public static bool RegisterOpenScene(out string message)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room registration is not available in Play Mode.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            message = "There is no valid open scene to register.";
            return false;
        }

        if (string.IsNullOrEmpty(scene.path))
        {
            message = "Save the open scene before registering it.";
            return false;
        }

        if (scene.isDirty)
        {
            message = $"Scene '{scene.name}' has unsaved changes. Save it before registering.";
            return false;
        }

        if (scene.name == MasterSceneName)
        {
            message = "The Master scene cannot be registered as a room.";
            return false;
        }

        string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
        if (string.IsNullOrEmpty(sceneGuid))
        {
            message = $"Could not resolve the scene GUID for '{scene.path}'.";
            return false;
        }

        if (!TryGetRegistry(out RoomRegistry registry, out message))
            return false;

        foreach (RoomDefinition existing in registry.Rooms)
        {
            if (existing == null)
                continue;

            if (string.Equals(existing.SceneGuid, sceneGuid, StringComparison.Ordinal))
            {
                message = $"Scene '{scene.name}' is already registered as room '{existing.RoomId}'.";
                return false;
            }

            if (string.Equals(existing.SceneName, scene.name, StringComparison.Ordinal))
            {
                message = $"Another registered scene already uses the name '{scene.name}'.";
                return false;
            }
        }

        EnsureFolder(DefinitionsFolder);

        RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
        definition.AssignIdentity(
            "room_" + Guid.NewGuid().ToString("N"),
            scene.name,
            scene.name,
            sceneGuid);

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{DefinitionsFolder}/{SanitizeFileName(scene.name)}.asset");

        AssetDatabase.CreateAsset(definition, assetPath);

        if (!registry.Add(definition))
        {
            AssetDatabase.DeleteAsset(assetPath);
            message = $"Could not add room '{scene.name}' to the registry.";
            return false;
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        message = $"Registered room '{scene.name}' with ID '{definition.RoomId}' at '{assetPath}'.";
        return true;
    }

    /// <summary>
    /// Registers a dropped SceneAsset or refreshes the registered identity for
    /// a scene that was renamed. Existing RoomIds and RoomDefinition assets are
    /// preserved; only a missing definition is created. The scene is enabled
    /// in Build Settings and the generated runtime catalog is synchronized so
    /// the graph authoring operation is sufficient for runtime room lookup.
    /// </summary>
    public static bool RegisterOrRefreshScene(
        SceneAsset sceneAsset,
        out RoomDefinition definition,
        out bool created,
        out string message)
    {
        definition = null;
        created = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room registration is not available in Play Mode.";
            return false;
        }

        if (sceneAsset == null || !AssetDatabase.Contains(sceneAsset))
        {
            message = "Drop a persistent Unity SceneAsset into the graph.";
            return false;
        }

        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        if (string.IsNullOrWhiteSpace(scenePath) || string.IsNullOrWhiteSpace(sceneGuid) ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            message = "The dropped asset does not resolve to a valid Unity scene path and GUID.";
            return false;
        }

        if (string.Equals(sceneAsset.name, MasterSceneName, StringComparison.Ordinal))
        {
            message = "The Master scene cannot be registered as a room.";
            return false;
        }

        Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
        if (loadedScene.IsValid() && loadedScene.isLoaded && loadedScene.isDirty)
        {
            message = $"Scene '{sceneAsset.name}' has unsaved changes. Save it before registering or refreshing it.";
            return false;
        }

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RegistryPath);
        if (registry == null || registry.Rooms == null)
        {
            message = $"RoomRegistry is missing or invalid at '{RegistryPath}'.";
            return false;
        }

        List<RoomDefinition> identityMatches = FindDefinitionsForScene(sceneGuid);
        if (identityMatches.Count > 1)
        {
            message = $"Scene GUID '{sceneGuid}' is already assigned to multiple RoomDefinitions. Resolve the identity conflict before importing the scene.";
            return false;
        }

        RoomDefinition existing = identityMatches.Count == 1 ? identityMatches[0] : null;
        foreach (RoomDefinition registered in registry.Rooms)
        {
            if (registered == null || registered == existing)
                continue;

            if (string.Equals(registered.SceneName, sceneAsset.name, StringComparison.Ordinal))
            {
                message = $"Another registered scene already uses the scene name '{sceneAsset.name}'.";
                return false;
            }
        }

        if (existing != null && !ValidateRoomIdUniqueness(registry, existing, out message))
            return false;

        string deterministicAssetPath = existing == null
            ? GetDefinitionAssetPath(sceneAsset.name, sceneGuid)
            : AssetDatabase.GetAssetPath(existing);
        if (existing == null && File.Exists(deterministicAssetPath))
        {
            RoomDefinition conflictingAsset = AssetDatabase.LoadAssetAtPath<RoomDefinition>(deterministicAssetPath);
            message = conflictingAsset == null
                ? $"A non-RoomDefinition asset already exists at '{deterministicAssetPath}'."
                : $"A RoomDefinition already exists at '{deterministicAssetPath}' with a different scene identity.";
            return false;
        }

        bool inRegistry = existing != null && ContainsDefinition(registry, existing);
        bool definitionNeedsUpdate = existing == null ||
            !string.Equals(existing.SceneName, sceneAsset.name, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(existing.DisplayName);
        if (!TryGetBuildSettingsState(scenePath, out bool sceneEnabled, out bool sceneListed, out message))
            return false;

        bool buildSettingsNeedUpdate = !sceneListed || !sceneEnabled;
        if (existing != null && inRegistry && !definitionNeedsUpdate && !buildSettingsNeedUpdate)
        {
            RuntimeRoomCatalogValidationReport currentCatalog = SynchronizeRuntimeCatalog();
            if (!currentCatalog.IsValid)
            {
                message = currentCatalog.Issues.Count > 0
                    ? "The room is registered, but RuntimeRoomCatalog synchronization failed: " + currentCatalog.Issues[0].Message
                    : "The room is registered, but RuntimeRoomCatalog synchronization failed.";
                return false;
            }

            definition = existing;
            message = $"Room '{sceneAsset.name}' is already registered as '{existing.RoomId}'; the graph was refreshed without creating duplicate assets.";
            return true;
        }

        EditorBuildSettingsScene[] previousBuildScenes = EditorBuildSettings.scenes;
        int previousRegistryCount = registry.Rooms.Count;
        int undoGroup = -1;
        bool addedToRegistry = false;
        bool createdAsset = false;
        int registryInsertIndex = registry.Rooms.Count;
        string previousDisplayName = existing != null ? existing.DisplayName : null;
        string previousSceneName = existing != null ? existing.SceneName : null;
        string previousSceneGuid = existing != null ? existing.SceneGuid : null;

        try
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(RegisterSceneUndoName);
            Undo.RegisterCompleteObjectUndo(registry, RegisterSceneUndoName);

            if (existing == null)
            {
                EnsureFolder(DefinitionsFolder);
                existing = ScriptableObject.CreateInstance<RoomDefinition>();
                existing.name = Path.GetFileNameWithoutExtension(deterministicAssetPath);
                existing.AssignIdentity(
                    CreateUniqueRoomId(registry),
                    sceneAsset.name,
                    sceneAsset.name,
                    sceneGuid);
                AssetDatabase.CreateAsset(existing, deterministicAssetPath);
                Undo.RegisterCreatedObjectUndo(existing, RegisterSceneUndoName);
                createdAsset = true;
                created = true;
            }
            else
            {
                Undo.RecordObject(existing, RegisterSceneUndoName);
                string displayName = string.IsNullOrWhiteSpace(existing.DisplayName)
                    ? sceneAsset.name
                    : existing.DisplayName;
                existing.AssignIdentity(
                    existing.RoomId,
                    displayName,
                    sceneAsset.name,
                    sceneGuid);
            }

            if (!ContainsDefinition(registry, existing))
            {
                if (!registry.Add(existing))
                    throw new InvalidOperationException("RoomRegistry rejected the RoomDefinition entry.");
                addedToRegistry = true;
            }

            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(registry);
            EnsureSceneEnabledInBuildSettings(scenePath);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssets();

            RuntimeRoomCatalogValidationReport catalogReport = SynchronizeRuntimeCatalog();
            if (!catalogReport.IsValid)
            {
                string detail = catalogReport.Issues.Count > 0
                    ? catalogReport.Issues[0].Message
                    : "RuntimeRoomCatalog generation failed.";
                throw new InvalidOperationException(detail);
            }

            Undo.CollapseUndoOperations(undoGroup);
            if (createdAsset)
            {
                CreatedRegistrationUndoEntries[AssetDatabase.GetAssetPath(existing)] =
                    new RegistrationUndoEntry(registryInsertIndex);
            }
            definition = existing;
            message = created
                ? $"Registered room '{sceneAsset.name}' with RoomId '{existing.RoomId}'. The scene is enabled in Build Settings and RuntimeRoomCatalog was regenerated."
                : $"Refreshed room '{sceneAsset.name}' while preserving RoomId '{existing.RoomId}'. RuntimeRoomCatalog was regenerated.";
            return true;
        }
        catch (Exception exception)
        {
            if (undoGroup >= 0)
            {
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                catch (Exception undoException)
                {
                    Debug.LogError("[RoomRegistration] Undo rollback failed: " + undoException.Message);
                }
            }

            if (addedToRegistry && existing != null && ContainsDefinition(registry, existing))
                registry.Remove(existing);

            if (registry.Rooms.Count > previousRegistryCount && registry.Rooms[registry.Rooms.Count - 1] == null)
                registry.Remove(null);

            if (existing != null && !createdAsset)
            {
                existing.AssignIdentity(
                    existing.RoomId,
                    previousDisplayName,
                    previousSceneName,
                    previousSceneGuid);
                EditorUtility.SetDirty(existing);
            }
            else if (createdAsset && existing != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(existing);
                if (!string.IsNullOrEmpty(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }

            EditorBuildSettings.scenes = previousBuildScenes;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            created = false;
            definition = null;
            message = "Room registration failed and was rolled back: " + exception.Message;
            return false;
        }
    }

    public static RuntimeRoomCatalogValidationReport SynchronizeRuntimeCatalog()
    {
        RuntimeRoomCatalogValidationReport report = RuntimeRoomCatalogGenerator.ValidateForBuild();
        return report.IsValid ? report : RuntimeRoomCatalogGenerator.GenerateOrUpdate();
    }

    public static void ReconcileUndoRedo()
    {
        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RegistryPath);
        if (registry == null || registry.Rooms == null || CreatedRegistrationUndoEntries.Count == 0)
            return;

        bool changed = false;
        foreach (KeyValuePair<string, RegistrationUndoEntry> entry in CreatedRegistrationUndoEntries)
        {
            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(entry.Key);
            if (definition == null)
            {
                changed |= registry.RemoveMissingAt(entry.Value.RegistryIndex);
                continue;
            }

            bool present = false;
            foreach (RoomDefinition registered in registry.Rooms)
            {
                if (registered == definition)
                {
                    present = true;
                    break;
                }
            }

            if (!present && !ContainsRoomId(registry, definition.RoomId))
            {
                changed |= registry.RemoveMissingAt(entry.Value.RegistryIndex);
                changed |= registry.Insert(entry.Value.RegistryIndex, definition);
            }
        }

        if (!changed)
            return;

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        RuntimeRoomCatalogValidationReport report = SynchronizeRuntimeCatalog();
        if (!report.IsValid)
        {
            string detail = report.Issues.Count > 0 ? report.Issues[0].Message : "Catalog validation failed.";
            Debug.LogError("[RoomRegistration] Could not synchronize RuntimeRoomCatalog after Undo/Redo: " + detail);
        }
    }

    private static void OnUndoRedoPerformed()
    {
        string groupName = Undo.GetCurrentGroupName();
        if (!string.IsNullOrEmpty(groupName) &&
            groupName.IndexOf(RegisterSceneUndoName, StringComparison.Ordinal) >= 0)
        {
            ReconcileUndoRedo();
        }
    }

    private static bool ContainsRoomId(RoomRegistry registry, string roomId)
    {
        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition != null && string.Equals(definition.RoomId, roomId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private sealed class RegistrationUndoEntry
    {
        public RegistrationUndoEntry(int registryIndex)
        {
            RegistryIndex = registryIndex;
        }

        public int RegistryIndex { get; }
    }

    private static List<RoomDefinition> FindDefinitionsForScene(string sceneGuid)
    {
        List<RoomDefinition> matches = new List<RoomDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:RoomDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RoomDefinition candidate = AssetDatabase.LoadAssetAtPath<RoomDefinition>(path);
            if (candidate != null && string.Equals(candidate.SceneGuid, sceneGuid, StringComparison.Ordinal))
                matches.Add(candidate);
        }

        return matches;
    }

    private static bool ValidateRoomIdUniqueness(
        RoomRegistry registry,
        RoomDefinition definition,
        out string message)
    {
        int matches = 0;
        foreach (RoomDefinition registered in registry.Rooms)
        {
            if (registered != null &&
                string.Equals(registered.RoomId, definition.RoomId, StringComparison.Ordinal))
            {
                matches++;
            }
        }

        if (string.IsNullOrWhiteSpace(definition.RoomId) || matches > 1)
        {
            message = $"RoomId '{definition.RoomId}' is missing or duplicated in RoomRegistry.";
            return false;
        }

        message = null;
        return true;
    }

    private static string CreateUniqueRoomId(RoomRegistry registry)
    {
        string roomId;
        bool exists;
        do
        {
            roomId = "room_" + Guid.NewGuid().ToString("N");
            exists = false;
            foreach (RoomDefinition registered in registry.Rooms)
            {
                if (registered != null && string.Equals(registered.RoomId, roomId, StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }
        } while (exists);

        return roomId;
    }

    private static string GetDefinitionAssetPath(string sceneName, string sceneGuid)
    {
        string cleanName = SanitizeFileName(sceneName);
        string suffix = sceneGuid.Length > 8 ? sceneGuid.Substring(0, 8) : sceneGuid;
        return $"{DefinitionsFolder}/{cleanName}_{suffix}.asset";
    }

    private static void EnsureSceneEnabledInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        List<EditorBuildSettingsScene> updated = new List<EditorBuildSettingsScene>(current.Length + 1);
        int matchIndex = -1;
        bool changed = false;

        for (int i = 0; i < current.Length; i++)
        {
            if (!string.Equals(current[i].path, scenePath, StringComparison.Ordinal))
            {
                updated.Add(current[i]);
                continue;
            }

            if (matchIndex >= 0)
                throw new InvalidOperationException($"Build Settings contains duplicate entries for '{scenePath}'.");

            matchIndex = updated.Count;
            if (current[i].enabled)
                updated.Add(current[i]);
            else
            {
                updated.Add(new EditorBuildSettingsScene(scenePath, true));
                changed = true;
            }
        }

        if (matchIndex < 0)
        {
            updated.Add(new EditorBuildSettingsScene(scenePath, true));
            changed = true;
        }

        if (changed)
            EditorBuildSettings.scenes = updated.ToArray();
    }

    private static bool TryGetBuildSettingsState(
        string scenePath,
        out bool enabled,
        out bool listed,
        out string message)
    {
        enabled = false;
        listed = false;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                continue;

            if (listed)
            {
                message = $"Build Settings contains duplicate entries for '{scenePath}'.";
                return false;
            }

            listed = true;
            enabled = scene.enabled;
        }

        message = null;
        return true;
    }

    private static bool ContainsDefinition(RoomRegistry registry, RoomDefinition definition)
    {
        foreach (RoomDefinition registered in registry.Rooms)
        {
            if (registered == definition)
                return true;
        }

        return false;
    }

    public static bool TryGetRegistry(out RoomRegistry registry, out string message)
    {
        registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RegistryPath);

        if (registry != null)
        {
            message = null;
            return true;
        }

        EnsureFolder(RegistryFolder);

        registry = ScriptableObject.CreateInstance<RoomRegistry>();
        AssetDatabase.CreateAsset(registry, RegistryPath);
        AssetDatabase.SaveAssets();

        message = null;
        return true;
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
}
