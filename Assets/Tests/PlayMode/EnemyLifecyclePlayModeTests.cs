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
    /// Phase 6.6.5 integration coverage for the real BaseEnemy lifecycle:
    /// room identity resolution in Start, death registration in Die, the legacy
    /// scene-name fallback, and the process-static tracker across room unloads.
    ///
    /// Enemies are created at runtime through a production concrete subclass
    /// (FlyingEnemy) and Unity runs Awake/OnEnable/Start naturally. No resolver
    /// or tracker call is used as a substitute for the lifecycle.
    /// </summary>
    public class EnemyLifecyclePlayModeTests : PlayModeTestBase
    {
        private const string RegisteredRoomPath =
            "Assets/Scenes/Rooms/Re-Entry Module/Re-EntryModule_WakeRoom.unity";
        private const string RegisteredRoomId =
            "room_0cd7bdc0537c4f4bb6937de388e8858d";
        private const string OtherRegisteredRoomId =
            "room_f4a2fbe688fb4506af039512474bd0f7";
        private const string UnregisteredRoomPath =
            "Assets/Scenes/Rooms/BaseRoom.unity";
        private const string MasterScenePath = "Assets/Scenes/Master.unity";

        private const string EnemyTypeName = "FlyingEnemy";
        private const string EnemyDataTypeName = "EnemyData";
        private const string TrackerTypeName = "SpearWander.Abilities.DeadEnemyTracker";
        private const string TrackingKeyTypeName = "EnemyRoomTrackingKey";
        private const string IdentityStatusTypeName = "RoomIdentityStatus";
        private const string ManagerTypeName = "GameSceneManager";
        private const string AbilityManagerTypeName =
            "SpearWander.Abilities.AbilityManager";
        private const string CheckpointManagerTypeName = "CheckpointManager";

        private const string RoomTrackingKeyField = "roomTrackingKey";
        private const string StableIdentityField = "stableIdentity";
        private const string EnemyDataField = "enemyData";
        private const string RoomNameField = "roomName";
        private const string DeadEnemiesField = "_deadEnemies";
        private const string CatalogFailureScenesField = "CatalogFailureScenes";

        private const float ManagerTimeoutSeconds = 30f;

        [UnityTest]
        public IEnumerator RegisteredRoomEnemyUsesStableRoomId()
        {
            List<UnityEngine.Object> temporaries = new List<UnityEngine.Object>();
            Scene roomScene = default;
            string identity = "phase665-a-registered-enemy";

            try
            {
                yield return Fixture.LoadSceneAdditive(
                    RegisteredRoomPath,
                    scene => roomScene = scene);

                Assert.That(roomScene.IsValid(), Is.True);
                Assert.That(roomScene.isLoaded, Is.True);
                Assert.That(roomScene.path, Is.EqualTo(RegisteredRoomPath));

                Component enemy = CreateEnemy(
                    roomScene,
                    identity,
                    maxHealth: 1,
                    temporaries);

                // Awake/OnEnable run on activation; Start runs before the next
                // frame update.
                yield return null;

                Assert.That(enemy, Is.Not.Null, "The enemy was unexpectedly destroyed.");
                Assert.That(
                    enemy.gameObject.scene.handle,
                    Is.EqualTo(roomScene.handle),
                    "The enemy was not created in the intended registered room.");
                Assert.That(
                    enemy.gameObject.scene.path,
                    Is.EqualTo(RegisteredRoomPath));

                string trackingKey =
                    ReflectionTestUtilities.GetInstanceField<string>(
                        enemy,
                        RoomTrackingKeyField);
                string legacyRoomName =
                    ReflectionTestUtilities.GetInstanceField<string>(
                        enemy,
                        RoomNameField);

                Assert.That(
                    trackingKey,
                    Is.EqualTo(RegisteredRoomId),
                    "Start did not resolve the registered room to its RoomId.");
                Assert.That(
                    trackingKey,
                    Is.Not.EqualTo(roomScene.name),
                    "The tracking key used the legacy scene name for a registered room.");
                Assert.That(
                    trackingKey,
                    Is.Not.EqualTo(legacyRoomName),
                    "The tracking key matched the legacy roomName field.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, identity),
                    Is.False,
                    "A live enemy was registered as dead.");

                // The key is cached once in Start and must not change while the
                // instance exists.
                yield return null;
                yield return null;
                string trackingKeyAfterFrames =
                    ReflectionTestUtilities.GetInstanceField<string>(
                        enemy,
                        RoomTrackingKeyField);

                Assert.That(
                    trackingKeyAfterFrames,
                    Is.EqualTo(trackingKey),
                    "The cached room tracking key changed across frames.");
                Assert.That(
                    trackingKeyAfterFrames,
                    Is.EqualTo(RegisteredRoomId));

                Debug.Log(
                    $"[EnemyLifecycle] Registered room key='{trackingKey}', " +
                    $"legacyRoomName='{legacyRoomName}', scene='{roomScene.name}'.");
            }
            finally
            {
                DestroyTemporaries(temporaries);
            }
        }

        [UnityTest]
        public IEnumerator DeathRegistersExactRoomAndIdentity()
        {
            List<UnityEngine.Object> temporaries = new List<UnityEngine.Object>();
            Scene roomScene = default;
            const string targetIdentity = "phase665-b-target-enemy";
            const string otherIdentity = "phase665-b-other-enemy";

            try
            {
                yield return Fixture.LoadSceneAdditive(
                    RegisteredRoomPath,
                    scene => roomScene = scene);

                Component targetEnemy = CreateEnemy(
                    roomScene,
                    targetIdentity,
                    maxHealth: 1,
                    temporaries);
                Component otherEnemy = CreateEnemy(
                    roomScene,
                    otherIdentity,
                    maxHealth: 1,
                    temporaries);

                yield return null;

                Assert.That(targetEnemy, Is.Not.Null);
                Assert.That(otherEnemy, Is.Not.Null);
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<string>(
                        targetEnemy,
                        RoomTrackingKeyField),
                    Is.EqualTo(RegisteredRoomId));

                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, targetIdentity),
                    Is.False,
                    "The target enemy was dead before taking damage.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, otherIdentity),
                    Is.False,
                    "The unrelated enemy was dead before the target died.");

                bool damaged = InvokeTakeDamage(targetEnemy, damage: 1);

                Assert.That(damaged, Is.True);
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<bool>(
                        targetEnemy,
                        "isDead"),
                    Is.True,
                    "The production death path did not set isDead.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, targetIdentity),
                    Is.True,
                    "The production death path did not register the enemy.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, otherIdentity),
                    Is.False,
                    "An unrelated identity was registered as dead.");
                Assert.That(
                    IsTrackedAsDead(OtherRegisteredRoomId, targetIdentity),
                    Is.False,
                    "The killed enemy was registered under an unrelated room.");
                Assert.That(
                    IsTrackedAsDead("phase665-b-unrelated-room", targetIdentity),
                    Is.False,
                    "The killed enemy was registered under an unknown room.");

                // Destroy is deferred to the end of the frame.
                yield return null;

                Assert.That(
                    IsAlive(targetEnemy),
                    Is.False,
                    "The dead enemy GameObject was not destroyed.");
                Assert.That(
                    IsAlive(otherEnemy),
                    Is.True,
                    "An unrelated enemy was destroyed with the target.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, targetIdentity),
                    Is.True,
                    "Death registration was lost after the enemy was destroyed.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, otherIdentity),
                    Is.False);

                Debug.Log(
                    $"[EnemyLifecycle] Death registered room='{RegisteredRoomId}', " +
                    $"identity='{targetIdentity}'; unrelated identity/room stayed untracked.");
            }
            finally
            {
                DestroyTemporaries(temporaries);
            }
        }

        [UnityTest]
        public IEnumerator UnregisteredSceneUsesLegacySceneNameFallback()
        {
            List<UnityEngine.Object> temporaries = new List<UnityEngine.Object>();
            Scene roomScene = default;
            const string identity = "phase665-c-legacy-enemy";
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            Type managerType =
                ReflectionTestUtilities.ResolveRuntimeType(ManagerTypeName);
            object preExistingManager = GetManager(managerType);
            object preExistingAbilityManager =
                GetStaticInstance(AbilityManagerTypeName);
            object preExistingCheckpointManager =
                GetStaticInstance(CheckpointManagerTypeName);

            try
            {
                // The Master scene supplies the real GameSceneManager needed for
                // the scene-name fallback. Failing messages are suppressed only
                // for the known batchmode Yarn/global-light noise; assertions
                // below still check the enemy-specific behaviour.
                LogAssert.ignoreFailingMessages = true;

                yield return Fixture.LoadSceneAdditive(
                    UnregisteredRoomPath,
                    scene => roomScene = scene);
                Assert.That(roomScene.path, Is.EqualTo(UnregisteredRoomPath));

                yield return LoadMasterAndWaitForManager(roomScene);

                Component enemy = CreateEnemy(
                    roomScene,
                    identity,
                    maxHealth: 1,
                    temporaries);
                yield return null;

                Assert.That(enemy, Is.Not.Null);
                Assert.That(
                    enemy.gameObject.scene.handle,
                    Is.EqualTo(roomScene.handle));

                string trackingKey =
                    ReflectionTestUtilities.GetInstanceField<string>(
                        enemy,
                        RoomTrackingKeyField);
                string legacyRoomName =
                    ReflectionTestUtilities.GetInstanceField<string>(
                        enemy,
                        RoomNameField);

                Assert.That(
                    trackingKey,
                    Is.EqualTo(roomScene.name),
                    "An unregistered scene did not fall back to its scene name.");
                Assert.That(
                    legacyRoomName,
                    Is.EqualTo(roomScene.name),
                    "The legacy roomName field was not the scene name.");
                Assert.That(
                    trackingKey,
                    Is.Not.EqualTo(RegisteredRoomId),
                    "An unregistered scene was assigned a registered RoomId.");
                Assert.That(
                    trackingKey,
                    Is.Not.EqualTo(OtherRegisteredRoomId),
                    "An unregistered scene was assigned another registered RoomId.");

                bool damaged = InvokeTakeDamage(enemy, damage: 1);

                Assert.That(damaged, Is.True);
                Assert.That(
                    IsTrackedAsDead(roomScene.name, identity),
                    Is.True,
                    "Death tracking did not use the legacy scene-name key.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, identity),
                    Is.False,
                    "Death tracking also registered the legacy enemy under a RoomId.");

                Debug.Log(
                    $"[EnemyLifecycle] Legacy fallback key='{trackingKey}', " +
                    $"identity='{identity}'.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;

                // Master-created persistent managers are moved to
                // DontDestroyOnLoad, so unloading the scene does not remove
                // them. Destroy only instances this test created.
                if (!IsAlive(preExistingManager))
                {
                    DestroyCreatedPersistentManager(
                        ManagerTypeName,
                        preExistingManager);
                }

                DestroyCreatedPersistentManager(
                    AbilityManagerTypeName,
                    preExistingAbilityManager);
                DestroyCreatedPersistentManager(
                    CheckpointManagerTypeName,
                    preExistingCheckpointManager);

                DestroyTemporaries(temporaries);
            }
        }

        [UnityTest]
        public IEnumerator DeathSurvivesSceneUnloadAndSuppressesRecreatedEnemy()
        {
            List<UnityEngine.Object> temporaries = new List<UnityEngine.Object>();
            Scene roomScene = default;
            const string identity = "phase665-d-persistent-enemy";
            const string survivorIdentity = "phase665-d-survivor-enemy";

            try
            {
                yield return Fixture.LoadSceneAdditive(
                    RegisteredRoomPath,
                    scene => roomScene = scene);

                Component firstEnemy = CreateEnemy(
                    roomScene,
                    identity,
                    maxHealth: 1,
                    temporaries);
                yield return null;

                Assert.That(firstEnemy, Is.Not.Null);
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<string>(
                        firstEnemy,
                        RoomTrackingKeyField),
                    Is.EqualTo(RegisteredRoomId));

                Assert.That(InvokeTakeDamage(firstEnemy, damage: 1), Is.True);
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, identity),
                    Is.True);

                yield return null;
                Assert.That(
                    IsAlive(firstEnemy),
                    Is.False,
                    "The dead enemy GameObject was not destroyed.");

                // Unload the source room and load the same logical room again.
                yield return Fixture.UnloadScene(roomScene);
                Assert.That(
                    Fixture.IsSceneLoaded(RegisteredRoomPath),
                    Is.False,
                    "The room scene remained loaded after unload.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, identity),
                    Is.True,
                    "Unloading the room incorrectly cleared the death registration.");

                Scene reloadedScene = default;
                yield return Fixture.LoadSceneAdditive(
                    RegisteredRoomPath,
                    scene => reloadedScene = scene);

                Assert.That(reloadedScene.path, Is.EqualTo(RegisteredRoomPath));
                Assert.That(reloadedScene.isLoaded, Is.True);

                Component recreatedEnemy = CreateEnemy(
                    reloadedScene,
                    identity,
                    maxHealth: 1,
                    temporaries);
                Component survivorEnemy = CreateEnemy(
                    reloadedScene,
                    survivorIdentity,
                    maxHealth: 1,
                    temporaries);

                // Start runs on the next frame; the recreated instance with the
                // same identity is suppressed by the tracker.
                yield return null;

                Assert.That(
                    IsAlive(recreatedEnemy),
                    Is.False,
                    "The recreated enemy with a registered death was not suppressed.");
                Assert.That(
                    IsAlive(survivorEnemy),
                    Is.True,
                    "An enemy with a different stable identity was incorrectly suppressed.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, identity),
                    Is.True,
                    "The death registration was lost after the room reload.");
                Assert.That(
                    IsTrackedAsDead(RegisteredRoomId, survivorIdentity),
                    Is.False);

                Debug.Log(
                    $"[EnemyLifecycle] Room unloaded/reloaded; death for " +
                    $"'{identity}' persisted and suppressed the recreated instance.");
            }
            finally
            {
                DestroyTemporaries(temporaries);
            }
        }

        [UnityTest]
        public IEnumerator StaticStateIsolationAndWarningDeduplication()
        {
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            PlayModeLogCapture capture = null;
            Type trackerType = null;
            Type trackingKeyType = null;
            Type statusType = null;

            try
            {
                LogAssert.ignoreFailingMessages = true;
                capture = Fixture.StartLogCapture();

                trackerType =
                    ReflectionTestUtilities.ResolveRuntimeType(TrackerTypeName);
                trackingKeyType =
                    ReflectionTestUtilities.ResolveRuntimeType(TrackingKeyTypeName);
                statusType =
                    ReflectionTestUtilities.ResolveRuntimeType(IdentityStatusTypeName);

                const string seededRoom = "phase665-e-static-room";
                const string seededIdentity = "phase665-e-static-enemy";

                // Two seed/reset cycles show that a Domain-Reload-disabled
                // process does not keep stale tracker state after the fixture
                // reset, and that reset remains repeatable.
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    ReflectionTestUtilities.InvokeStatic(
                        trackerType,
                        "MarkDead",
                        seededRoom,
                        seededIdentity);
                    Assert.That(
                        IsTrackedAsDead(seededRoom, seededIdentity),
                        Is.True,
                        $"Cycle {cycle}: MarkDead did not register the enemy.");

                    PlayModeTestFixture.ResetStaticState();

                    Assert.That(
                        IsTrackedAsDead(seededRoom, seededIdentity),
                        Is.False,
                        $"Cycle {cycle}: tracker state survived ResetStaticState.");
                    AssertCollectionCount(
                        ReflectionTestUtilities.GetStaticField(
                            trackerType,
                            DeadEnemiesField),
                        0,
                        $"Cycle {cycle}: the tracker dictionary was not empty after reset.");
                }

                const string dedupScenePath =
                    "Assets/TestRooms/Phase665WarningDedup.unity";
                const string dedupSceneName = "Phase665WarningDedupScene";
                object catalogMissing =
                    Enum.Parse(statusType, "CatalogMissing");

                capture.Clear();

                string firstKey = ResolveTrackingKey(
                    trackingKeyType,
                    dedupScenePath,
                    dedupSceneName,
                    catalogMissing);
                string secondKey = ResolveTrackingKey(
                    trackingKeyType,
                    dedupScenePath,
                    dedupSceneName,
                    catalogMissing);

                Assert.That(firstKey, Is.EqualTo(dedupSceneName));
                Assert.That(secondKey, Is.EqualTo(dedupSceneName));
                Assert.That(
                    CountTrackingWarnings(capture, dedupScenePath, dedupSceneName),
                    Is.EqualTo(1),
                    "Catalog failure diagnostics were not deduplicated per scene.");

                AssertCollectionCount(
                    ReflectionTestUtilities.GetStaticField(
                        trackingKeyType,
                        CatalogFailureScenesField),
                    1,
                    "Expected exactly one deduplicated scene entry before reset.");

                PlayModeTestFixture.ResetStaticState();

                AssertCollectionCount(
                    ReflectionTestUtilities.GetStaticField(
                        trackingKeyType,
                        CatalogFailureScenesField),
                    0,
                    "ResetStaticState did not clear the warning deduplication set.");

                capture.Clear();
                string thirdKey = ResolveTrackingKey(
                    trackingKeyType,
                    dedupScenePath,
                    dedupSceneName,
                    catalogMissing);

                Assert.That(thirdKey, Is.EqualTo(dedupSceneName));
                Assert.That(
                    CountTrackingWarnings(capture, dedupScenePath, dedupSceneName),
                    Is.EqualTo(1),
                    "The warning was not emitted again after the reset.");

                yield return null;

                Debug.Log(
                    "[EnemyLifecycle] Static tracker and warning deduplication " +
                    "state reset cleanly across repeated cycles.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;

                if (capture != null)
                    capture.Dispose();
            }
        }

        private Component CreateEnemy(
            Scene scene,
            string stableIdentity,
            int maxHealth,
            List<UnityEngine.Object> temporaries)
        {
            Type enemyType =
                ReflectionTestUtilities.ResolveRuntimeType(EnemyTypeName);
            Type enemyDataType =
                ReflectionTestUtilities.ResolveRuntimeType(EnemyDataTypeName);

            // Create inactive so Awake observes the configured EnemyData and
            // stable identity, then let Unity run the real lifecycle on
            // activation in the owning room scene.
            GameObject enemyObject = Fixture.CreateGameObject(
                "Phase665Enemy_" + stableIdentity);
            enemyObject.SetActive(false);

            Component enemy = enemyObject.AddComponent(enemyType);

            ScriptableObject enemyData =
                ScriptableObject.CreateInstance(enemyDataType);
            temporaries.Add(enemyData);
            ReflectionTestUtilities.SetInstanceField(
                enemyData,
                "maxHealth",
                maxHealth);
            ReflectionTestUtilities.SetInstanceField(
                enemyData,
                "speed",
                0f);

            ReflectionTestUtilities.SetInstanceField(
                enemy,
                EnemyDataField,
                enemyData);
            ReflectionTestUtilities.SetInstanceField(
                enemy,
                StableIdentityField,
                stableIdentity);

            if (enemyObject.scene.handle != scene.handle)
                SceneManager.MoveGameObjectToScene(enemyObject, scene);

            Assert.That(
                enemyObject.scene.handle,
                Is.EqualTo(scene.handle),
                "The enemy could not be moved into the owning room scene.");

            enemyObject.SetActive(true);
            return enemy;
        }

        private static bool InvokeTakeDamage(Component enemy, int damage)
        {
            object result = ReflectionTestUtilities.InvokeInstance(
                enemy,
                "TakeDamage",
                damage,
                Vector2.zero,
                Vector2.zero);

            return result is bool boolResult && boolResult;
        }

        private static bool IsTrackedAsDead(string roomKey, string identity)
        {
            Type trackerType =
                ReflectionTestUtilities.ResolveRuntimeType(TrackerTypeName);

            return ReflectionTestUtilities.InvokeStatic<bool>(
                trackerType,
                "IsDead",
                roomKey,
                identity);
        }

        private static string ResolveTrackingKey(
            Type trackingKeyType,
            string scenePath,
            string sceneName,
            object catalogMissingStatus)
        {
            return ReflectionTestUtilities.InvokeStatic<string>(
                trackingKeyType,
                "Resolve",
                scenePath,
                sceneName,
                "CommittedRoom",
                true,
                catalogMissingStatus,
                null,
                "RuntimeRoomCatalog is missing for the Phase 6.6.5 test.");
        }

        private static int CountTrackingWarnings(
            PlayModeLogCapture capture,
            string scenePath,
            string sceneName)
        {
            int count = 0;
            IReadOnlyList<PlayModeLogCapture.Entry> entries = capture.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                PlayModeLogCapture.Entry entry = entries[i];
                if (entry.Type != LogType.Warning || entry.Message == null)
                    continue;

                if (entry.Message.IndexOf(
                        "[EnemyRoomTrackingKey]",
                        StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (entry.Message.IndexOf(scenePath, StringComparison.Ordinal) < 0 &&
                    entry.Message.IndexOf(sceneName, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void AssertCollectionCount(
            object collection,
            int expected,
            string message)
        {
            Assert.That(
                collection,
                Is.Not.Null,
                "The inspected static field was null.");
            Assert.That(
                ReflectionTestUtilities.GetProperty<int>(collection, "Count"),
                Is.EqualTo(expected),
                message);
        }

        private static void DestroyTemporaries(List<UnityEngine.Object> temporaries)
        {
            for (int i = temporaries.Count - 1; i >= 0; i--)
            {
                if (temporaries[i] != null)
                    UnityEngine.Object.Destroy(temporaries[i]);
            }

            temporaries.Clear();
        }

        private IEnumerator LoadMasterAndWaitForManager(Scene roomScene)
        {
            Type managerType =
                ReflectionTestUtilities.ResolveRuntimeType(ManagerTypeName);

            // A GameSceneManager is moved to DontDestroyOnLoad and can outlive
            // the Master scene that created it. Reuse a live instance rather
            // than trying to replace it.
            UnityEngine.Object existingManager =
                GetManager(managerType) as UnityEngine.Object;
            bool managerExisted = IsAlive(existingManager);
            bool masterAlreadyLoaded = Fixture.IsSceneLoaded(MasterScenePath);

            if (!managerExisted)
            {
                if (!masterAlreadyLoaded)
                {
                    yield return Fixture.LoadSceneAdditive(
                        MasterScenePath,
                        scene => DisableYarnLineAdvancerInput(scene));
                }
                else
                {
                    DisableYarnLineAdvancerInput(
                        Fixture.GetLoadedScene(MasterScenePath));
                }
            }
            else if (masterAlreadyLoaded)
            {
                DisableYarnLineAdvancerInput(
                    Fixture.GetLoadedScene(MasterScenePath));
            }

            object manager = null;
            float deadline = Time.realtimeSinceStartup + ManagerTimeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                manager = GetManager(managerType);
                if (IsAlive(manager))
                {
                    if (managerExisted)
                        break;

                    string currentRoom =
                        ReflectionTestUtilities.GetProperty<string>(
                            manager,
                            "CurrentRoom");

                    if (string.Equals(
                            currentRoom,
                            roomScene.name,
                            StringComparison.Ordinal))
                    {
                        break;
                    }
                }

                yield return null;
            }

            Assert.That(
                IsAlive(manager),
                Is.True,
                "The real Master scene did not create a live GameSceneManager.");

            // The manager must not be left in a transition while the test
            // creates and kills an enemy.
            ReflectionTestUtilities.SetInstanceField(
                manager,
                "_isTransitioning",
                false);
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

        private static bool IsAlive(object unityObject)
        {
            if (unityObject == null)
                return false;

            UnityEngine.Object candidate = unityObject as UnityEngine.Object;
            return candidate != null;
        }

        private static void DisableYarnLineAdvancerInput(Scene masterScene)
        {
            // Yarn declares KeyCodes as a namespace, so its full name uses a
            // dot rather than a nested-type plus.
            Type inputType =
                FindLoadedType("Yarn.Unity.LineAdvancerInput.KeyCodes");
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
                Type type = assemblies[i].GetType(
                    fullName,
                    throwOnError: false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
