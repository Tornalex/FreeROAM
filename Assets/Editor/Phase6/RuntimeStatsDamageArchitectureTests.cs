using System.Reflection;
using NUnit.Framework;
using SpearWander.Abilities;
using SpearWander.Boss;
using UnityEditor;

namespace ProjectSpear.Phase6
{
    [TestFixture]
    public class RuntimeStatsDamageArchitectureTests
    {
        [Test]
        public void MutableHealthEssenceAndDeathStateRemainOnRuntimeComponents()
        {
            AssertField(typeof(PlayerHealth), "_currentHealth");
            AssertField(typeof(PlayerHealth), "_iFramesTimer");
            AssertField(typeof(PlayerEssence), "_currentEssence");
            AssertField(typeof(BaseEnemy), "currentHealth");
            AssertField(typeof(BaseEnemy), "isDead");
            AssertField(typeof(Boss), "currentHealth");
            AssertField(typeof(Boss), "isDead");

            AssertNoField(typeof(PlayerStatsData), "_currentHealth");
            AssertNoField(typeof(PlayerStatsData), "_iFramesTimer");
            AssertNoField(typeof(PlayerEssenceData), "_currentEssence");
            AssertNoField(typeof(EnemyData), "currentHealth");
            AssertNoField(typeof(BossData), "currentHealth");
            AssertNoField(typeof(BossData), "isDead");
        }

        [Test]
        public void RuntimeStatOwnersAreInstanceComponentsNotAParallelStatsFramework()
        {
            Assert.IsNull(
                typeof(Player).Assembly.GetType("PlayerRuntimeStats"),
                "No new PlayerRuntimeStats aggregate is needed: the existing components each own distinct runtime state.");
            Assert.IsNull(
                typeof(BaseEnemy).Assembly.GetType("EnemyRuntimeStats"),
                "No generic EnemyRuntimeStats layer is needed for the existing archetypes.");
            Assert.IsNull(
                typeof(Boss).Assembly.GetType("BossRuntimeStats"),
                "Boss runtime state remains on Boss/FreeRoamBoss components.");
        }

        [Test]
        public void AuthoringAssetsStillOwnTheirExistingConfigurationValues()
        {
            PlayerStatsData playerStats =
                AssetDatabase.LoadAssetAtPath<PlayerStatsData>(
                    "Assets/Data/Player Data/Player Stats Data.asset");
            PlayerEssenceData essence =
                AssetDatabase.LoadAssetAtPath<PlayerEssenceData>(
                    "Assets/Data/Player Data/Player Essence Data.asset");
            PlayerHealData heal =
                AssetDatabase.LoadAssetAtPath<PlayerHealData>(
                    "Assets/Data/Player Data/Player Heal Data.asset");
            PlayerCombatData combat =
                AssetDatabase.LoadAssetAtPath<PlayerCombatData>(
                    "Assets/Data/Player Data/Player Combat Data.asset");
            EnemyData enemy =
                AssetDatabase.LoadAssetAtPath<EnemyData>(
                    "Assets/Data/Enemy Data/Ground Enemy Data.asset");
            BossData boss =
                AssetDatabase.LoadAssetAtPath<BossData>("Assets/Data/Boss Data.asset");

            Assert.IsNotNull(playerStats);
            Assert.IsNotNull(essence);
            Assert.IsNotNull(heal);
            Assert.IsNotNull(combat);
            Assert.IsNotNull(enemy);
            Assert.IsNotNull(boss);

            Assert.That(playerStats.maxHealth, Is.EqualTo(5));
            Assert.That(playerStats.iFramesDuration, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(essence.maxEssence, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(heal.essenceCostPerHeal, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(heal.healthPerHeal, Is.EqualTo(2));
            Assert.That(heal.healDuration, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(combat.dashDamage, Is.EqualTo(1));
            Assert.That(combat.pogoDamage, Is.EqualTo(1));
            Assert.That(enemy.maxHealth, Is.GreaterThan(0));
            Assert.That(boss.maxHealth, Is.EqualTo(10));
        }

        [Test]
        public void DamageContractsPreservePlayerEnemyAndBossResponsibilities()
        {
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(PlayerHealth)));
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(BaseEnemy)));
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(Boss)));
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(WeakPoint)));
            Assert.IsTrue(typeof(IPlayerDamageReceiver).IsAssignableFrom(typeof(WalkingEnemy)));
            Assert.IsFalse(
                typeof(IPlayerDamageReceiver).IsAssignableFrom(typeof(PlayerHealth)),
                "PlayerHealth receives damage; it is not a player-attack damage source.");

            MethodInfo damageMethod = typeof(IDamageable).GetMethod("TakeDamage");
            Assert.IsNotNull(damageMethod);
            Assert.That(damageMethod.GetParameters().Length, Is.EqualTo(3));
            Assert.IsNotNull(
                typeof(DamageableExtensions).GetMethod("TakeDamageFromPlayer"),
                "Player attack attribution must continue to use the existing targeted extension.");
        }

        private static void AssertField(System.Type type, string name)
        {
            Assert.IsNotNull(
                FindField(type, name),
                type.Name + " must own runtime state field " + name + ".");
        }

        private static void AssertNoField(System.Type type, string name)
        {
            Assert.IsNull(
                FindField(type, name),
                type.Name + " must not store runtime state field " + name + ".");
        }

        private static FieldInfo FindField(System.Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            return null;
        }
    }
}
