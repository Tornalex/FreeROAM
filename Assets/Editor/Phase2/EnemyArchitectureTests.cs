using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase2
{
    /// <summary>
    /// Phase 2 enemy data architecture tests.
    /// These tests are read-only with respect to project assets: they may create
    /// in-memory enemies, but they never save a prefab or a ScriptableObject.
    /// </summary>
    [TestFixture]
    public class EnemyArchitectureTests
    {
        private const string GroundDataPath = "Assets/Data/Enemy Data/Ground Enemy Data.asset";
        private const string FlyingDataPath = "Assets/Data/Enemy Data/Flying Enemy Data.asset";
        private const string SmallDataPath = "Assets/Data/Enemy Data/Ground Enemy Small Data.asset";
        private const string WalkingProfilePath =
            "Assets/Data/Enemy Data/Walking Enemy AI Profile.asset";
        private const string FlyingProfilePath =
            "Assets/Data/Enemy Data/Flying Enemy AI Profile.asset";

        private const string WalkingPrefabPath = "Assets/Prefabs/Enemies/Enemy.prefab";
        private const string FlyingPrefabPath = "Assets/Prefabs/Enemies/Flying Enemy.prefab";
        private const string RecallPrefabPath = "Assets/Prefabs/Enemies/RecallEnemy.prefab";
        private const string ProjectilePrefabPath = "Assets/Prefabs/Enemies/EnemyProjectile.prefab";

        [Test]
        public void EnemyDataAssetsExposeSharedStats()
        {
            EnemyData ground = Load<EnemyData>(GroundDataPath);
            Assert.That(ground.maxHealth, Is.EqualTo(4), "Ground maxHealth");
            Assert.That(ground.speed, Is.EqualTo(5f).Within(0.0001f), "Ground speed");
            AssertVector(new Vector2(10f, 5f), ground.knockbackForce, "Ground knockbackForce");
            Assert.That(ground.knockbackDuration, Is.EqualTo(0.1f).Within(0.0001f));

            EnemyData flying = Load<EnemyData>(FlyingDataPath);
            Assert.That(flying.maxHealth, Is.EqualTo(2), "Flying maxHealth");
            Assert.That(flying.speed, Is.EqualTo(6f).Within(0.0001f), "Flying speed");
            AssertVector(new Vector2(10f, 5f), flying.knockbackForce, "Flying knockbackForce");
            Assert.That(flying.knockbackDuration, Is.EqualTo(0.1f).Within(0.0001f));

            EnemyData small = Load<EnemyData>(SmallDataPath);
            Assert.That(small.maxHealth, Is.EqualTo(3), "Small maxHealth");
            Assert.That(small.speed, Is.EqualTo(5f).Within(0.0001f), "Small speed");
            Assert.That(small.knockbackDuration, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void EnemyProfilesAreAssignedToPrefabs()
        {
            WalkingEnemyProfile walkingProfile =
                Load<WalkingEnemyProfile>(WalkingProfilePath);
            FlyingEnemyProfile flyingProfile =
                Load<FlyingEnemyProfile>(FlyingProfilePath);

            WalkingEnemy walking = PrefabComponent<WalkingEnemy>(WalkingPrefabPath);
            FlyingEnemy flying = PrefabComponent<FlyingEnemy>(FlyingPrefabPath);

            AssertFieldReference(walking, "aiProfile", walkingProfile);
            AssertFieldReference(flying, "aiProfile", flyingProfile);

            // The tuning that moved into the profiles must no longer be
            // serialized twice on the prefab components.
            AssertFieldMissing(walking, "playerAcquisitionRadius");
            AssertFieldMissing(walking, "playerAggroRadius");
            AssertFieldMissing(walking, "chaseDirectionDeadzone");
            AssertFieldMissing(walking, "wallCheckDistance");
            AssertFieldMissing(walking, "groundCheckDistance");
            AssertFieldMissing(walking, "groundCheckForwardOffset");
            AssertFieldMissing(flying, "flySpeed");
            AssertFieldMissing(flying, "chaseSpeed");
            AssertFieldMissing(flying, "patrolRadius");
            AssertFieldMissing(flying, "detectionRadius");
            AssertFieldMissing(flying, "loseRadius");
            AssertFieldMissing(flying, "stunDuration");
        }

        [Test]
        public void WalkingProfileMatchesPreviousPrefabTuning()
        {
            WalkingEnemyProfile profile = Load<WalkingEnemyProfile>(WalkingProfilePath);

            Assert.That(profile.DetectionRadius, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(profile.LoseRadius, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(profile.ChaseDirectionDeadzone, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(profile.WallCheckDistance, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(profile.GroundCheckDistance, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(profile.GroundCheckForwardOffset, Is.EqualTo(0.15f).Within(0.0001f));
        }

        [Test]
        public void FlyingProfileMatchesPreviousPrefabTuning()
        {
            FlyingEnemyProfile profile = Load<FlyingEnemyProfile>(FlyingProfilePath);

            Assert.That(profile.DetectionRadius, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(profile.LoseRadius, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(profile.FlySpeed, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(profile.ChaseSpeed, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(profile.PatrolRadius, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(profile.StunDuration, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void RecallEnemyKeepsInstanceTuningForSceneOverrides()
        {
            // RecallEnemy tuning stays on the component because existing room
            // scenes override it per instance. Moving it to a shared profile
            // would silently change those rooms.
            RecallEnemy recall = PrefabComponent<RecallEnemy>(RecallPrefabPath);

            AssertFieldFloat(recall, "preferredDistance", 12f);
            AssertFieldFloat(recall, "distanceTolerance", 1f);
            AssertFieldFloat(recall, "moveSpeed", 8f);
            AssertFieldFloat(recall, "dodgeJumpForce", 30f);
            AssertFieldFloat(recall, "dodgeDetectionDistance", 6f);
            AssertFieldFloat(recall, "dodgeCooldown", 0.75f);
            AssertFieldFloat(recall, "spearDirectionThreshold", 0.98f);
            AssertFieldFloat(recall, "attackInterval", 2f);
            AssertFieldFloat(recall, "projectileSpeed", 30f);
            AssertFieldFloat(recall, "attackTelegraphDuration", 0.4f);
            AssertFieldFloat(recall, "flashInterval", 0.05f);
            AssertFieldColor(recall, "telegraphColor", Color.red);
        }

        [Test]
        public void EnemyProfilesExplicitlySerializeMigratedParameters()
        {
            AssertAssetContains(
                WalkingProfilePath,
                "detectionRadius: 6",
                "loseRadius: 10",
                "chaseDirectionDeadzone: 0.4",
                "wallCheckDistance: 0.1",
                "groundCheckDistance: 0.75",
                "groundCheckForwardOffset: 0.15");

            AssertAssetContains(
                FlyingProfilePath,
                "detectionRadius: 15",
                "loseRadius: 20",
                "flySpeed: 1",
                "chaseSpeed: 6",
                "patrolRadius: 1",
                "stunDuration: 0.1");
        }

        [Test]
        public void RuntimeDamageDoesNotMutateSharedEnemyData()
        {
            EnemyData data = Load<EnemyData>(GroundDataPath);
            WalkingEnemyProfile profile = Load<WalkingEnemyProfile>(WalkingProfilePath);
            string before = Snapshot(data);

            WalkingEnemy enemy = null;
            try
            {
                enemy = CreateWalkingEnemy(data, profile, "Phase2DamageEnemy");

                Assert.That(CurrentHealth(enemy), Is.EqualTo(data.maxHealth));

                enemy.TakeDamage(1, Vector2.zero, new Vector2(-1f, 0f));

                Assert.That(
                    CurrentHealth(enemy),
                    Is.EqualTo(data.maxHealth - 1),
                    "Runtime damage did not change instance health.");
                Assert.That(
                    before,
                    Is.EqualTo(Snapshot(data)),
                    "Runtime damage mutated the shared EnemyData asset.");
            }
            finally
            {
                Destroy(enemy);
            }
        }

        [Test]
        public void SharedEnemyDataKeepsRuntimeHealthIndependent()
        {
            EnemyData data = Load<EnemyData>(GroundDataPath);
            WalkingEnemyProfile profile = Load<WalkingEnemyProfile>(WalkingProfilePath);
            string before = Snapshot(data);

            WalkingEnemy first = null;
            WalkingEnemy second = null;
            try
            {
                first = CreateWalkingEnemy(data, profile, "Phase2EnemyA");
                second = CreateWalkingEnemy(data, profile, "Phase2EnemyB");

                first.TakeDamage(2, Vector2.zero, new Vector2(-1f, 0f));

                Assert.That(CurrentHealth(first), Is.EqualTo(data.maxHealth - 2));
                Assert.That(
                    CurrentHealth(second),
                    Is.EqualTo(data.maxHealth),
                    "A second enemy sharing the same EnemyData was affected by the first.");
                Assert.That(before, Is.EqualTo(Snapshot(data)));
            }
            finally
            {
                Destroy(first);
                Destroy(second);
            }
        }

        [Test]
        public void EnemyArchetypesStillReadTheirAuthoringData()
        {
            WalkingEnemy walking = PrefabComponent<WalkingEnemy>(WalkingPrefabPath);
            FlyingEnemy flying = PrefabComponent<FlyingEnemy>(FlyingPrefabPath);
            RecallEnemy recall = PrefabComponent<RecallEnemy>(RecallPrefabPath);

            AssertFieldReference(walking, "enemyData", Load<EnemyData>(GroundDataPath));
            AssertFieldReference(flying, "enemyData", Load<EnemyData>(FlyingDataPath));
            AssertFieldReference(recall, "enemyData", Load<EnemyData>(GroundDataPath));

            EnemyProjectile projectile = PrefabComponent<EnemyProjectile>(ProjectilePrefabPath);
            AssertFieldInt(projectile, "damage", 1);
            AssertFieldFloat(projectile, "lifetime", 5f);
        }

        [Test]
        public void EnemyPrefabsHaveNoMissingScripts()
        {
            string[] prefabPaths =
            {
                WalkingPrefabPath,
                FlyingPrefabPath,
                RecallPrefabPath,
                ProjectilePrefabPath
            };

            foreach (string path in prefabPaths)
            {
                GameObject prefab = Load<GameObject>(path);
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
                Assert.That(missing, Is.EqualTo(0), path + " has missing scripts.");
            }
        }

        [Test]
        public void GroundEnemyFastWasNotReintroduced()
        {
            const string removedPath = "Assets/Prefabs/Enemies/GroundEnemyFast.prefab";

            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(removedPath),
                Is.Null,
                "GroundEnemyFast was deliberately removed in Phase 1.");

            string[] matches = AssetDatabase.FindAssets("GroundEnemyFast t:Prefab");
            Assert.That(matches, Is.Empty, "A GroundEnemyFast prefab exists again.");
        }

        private static WalkingEnemy CreateWalkingEnemy(
            EnemyData data,
            WalkingEnemyProfile profile,
            string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<SpriteRenderer>();

            WalkingEnemy enemy = gameObject.AddComponent<WalkingEnemy>();
            SetField(enemy, "enemyData", data);
            SetField(enemy, "aiProfile", profile);

            // EditMode instantiation does not run Awake automatically.
            InvokeAwake(enemy);
            return enemy;
        }

        private static int CurrentHealth(BaseEnemy enemy)
        {
            FieldInfo field = FindField(typeof(BaseEnemy), "currentHealth");
            return (int)field.GetValue(enemy);
        }

        private static string Snapshot(EnemyData data)
        {
            return string.Join(
                "|",
                new[]
                {
                    data.maxHealth.ToString(CultureInfo.InvariantCulture),
                    data.speed.ToString("R", CultureInfo.InvariantCulture),
                    data.knockbackForce.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                    data.knockbackForce.y.ToString("R", CultureInfo.InvariantCulture),
                    data.knockbackDuration.ToString("R", CultureInfo.InvariantCulture)
                });
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, "Missing asset: " + path);
            return asset;
        }

        private static GameObject LoadPrefab(string path)
        {
            return Load<GameObject>(path);
        }

        private static T PrefabComponent<T>(string prefabPath) where T : Component
        {
            T component = LoadPrefab(prefabPath).GetComponent<T>();
            Assert.IsNotNull(component, typeof(T).Name + " is missing on " + prefabPath);
            return component;
        }

        private static SerializedProperty FindProperty(Component component, string fieldName)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                "Serialized field '" + fieldName + "' was not found on " +
                component.GetType().Name + ".");
            return property;
        }

        private static void AssertFieldReference(
            Component component,
            string fieldName,
            UnityEngine.Object expected)
        {
            Assert.AreSame(
                expected,
                FindProperty(component, fieldName).objectReferenceValue,
                fieldName + " on " + component.GetType().Name + " is not the expected asset.");
        }

        private static void AssertFieldMissing(Component component, string fieldName)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.Update();
            Assert.IsNull(
                serializedObject.FindProperty(fieldName),
                "Legacy tuning field '" + fieldName + "' is still serialized on " +
                component.GetType().Name + ".");
        }

        private static void AssertFieldFloat(Component component, string fieldName, float expected)
        {
            Assert.That(
                FindProperty(component, fieldName).floatValue,
                Is.EqualTo(expected).Within(0.0001f),
                fieldName);
        }

        private static void AssertFieldInt(Component component, string fieldName, int expected)
        {
            Assert.That(FindProperty(component, fieldName).intValue, Is.EqualTo(expected), fieldName);
        }

        private static void AssertFieldColor(Component component, string fieldName, Color expected)
        {
            Color actual = FindProperty(component, fieldName).colorValue;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), fieldName + ".r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), fieldName + ".g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), fieldName + ".b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), fieldName + ".a");
        }

        private static void AssertVector(Vector2 expected, Vector2 actual, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), label + ".x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), label + ".y");
        }

        private static void AssertAssetContains(string assetPath, params string[] expectedLines)
        {
            string relativePath = assetPath.Substring("Assets/".Length);
            string fullPath = Path.Combine(Application.dataPath, relativePath);
            Assert.IsTrue(File.Exists(fullPath), "Asset file not found: " + fullPath);

            string[] lines = File.ReadAllText(fullPath).Split('\n');
            foreach (string expected in expectedLines)
            {
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == expected)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(
                    found,
                    assetPath + " does not explicitly serialize '" + expected + "'.");
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            field.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
            }

            Assert.Fail("Field '" + fieldName + "' was not found on " + type.Name + ".");
            return null;
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            MethodInfo method = component.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Awake was not found on " + component.GetType().Name + ".");
            method.Invoke(component, null);
        }

        private static void Destroy(UnityEngine.Object instance)
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
