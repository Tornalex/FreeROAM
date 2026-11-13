using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public sealed class RoomAuthoringAutomationTests
{
    private const string UnregisteredScenePath =
        "Assets/Scenes/Rooms/Legacy/2.1 Spear_Advanced/R_Spear_Advanced_2.unity";
    private const string RegisteredScenePath =
        "Assets/Scenes/Rooms/Re-Entry Module/Re-EntryModule_WakeRoom.unity";

    [Test]
    public void DroppedUnregisteredSceneCreatesOneIdentityBuildEntryAndCatalogEntry()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(UnregisteredScenePath);
        Assert.That(sceneAsset, Is.Not.Null, "The test scene asset is missing.");

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        Assert.That(registry, Is.Not.Null);
        int initialRegistryCount = registry.Rooms.Count;

        string sceneGuid = AssetDatabase.AssetPathToGUID(UnregisteredScenePath);
        Assert.That(FindDefinitionsForGuid(sceneGuid), Is.Empty,
            "The fixture expects an unregistered scene with no existing RoomDefinition.");

        EditorBuildSettingsScene[] previousBuildScenes = CloneBuildScenes(EditorBuildSettings.scenes);
        RoomDefinition createdDefinition = null;
        string createdAssetPath = null;

        try
        {
            bool registered = RoomRegistrationService.RegisterOrRefreshScene(
                sceneAsset,
                out createdDefinition,
                out bool created,
                out string message);

            Assert.That(registered, Is.True, message);
            Assert.That(created, Is.True, "The first drop did not create a RoomDefinition.");
            Assert.That(createdDefinition, Is.Not.Null);
            Assert.That(createdDefinition.RoomId, Does.StartWith("room_"));
            Assert.That(createdDefinition.SceneGuid, Is.EqualTo(sceneGuid));
            Assert.That(createdDefinition.SceneName, Is.EqualTo(sceneAsset.name));
            Assert.That(CountDefinitionsForGuid(sceneGuid), Is.EqualTo(1));
            Assert.That(CountRegistryDefinitionsForGuid(registry, sceneGuid), Is.EqualTo(1));

            int registryCountAfterFirstDrop = registry.Rooms.Count;
            RoomDefinition repeatedDefinition = null;
            bool repeated = RoomRegistrationService.RegisterOrRefreshScene(
                sceneAsset,
                out repeatedDefinition,
                out bool repeatedCreated,
                out string repeatedMessage);

            Assert.That(repeated, Is.True, repeatedMessage);
            Assert.That(repeatedCreated, Is.False, "A repeated drop created a duplicate identity.");
            Assert.That(repeatedDefinition, Is.SameAs(createdDefinition));
            Assert.That(registry.Rooms.Count, Is.EqualTo(registryCountAfterFirstDrop));
            Assert.That(CountDefinitionsForGuid(sceneGuid), Is.EqualTo(1));

            Assert.That(IsSceneEnabled(UnregisteredScenePath), Is.True,
                "A graph-authored room scene must be available to runtime scene loading.");
            RuntimeRoomCatalogValidationReport catalogReport = RuntimeRoomCatalogGenerator.ValidateForBuild();
            Assert.That(catalogReport.IsValid, Is.True, FormatCatalogIssues(catalogReport));

            RuntimeRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<RuntimeRoomCatalog>(RuntimeRoomCatalog.AssetPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(FindCatalogEntry(catalog, createdDefinition.RoomId), Is.Not.Null);
            Assert.That(FindCatalogEntry(catalog, createdDefinition.RoomId).ScenePath,
                Is.EqualTo(UnregisteredScenePath));

            createdAssetPath = AssetDatabase.GetAssetPath(createdDefinition);

            // Registration Undo/Redo owns the identity and registry membership.
            // The catalog is derived data and is explicitly synchronized after
            // each state transition, just as the graph window does.
            Undo.PerformUndo();
            RoomRegistrationService.ReconcileUndoRedo();
            Assert.That(CountRegistryDefinitionsForGuid(registry, sceneGuid), Is.EqualTo(0));
            Assert.That(RoomRegistrationService.SynchronizeRuntimeCatalog().IsValid, Is.True);

            Undo.PerformRedo();
            RoomRegistrationService.ReconcileUndoRedo();
            RoomDefinition redone = AssetDatabase.LoadAssetAtPath<RoomDefinition>(createdAssetPath);
            Assert.That(redone, Is.Not.Null);
            Assert.That(CountRegistryDefinitionsForGuid(registry, sceneGuid), Is.EqualTo(1));
            Assert.That(RoomRegistrationService.SynchronizeRuntimeCatalog().IsValid, Is.True);
        }
        finally
        {
            RemoveDefinitionsForGuid(registry, sceneGuid);
            if (registry.Rooms.Count > initialRegistryCount && registry.Rooms[registry.Rooms.Count - 1] == null)
                registry.Remove(null);
            if (!string.IsNullOrEmpty(createdAssetPath) &&
                AssetDatabase.LoadAssetAtPath<RoomDefinition>(createdAssetPath) == null &&
                File.Exists(createdAssetPath))
            {
                AssetDatabase.DeleteAsset(createdAssetPath);
            }
            EditorBuildSettings.scenes = previousBuildScenes;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            RuntimeRoomCatalogValidationReport cleanupReport = RuntimeRoomCatalogGenerator.GenerateOrUpdate();
            if (!cleanupReport.IsValid)
                Debug.LogError("[RoomAuthoringAutomationTests] Catalog cleanup failed: " + FormatCatalogIssues(cleanupReport));
        }
    }

    [Test]
    public void InvalidSceneDropIsRejectedWithoutCreatingRoomAssets()
    {
        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        Assert.That(registry, Is.Not.Null);
        int registryCount = registry.Rooms.Count;
        int definitionCount = AssetDatabase.FindAssets("t:RoomDefinition").Length;

        bool registered = RoomRegistrationService.RegisterOrRefreshScene(
            null,
            out RoomDefinition definition,
            out bool created,
            out string message);

        Assert.That(registered, Is.False);
        Assert.That(definition, Is.Null);
        Assert.That(created, Is.False);
        Assert.That(message, Does.Contain("SceneAsset"));
        Assert.That(registry.Rooms.Count, Is.EqualTo(registryCount));
        Assert.That(AssetDatabase.FindAssets("t:RoomDefinition").Length, Is.EqualTo(definitionCount));
    }

    [Test]
    public void DoorDiscoveryReportsTransformPositionsAndSpatialFallbackState()
    {
        RoomDefinition definition = FindDefinitionForScene(RegisteredScenePath);
        Assert.That(definition, Is.Not.Null);

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(RegisteredScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(RegisteredScenePath, OpenSceneMode.Additive);

        try
        {
            Dictionary<string, Vector2> transformsByDoorId = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            Bounds roomBounds = default;
            bool hasRoomBounds = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(includeInactive: true))
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                        !collider.gameObject.CompareTag("CameraBounds"))
                    {
                        continue;
                    }

                    if (!hasRoomBounds)
                    {
                        roomBounds = collider.bounds;
                        hasRoomBounds = true;
                    }
                    else
                    {
                        roomBounds.Encapsulate(collider.bounds);
                    }
                }

                foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                {
                    if (!string.IsNullOrWhiteSpace(transition.DoorId))
                        transformsByDoorId[transition.DoorId] =
                            new Vector2(transition.transform.position.x, transition.transform.position.y);
                }
            }

            List<DiscoveredDoor> doors = RoomDiscoveryService.DiscoverDoors(definition, out string message);
            Assert.That(doors, Is.Not.Null, message);
            Assert.That(doors.Count, Is.EqualTo(transformsByDoorId.Count));
            Assert.That(hasRoomBounds, Is.True, "The fixture scene must provide a room-bounds collider.");

            foreach (DiscoveredDoor door in doors)
            {
                Assert.That(transformsByDoorId.TryGetValue(door.StableDoorId, out Vector2 transformPosition), Is.True);
                Assert.That(door.ScenePosition, Is.EqualTo(transformPosition),
                    "Door discovery did not capture the RoomTransition Transform position.");
                Assert.That(float.IsNaN(door.NormalizedScenePosition.x) || float.IsInfinity(door.NormalizedScenePosition.x), Is.False);
                Assert.That(float.IsNaN(door.NormalizedScenePosition.y) || float.IsInfinity(door.NormalizedScenePosition.y), Is.False);
                Assert.That(door.NormalizedScenePosition.x, Is.InRange(0f, 1f));
                Assert.That(door.NormalizedScenePosition.y, Is.InRange(0f, 1f));
                Assert.That(door.HasSpatialData, Is.True,
                    "The available CameraBounds collider should provide spatial socket data.");
                Vector2 expectedNormalized = new Vector2(
                    Mathf.InverseLerp(roomBounds.min.x, roomBounds.max.x, transformPosition.x),
                    Mathf.InverseLerp(roomBounds.min.y, roomBounds.max.y, transformPosition.y));
                Assert.That(Vector2.Distance(door.NormalizedScenePosition, expectedNormalized), Is.LessThan(0.0001f));
            }
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded &&
                SceneManager.GetActiveScene() != originalActiveScene)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    private static RoomDefinition FindDefinitionForScene(string scenePath)
    {
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        foreach (string guid in AssetDatabase.FindAssets("t:RoomDefinition"))
        {
            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && string.Equals(definition.SceneGuid, sceneGuid, StringComparison.Ordinal))
                return definition;
        }

        return null;
    }

    private static List<RoomDefinition> FindDefinitionsForGuid(string sceneGuid)
    {
        List<RoomDefinition> result = new List<RoomDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:RoomDefinition"))
        {
            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && string.Equals(definition.SceneGuid, sceneGuid, StringComparison.Ordinal))
                result.Add(definition);
        }

        return result;
    }

    private static int CountDefinitionsForGuid(string sceneGuid)
    {
        return FindDefinitionsForGuid(sceneGuid).Count;
    }

    private static int CountRegistryDefinitionsForGuid(RoomRegistry registry, string sceneGuid)
    {
        int count = 0;
        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition != null && string.Equals(definition.SceneGuid, sceneGuid, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static void RemoveDefinitionsForGuid(RoomRegistry registry, string sceneGuid)
    {
        List<RoomDefinition> definitions = FindDefinitionsForGuid(sceneGuid);
        foreach (RoomDefinition definition in definitions)
        {
            registry.Remove(definition);
            string assetPath = AssetDatabase.GetAssetPath(definition);
            if (!string.IsNullOrEmpty(assetPath))
                AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static EditorBuildSettingsScene[] CloneBuildScenes(EditorBuildSettingsScene[] scenes)
    {
        // Preserve each entry's serialized GUID as well as path/enabled. A
        // path-only constructor zeros the GUID for intentionally stale disabled
        // entries, which would itself mutate unrelated build configuration.
        return (EditorBuildSettingsScene[])scenes.Clone();
    }

    private static bool IsSceneEnabled(string path)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled && string.Equals(scene.path, path, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static RuntimeRoomCatalogEntry FindCatalogEntry(RuntimeRoomCatalog catalog, string roomId)
    {
        foreach (RuntimeRoomCatalogEntry entry in catalog.Entries)
            if (entry != null && string.Equals(entry.RoomId, roomId, StringComparison.Ordinal))
                return entry;
        return null;
    }

    private static string FormatCatalogIssues(RuntimeRoomCatalogValidationReport report)
    {
        List<string> messages = new List<string>();
        foreach (RuntimeRoomCatalogValidationIssue issue in report.Issues)
            messages.Add(issue.Code + ": " + issue.Message);
        return string.Join("\n", messages);
    }
}
