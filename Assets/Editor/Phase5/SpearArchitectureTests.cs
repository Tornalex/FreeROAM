using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase5
{
    [TestFixture]
    public class SpearArchitectureTests
    {
        private const string ConfigPath = "Assets/Data/Spear Data/Spear Config.asset";
        private const string SpearPrefabPath = "Assets/Prefabs/Utilities/SpearV2.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/Utilities/Player.prefab";
        private const string PlayerCombatDataPath = "Assets/Data/Player Data/Player Combat Data.asset";
        private const string PlayerRopeDataPath = "Assets/Data/Player Data/Player Rope Data.asset";

        private const string ConfigScriptGuid = "74507ab835d14ab98640efa6e9616fc1";
        private const string ConfigAssetGuid = "f934e87297534185bdc71c2af0d9adec";
        private const string RopeMaterialGuid = "79809b4fc57ad41b982c1dd5ceb812b2";

        [Test]
        public void SpearConfigAssetContainsPreviousEffectiveTuning()
        {
            SpearConfig config = Load<SpearConfig>(ConfigPath);

            Assert.That(config.EnemyBounceSpeed, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(config.BounceAngle, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(config.TipRaycastLength, Is.EqualTo(1.5f).Within(0.0001f));
            // SpearV2.prefab serialized 0; Spear.InitializePhysics clamped it
            // to 0.15 before gameplay. The asset stores that effective value.
            Assert.That(config.EmbedDepth, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(config.RigidbodyMass, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(config.FlightGravityScale, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(config.FlightLinearDamping, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(config.BossBounceSpeedMultiplier, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(config.BossBounceGravityScale, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(config.BossCollisionIgnoreDuration, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(config.ReturnDuration, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(config.MaxReturnSpeed, Is.EqualTo(120f).Within(0.0001f));
            Assert.That(config.CatchDistance, Is.EqualTo(0.75f).Within(0.0001f));

            Assert.That(config.DroppedGravityScale, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(config.DroppedLinearDamping, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(config.EnemyDeathHorizontalImpulse, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(config.EnemyDeathUpwardImpulse, Is.EqualTo(10f).Within(0.0001f));

            Assert.That(config.RopeAttachOffset, Is.EqualTo(new Vector2(-0.8f, 0f)));
            Assert.That(config.RopeWidth, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.IsNotNull(config.RopeMaterial);
            Assert.That(config.RopeStartColor, Is.EqualTo(new Color(0.5f, 0.35f, 0.15f, 1f)));
            Assert.That(config.RopeEndColor, Is.EqualTo(new Color(0.5f, 0.35f, 0.35f, 1f)));

            SerializedObject serialized = new SerializedObject(config);
            AnimationCurve curve = serialized.FindProperty("returnCurve").animationCurveValue;
            Assert.IsNotNull(curve);
            Assert.That(curve.length, Is.EqualTo(2));
            Assert.That(curve.keys[0].time, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(curve.keys[0].value, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(curve.keys[1].time, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(curve.keys[1].value, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(curve.keys[1].inTangent, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(curve.keys[1].outTangent, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void SpearPrefabUsesTheAuthoringConfigAndKeepsStructuralTags()
        {
            Spear spear = PrefabComponent<Spear>(SpearPrefabPath);
            SpearConfig config = Load<SpearConfig>(ConfigPath);
            SerializedObject serialized = new SerializedObject(spear);
            serialized.Update();

            Assert.That(
                serialized.FindProperty("spearConfig").objectReferenceValue,
                Is.SameAs(config));
            Assert.That(serialized.FindProperty("cameraBoundsTag").stringValue, Is.EqualTo("CameraBounds"));
            Assert.That(
                serialized.FindProperty("directionChangerTag").stringValue,
                Is.EqualTo("EnemyDirectionChanger"));

            foreach (string movedField in new[]
                     {
                         "enemyBounceSpeed", "bounceAngle", "tipRaycastLength", "embedDepth",
                         "returnCurve", "returnDuration", "maxReturnSpeed", "catchDistance",
                         "ropeAttachOffset", "ropeWidth", "ropeMaterial"
                     })
            {
                Assert.IsNull(
                    serialized.FindProperty(movedField),
                    "Duplicated spear tuning remains serialized on the component: " + movedField);
            }
        }

        [Test]
        public void SpearConfigExplicitlySerializesEveryMigratedValue()
        {
            AssertAssetContains(
                ConfigPath,
                "enemyBounceSpeed: 10",
                "bounceAngle: 90",
                "tipRaycastLength: 1.5",
                "embedDepth: 0.15",
                "rigidbodyMass: 2",
                "flightGravityScale: 3",
                "flightLinearDamping: 0",
                "bossBounceSpeedMultiplier: 0.6",
                "bossBounceGravityScale: 1",
                "bossCollisionIgnoreDuration: 0.5",
                "returnDuration: 1",
                "maxReturnSpeed: 120",
                "catchDistance: 0.75",
                "droppedGravityScale: 1.5",
                "droppedLinearDamping: 2",
                "enemyDeathHorizontalImpulse: 6",
                "enemyDeathUpwardImpulse: 10",
                "ropeAttachOffset: {x: -0.8, y: 0}",
                "ropeWidth: 0.1",
                "ropeMaterial: {fileID: 2100000, guid: " + RopeMaterialGuid + ", type: 2}",
                "ropeStartColor: {r: 0.5, g: 0.35, b: 0.15, a: 1}",
                "ropeEndColor: {r: 0.5, g: 0.35, b: 0.35, a: 1}");

            string assetText = File.ReadAllText(AbsoluteAssetPath(ConfigPath));
            StringAssert.Contains("returnCurve:", assetText);
            StringAssert.Contains("inSlope: 2", assetText);
            StringAssert.Contains("outSlope: 2", assetText);
        }

        [Test]
        public void PlayerCombatAndRopeDataRemainTheOwnersOfPlayerTuning()
        {
            SpearConfig config = Load<SpearConfig>(ConfigPath);
            PlayerCombatData combatData = Load<PlayerCombatData>(PlayerCombatDataPath);
            PlayerRopeData ropeData = Load<PlayerRopeData>(PlayerRopeDataPath);

            Assert.That(combatData.shootForce, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(combatData.throwCooldown, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(combatData.spearImpactDamage, Is.EqualTo(1));
            Assert.That(combatData.spearRecallDamage, Is.EqualTo(1));
            Assert.That(ropeData.ropeLength, Is.EqualTo(5f).Within(0.0001f));

            string[] playerOwnedNames =
            {
                "shootForce", "throwCooldown", "spearImpactDamage", "spearRecallDamage",
                "ropeLength", "climbSpeed", "dismountJumpForce", "dismountCooldown"
            };

            foreach (string name in playerOwnedNames)
            {
                Assert.IsNull(
                    typeof(SpearConfig).GetField(
                        name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    "Player-owned tuning was duplicated in SpearConfig: " + name);
            }
        }

        [Test]
        public void RuntimeStateIsStoredOnSpearInstancesNotInSpearConfig()
        {
            foreach (string stateName in new[]
                     {
                         "currentState", "_rb", "_playerTransform", "_returnTimer",
                         "_impactDamage", "_recallDamage", "_canSpawnRope", "_ropeLength",
                         "_rope", "_lastVelocity", "_enemiesHitDuringReturn"
                     })
            {
                Assert.IsNull(
                    typeof(SpearConfig).GetField(
                        stateName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    "Runtime state must not be stored in SpearConfig: " + stateName);
            }

            FieldInfo state = typeof(Spear).GetField(
                "currentState",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(state, "The per-instance spear state field was removed.");
        }

        [Test]
        public void SpearAndConfigReferencesResolveAndPrefabComponentsRemainPresent()
        {
            Assert.That(AssetDatabase.GUIDToAssetPath(ConfigScriptGuid), Is.EqualTo("Assets/Scripts/ScriptableObjects/SpearConfig.cs"));
            Assert.That(AssetDatabase.GUIDToAssetPath(ConfigAssetGuid), Is.EqualTo(ConfigPath));
            Assert.That(
                AssetDatabase.GUIDToAssetPath(RopeMaterialGuid),
                Is.EqualTo("Assets/Imports/LeanTween/Examples/Material/DirectionalArrowMaterial.mat"));

            GameObject spearPrefab = Load<GameObject>(SpearPrefabPath);
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(spearPrefab), Is.EqualTo(0));
            Assert.IsNotNull(spearPrefab.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(spearPrefab.GetComponent<BoxCollider2D>());
            Assert.IsNotNull(spearPrefab.GetComponent<PlatformEffector2D>());
            Assert.IsNotNull(spearPrefab.GetComponentInChildren<SpearTip>(true));
        }

        private static void AssertAssetContains(string path, params string[] expectedLines)
        {
            string[] lines = File.ReadAllText(AbsoluteAssetPath(path)).Split('\n');
            foreach (string expectedLine in expectedLines)
            {
                Assert.That(
                    Array.Exists(lines, line => line.Trim() == expectedLine),
                    Is.True,
                    path + " does not explicitly serialize '" + expectedLine + "'.");
            }
        }

        private static string AbsoluteAssetPath(string path)
        {
            return Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, "Missing asset: " + path);
            return asset;
        }

        private static T PrefabComponent<T>(string path) where T : Component
        {
            GameObject prefab = Load<GameObject>(path);
            T component = prefab.GetComponent<T>();
            Assert.IsNotNull(component, typeof(T).Name + " is missing from " + path);
            return component;
        }
    }
}
