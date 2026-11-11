using NUnit.Framework;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Captures the current EnemyData assets and the per-prefab enemy tuning.
    /// These values are the regression baseline for the planned enemy data
    /// consolidation; they must not change without a deliberate balance change.
    /// </summary>
    [TestFixture]
    public class EnemyBaselineTests
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

        [Test]
        public void GroundEnemyDataMatchesCapturedBaseline()
        {
            EnemyData data = BaselineTestSupport.LoadAsset<EnemyData>(GroundDataPath);

            Assert.That(data.maxHealth, Is.EqualTo(4), "maxHealth");
            BaselineTestSupport.AssertClose(5f, data.speed, "speed");
            BaselineTestSupport.AssertVector2(
                new Vector2(10f, 5f),
                data.knockbackForce,
                "knockbackForce");
            BaselineTestSupport.AssertClose(0.1f, data.knockbackDuration, "knockbackDuration");
        }

        [Test]
        public void FlyingEnemyDataMatchesCapturedBaseline()
        {
            EnemyData data = BaselineTestSupport.LoadAsset<EnemyData>(FlyingDataPath);

            Assert.That(data.maxHealth, Is.EqualTo(2), "maxHealth");
            BaselineTestSupport.AssertClose(6f, data.speed, "speed");
            BaselineTestSupport.AssertVector2(
                new Vector2(10f, 5f),
                data.knockbackForce,
                "knockbackForce");
            BaselineTestSupport.AssertClose(0.1f, data.knockbackDuration, "knockbackDuration");
        }

        [Test]
        public void SmallEnemyDataRemainsCapturedEvenThoughItIsCurrentlyUnreferenced()
        {
            EnemyData data = BaselineTestSupport.LoadAsset<EnemyData>(SmallDataPath);

            Assert.That(data.maxHealth, Is.EqualTo(3), "maxHealth");
            BaselineTestSupport.AssertClose(5f, data.speed, "speed");
            BaselineTestSupport.AssertVector2(
                new Vector2(10f, 5f),
                data.knockbackForce,
                "knockbackForce");
            BaselineTestSupport.AssertClose(0.2f, data.knockbackDuration, "knockbackDuration");
        }

        [Test]
        public void WalkingEnemyPrefabMatchesCapturedBaseline()
        {
            WalkingEnemy enemy = BaselineTestSupport.PrefabComponent<WalkingEnemy>(WalkingPrefabPath);
            WalkingEnemyProfile profile =
                BaselineTestSupport.LoadAsset<WalkingEnemyProfile>(WalkingProfilePath);

            BaselineTestSupport.AssertReference(
                enemy,
                "enemyData",
                BaselineTestSupport.LoadAsset<EnemyData>(GroundDataPath));
            BaselineTestSupport.AssertReference(enemy, "aiProfile", profile);
            Assert.That(
                BaselineTestSupport.Text(enemy, "roomName"),
                Is.EqualTo(string.Empty),
                "roomName");

            BaselineTestSupport.AssertClose(
                6f,
                profile.DetectionRadius,
                "detectionRadius");
            BaselineTestSupport.AssertClose(
                10f,
                profile.LoseRadius,
                "loseRadius");
            BaselineTestSupport.AssertClose(
                0.4f,
                profile.ChaseDirectionDeadzone,
                "chaseDirectionDeadzone");
            BaselineTestSupport.AssertClose(
                0.1f,
                profile.WallCheckDistance,
                "wallCheckDistance");
            BaselineTestSupport.AssertClose(
                0.75f,
                profile.GroundCheckDistance,
                "groundCheckDistance");
            BaselineTestSupport.AssertClose(
                0.15f,
                profile.GroundCheckForwardOffset,
                "groundCheckForwardOffset");
            Assert.That(BaselineTestSupport.Int(enemy, "groundLayers"), Is.EqualTo(1), "groundLayers");
            Assert.That(
                BaselineTestSupport.Int(enemy, "obstacleLayers"),
                Is.EqualTo(1),
                "obstacleLayers");

            AssertWalkingBody(WalkingPrefabPath, RigidbodyType2D.Dynamic, 1f, 1f, 6);
        }

        [Test]
        public void FlyingEnemyPrefabMatchesCapturedBaseline()
        {
            FlyingEnemy enemy = BaselineTestSupport.PrefabComponent<FlyingEnemy>(FlyingPrefabPath);
            FlyingEnemyProfile profile =
                BaselineTestSupport.LoadAsset<FlyingEnemyProfile>(FlyingProfilePath);

            BaselineTestSupport.AssertReference(
                enemy,
                "enemyData",
                BaselineTestSupport.LoadAsset<EnemyData>(FlyingDataPath));
            BaselineTestSupport.AssertReference(enemy, "aiProfile", profile);
            Assert.That(
                BaselineTestSupport.Text(enemy, "roomName"),
                Is.EqualTo(string.Empty),
                "roomName");

            BaselineTestSupport.AssertClose(
                15f,
                profile.DetectionRadius,
                "detectionRadius");
            BaselineTestSupport.AssertClose(
                20f,
                profile.LoseRadius,
                "loseRadius");
            BaselineTestSupport.AssertClose(1f, profile.FlySpeed, "flySpeed");
            BaselineTestSupport.AssertClose(6f, profile.ChaseSpeed, "chaseSpeed");
            BaselineTestSupport.AssertClose(1f, profile.PatrolRadius, "patrolRadius");
            BaselineTestSupport.AssertClose(0.1f, profile.StunDuration, "stunDuration");

            Rigidbody2D body = BaselineTestSupport.LoadPrefab(FlyingPrefabPath)
                .GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Flying enemy Rigidbody2D is missing.");
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic), "bodyType");
            BaselineTestSupport.AssertClose(1f, body.mass, "Rigidbody2D.mass");
            BaselineTestSupport.AssertClose(0f, body.gravityScale, "Rigidbody2D.gravityScale");
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.None), "constraints");
        }

        [Test]
        public void RecallEnemyPrefabMatchesCapturedBaseline()
        {
            RecallEnemy enemy = BaselineTestSupport.PrefabComponent<RecallEnemy>(RecallPrefabPath);

            BaselineTestSupport.AssertReference(
                enemy,
                "enemyData",
                BaselineTestSupport.LoadAsset<EnemyData>(GroundDataPath));
            Assert.That(
                BaselineTestSupport.Text(enemy, "roomName"),
                Is.EqualTo("BaseRoom"),
                "roomName");

            BaselineTestSupport.AssertClose(
                12f,
                BaselineTestSupport.Float(enemy, "preferredDistance"),
                "preferredDistance");
            BaselineTestSupport.AssertClose(
                1f,
                BaselineTestSupport.Float(enemy, "distanceTolerance"),
                "distanceTolerance");
            BaselineTestSupport.AssertClose(
                8f,
                BaselineTestSupport.Float(enemy, "moveSpeed"),
                "moveSpeed");
            BaselineTestSupport.AssertClose(
                30f,
                BaselineTestSupport.Float(enemy, "dodgeJumpForce"),
                "dodgeJumpForce");
            BaselineTestSupport.AssertClose(
                6f,
                BaselineTestSupport.Float(enemy, "dodgeDetectionDistance"),
                "dodgeDetectionDistance");
            BaselineTestSupport.AssertClose(
                0.75f,
                BaselineTestSupport.Float(enemy, "dodgeCooldown"),
                "dodgeCooldown");
            BaselineTestSupport.AssertClose(
                0.98f,
                BaselineTestSupport.Float(enemy, "spearDirectionThreshold"),
                "spearDirectionThreshold");
            BaselineTestSupport.AssertClose(
                2f,
                BaselineTestSupport.Float(enemy, "attackInterval"),
                "attackInterval");
            BaselineTestSupport.AssertClose(
                30f,
                BaselineTestSupport.Float(enemy, "projectileSpeed"),
                "projectileSpeed");
            BaselineTestSupport.AssertClose(
                0.4f,
                BaselineTestSupport.Float(enemy, "attackTelegraphDuration"),
                "attackTelegraphDuration");
            BaselineTestSupport.AssertClose(
                0.05f,
                BaselineTestSupport.Float(enemy, "flashInterval"),
                "flashInterval");

            Rigidbody2D body = BaselineTestSupport.LoadPrefab(RecallPrefabPath)
                .GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Recall enemy Rigidbody2D is missing.");
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic), "bodyType");
            BaselineTestSupport.AssertClose(4f, body.mass, "Rigidbody2D.mass");
            BaselineTestSupport.AssertClose(10f, body.gravityScale, "Rigidbody2D.gravityScale");
            Assert.That(
                body.constraints,
                Is.EqualTo(RigidbodyConstraints2D.FreezeRotation),
                "constraints");
        }

        private static void AssertWalkingBody(
            string prefabPath,
            RigidbodyType2D expectedBodyType,
            float expectedMass,
            float expectedGravityScale,
            int expectedConstraints)
        {
            Rigidbody2D body = BaselineTestSupport.LoadPrefab(prefabPath)
                .GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, prefabPath + " Rigidbody2D is missing.");
            Assert.That(body.bodyType, Is.EqualTo(expectedBodyType), "bodyType");
            BaselineTestSupport.AssertClose(expectedMass, body.mass, "Rigidbody2D.mass");
            BaselineTestSupport.AssertClose(
                expectedGravityScale,
                body.gravityScale,
                "Rigidbody2D.gravityScale");
            Assert.That(
                (int)body.constraints,
                Is.EqualTo(expectedConstraints),
                "Rigidbody2D.constraints");
        }
    }
}
