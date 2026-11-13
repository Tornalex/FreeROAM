using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Captures the current player tuning that already lives in ScriptableObjects
    /// and the Player prefab. Values intentionally mirror the serialized assets;
    /// update the expectation only when a balance change is deliberate.
    /// </summary>
    [TestFixture]
    public class PlayerBaselineTests
    {
        private const string StatsPath = "Assets/Data/Player Data/Player Stats Data.asset";
        private const string CombatPath = "Assets/Data/Player Data/Player Combat Data.asset";
        private const string DashPath = "Assets/Data/Player Data/Player Dash Data.asset";
        private const string PogoPath = "Assets/Data/Player Data/Player Pogo Data.asset";
        private const string RopePath = "Assets/Data/Player Data/Player Rope Data.asset";
        private const string EssencePath = "Assets/Data/Player Data/Player Essence Data.asset";
        private const string HealPath = "Assets/Data/Player Data/Player Heal Data.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Utilities/Player.prefab";

        [Test]
        public void PlayerStatsDataMatchesCapturedBaseline()
        {
            PlayerStatsData data = BaselineTestSupport.LoadAsset<PlayerStatsData>(StatsPath);

            BaselineTestSupport.AssertClose(10f, data.walkSpeed, "walkSpeed");
            BaselineTestSupport.AssertClose(16f, data.runSpeed, "runSpeed");
            BaselineTestSupport.AssertClose(
                0.1f,
                data.knockbackInterruptThreshold,
                "knockbackInterruptThreshold");
            BaselineTestSupport.AssertClose(
                0.1f,
                data.moveInputThreshold,
                "moveInputThreshold");
            BaselineTestSupport.AssertClose(24f, data.jumpForce, "jumpForce");
            BaselineTestSupport.AssertClose(0.2f, data.jumpCutMultiplier, "jumpCutMultiplier");
            BaselineTestSupport.AssertClose(0.1f, data.jumpBufferTime, "jumpBufferTime");
            BaselineTestSupport.AssertClose(0.05f, data.coyoteTime, "coyoteTime");
            Assert.That(data.maxHealth, Is.EqualTo(5), "maxHealth");
            BaselineTestSupport.AssertClose(0.75f, data.iFramesDuration, "iFramesDuration");
            BaselineTestSupport.AssertVector2(new Vector2(15f, 10f), data.damageForce, "damageForce");
            BaselineTestSupport.AssertClose(
                0.3f,
                data.damageKnockbackDuration,
                "damageKnockbackDuration");
            Assert.That(data.contactDamage, Is.EqualTo(1), "contactDamage");
            BaselineTestSupport.AssertClose(
                0.05f,
                data.iFramesBlinkInterval,
                "iFramesBlinkInterval");
            BaselineTestSupport.AssertClose(
                0.2f,
                data.iFramesBlinkMinAlpha,
                "iFramesBlinkMinAlpha");
            BaselineTestSupport.AssertClose(
                0.9f,
                data.groundNormalThreshold,
                "groundNormalThreshold");
            BaselineTestSupport.AssertClose(
                0.2f,
                data.groundPointOffset,
                "groundPointOffset");
        }

        [Test]
        public void PlayerCombatDataMatchesCapturedBaseline()
        {
            PlayerCombatData data = BaselineTestSupport.LoadAsset<PlayerCombatData>(CombatPath);

            BaselineTestSupport.AssertClose(100f, data.shootForce, "shootForce");
            BaselineTestSupport.AssertClose(0f, data.throwCooldown, "throwCooldown");
            Assert.That(data.spearImpactDamage, Is.EqualTo(1), "spearImpactDamage");
            Assert.That(data.spearRecallDamage, Is.EqualTo(1), "spearRecallDamage");
            Assert.That(data.dashDamage, Is.EqualTo(1), "dashDamage");
            Assert.That(data.pogoDamage, Is.EqualTo(1), "pogoDamage");
            BaselineTestSupport.AssertClose(0.15f, data.aimDeadzone, "aimDeadzone");
            BaselineTestSupport.AssertClose(12f, data.aimSmoothing, "aimSmoothing");
            BaselineTestSupport.AssertClose(
                0.1f,
                data.aimInputThreshold,
                "aimInputThreshold");
        }

        [Test]
        public void PlayerDashDataMatchesCapturedBaseline()
        {
            PlayerDashData data = BaselineTestSupport.LoadAsset<PlayerDashData>(DashPath);

            BaselineTestSupport.AssertClose(18f, data.dashSpeed, "dashSpeed");
            BaselineTestSupport.AssertClose(0.3f, data.dashDuration, "dashDuration");
            BaselineTestSupport.AssertClose(0.2f, data.dashCooldown, "dashCooldown");
            BaselineTestSupport.AssertClose(
                0.05f,
                data.postDashInvincibility,
                "postDashInvincibility");
            BaselineTestSupport.AssertVector2(
                new Vector2(20f, 10f),
                data.knockbackForce,
                "knockbackForce");
            BaselineTestSupport.AssertClose(0.3f, data.knockbackDuration, "knockbackDuration");
            BaselineTestSupport.AssertClose(
                0.05f,
                data.dashReadyBlinkInterval,
                "dashReadyBlinkInterval");
            Assert.That(
                data.dashReadyBlinkCount,
                Is.EqualTo(3),
                "dashReadyBlinkCount");
            BaselineTestSupport.AssertClose(
                0.1f,
                data.knockbackInterruptThreshold,
                "knockbackInterruptThreshold");
        }

        [Test]
        public void PlayerPogoDataMatchesCapturedBaseline()
        {
            PlayerPogoData data = BaselineTestSupport.LoadAsset<PlayerPogoData>(PogoPath);

            BaselineTestSupport.AssertClose(25f, data.plungeSpeed, "plungeSpeed");
            BaselineTestSupport.AssertClose(24f, data.bounceForce, "bounceForce");
            BaselineTestSupport.AssertClose(
                0.05f,
                data.postPogoInvincibility,
                "postPogoInvincibility");
            BaselineTestSupport.AssertClose(0.15f, data.pogoStunDuration, "pogoStunDuration");
            BaselineTestSupport.AssertClose(0.2f, data.bounceOffset, "bounceOffset");
        }

        [Test]
        public void PlayerRopeDataMatchesCapturedBaseline()
        {
            PlayerRopeData data = BaselineTestSupport.LoadAsset<PlayerRopeData>(RopePath);

            BaselineTestSupport.AssertClose(10f, data.climbSpeed, "climbSpeed");
            BaselineTestSupport.AssertClose(12f, data.dismountJumpForce, "dismountJumpForce");
            BaselineTestSupport.AssertClose(0.5f, data.dismountCooldown, "dismountCooldown");
            BaselineTestSupport.AssertClose(5f, data.ropeLength, "ropeLength");
            BaselineTestSupport.AssertClose(
                0.5f,
                data.horizontalDismountThreshold,
                "horizontalDismountThreshold");
            BaselineTestSupport.AssertClose(
                0.1f,
                data.verticalInputThreshold,
                "verticalInputThreshold");
            BaselineTestSupport.AssertClose(
                0.15f,
                data.autoDismountOffset,
                "autoDismountOffset");
            BaselineTestSupport.AssertClose(
                0.1f,
                data.climbInputThreshold,
                "climbInputThreshold");
        }

        [Test]
        public void PlayerEssenceDataMatchesCapturedBaseline()
        {
            PlayerEssenceData data = BaselineTestSupport.LoadAsset<PlayerEssenceData>(EssencePath);

            BaselineTestSupport.AssertClose(100f, data.maxEssence, "maxEssence");
            BaselineTestSupport.AssertClose(10f, data.essencePerBaseCatch, "essencePerBaseCatch");
            BaselineTestSupport.AssertClose(
                40f,
                data.essencePerPerfectCatch,
                "essencePerPerfectCatch");
        }

        [Test]
        public void PlayerHealDataMatchesCapturedBaseline()
        {
            PlayerHealData data = BaselineTestSupport.LoadAsset<PlayerHealData>(HealPath);

            BaselineTestSupport.AssertClose(1f, data.healDuration, "healDuration");
            Assert.That(data.healthPerHeal, Is.EqualTo(2), "healthPerHeal");
            BaselineTestSupport.AssertClose(
                50f,
                data.essenceCostPerHeal,
                "essenceCostPerHeal");
        }

        [Test]
        public void PlayerPrefabReferencesTheCapturedDataAssets()
        {
            Player player = BaselineTestSupport.PrefabComponent<Player>(PlayerPrefabPath);

            BaselineTestSupport.AssertReference(
                player,
                "playerStats",
                BaselineTestSupport.LoadAsset<PlayerStatsData>(StatsPath));
            BaselineTestSupport.AssertReference(
                player,
                "combatStats",
                BaselineTestSupport.LoadAsset<PlayerCombatData>(CombatPath));
            BaselineTestSupport.AssertReference(
                player,
                "essenceStats",
                BaselineTestSupport.LoadAsset<PlayerEssenceData>(EssencePath));
            BaselineTestSupport.AssertReference(
                player,
                "healStats",
                BaselineTestSupport.LoadAsset<PlayerHealData>(HealPath));
            BaselineTestSupport.AssertReference(
                player,
                "dashStats",
                BaselineTestSupport.LoadAsset<PlayerDashData>(DashPath));
            BaselineTestSupport.AssertReference(
                player,
                "ropeStats",
                BaselineTestSupport.LoadAsset<PlayerRopeData>(RopePath));
            BaselineTestSupport.AssertReference(
                player,
                "pogoStats",
                BaselineTestSupport.LoadAsset<PlayerPogoData>(PogoPath));
        }

        [Test]
        public void PlayerPrefabRigidbodyAndBodyColliderMatchBaseline()
        {
            Player player = BaselineTestSupport.PrefabComponent<Player>(PlayerPrefabPath);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Player Rigidbody2D is missing.");
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic), "bodyType");
            BaselineTestSupport.AssertClose(2f, body.mass, "Rigidbody2D.mass");
            BaselineTestSupport.AssertClose(
                5f,
                body.gravityScale,
                "Rigidbody2D.gravityScale");
            BaselineTestSupport.AssertClose(
                0.05f,
                body.angularDamping,
                "Rigidbody2D.angularDamping");
            Assert.That(
                body.constraints,
                Is.EqualTo(RigidbodyConstraints2D.FreezeRotation),
                "Rigidbody2D.constraints");
            Assert.That(
                body.collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode2D.Continuous),
                "Rigidbody2D.collisionDetectionMode");

            BoxCollider2D collider = player.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(collider, "Player body BoxCollider2D is missing.");
            BaselineTestSupport.AssertClose(0.1f, collider.offset.x, "bodyCollider.offset.x");
            BaselineTestSupport.AssertClose(
                0.44911927f,
                collider.offset.y,
                "bodyCollider.offset.y");
            BaselineTestSupport.AssertClose(0.65f, collider.size.x, "bodyCollider.size.x");
            BaselineTestSupport.AssertClose(
                1.6517614f,
                collider.size.y,
                "bodyCollider.size.y");
        }

        [Test]
        public void PlayerDataAssetsExplicitlySerializeMigratedParameters()
        {
            // The runtime asset loader applies C# field initializers for fields
            // that are missing from the YAML. These assertions read the asset
            // files themselves, so they prove the values are explicitly
            // authored instead of silently inherited from code defaults.
            AssertAssetContains(
                StatsPath,
                "knockbackInterruptThreshold: 0.1",
                "moveInputThreshold: 0.1",
                "contactDamage: 1",
                "iFramesBlinkInterval: 0.05",
                "iFramesBlinkMinAlpha: 0.2",
                "groundNormalThreshold: 0.9",
                "groundPointOffset: 0.2");

            AssertAssetContains(
                CombatPath,
                "aimDeadzone: 0.15",
                "aimSmoothing: 12",
                "aimInputThreshold: 0.1");

            AssertAssetContains(
                DashPath,
                "dashReadyBlinkInterval: 0.05",
                "dashReadyBlinkCount: 3",
                "knockbackInterruptThreshold: 0.1");

            AssertAssetContains(
                PogoPath,
                "bounceOffset: 0.2");

            AssertAssetContains(
                RopePath,
                "horizontalDismountThreshold: 0.5",
                "verticalInputThreshold: 0.1",
                "autoDismountOffset: 0.15",
                "climbInputThreshold: 0.1");

            AssertAssetContains(
                EssencePath,
                "essencePerPerfectCatch: 40");
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
    }
}
