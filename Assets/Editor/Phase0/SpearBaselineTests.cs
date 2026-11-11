using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Captures the spear feel values from the shared SpearConfig asset plus
    /// the Player combat wiring. These values are the regression baseline for
    /// the Phase 5 configuration extraction.
    /// </summary>
    [TestFixture]
    public class SpearBaselineTests
    {
        private const string SpearPrefabPath = "Assets/Prefabs/Utilities/SpearV2.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/Utilities/Player.prefab";
        private const string SpearConfigPath = "Assets/Data/Spear Data/Spear Config.asset";

        [Test]
        public void SpearPrefabSerializedTuningMatchesCapturedBaseline()
        {
            Spear spear = BaselineTestSupport.PrefabComponent<Spear>(SpearPrefabPath);
            SpearConfig config = BaselineTestSupport.LoadAsset<SpearConfig>(SpearConfigPath);

            BaselineTestSupport.AssertReference(spear, "spearConfig", config);

            BaselineTestSupport.AssertClose(
                10f,
                config.EnemyBounceSpeed,
                "enemyBounceSpeed");
            BaselineTestSupport.AssertClose(
                90f,
                config.BounceAngle,
                "bounceAngle");
            Assert.That(
                BaselineTestSupport.Text(spear, "cameraBoundsTag"),
                Is.EqualTo("CameraBounds"),
                "cameraBoundsTag");
            Assert.That(
                BaselineTestSupport.Text(spear, "directionChangerTag"),
                Is.EqualTo("EnemyDirectionChanger"),
                "directionChangerTag");
            BaselineTestSupport.AssertClose(
                1.5f,
                config.TipRaycastLength,
                "tipRaycastLength");
            BaselineTestSupport.AssertClose(
                0.15f,
                config.EmbedDepth,
                "effective embedDepth (the prefab's prior zero was clamped in Awake)");
            BaselineTestSupport.AssertClose(
                1f,
                config.ReturnDuration,
                "returnDuration");
            BaselineTestSupport.AssertClose(
                120f,
                config.MaxReturnSpeed,
                "maxReturnSpeed");
            BaselineTestSupport.AssertClose(
                0.75f,
                config.CatchDistance,
                "catchDistance");
            BaselineTestSupport.AssertVector2(
                new Vector2(-0.8f, 0f),
                config.RopeAttachOffset,
                "ropeAttachOffset");
            BaselineTestSupport.AssertClose(
                0.1f,
                config.RopeWidth,
                "ropeWidth");
            Assert.IsNotNull(
                config.RopeMaterial,
                "ropeMaterial must remain assigned.");

            SerializedObject serializedSpear = new SerializedObject(spear);
            foreach (string migratedField in new[]
                     {
                         "enemyBounceSpeed", "bounceAngle", "tipRaycastLength", "embedDepth",
                         "returnCurve", "returnDuration", "maxReturnSpeed", "catchDistance",
                         "ropeAttachOffset", "ropeWidth", "ropeMaterial"
                     })
            {
                Assert.IsNull(
                    serializedSpear.FindProperty(migratedField),
                    "Migrated tuning field remains serialized on Spear: " + migratedField);
            }
        }

        [Test]
        public void SpearReturnCurveMatchesCapturedBaseline()
        {
            Spear spear = BaselineTestSupport.PrefabComponent<Spear>(SpearPrefabPath);
            SpearConfig config = BaselineTestSupport.LoadAsset<SpearConfig>(SpearConfigPath);
            SerializedObject serializedConfig = new SerializedObject(config);
            AnimationCurve curve = serializedConfig.FindProperty("returnCurve").animationCurveValue;

            Assert.IsNotNull(curve, "returnCurve is missing.");
            Assert.That(curve.length, Is.EqualTo(2), "returnCurve key count");
            Assert.That(curve.keys[0].time, Is.EqualTo(0f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[0].value, Is.EqualTo(0f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[0].inTangent, Is.EqualTo(0f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[0].outTangent, Is.EqualTo(0f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[1].time, Is.EqualTo(1f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[1].value, Is.EqualTo(1f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[1].inTangent, Is.EqualTo(2f).Within(BaselineTestSupport.Tolerance));
            Assert.That(curve.keys[1].outTangent, Is.EqualTo(2f).Within(BaselineTestSupport.Tolerance));
            SerializedObject serializedSpear = new SerializedObject(spear);
            Assert.That(
                serializedSpear.FindProperty("spearConfig").objectReferenceValue,
                Is.SameAs(config),
                "The runtime spear does not use the configured return curve asset.");
        }

        [Test]
        public void SpearPrefabPhysicsMatchesCapturedBaseline()
        {
            GameObject prefab = BaselineTestSupport.LoadPrefab(SpearPrefabPath);
            SpearConfig config = BaselineTestSupport.LoadAsset<SpearConfig>(SpearConfigPath);

            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Spear Rigidbody2D is missing.");
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic), "bodyType");
            // Rigidbody2D values are initialized from SpearConfig in Awake;
            // neutral serialized values prevent a second authoring source.
            BaselineTestSupport.AssertClose(1f, body.mass, "Rigidbody2D.mass serialized fallback");
            BaselineTestSupport.AssertClose(1f, body.gravityScale, "Rigidbody2D.gravityScale serialized fallback");
            BaselineTestSupport.AssertClose(0f, body.linearDamping, "Rigidbody2D.linearDamping");
            BaselineTestSupport.AssertClose(0f, body.angularDamping, "Rigidbody2D.angularDamping");
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.None), "constraints");
            Assert.That(
                body.collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode2D.Continuous),
                "collisionDetectionMode");
            BaselineTestSupport.AssertClose(2f, config.RigidbodyMass, "effective Rigidbody2D.mass");
            BaselineTestSupport.AssertClose(3f, config.FlightGravityScale, "effective Rigidbody2D.gravityScale");
            BaselineTestSupport.AssertClose(0f, config.FlightLinearDamping, "effective Rigidbody2D.linearDamping");

            BoxCollider2D collider = prefab.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(collider, "Spear BoxCollider2D is missing.");
            BaselineTestSupport.AssertClose(
                0.01820147f,
                collider.offset.x,
                "collider.offset.x");
            BaselineTestSupport.AssertClose(
                -0.029523931f,
                collider.offset.y,
                "collider.offset.y");
            BaselineTestSupport.AssertClose(2.3170688f, collider.size.x, "collider.size.x");
            BaselineTestSupport.AssertClose(0.0865013f, collider.size.y, "collider.size.y");
        }

        [Test]
        public void PlayerCombatPrefabWiringMatchesCapturedBaseline()
        {
            PlayerCombat combat = BaselineTestSupport.PrefabComponent<PlayerCombat>(PlayerPrefabPath);

            Assert.IsNotNull(
                BaselineTestSupport.ObjectReference(combat, "spearPrefab"),
                "spearPrefab must remain assigned.");
            Assert.IsNotNull(
                BaselineTestSupport.ObjectReference(combat, "firePoint"),
                "firePoint must remain assigned.");
            Assert.IsNull(
                BaselineTestSupport.ObjectReference(combat, "_trajectoryPredictor"),
                "_trajectoryPredictor is intentionally unassigned in the prefab; " +
                "PlayerCombat disables the trajectory preview when it is null.");
            Assert.IsNull(
                BaselineTestSupport.ObjectReference(combat, "_trajectoryRenderer"),
                "_trajectoryRenderer is intentionally unassigned in the prefab; " +
                "PlayerCombat disables the trajectory preview when it is null.");
        }
    }
}
