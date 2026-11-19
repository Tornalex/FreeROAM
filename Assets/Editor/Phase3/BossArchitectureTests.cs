using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SpearWander.Boss;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase3
{
    /// <summary>
    /// Phase 3 boss data architecture tests.
    /// These tests are read-only with respect to project assets: they load and
    /// inspect assets, prefabs and the arena scene text, but they never save.
    /// </summary>
    [TestFixture]
    public class BossArchitectureTests
    {
        private const string BossDataPath = "Assets/Data/Boss Data.asset";
        private const string BossProfilePath =
            "Assets/Data/Boss Data/FreeRoam Boss AI Profile.asset";
        private const string TongueDataPath =
            "Assets/Data/Boss Data/Tongue Attack Data.asset";
        private const string SporeDataPath =
            "Assets/Data/Boss Data/Spore Attack Data.asset";
        private const string JumpDataPath =
            "Assets/Data/Boss Data/Frog/Attacks/Jump Attack Data.asset";
        private const string DashJumpDataPath =
            "Assets/Data/Boss Data/Frog/Attacks/Dash Jump Attack Data.asset";
        private const string ArenaProfilePath =
            "Assets/Data/Boss Data/Arena Boss AI Profile.asset";
        private const string ArenaTongueDataPath =
            "Assets/Data/Boss Data/Arena Tongue Attack Data.asset";
        private const string ArenaSporeDataPath =
            "Assets/Data/Boss Data/Arena Spore Attack Data.asset";
        private const string ArenaJumpDataPath =
            "Assets/Data/Boss Data/Arena Jump Attack Data.asset";
        private const string ArenaDashJumpDataPath =
            "Assets/Data/Boss Data/Arena Dash Jump Attack Data.asset";

        private const string BossPrefabPath = "Assets/Prefabs/Enemies/FreeRoam Boss.prefab";
        private const string SporePrefabPath = "Assets/Prefabs/Enemies/Spore Cloud.prefab";
        private const string ArenaScenePath =
            "Assets/Scenes/Rooms/Yellow Forest/YellowForest_Arena.unity";

        private const string BossDataGuid = "8e9863f4f1429694385dab2b4dfd3a63";
        private const string BossPrefabGuid = "709b216ecb734b98be3f635932d5e489";
        private const string SporePrefabGuid = "306cc5e626ff4a7cb9ea147f176257a7";
        private const string AnimatorControllerGuid = "25c7aa268c6b35d4ab6eef0b2ccb85cb";

        private const string BossProfileGuid = "eb70b1a0b14c4c34a85dede04c4225c7";
        private const string TongueDataGuid = "ab681934668c4a15961c8d3681baa569";
        private const string SporeDataGuid = "de5d27e11ad649348611d1fd0f672325";
        private const string ArenaProfileGuid = "d8a9c5ee75184325bfe731dc5fb001ea";
        private const string ArenaTongueGuid = "c6c42174e0e947f083593307a69d8234";
        private const string ArenaSporeGuid = "ac5148b6bf6a4f36a3c98ac8cc230091";

        [Test]
        public void BossDataKeepsFundamentalStatistics()
        {
            BossData data = Load<BossData>(BossDataPath);

            Assert.That(data.maxHealth, Is.EqualTo(10), "maxHealth");
            Assert.That(data.knockbackForce, Is.EqualTo(new Vector2(15f, 10f)), "knockbackForce");
            Assert.That(data.knockbackDuration, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(
                data.weakPointDamageMultiplier,
                Is.EqualTo(5f).Within(0.0001f),
                "weakPointDamageMultiplier");
            Assert.That(data.hitVfxType, Is.EqualTo(VFXType.HitSpear), "hitVfxType");
            Assert.That(data.hitSfxType, Is.EqualTo(SFXType.EnemyPierced), "hitSfxType");
            Assert.That(data.deathVfxType, Is.EqualTo(VFXType.EnemyDeath), "deathVfxType");
            Assert.That(data.deathSfxType, Is.EqualTo(SFXType.EnemyPierced), "deathSfxType");
        }

        [Test]
        public void FreeRoamBossPrefabUsesAuthoringAssets()
        {
            FreeRoamBoss boss = PrefabComponent<FreeRoamBoss>(BossPrefabPath);

            AssertFieldReference(boss, "bossData", Load<BossData>(BossDataPath));
            AssertFieldReference(boss, "aiProfile", Load<BossAIProfile>(BossProfilePath));
            AssertFieldReference(boss, "tongueAttackData", Load<TongueAttackData>(TongueDataPath));
            AssertFieldReference(boss, "sporeAttackData", Load<SporeAttackData>(SporeDataPath));

            AssertFieldReference(boss, "sporePrefab", Load<GameObject>(SporePrefabPath));
            Assert.That(
                ObjectReference(boss, "tongueOrigin"),
                Is.Not.Null,
                "tongueOrigin must stay assigned.");
            Assert.That(
                ObjectReference(boss, "groundCheckPoint"),
                Is.Not.Null,
                "groundCheckPoint must stay assigned.");

            // The tuning that moved into the assets must no longer be
            // serialized twice on the prefab component.
            AssertFieldMissing(boss, "detectionRange");
            AssertFieldMissing(boss, "groundLayers");
            AssertFieldMissing(boss, "playerLayers");
            AssertFieldMissing(boss, "jumpUpVelocity");
            AssertFieldMissing(boss, "jumpDistance");
            AssertFieldMissing(boss, "decisionInterval");
            AssertFieldMissing(boss, "jumpTowardWeight");
            AssertFieldMissing(boss, "tongueWindupTime");
            AssertFieldMissing(boss, "tongueDamage");
            AssertFieldMissing(boss, "sporeLifetime");
            AssertFieldMissing(boss, "maxActiveSpores");
            AssertFieldMissing(boss, "dashJumpCooldown");
            AssertFieldMissing(boss, "currentHealth");
            AssertFieldMissing(boss, "isDead");
        }

        [Test]
        public void BossAIProfileMatchesPreviousPrefabTuning()
        {
            BossAIProfile profile = Load<BossAIProfile>(BossProfilePath);
            JumpAttackData jump = Load<JumpAttackData>(JumpDataPath);
            DashJumpAttackData dashJump =
                Load<DashJumpAttackData>(DashJumpDataPath);

            Assert.That(profile.DetectionRange, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(profile.GroundLayers.value, Is.EqualTo(1));
            Assert.That(profile.PlayerLayers.value, Is.EqualTo(8));
            Assert.That(profile.GroundCheckRadius, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(profile.TongueObstructionLayers.value, Is.EqualTo(1));
            Assert.That(profile.SporeObstructionLayers.value, Is.EqualTo(1));
            Assert.That(dashJump.DashJumpObstructionLayers.value, Is.EqualTo(1));

            Assert.That(jump.JumpUpVelocity, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(jump.JumpDistance, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(jump.JumpMaxHorizontalSpeed, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(jump.LandingRecovery, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(profile.PreferredDistance, Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(profile.RetreatDistance, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(profile.DecisionInterval, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(profile.DecisionIntervalJitter, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(jump.MaxAirborneDuration, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(jump.RecoveryCooldown, Is.EqualTo(1f).Within(0.0001f));

            Assert.That(profile.JumpTowardWeight, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(profile.JumpAwayWeight, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(profile.DashJumpWeight, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(profile.TongueWeight, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(profile.SporeWeight, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(profile.RepeatWeightMultiplier, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(
                profile.AttackAfterAttackWeightMultiplier,
                Is.EqualTo(0.4f).Within(0.0001f));

            Assert.That(dashJump.DashJumpMinDistance, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(dashJump.DashJumpHorizontalSpeed, Is.EqualTo(18f).Within(0.0001f));
            Assert.That(dashJump.DashJumpUpVelocity, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(dashJump.DashJumpLandingOffset, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(dashJump.DashJumpPreparationTime, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(dashJump.DashJumpRecoveryTime, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(dashJump.DashJumpCooldown, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(
                dashJump.DashJumpMaxAirborneDuration,
                Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void TongueAttackDataMatchesPreviousPrefabTuning()
        {
            TongueAttackData tongue = Load<TongueAttackData>(TongueDataPath);

            Assert.That(tongue.Damage, Is.EqualTo(2), "damage");
            Assert.That(tongue.Cooldown, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(tongue.WindupTime, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(tongue.ExtendTime, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(tongue.RetractTime, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(tongue.RecoveryTime, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(tongue.Range, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(tongue.Width, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(tongue.SortingOrder, Is.EqualTo(10));
            Assert.That(tongue.TongueColor.r, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(tongue.TongueColor.g, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(tongue.TongueColor.b, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(tongue.TongueColor.a, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SporeAttackDataMatchesPreviousPrefabTuning()
        {
            SporeAttackData spores = Load<SporeAttackData>(SporeDataPath);

            Assert.That(spores.Damage, Is.EqualTo(1), "damage");
            Assert.That(spores.Cooldown, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(spores.WindupTime, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(spores.RecoveryTime, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(spores.Lifetime, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(spores.Radius, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(spores.MaxActiveSpores, Is.EqualTo(3));
            Assert.That(spores.Drift, Is.EqualTo(new Vector2(0.35f, 0f)));
            Assert.That(spores.Range, Is.EqualTo(9f).Within(0.0001f));
        }

        [Test]
        public void ArenaAssetsPreservePreviousOverrideValues()
        {
            // These are the values the arena scene used to override on its boss
            // instance before the migration. They must survive verbatim.
            BossAIProfile profile = Load<BossAIProfile>(ArenaProfilePath);
            JumpAttackData jump = Load<JumpAttackData>(ArenaJumpDataPath);
            DashJumpAttackData dashJump =
                Load<DashJumpAttackData>(ArenaDashJumpDataPath);
            Assert.That(profile.DetectionRange, Is.EqualTo(25f).Within(0.0001f));
            Assert.That(jump.JumpUpVelocity, Is.EqualTo(25f).Within(0.0001f));
            Assert.That(jump.JumpDistance, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(jump.JumpMaxHorizontalSpeed, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(jump.RecoveryCooldown, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(profile.JumpTowardWeight, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(profile.JumpAwayWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(profile.DashJumpWeight, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(dashJump.DashJumpMinDistance, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(dashJump.DashJumpHorizontalSpeed, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(dashJump.DashJumpUpVelocity, Is.EqualTo(16f).Within(0.0001f));
            Assert.That(dashJump.DashJumpLandingOffset, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(dashJump.DashJumpCooldown, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                dashJump.DashJumpMaxAirborneDuration,
                Is.EqualTo(4f).Within(0.0001f));

            // Values the arena did not override keep the prefab value.
            Assert.That(jump.LandingRecovery, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(profile.PreferredDistance, Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(profile.DecisionInterval, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(dashJump.DashJumpPreparationTime, Is.EqualTo(0.45f).Within(0.0001f));

            TongueAttackData tongue = Load<TongueAttackData>(ArenaTongueDataPath);
            Assert.That(tongue.Damage, Is.EqualTo(1));
            Assert.That(tongue.Cooldown, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(tongue.Range, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(tongue.Width, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(tongue.WindupTime, Is.EqualTo(0.45f).Within(0.0001f));

            SporeAttackData spores = Load<SporeAttackData>(ArenaSporeDataPath);
            Assert.That(spores.Damage, Is.EqualTo(1));
            Assert.That(spores.Cooldown, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(spores.Lifetime, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(spores.Radius, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(spores.MaxActiveSpores, Is.EqualTo(1));
            Assert.That(spores.Drift, Is.EqualTo(Vector2.zero));
            Assert.That(spores.Range, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void ArenaScenePointsBossAtArenaAssets()
        {
            string scenePath = Path.Combine(
                Application.dataPath,
                ArenaScenePath.Substring("Assets/".Length));
            Assert.IsTrue(File.Exists(scenePath), "Arena scene not found: " + scenePath);

            string[] lines = File.ReadAllText(scenePath).Split('\n');

            AssertSceneOverride(lines, "aiProfile", ArenaProfileGuid);
            AssertSceneOverride(lines, "tongueAttackData", ArenaTongueGuid);
            AssertSceneOverride(lines, "sporeAttackData", ArenaSporeGuid);

            // Migrated fields must not stay as orphan prefab overrides.
            string[] migrated =
            {
                "detectionRange", "jumpUpVelocity", "jumpDistance",
                "jumpMaxHorizontalSpeed", "recoveryCooldown", "jumpTowardWeight",
                "jumpAwayWeight", "dashJumpWeight", "dashJumpMinDistance",
                "dashJumpHorizontalSpeed", "dashJumpUpVelocity",
                "dashJumpLandingOffset", "dashJumpCooldown",
                "dashJumpMaxAirborneDuration", "tongueRange", "tongueWidth",
                "tongueDamage", "tongueCooldown", "sporeRange", "sporeRadius",
                "sporeWeight", "sporeCooldown", "sporeLifetime", "maxActiveSpores",
                "sporeDrift.x", "tongueWeight", "repeatWeightMultiplier",
                "attackAfterAttackWeightMultiplier"
            };

            foreach (string field in migrated)
            {
                Assert.IsFalse(
                    ContainsSceneProperty(lines, field),
                    "Orphan prefab override still targets migrated field '" + field + "'.");
            }
        }

        [Test]
        public void BossAssetsExplicitlySerializeMigratedParameters()
        {
            AssertAssetContains(
                BossProfilePath,
                "detectionRange: 14",
                "groundCheckRadius: 0.22",
                "jumpTowardWeight: 3",
                "repeatWeightMultiplier: 0.35");

            AssertAssetContains(
                JumpDataPath,
                "jumpUpVelocity: 20",
                "jumpDistance: 4",
                "recoveryCooldown: 1");

            AssertAssetContains(
                DashJumpDataPath,
                "dashJumpCooldown: 5",
                "dashJumpMaxAirborneDuration: 2");

            AssertAssetContains(
                TongueDataPath,
                "damage: 2",
                "cooldown: 2.5",
                "windupTime: 0.45",
                "range: 6",
                "width: 0.4",
                "sortingOrder: 10");

            AssertAssetContains(
                SporeDataPath,
                "damage: 1",
                "cooldown: 4",
                "lifetime: 6",
                "radius: 0.9",
                "maxActiveSpores: 3",
                "range: 9");

            AssertAssetContains(
                ArenaProfilePath,
                "detectionRange: 25");

            AssertAssetContains(
                ArenaJumpDataPath,
                "jumpDistance: 60");

            AssertAssetContains(
                ArenaDashJumpDataPath,
                "dashJumpHorizontalSpeed: 100");

            AssertAssetContains(
                ArenaTongueDataPath,
                "cooldown: 1",
                "range: 12",
                "width: 0.3");

            AssertAssetContains(
                ArenaSporeDataPath,
                "cooldown: 1",
                "lifetime: 3",
                "radius: 10",
                "maxActiveSpores: 1");
        }

        [Test]
        public void RuntimeStateIsNotStoredInAuthoringAssets()
        {
            Type[] authoringTypes =
            {
                typeof(BossData),
                typeof(BossAIProfile),
                typeof(BossAttackData),
                typeof(JumpAttackData),
                typeof(DashJumpAttackData)
            };

            string[] runtimeFields =
            {
                "currentHealth", "isDead", "isAttacking", "isStunned", "stunTimer",
                "state", "stateTimer", "decisionTimer", "tongueCooldownTimer",
                "sporeCooldownTimer", "dashJumpCooldownTimer", "playerTransform",
                "activeSpores"
            };

            foreach (Type type in authoringTypes)
            {
                foreach (string fieldName in runtimeFields)
                {
                    FieldInfo field = type.GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);
                    Assert.IsNull(
                        field,
                        type.Name + " must not store runtime state field '" + fieldName + "'.");
                }
            }
        }

        [Test]
        public void BossPrefabsHaveNoMissingScripts()
        {
            foreach (string path in new[] { BossPrefabPath, SporePrefabPath })
            {
                GameObject prefab = Load<GameObject>(path);
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
                Assert.That(missing, Is.EqualTo(0), path + " has missing scripts.");
            }
        }

        [Test]
        public void BossWeakPointStaysWiredToTheBossHierarchy()
        {
            GameObject prefab = Load<GameObject>(BossPrefabPath);
            FreeRoamBoss boss = prefab.GetComponent<FreeRoamBoss>();
            WeakPoint weakPoint = prefab.GetComponentInChildren<WeakPoint>(true);

            Assert.IsNotNull(boss, "Boss component missing.");
            Assert.IsNotNull(weakPoint, "WeakPoint component missing.");
            Assert.That(
                weakPoint.transform.IsChildOf(boss.transform),
                Is.True,
                "WeakPoint is no longer inside the boss hierarchy.");
            Assert.That(
                ObjectReference(boss, "weakPoint"),
                Is.SameAs(weakPoint),
                "The boss does not reference its WeakPoint.");
            Assert.That(
                woodWeakPointMultiplier(weakPoint),
                Is.EqualTo(5f).Within(0.0001f),
                "WeakPoint damage multiplier baseline changed.");
        }

        private static float woodWeakPointMultiplier(WeakPoint weakPoint)
        {
            SerializedObject serializedObject = new SerializedObject(weakPoint);
            serializedObject.Update();
            return serializedObject.FindProperty("damageMultiplier").floatValue;
        }

        [Test]
        public void BossAssetReferencesResolveToExistingAssets()
        {
            string[] guids =
            {
                BossDataGuid,
                BossPrefabGuid,
                SporePrefabGuid,
                AnimatorControllerGuid,
                BossProfileGuid,
                TongueDataGuid,
                SporeDataGuid,
                ArenaProfileGuid,
                ArenaTongueGuid,
                ArenaSporeGuid
            };

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.IsFalse(
                    string.IsNullOrEmpty(path),
                    "GUID " + guid + " does not resolve to a project asset.");
            }
        }

        [Test]
        public void ArenaSceneKeepsBossAnimatorControllerOverride()
        {
            string scenePath = Path.Combine(
                Application.dataPath,
                ArenaScenePath.Substring("Assets/".Length));
            string[] lines = File.ReadAllText(scenePath).Split('\n');

            int index = Array.FindIndex(
                lines,
                line => line.Trim() == "propertyPath: m_Controller");
            Assert.That(
                index,
                Is.GreaterThanOrEqualTo(0),
                "The arena scene lost its boss Animator Controller override.");

            string objectReference = index + 2 < lines.Length
                ? lines[index + 2]
                : string.Empty;
            Assert.That(
                objectReference.Contains(AnimatorControllerGuid),
                Is.True,
                "The arena boss no longer uses its Animator Controller.");
        }

        private static void AssertSceneOverride(string[] lines, string propertyPath, string guid)
        {
            int index = Array.FindIndex(
                lines,
                line => line.Trim() == "propertyPath: " + propertyPath);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                "Scene has no override for '" + propertyPath + "'.");

            string objectReference = index + 2 < lines.Length ? lines[index + 2] : string.Empty;
            Assert.That(
                objectReference.Contains(guid),
                Is.True,
                "Scene override '" + propertyPath + "' does not point at " + guid + ".");
        }

        private static bool ContainsSceneProperty(string[] lines, string propertyPath)
        {
            return Array.Exists(lines, line => line.Trim() == "propertyPath: " + propertyPath);
        }

        private static void AssertAssetContains(string assetPath, params string[] expectedLines)
        {
            string fullPath = Path.Combine(
                Application.dataPath,
                assetPath.Substring("Assets/".Length));
            Assert.IsTrue(File.Exists(fullPath), "Asset file not found: " + fullPath);

            string[] lines = File.ReadAllText(fullPath).Split('\n');
            foreach (string expected in expectedLines)
            {
                Assert.That(
                    Array.Exists(lines, line => line.Trim() == expected),
                    Is.True,
                    assetPath + " does not explicitly serialize '" + expected + "'.");
            }
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, "Missing asset: " + path);
            return asset;
        }

        private static T PrefabComponent<T>(string prefabPath) where T : Component
        {
            GameObject prefab = Load<GameObject>(prefabPath);
            T component = prefab.GetComponent<T>();
            Assert.IsNotNull(component, typeof(T).Name + " is missing on " + prefabPath);
            return component;
        }

        private static UnityEngine.Object ObjectReference(Component component, string fieldName)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            Assert.IsNotNull(property, "Serialized field '" + fieldName + "' not found.");
            return property.objectReferenceValue;
        }

        private static void AssertFieldReference(
            Component component,
            string fieldName,
            UnityEngine.Object expected)
        {
            Assert.AreSame(
                expected,
                ObjectReference(component, fieldName),
                fieldName + " on " + component.GetType().Name + " is not the expected asset.");
        }

        private static void AssertFieldMissing(Component component, string fieldName)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.Update();
            Assert.IsNull(
                serializedObject.FindProperty(fieldName),
                "Field '" + fieldName + "' is still serialized on " +
                component.GetType().Name + ".");
        }
    }
}
