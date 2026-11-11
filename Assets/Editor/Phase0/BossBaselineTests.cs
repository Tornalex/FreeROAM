using NUnit.Framework;
using SpearWander.Boss;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Captures the current BossData asset, the FreeRoamBoss prefab tuning, and
    /// the boss helper prefabs. These values are the regression baseline for the
    /// boss data extraction; they must not change without a deliberate balance
    /// change. After the Phase 3 extraction the prefab tuning lives in the
    /// BossAIProfile and BossAttackData assets, so the same values are asserted
    /// through those assets.
    /// </summary>
    [TestFixture]
    public class BossBaselineTests
    {
        private const string BossDataPath = "Assets/Data/Boss Data.asset";
        private const string BossProfilePath =
            "Assets/Data/Boss Data/FreeRoam Boss AI Profile.asset";
        private const string TongueDataPath =
            "Assets/Data/Boss Data/Tongue Attack Data.asset";
        private const string SporeDataPath =
            "Assets/Data/Boss Data/Spore Attack Data.asset";
        private const string BossPrefabPath = "Assets/Prefabs/Enemies/FreeRoam Boss.prefab";
        private const string SporePrefabPath = "Assets/Prefabs/Enemies/Spore Cloud.prefab";

        [Test]
        public void BossDataAssetMatchesCapturedBaseline()
        {
            BossData data = BaselineTestSupport.LoadAsset<BossData>(BossDataPath);

            Assert.That(data.maxHealth, Is.EqualTo(10), "maxHealth");
            BaselineTestSupport.AssertClose(360f, data.rotationSpeed, "rotationSpeed");
            BaselineTestSupport.AssertClose(2f, data.moveSpeed, "moveSpeed");
            BaselineTestSupport.AssertClose(10f, data.chaseDistance, "chaseDistance");

            BaselineTestSupport.AssertClose(2f, data.meleeAttackRange, "meleeAttackRange");
            BaselineTestSupport.AssertClose(
                2f,
                data.meleeAttackCooldown,
                "meleeAttackCooldown");
            Assert.That(data.meleeDamage, Is.EqualTo(2), "meleeDamage");
            BaselineTestSupport.AssertClose(
                15f,
                data.meleeKnockbackForce,
                "meleeKnockbackForce");

            BaselineTestSupport.AssertClose(8f, data.rangedAttackRange, "rangedAttackRange");
            BaselineTestSupport.AssertClose(
                3f,
                data.rangedAttackCooldown,
                "rangedAttackCooldown");
            Assert.That(data.rangedDamage, Is.EqualTo(1), "rangedDamage");
            Assert.IsNull(data.projectilePrefab, "projectilePrefab");
            BaselineTestSupport.AssertClose(10f, data.projectileSpeed, "projectileSpeed");

            BaselineTestSupport.AssertClose(4f, data.antiAirRange, "antiAirRange");
            BaselineTestSupport.AssertClose(2.5f, data.antiAirCooldown, "antiAirCooldown");
            Assert.That(data.antiAirDamage, Is.EqualTo(2), "antiAirDamage");
            BaselineTestSupport.AssertClose(
                20f,
                data.antiAirVerticalForce,
                "antiAirVerticalForce");

            BaselineTestSupport.AssertClose(
                5f,
                data.weakPointDamageMultiplier,
                "weakPointDamageMultiplier");
            BaselineTestSupport.AssertVector2(
                new Vector2(15f, 10f),
                data.knockbackForce,
                "knockbackForce");
            BaselineTestSupport.AssertClose(0.1f, data.knockbackDuration, "knockbackDuration");

            Assert.That(data.hitVfxType, Is.EqualTo(VFXType.HitSpear), "hitVfxType");
            Assert.That(data.hitSfxType, Is.EqualTo(SFXType.EnemyPierced), "hitSfxType");
            Assert.That(data.deathVfxType, Is.EqualTo(VFXType.EnemyDeath), "deathVfxType");
            Assert.That(data.deathSfxType, Is.EqualTo(SFXType.EnemyPierced), "deathSfxType");
        }

        [Test]
        public void FreeRoamBossPrefabMatchesCapturedBaseline()
        {
            FreeRoamBoss boss = BaselineTestSupport.PrefabComponent<FreeRoamBoss>(BossPrefabPath);
            BossAIProfile aiProfile =
                BaselineTestSupport.LoadAsset<BossAIProfile>(BossProfilePath);
            TongueAttackData tongue =
                BaselineTestSupport.LoadAsset<TongueAttackData>(TongueDataPath);
            SporeAttackData spores =
                BaselineTestSupport.LoadAsset<SporeAttackData>(SporeDataPath);

            BaselineTestSupport.AssertReference(
                boss,
                "bossData",
                BaselineTestSupport.LoadAsset<BossData>(BossDataPath));
            BaselineTestSupport.AssertReference(boss, "aiProfile", aiProfile);
            BaselineTestSupport.AssertReference(boss, "tongueAttackData", tongue);
            BaselineTestSupport.AssertReference(boss, "sporeAttackData", spores);
            Assert.IsNotNull(
                BaselineTestSupport.ObjectReference(boss, "tongueOrigin"),
                "tongueOrigin must remain assigned.");
            Assert.IsNotNull(
                BaselineTestSupport.ObjectReference(boss, "groundCheckPoint"),
                "groundCheckPoint must remain assigned.");
            Assert.IsNotNull(
                BaselineTestSupport.ObjectReference(boss, "sporePrefab"),
                "sporePrefab must remain assigned.");
            Assert.IsFalse(
                BaselineTestSupport.Bool(boss, "debugMode"),
                "debugMode baseline is false.");

            BaselineTestSupport.AssertClose(
                14f,
                aiProfile.DetectionRange,
                "detectionRange");
            Assert.That(aiProfile.GroundLayers.value, Is.EqualTo(1), "groundLayers");
            Assert.That(aiProfile.PlayerLayers.value, Is.EqualTo(8), "playerLayers");
            BaselineTestSupport.AssertClose(
                0.22f,
                aiProfile.GroundCheckRadius,
                "groundCheckRadius");
            Assert.That(
                aiProfile.TongueObstructionLayers.value,
                Is.EqualTo(1),
                "tongueObstructionLayers");
            Assert.That(
                aiProfile.SporeObstructionLayers.value,
                Is.EqualTo(1),
                "sporeObstructionLayers");
            Assert.That(
                aiProfile.DashJumpObstructionLayers.value,
                Is.EqualTo(1),
                "dashJumpObstructionLayers");

            BaselineTestSupport.AssertClose(
                20f,
                aiProfile.JumpUpVelocity,
                "jumpUpVelocity");
            BaselineTestSupport.AssertClose(
                4f,
                aiProfile.JumpDistance,
                "jumpDistance");
            BaselineTestSupport.AssertClose(
                7f,
                aiProfile.JumpMaxHorizontalSpeed,
                "jumpMaxHorizontalSpeed");
            BaselineTestSupport.AssertClose(
                0.35f,
                aiProfile.LandingRecovery,
                "landingRecovery");
            BaselineTestSupport.AssertClose(
                3.5f,
                aiProfile.PreferredDistance,
                "preferredDistance");
            BaselineTestSupport.AssertClose(
                2f,
                aiProfile.RetreatDistance,
                "retreatDistance");
            BaselineTestSupport.AssertClose(
                0.5f,
                aiProfile.DecisionInterval,
                "decisionInterval");
            BaselineTestSupport.AssertClose(
                0.15f,
                aiProfile.DecisionIntervalJitter,
                "decisionIntervalJitter");
            BaselineTestSupport.AssertClose(
                3f,
                aiProfile.MaxAirborneDuration,
                "maxAirborneDuration");
            BaselineTestSupport.AssertClose(
                1f,
                aiProfile.RecoveryCooldown,
                "recoveryCooldown");

            BaselineTestSupport.AssertClose(
                3f,
                aiProfile.JumpTowardWeight,
                "jumpTowardWeight");
            BaselineTestSupport.AssertClose(
                1.5f,
                aiProfile.JumpAwayWeight,
                "jumpAwayWeight");
            BaselineTestSupport.AssertClose(
                1.5f,
                aiProfile.DashJumpWeight,
                "dashJumpWeight");
            BaselineTestSupport.AssertClose(
                2f,
                aiProfile.TongueWeight,
                "tongueWeight");
            BaselineTestSupport.AssertClose(
                2f,
                aiProfile.SporeWeight,
                "sporeWeight");
            BaselineTestSupport.AssertClose(
                0.35f,
                aiProfile.RepeatWeightMultiplier,
                "repeatWeightMultiplier");
            BaselineTestSupport.AssertClose(
                0.4f,
                aiProfile.AttackAfterAttackWeightMultiplier,
                "attackAfterAttackWeightMultiplier");

            BaselineTestSupport.AssertClose(
                0.45f,
                tongue.WindupTime,
                "tongueWindupTime");
            BaselineTestSupport.AssertClose(
                0.12f,
                tongue.ExtendTime,
                "tongueExtendTime");
            BaselineTestSupport.AssertClose(
                0.22f,
                tongue.RetractTime,
                "tongueRetractTime");
            BaselineTestSupport.AssertClose(
                0.3f,
                tongue.RecoveryTime,
                "tongueRecoveryTime");
            BaselineTestSupport.AssertClose(
                6f,
                tongue.Range,
                "tongueRange");
            BaselineTestSupport.AssertClose(
                0.4f,
                tongue.Width,
                "tongueWidth");
            Assert.That(tongue.Damage, Is.EqualTo(2), "tongueDamage");
            BaselineTestSupport.AssertClose(
                2.5f,
                tongue.Cooldown,
                "tongueCooldown");
            BaselineTestSupport.AssertColor(
                new Color(0.9f, 0.3f, 0.4f, 1f),
                tongue.TongueColor,
                "tongueColor");
            Assert.That(tongue.SortingOrder, Is.EqualTo(10), "tongueSortingOrder");

            BaselineTestSupport.AssertClose(
                0.6f,
                spores.WindupTime,
                "sporeWindupTime");
            BaselineTestSupport.AssertClose(
                0.35f,
                spores.RecoveryTime,
                "sporeRecoveryTime");
            BaselineTestSupport.AssertClose(
                6f,
                spores.Lifetime,
                "sporeLifetime");
            BaselineTestSupport.AssertClose(
                0.9f,
                spores.Radius,
                "sporeRadius");
            Assert.That(spores.Damage, Is.EqualTo(1), "sporeDamage");
            BaselineTestSupport.AssertClose(
                4f,
                spores.Cooldown,
                "sporeCooldown");
            Assert.That(spores.MaxActiveSpores, Is.EqualTo(3), "maxActiveSpores");
            BaselineTestSupport.AssertVector2(
                new Vector2(0.35f, 0f),
                spores.Drift,
                "sporeDrift");
            BaselineTestSupport.AssertClose(
                9f,
                spores.Range,
                "sporeRange");

            BaselineTestSupport.AssertClose(
                7f,
                aiProfile.DashJumpMinDistance,
                "dashJumpMinDistance");
            BaselineTestSupport.AssertClose(
                18f,
                aiProfile.DashJumpHorizontalSpeed,
                "dashJumpHorizontalSpeed");
            BaselineTestSupport.AssertClose(
                10f,
                aiProfile.DashJumpUpVelocity,
                "dashJumpUpVelocity");
            BaselineTestSupport.AssertClose(
                1f,
                aiProfile.DashJumpLandingOffset,
                "dashJumpLandingOffset");
            BaselineTestSupport.AssertClose(
                0.45f,
                aiProfile.DashJumpPreparationTime,
                "dashJumpPreparationTime");
            BaselineTestSupport.AssertClose(
                0.45f,
                aiProfile.DashJumpRecoveryTime,
                "dashJumpRecoveryTime");
            BaselineTestSupport.AssertClose(
                5f,
                aiProfile.DashJumpCooldown,
                "dashJumpCooldown");
            BaselineTestSupport.AssertClose(
                2f,
                aiProfile.DashJumpMaxAirborneDuration,
                "dashJumpMaxAirborneDuration");

            Rigidbody2D body = BaselineTestSupport.LoadPrefab(BossPrefabPath)
                .GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Boss Rigidbody2D is missing.");
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic), "bodyType");
            BaselineTestSupport.AssertClose(1f, body.mass, "Rigidbody2D.mass");
            BaselineTestSupport.AssertClose(4f, body.gravityScale, "Rigidbody2D.gravityScale");
            Assert.That(
                body.constraints,
                Is.EqualTo(RigidbodyConstraints2D.FreezeRotation),
                "Rigidbody2D.constraints");
        }

        [Test]
        public void BossWeakPointPrefabMatchesCapturedBaseline()
        {
            WeakPoint weakPoint =
                BaselineTestSupport.PrefabChildComponent<WeakPoint>(BossPrefabPath);

            BaselineTestSupport.AssertColor(
                new Color(1f, 0f, 0f, 1f),
                BaselineTestSupport.ColorValue(weakPoint, "baseColor"),
                "baseColor");
            BaselineTestSupport.AssertColor(
                Color.white,
                BaselineTestSupport.ColorValue(weakPoint, "hitFlashColor"),
                "hitFlashColor");
            BaselineTestSupport.AssertClose(
                3f,
                BaselineTestSupport.Float(weakPoint, "pulseSpeed"),
                "pulseSpeed");
            BaselineTestSupport.AssertClose(
                0.2f,
                BaselineTestSupport.Float(weakPoint, "pulseAmount"),
                "pulseAmount");
            BaselineTestSupport.AssertClose(
                0.1f,
                BaselineTestSupport.Float(weakPoint, "hitFlashDuration"),
                "hitFlashDuration");
            BaselineTestSupport.AssertClose(
                5f,
                BaselineTestSupport.Float(weakPoint, "damageMultiplier"),
                "damageMultiplier");
            Assert.IsFalse(
                BaselineTestSupport.Bool(weakPoint, "invulnerable"),
                "invulnerable baseline is false.");
        }

        [Test]
        public void SporeCloudPrefabMatchesCapturedBaseline()
        {
            SporeCloud cloud =
                BaselineTestSupport.PrefabComponent<SporeCloud>(SporePrefabPath);

            Assert.That(BaselineTestSupport.Int(cloud, "damage"), Is.EqualTo(1), "damage");
            BaselineTestSupport.AssertClose(
                6f,
                BaselineTestSupport.Float(cloud, "lifetime"),
                "lifetime");
            BaselineTestSupport.AssertClose(0.9f, BaselineTestSupport.Float(cloud, "radius"), "radius");
            BaselineTestSupport.AssertVector2(
                Vector2.zero,
                BaselineTestSupport.Vector2Value(cloud, "driftVelocity"),
                "driftVelocity");
            BaselineTestSupport.AssertClose(
                0f,
                BaselineTestSupport.Float(cloud, "activationDelay"),
                "activationDelay");
            BaselineTestSupport.AssertColor(
                new Color(0.55f, 0.85f, 0.45f, 0.75f),
                BaselineTestSupport.ColorValue(cloud, "cloudColor"),
                "cloudColor");
            Assert.That(
                BaselineTestSupport.Int(cloud, "sortingOrder"),
                Is.EqualTo(5),
                "sortingOrder");
            BaselineTestSupport.AssertClose(
                2.5f,
                BaselineTestSupport.Float(cloud, "pulseSpeed"),
                "pulseSpeed");
            BaselineTestSupport.AssertClose(
                0.06f,
                BaselineTestSupport.Float(cloud, "pulseAmount"),
                "pulseAmount");
            BaselineTestSupport.AssertClose(
                1.25f,
                BaselineTestSupport.Float(cloud, "fadeDuration"),
                "fadeDuration");
            BaselineTestSupport.AssertClose(
                0.18f,
                BaselineTestSupport.Float(cloud, "warningAlpha"),
                "warningAlpha");
            Assert.That(
                BaselineTestSupport.Int(cloud, "playerLayers"),
                Is.EqualTo(8),
                "playerLayers");
            Assert.That(
                BaselineTestSupport.Int(cloud, "obstructionLayers"),
                Is.EqualTo(1),
                "obstructionLayers");
            BaselineTestSupport.AssertClose(
                0.4f,
                BaselineTestSupport.Float(cloud, "damageCooldown"),
                "damageCooldown");
        }
    }
}
