using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Runtime coverage for spear authoring data isolation and the live
    /// trajectory, impact, return, drop and generated-rope paths.
    /// </summary>
    public class SpearArchitecturePlayModeTests : PlayModeTestBase
    {
        private const string SpearTypeName = "Spear";
        private const string SpearConfigTypeName = "SpearConfig";
        private const string SpearPredictorTypeName = "SpearTrajectoryPredictor";
        private const string SpearTipTypeName = "SpearTip";
        private const string RopeTypeName = "Rope";
        private const string BossTypeName = "SpearWander.Boss.Boss";
        private const string BossDataTypeName = "BossData";
        private const string WalkingEnemyTypeName = "WalkingEnemy";
        private const string WalkingProfileTypeName = "WalkingEnemyProfile";
        private const string EnemyDataTypeName = "EnemyData";

        [UnityTest]
        public IEnumerator SharedConfigKeepsSpearStateAndDamageIndependent()
        {
            ScriptableObject config = CreateConfig();
            GameObject playerObject = CreatePlayerProxy(new Vector3(7000f, 0f, 0f));
            GameObject firstObject = CreateSpearObject(config, new Vector3(7000f, 5f, 0f));
            GameObject secondObject = CreateSpearObject(config, new Vector3(7010f, 5f, 0f));

            string configBefore = SnapshotConfig(config);
            try
            {
                Component first = firstObject.GetComponent(ResolveRuntimeType(SpearTypeName));
                Component second = secondObject.GetComponent(ResolveRuntimeType(SpearTypeName));
                Collider2D playerCollider = playerObject.GetComponent<Collider2D>();

                Rigidbody2D firstBody = firstObject.GetComponent<Rigidbody2D>();
                Assert.That(firstBody.mass, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(firstBody.gravityScale, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(firstBody.linearDamping, Is.EqualTo(0f).Within(0.0001f));

                ReflectionTestUtilities.InvokeInstance(first, "Initialize", playerCollider, false, 5f);
                ReflectionTestUtilities.InvokeInstance(second, "Initialize", playerCollider, false, 5f);
                ReflectionTestUtilities.InvokeInstance(first, "SetDamage", 3, 8);
                ReflectionTestUtilities.InvokeInstance(second, "SetDamage", 1, 2);
                ReflectionTestUtilities.SetInstanceField(
                    first,
                    "currentState",
                    Enum.Parse(ResolveRuntimeType(SpearTypeName + "+SpearState"), "Embedded"));

                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(first, "RecallDamage"),
                    Is.EqualTo(8));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(second, "RecallDamage"),
                    Is.EqualTo(2));
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(first, "currentState").ToString(),
                    Is.EqualTo("Embedded"));
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(second, "currentState").ToString(),
                    Is.EqualTo("Flying"));
                Assert.That(SnapshotConfig(config), Is.EqualTo(configBefore));

                yield return null;
            }
            finally
            {
                DestroyObject(config);
            }
        }

        [UnityTest]
        public IEnumerator TrajectoryPredictorUsesLaunchImpulseAndPrefabPhysicsWithoutMutatingSpear()
        {
            ScriptableObject config = CreateConfig();
            GameObject playerObject = CreatePlayerProxy(new Vector3(8500f, 8500f, 0f));
            GameObject spearObject = CreateSpearObject(
                config,
                new Vector3(8000f, 8000f, 0f),
                includeTip: true);
            Rigidbody2D body = spearObject.GetComponent<Rigidbody2D>();
            body.mass = 2f;
            body.gravityScale = 3f;
            body.linearDamping = 0f;

            GameObject predictorObject = Fixture.CreateGameObject("Phase5TrajectoryPredictor");
            Component predictor = predictorObject.AddComponent(
                ResolveRuntimeType(SpearPredictorTypeName));
            string configBefore = SnapshotConfig(config);

            try
            {
                ReflectionTestUtilities.InvokeInstance(
                    predictor,
                    "Configure",
                    spearObject,
                    playerObject.GetComponent<Collider2D>());

                int pointCount = ReflectionTestUtilities.InvokeInstance<int>(
                    predictor,
                    "Predict",
                    new Vector2(8000f, 8000f),
                    Vector2.right,
                    100f);

                Assert.That(pointCount, Is.GreaterThan(1));
                Vector2[] points = ReflectionTestUtilities.GetProperty<Vector2[]>(
                    predictor,
                    "Points");
                float deltaTime = Time.fixedDeltaTime;
                Assert.That(
                    points[1].x - points[0].x,
                    Is.EqualTo(50f * deltaTime).Within(0.001f),
                    "Predictor launch velocity must match impulse / Rigidbody mass.");
                Assert.That(
                    points[1].y - points[0].y,
                    Is.EqualTo(Physics2D.gravity.y * 3f * deltaTime * deltaTime)
                        .Within(0.001f),
                    "Predictor vertical displacement must use the spear Rigidbody gravity.");
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(SnapshotConfig(config), Is.EqualTo(configBefore));

                yield return null;
            }
            finally
            {
                DestroyObject(config);
            }
        }

        [UnityTest]
        public IEnumerator ImpactAndRecallDamageUsePerInstanceCombatValues()
        {
            ScriptableObject config = CreateConfig();
            ScriptableObject bossData = CreateBossData(maxHealth: 20);
            GameObject playerObject = CreatePlayerProxy(new Vector3(9000f, 0f, 0f));
            GameObject spearObject = CreateSpearObject(config, new Vector3(9000f, 4f, 0f));
            GameObject bossObject = CreateBoss(bossData, new Vector3(9005f, 0f, 0f));

            try
            {
                Component spear = spearObject.GetComponent(ResolveRuntimeType(SpearTypeName));
                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossTypeName));
                Collider2D playerCollider = playerObject.GetComponent<Collider2D>();
                ReflectionTestUtilities.InvokeInstance(spear, "Initialize", playerCollider, false, 5f);
                ReflectionTestUtilities.InvokeInstance(spear, "SetDamage", 3, 7);

                bool throwPathContinues = ReflectionTestUtilities.InvokeInstance<bool>(
                    spear,
                    "ProcessDamageableHit",
                    boss,
                    Vector2.zero);

                Assert.IsTrue(throwPathContinues);
                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(boss, "CurrentHealth"),
                    Is.EqualTo(17),
                    "Flying impact did not apply the instance's impact damage.");

                ReflectionTestUtilities.InvokeInstance(spear, "StartReturn", playerObject.transform);
                bool returnPathContinues = ReflectionTestUtilities.InvokeInstance<bool>(
                    spear,
                    "ProcessDamageableHit",
                    boss,
                    Vector2.zero);

                Assert.IsFalse(returnPathContinues);
                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(boss, "CurrentHealth"),
                    Is.EqualTo(10),
                    "Recall did not apply the instance's recall damage.");
                Assert.IsTrue(
                    ReflectionTestUtilities.GetProperty<bool>(spear, "HasHitEnemy"),
                    "Accepted impact damage was not recorded on the spear instance.");

                yield return null;
            }
            finally
            {
                DestroyObject(config);
                DestroyObject(bossData);
            }
        }

        [UnityTest]
        public IEnumerator SpearImpactRemainsCompatibleWithOrdinaryEnemies()
        {
            ScriptableObject config = CreateConfig();
            ScriptableObject enemyData = CreateEnemyData(maxHealth: 5);
            ScriptableObject enemyProfile = ScriptableObject.CreateInstance(
                ResolveRuntimeType(WalkingProfileTypeName));
            GameObject playerObject = CreatePlayerProxy(new Vector3(11000f, 0f, 0f));
            GameObject spearObject = CreateSpearObject(config, new Vector3(11000f, 3f, 0f));
            GameObject enemyObject = CreateWalkingEnemy(
                enemyData,
                enemyProfile,
                new Vector3(11005f, 0f, 0f));

            try
            {
                Component spear = spearObject.GetComponent(ResolveRuntimeType(SpearTypeName));
                Component enemy = enemyObject.GetComponent(
                    ResolveRuntimeType(WalkingEnemyTypeName));
                ReflectionTestUtilities.InvokeInstance(
                    spear,
                    "Initialize",
                    playerObject.GetComponent<Collider2D>(),
                    false,
                    5f);
                ReflectionTestUtilities.InvokeInstance(spear, "SetDamage", 2, 7);

                ReflectionTestUtilities.InvokeInstance(
                    spear,
                    "ProcessDamageableHit",
                    enemy,
                    Vector2.zero);

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(enemy, "currentHealth"),
                    Is.EqualTo(3),
                    "The flying spear did not deliver impact damage to WalkingEnemy.");
                Assert.IsTrue(
                    ReflectionTestUtilities.GetProperty<bool>(spear, "HasHitEnemy"));

                yield return null;
            }
            finally
            {
                DestroyObject(config);
                DestroyObject(enemyData);
                DestroyObject(enemyProfile);
            }
        }

        [UnityTest]
        public IEnumerator EmbeddingDropAndRopeUseConfigWhileRopeLengthRemainsPlayerOwned()
        {
            ScriptableObject config = CreateConfig();
            ReflectionTestUtilities.SetInstanceField(config, "embedDepth", 0.3f);
            ReflectionTestUtilities.SetInstanceField(config, "droppedGravityScale", 2.25f);
            ReflectionTestUtilities.SetInstanceField(config, "droppedLinearDamping", 3.5f);
            ReflectionTestUtilities.SetInstanceField(config, "enemyDeathHorizontalImpulse", 4f);
            ReflectionTestUtilities.SetInstanceField(config, "enemyDeathUpwardImpulse", 9f);
            ReflectionTestUtilities.SetInstanceField(config, "ropeAttachOffset", new Vector2(-0.4f, 0.2f));
            ReflectionTestUtilities.SetInstanceField(config, "ropeWidth", 0.12f);

            GameObject playerObject = CreatePlayerProxy(new Vector3(10000f, 0f, 0f));
            GameObject spearObject = CreateSpearObject(config, new Vector3(10000f, 3f, 0f));
            GameObject wallObject = Fixture.CreateGameObject("Phase5SpearWall");
            wallObject.transform.position = new Vector3(10002f, 0f, 0f);
            Collider2D wallCollider = wallObject.AddComponent<BoxCollider2D>();
            string configBefore = SnapshotConfig(config);

            try
            {
                Component spear = spearObject.GetComponent(ResolveRuntimeType(SpearTypeName));
                ReflectionTestUtilities.InvokeInstance(
                    spear,
                    "Initialize",
                    playerObject.GetComponent<Collider2D>(),
                    true,
                    5f); // This length is supplied by PlayerRopeData in PlayerCombat.
                ReflectionTestUtilities.InvokeInstance(
                    spear,
                    "ProcessHit",
                    wallCollider,
                    new Vector2(10001f, 0f),
                    Vector2.right,
                    true);

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(spear, "currentState").ToString(),
                    Is.EqualTo("Embedded"));

                Component rope = spearObject.GetComponentInChildren(ResolveRuntimeType(RopeTypeName));
                Assert.IsNotNull(rope, "Embedding did not create the rope component.");
                Assert.That(
                    rope.transform.localPosition,
                    Is.EqualTo(new Vector3(-0.4f, 0.2f, 0f)));

                LineRenderer line = rope.GetComponent<LineRenderer>();
                Assert.That(line.startWidth, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(line.endWidth, Is.EqualTo(0.12f).Within(0.0001f));
                EdgeCollider2D edge = rope.GetComponent<EdgeCollider2D>();
                Assert.That(edge.points[1], Is.EqualTo(new Vector2(0f, -5f)));

                ReflectionTestUtilities.InvokeInstance(spear, "OnEnemyDeath");
                Rigidbody2D body = spearObject.GetComponent<Rigidbody2D>();
                Assert.That(body.gravityScale, Is.EqualTo(2.25f).Within(0.0001f));
                Assert.That(body.linearVelocity.y, Is.EqualTo(4.5f).Within(0.0001f));
                Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThanOrEqualTo(4f));
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(spear, "currentState").ToString(),
                    Is.EqualTo("Dropped"));

                ReflectionTestUtilities.InvokeInstance(spear, "StartReturn", playerObject.transform);
                ReflectionTestUtilities.InvokeInstance(spear, "AbortReturn");
                Assert.That(body.gravityScale, Is.EqualTo(2.25f).Within(0.0001f));
                Assert.That(body.linearDamping, Is.EqualTo(3.5f).Within(0.0001f));
                Assert.That(SnapshotConfig(config), Is.EqualTo(configBefore));

                yield return null;
            }
            finally
            {
                DestroyObject(config);
            }
        }

        private GameObject CreateSpearObject(
            ScriptableObject config,
            Vector3 position,
            bool includeTip = false)
        {
            GameObject spearObject = Fixture.CreateGameObject("Phase5RuntimeSpear");
            spearObject.SetActive(false);
            spearObject.transform.position = position;

            Rigidbody2D body = spearObject.AddComponent<Rigidbody2D>();
            body.mass = 1f;
            body.gravityScale = 3f;
            body.linearDamping = 0f;
            spearObject.AddComponent<BoxCollider2D>();
            spearObject.AddComponent<PlatformEffector2D>();

            Component spear = spearObject.AddComponent(ResolveRuntimeType(SpearTypeName));
            ReflectionTestUtilities.SetInstanceField(spear, "spearConfig", config);

            if (includeTip)
            {
                GameObject tipObject = new GameObject("Tip");
                tipObject.transform.SetParent(spearObject.transform, false);
                tipObject.transform.localPosition = new Vector3(1f, 0f, 0f);
                BoxCollider2D tipCollider = tipObject.AddComponent<BoxCollider2D>();
                tipCollider.isTrigger = true;
                tipCollider.size = new Vector2(0.01f, 0.01f);
                tipObject.AddComponent(ResolveRuntimeType(SpearTipTypeName));
            }

            spearObject.SetActive(true);
            return spearObject;
        }

        private GameObject CreatePlayerProxy(Vector3 position)
        {
            GameObject player = Fixture.CreateGameObject("Phase5PlayerProxy");
            player.tag = "Player";
            player.transform.position = position;
            player.AddComponent<BoxCollider2D>();
            return player;
        }

        private GameObject CreateBoss(ScriptableObject data, Vector3 position)
        {
            GameObject bossObject = Fixture.CreateGameObject("Phase5BossTarget");
            bossObject.SetActive(false);
            bossObject.transform.position = position;
            bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            bossObject.AddComponent<SpriteRenderer>();
            bossObject.AddComponent<Animator>();
            Component boss = bossObject.AddComponent(ResolveRuntimeType(BossTypeName));
            ReflectionTestUtilities.SetInstanceField(boss, "bossData", data);
            bossObject.SetActive(true);
            return bossObject;
        }

        private GameObject CreateWalkingEnemy(
            ScriptableObject enemyData,
            ScriptableObject profile,
            Vector3 position)
        {
            GameObject enemyObject = Fixture.CreateGameObject("Phase5WalkingEnemyTarget");
            enemyObject.SetActive(false);
            enemyObject.transform.position = position;
            enemyObject.AddComponent<Rigidbody2D>();
            enemyObject.AddComponent<SpriteRenderer>();
            enemyObject.AddComponent<BoxCollider2D>();

            Component enemy = enemyObject.AddComponent(
                ResolveRuntimeType(WalkingEnemyTypeName));
            ReflectionTestUtilities.SetInstanceField(enemy, "enemyData", enemyData);
            ReflectionTestUtilities.SetInstanceField(enemy, "aiProfile", profile);
            enemyObject.SetActive(true);
            return enemyObject;
        }

        private static ScriptableObject CreateConfig()
        {
            Type configType = ResolveRuntimeType(SpearConfigTypeName);
            ScriptableObject config = ScriptableObject.CreateInstance(configType);
            ReflectionTestUtilities.SetInstanceField(config, "enemyBounceSpeed", 10f);
            ReflectionTestUtilities.SetInstanceField(config, "bounceAngle", 90f);
            ReflectionTestUtilities.SetInstanceField(config, "tipRaycastLength", 1.5f);
            ReflectionTestUtilities.SetInstanceField(config, "embedDepth", 0.15f);
            ReflectionTestUtilities.SetInstanceField(config, "bossBounceSpeedMultiplier", 0.6f);
            ReflectionTestUtilities.SetInstanceField(config, "bossBounceGravityScale", 1f);
            ReflectionTestUtilities.SetInstanceField(config, "bossCollisionIgnoreDuration", 0.5f);
            ReflectionTestUtilities.SetInstanceField(config, "returnCurve", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            ReflectionTestUtilities.SetInstanceField(config, "returnDuration", 1f);
            ReflectionTestUtilities.SetInstanceField(config, "maxReturnSpeed", 120f);
            ReflectionTestUtilities.SetInstanceField(config, "catchDistance", 0.75f);
            ReflectionTestUtilities.SetInstanceField(config, "droppedGravityScale", 1.5f);
            ReflectionTestUtilities.SetInstanceField(config, "droppedLinearDamping", 2f);
            ReflectionTestUtilities.SetInstanceField(config, "enemyDeathHorizontalImpulse", 6f);
            ReflectionTestUtilities.SetInstanceField(config, "enemyDeathUpwardImpulse", 10f);
            ReflectionTestUtilities.SetInstanceField(config, "ropeAttachOffset", new Vector2(-0.8f, 0f));
            ReflectionTestUtilities.SetInstanceField(config, "ropeWidth", 0.1f);
            ReflectionTestUtilities.SetInstanceField(config, "ropeStartColor", new Color(0.5f, 0.35f, 0.15f, 1f));
            ReflectionTestUtilities.SetInstanceField(config, "ropeEndColor", new Color(0.5f, 0.35f, 0.35f, 1f));
            return config;
        }

        private static ScriptableObject CreateBossData(int maxHealth)
        {
            ScriptableObject data = ScriptableObject.CreateInstance(
                ResolveRuntimeType(BossDataTypeName));
            ReflectionTestUtilities.SetInstanceField(data, "maxHealth", maxHealth);
            ReflectionTestUtilities.SetInstanceField(data, "weakPointDamageMultiplier", 1f);
            ReflectionTestUtilities.SetInstanceField(data, "knockbackForce", Vector2.zero);
            ReflectionTestUtilities.SetInstanceField(data, "knockbackDuration", 0f);
            return data;
        }

        private static ScriptableObject CreateEnemyData(int maxHealth)
        {
            ScriptableObject data = ScriptableObject.CreateInstance(
                ResolveRuntimeType(EnemyDataTypeName));
            ReflectionTestUtilities.SetInstanceField(data, "maxHealth", maxHealth);
            ReflectionTestUtilities.SetInstanceField(data, "speed", 0f);
            return data;
        }

        private static string SnapshotConfig(ScriptableObject config)
        {
            return string.Join(
                "|",
                new object[]
                {
                    ReflectionTestUtilities.GetProperty(config, "EnemyBounceSpeed"),
                    ReflectionTestUtilities.GetProperty(config, "BounceAngle"),
                    ReflectionTestUtilities.GetProperty(config, "EmbedDepth"),
                    ReflectionTestUtilities.GetProperty(config, "RigidbodyMass"),
                    ReflectionTestUtilities.GetProperty(config, "FlightGravityScale"),
                    ReflectionTestUtilities.GetProperty(config, "FlightLinearDamping"),
                    ReflectionTestUtilities.GetProperty(config, "ReturnDuration"),
                    ReflectionTestUtilities.GetProperty(config, "MaxReturnSpeed"),
                    ReflectionTestUtilities.GetProperty(config, "CatchDistance"),
                    ReflectionTestUtilities.GetProperty(config, "DroppedGravityScale"),
                    ReflectionTestUtilities.GetProperty(config, "DroppedLinearDamping"),
                    ReflectionTestUtilities.GetProperty(config, "EnemyDeathUpwardImpulse"),
                    ReflectionTestUtilities.GetProperty(config, "RopeAttachOffset"),
                    ReflectionTestUtilities.GetProperty(config, "RopeWidth")
                });
        }

        private static Type ResolveRuntimeType(string typeName)
        {
            return ReflectionTestUtilities.ResolveRuntimeType(typeName);
        }

        private static void DestroyObject(UnityEngine.Object instance)
        {
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }
    }
}
