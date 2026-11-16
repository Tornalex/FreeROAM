using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Phase 6 integration coverage for per-entity health/Essence state and the
    /// existing player-damage routes. Runtime components are invoked directly
    /// where physics callbacks are not needed; projectile callbacks use the
    /// actual receiver methods.
    /// </summary>
    public class RuntimeStatsDamagePlayModeTests : PlayModeTestBase
    {
        private const string PlayerTypeName = "Player";
        private const string PlayerStatsDataTypeName = "PlayerStatsData";
        private const string PlayerCombatDataTypeName = "PlayerCombatData";
        private const string PlayerEssenceDataTypeName = "PlayerEssenceData";
        private const string PlayerHealDataTypeName = "PlayerHealData";
        private const string PlayerDashDataTypeName = "PlayerDashData";
        private const string PlayerRopeDataTypeName = "PlayerRopeData";
        private const string PlayerPogoDataTypeName = "PlayerPogoData";
        private const string EnemyDataTypeName = "EnemyData";
        private const string WalkingEnemyTypeName = "WalkingEnemy";
        private const string WalkingEnemyProfileTypeName = "WalkingEnemyProfile";
        private const string EnemyProjectileTypeName = "EnemyProjectile";
        private const string BossProjectileTypeName = "SpearWander.Boss.BossProjectile";

        [UnityTest]
        public IEnumerator HealthDamageHealingAndIFramesStayPerPlayer()
        {
            ScriptableObject sharedStats = CreateData(PlayerStatsDataTypeName);
            ScriptableObject sharedEssence = CreateData(PlayerEssenceDataTypeName);
            SetPlayerStatsForTest(sharedStats);

            PlayerFixture first = CreatePlayerFixture(
                "Phase6PlayerOne",
                sharedStats,
                sharedEssence);
            PlayerFixture second = CreatePlayerFixture(
                "Phase6PlayerTwo",
                sharedStats,
                sharedEssence);

            try
            {
                Assert.That(CurrentHealth(first), Is.EqualTo(5), "Health did not initialize from PlayerStatsData.");
                Assert.That(CurrentHealth(second), Is.EqualTo(5));
                Assert.That(CurrentEssence(first), Is.EqualTo(0f));
                Assert.That(CurrentEssence(second), Is.EqualTo(0f));

                Assert.IsTrue(TakePlayerDamage(first, 1));
                Assert.That(CurrentHealth(first), Is.EqualTo(4));
                Assert.IsTrue(IsInvulnerable(first));
                Assert.IsFalse(TakePlayerDamage(first, 1), "An i-frame hit should be rejected.");
                Assert.That(CurrentHealth(first), Is.EqualTo(4));

                ReflectionTestUtilities.SetInstanceField(first.Health, "_iFramesTimer", 0f);
                Assert.IsTrue(TakePlayerDamage(first, 2));
                Assert.That(CurrentHealth(first), Is.EqualTo(2));
                Assert.That(CurrentHealth(second), Is.EqualTo(5), "Player health state was shared.");

                ReflectionTestUtilities.InvokeInstance(first.Health, "Heal", 100);
                Assert.That(CurrentHealth(first), Is.EqualTo(5), "Healing exceeded MaxHealth.");
                ReflectionTestUtilities.InvokeInstance(first.Health, "Heal", -100);
                Assert.That(CurrentHealth(first), Is.EqualTo(0), "Health did not respect its lower bound.");
                Assert.That(CurrentHealth(second), Is.EqualTo(5));

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(sharedStats, "maxHealth"),
                    Is.EqualTo(5),
                    "Runtime health changed shared PlayerStatsData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(sharedStats, "iFramesDuration"),
                    Is.EqualTo(0.5f).Within(0.0001f));

                yield return null;
            }
            finally
            {
                DestroyPlayerData(first);
                DestroyPlayerData(second);
                DestroyObject(sharedStats);
                DestroyObject(sharedEssence);
            }
        }

        [UnityTest]
        public IEnumerator EssenceCatchCapsConsumptionAndHealingKeepAuthoringDataImmutable()
        {
            PlayerFixture player = CreatePlayerFixture("Phase6EssencePlayer");
            string dataBefore = SnapshotEssenceData(player.EssenceData);

            try
            {
                AddEssence(player, false);
                Assert.That(CurrentEssence(player), Is.EqualTo(10f).Within(0.0001f));
                AddEssence(player, true);
                Assert.That(CurrentEssence(player), Is.EqualTo(50f).Within(0.0001f));

                AddEssence(player, true);
                AddEssence(player, true);
                Assert.That(CurrentEssence(player), Is.EqualTo(100f).Within(0.0001f));
                AddEssence(player, false);
                Assert.That(CurrentEssence(player), Is.EqualTo(100f).Within(0.0001f));

                Assert.IsFalse(ConsumeEssence(player, -1f));
                Assert.That(CurrentEssence(player), Is.EqualTo(100f).Within(0.0001f));
                Assert.IsTrue(ConsumeEssence(player, 50f));
                Assert.That(CurrentEssence(player), Is.EqualTo(50f).Within(0.0001f));
                Assert.IsFalse(ConsumeEssence(player, 51f));
                Assert.That(CurrentEssence(player), Is.EqualTo(50f).Within(0.0001f));

                ReflectionTestUtilities.SetInstanceField(player.Health, "_currentHealth", 3);
                ReflectionTestUtilities.InvokeInstance(player.Essence, "TryHeal");
                Assert.That(CurrentHealth(player), Is.EqualTo(5));
                Assert.That(CurrentEssence(player), Is.EqualTo(0f).Within(0.0001f));

                ReflectionTestUtilities.InvokeInstance(player.Essence, "Refill");
                Assert.That(CurrentEssence(player), Is.EqualTo(100f).Within(0.0001f));
                ReflectionTestUtilities.InvokeInstance(player.Essence, "ResetState");
                Assert.That(CurrentEssence(player), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(SnapshotEssenceData(player.EssenceData), Is.EqualTo(dataBefore));

                yield return null;
            }
            finally
            {
                DestroyPlayerData(player);
            }
        }

        [UnityTest]
        public IEnumerator PlayerDeathDisablesRuntimeCollisionWithoutMutatingData()
        {
            PlayerFixture player = CreatePlayerFixture("Phase6DyingPlayer");

            try
            {
                ReflectionTestUtilities.SetInstanceField(player.Health, "_currentHealth", 1);
                Assert.IsTrue(TakePlayerDamage(player, 1));

                Assert.That(CurrentHealth(player), Is.EqualTo(0));
                Assert.IsTrue(player.Player.gameObject.GetComponent<Collider2D>().enabled == false);
                Assert.IsTrue(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        player.Player,
                        "HasControlRequest",
                        Enum.Parse(
                            ReflectionTestUtilities.ResolveRuntimeType("Player+ControlReason"),
                            "Death")),
                    "Player death did not register its control lock.");

                yield return null;
            }
            finally
            {
                DestroyPlayerData(player);
            }
        }

        [UnityTest]
        public IEnumerator EnemyAndBossProjectilesUsePlayerHealthAndRespectIFrames()
        {
            PlayerFixture player = CreatePlayerFixture("Phase6ProjectilePlayer");

            try
            {
                GameObject enemyProjectileObject = CreateEnemyProjectile(1);
                Component enemyProjectile = enemyProjectileObject.GetComponent(
                    ResolveRuntimeType(EnemyProjectileTypeName));

                ReflectionTestUtilities.InvokeInstance(
                    enemyProjectile,
                    "OnTriggerEnter2D",
                    player.Player.GetComponent<Collider2D>());

                Assert.That(CurrentHealth(player), Is.EqualTo(4));
                Assert.IsTrue(IsInvulnerable(player));

                GameObject blockedBossProjectileObject = CreateBossProjectile(2);
                Component blockedBossProjectile = blockedBossProjectileObject.GetComponent(
                    ResolveRuntimeType(BossProjectileTypeName));
                ReflectionTestUtilities.InvokeInstance(
                    blockedBossProjectile,
                    "OnTriggerEnter2D",
                    player.Player.GetComponent<Collider2D>());

                Assert.That(
                    CurrentHealth(player),
                    Is.EqualTo(4),
                    "A projectile bypassed the active player i-frames.");

                ReflectionTestUtilities.SetInstanceField(player.Health, "_iFramesTimer", 0f);
                GameObject acceptedBossProjectileObject = CreateBossProjectile(2);
                Component acceptedBossProjectile = acceptedBossProjectileObject.GetComponent(
                    ResolveRuntimeType(BossProjectileTypeName));
                ReflectionTestUtilities.InvokeInstance(
                    acceptedBossProjectile,
                    "OnTriggerEnter2D",
                    player.Player.GetComponent<Collider2D>());

                Assert.That(CurrentHealth(player), Is.EqualTo(2));
                Assert.IsTrue(IsInvulnerable(player));
                yield return null;
            }
            finally
            {
                DestroyPlayerData(player);
            }
        }

        [UnityTest]
        public IEnumerator DashAndPogoUsePlayerCombatDamageAgainstEnemies()
        {
            PlayerFixture player = CreatePlayerFixture("Phase6AbilityDamagePlayer");
            ScriptableObject enemyData = CreateEnemyData(10);
            ScriptableObject enemyProfile = ScriptableObject.CreateInstance(
                ResolveRuntimeType(WalkingEnemyProfileTypeName));
            GameObject enemyObject = CreateEnemyTarget(
                enemyData,
                enemyProfile,
                new Vector3(2000f, 0f, 0f));

            try
            {
                Component dash = player.Dash;
                ReflectionTestUtilities.InvokeInstance(
                    dash,
                    "ApplyDashHit",
                    enemyObject.GetComponent(ResolveRuntimeType(WalkingEnemyTypeName)),
                    Vector2.zero,
                    (Vector2)enemyObject.transform.position);
                Assert.That(EnemyHealth(enemyObject), Is.EqualTo(7), "Dash did not apply PlayerCombatData.dashDamage.");
                Assert.IsTrue(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        player.Knockback,
                        "IsPersistentKnockback"),
                    "Dash knockback protection was not applied.");

                ReflectionTestUtilities.SetInstanceField(
                    enemyObject.GetComponent(ResolveRuntimeType(WalkingEnemyTypeName)),
                    "currentHealth",
                    10);
                ReflectionTestUtilities.InvokeInstance(
                    player.Pogo,
                    "ExecuteBounce",
                    enemyObject.GetComponent(ResolveRuntimeType(WalkingEnemyTypeName)),
                    enemyObject);

                Assert.That(EnemyHealth(enemyObject), Is.EqualTo(8), "Pogo did not apply PlayerCombatData.pogoDamage.");
                Assert.IsTrue(
                    ReflectionTestUtilities.GetProperty<bool>(player.Pogo, "HasPostPogoProtection"));
                Assert.IsTrue(
                    ReflectionTestUtilities.GetProperty<bool>(player.Pogo, "IsPogoStunned"));
                Assert.That(player.Rigidbody.linearVelocity.y, Is.EqualTo(12f).Within(0.0001f));

                yield return null;
            }
            finally
            {
                DestroyPlayerData(player);
                DestroyObject(enemyData);
                DestroyObject(enemyProfile);
            }
        }

        private PlayerFixture CreatePlayerFixture(
            string name,
            ScriptableObject sharedStats = null,
            ScriptableObject sharedEssence = null)
        {
            GameObject root = Fixture.CreateGameObject(name);
            root.SetActive(false);
            root.tag = "Player";
            root.transform.position = new Vector3(5000f, 5000f, 0f);
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
            root.AddComponent<Animator>();

            GameObject feetObject = new GameObject("Feet");
            feetObject.transform.SetParent(root.transform, false);
            feetObject.AddComponent<BoxCollider2D>().isTrigger = true;
            root.AddComponent(ResolveRuntimeType("PlayerFeet"));

            Component player = root.AddComponent(ResolveRuntimeType(PlayerTypeName));
            List<ScriptableObject> owned = new List<ScriptableObject>();

            ScriptableObject stats = sharedStats ?? CreateData(PlayerStatsDataTypeName);
            ScriptableObject combat = CreateData(PlayerCombatDataTypeName);
            ScriptableObject essence = sharedEssence ?? CreateData(PlayerEssenceDataTypeName);
            ScriptableObject healData = CreateData(PlayerHealDataTypeName);
            ScriptableObject dashData = CreateData(PlayerDashDataTypeName);
            ScriptableObject ropeData = CreateData(PlayerRopeDataTypeName);
            ScriptableObject pogoData = CreateData(PlayerPogoDataTypeName);
            owned.Add(combat);
            owned.Add(healData);
            owned.Add(dashData);
            owned.Add(ropeData);
            owned.Add(pogoData);
            if (sharedStats == null) owned.Add(stats);
            if (sharedEssence == null) owned.Add(essence);

            SetPlayerStatsForTest(stats);
            ReflectionTestUtilities.SetInstanceField(combat, "dashDamage", 3);
            ReflectionTestUtilities.SetInstanceField(combat, "pogoDamage", 2);
            ReflectionTestUtilities.SetInstanceField(dashData, "knockbackForce", Vector2.zero);
            ReflectionTestUtilities.SetInstanceField(dashData, "knockbackDuration", 0f);
            ReflectionTestUtilities.SetInstanceField(pogoData, "bounceForce", 12f);
            ReflectionTestUtilities.SetInstanceField(pogoData, "bounceOffset", 0f);
            ReflectionTestUtilities.SetInstanceField(pogoData, "postPogoInvincibility", 0.25f);
            ReflectionTestUtilities.SetInstanceField(pogoData, "pogoStunDuration", 0.2f);

            ReflectionTestUtilities.SetInstanceField(player, "playerStats", stats);
            ReflectionTestUtilities.SetInstanceField(player, "combatStats", combat);
            ReflectionTestUtilities.SetInstanceField(player, "essenceStats", essence);
            ReflectionTestUtilities.SetInstanceField(player, "healStats", healData);
            ReflectionTestUtilities.SetInstanceField(player, "dashStats", dashData);
            ReflectionTestUtilities.SetInstanceField(player, "ropeStats", ropeData);
            ReflectionTestUtilities.SetInstanceField(player, "pogoStats", pogoData);

            Component input = root.AddComponent(ResolveRuntimeType("PlayerInputHandler"));
            Component movement = root.AddComponent(ResolveRuntimeType("PlayerMovement"));
            Component jump = root.AddComponent(ResolveRuntimeType("PlayerJump"));
            Component dash = root.AddComponent(ResolveRuntimeType("PlayerDash"));
            Component pogo = root.AddComponent(ResolveRuntimeType("PlayerPogo"));
            Component combatComponent = root.AddComponent(ResolveRuntimeType("PlayerCombat"));
            Component health = root.AddComponent(ResolveRuntimeType("PlayerHealth"));
            Component essenceComponent = root.AddComponent(ResolveRuntimeType("PlayerEssence"));
            Component knockback = root.AddComponent(ResolveRuntimeType("Knockback"));

            ((Behaviour)movement).enabled = false;
            ((Behaviour)jump).enabled = false;
            ((Behaviour)dash).enabled = false;
            ((Behaviour)pogo).enabled = false;
            ((Behaviour)combatComponent).enabled = false;

            root.SetActive(true);

            // Disabled components may not receive Awake; wire only their
            // runtime owner references needed by direct gameplay callbacks.
            ReflectionTestUtilities.SetInstanceField(dash, "_player", player);
            ReflectionTestUtilities.SetInstanceField(pogo, "_player", player);
            ReflectionTestUtilities.SetInstanceField(movement, "_player", player);

            return new PlayerFixture(
                root,
                player,
                health,
                essenceComponent,
                dash,
                pogo,
                knockback,
                body,
                collider,
                sprite,
                stats,
                combat,
                essence,
                owned);
        }

        private GameObject CreateEnemyTarget(
            ScriptableObject enemyData,
            ScriptableObject profile,
            Vector3 position)
        {
            GameObject enemy = Fixture.CreateGameObject("Phase6EnemyTarget");
            enemy.SetActive(false);
            enemy.transform.position = position;
            enemy.tag = "Enemy";
            enemy.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            enemy.AddComponent<BoxCollider2D>();
            enemy.AddComponent<SpriteRenderer>();
            Component walkingEnemy = enemy.AddComponent(ResolveRuntimeType(WalkingEnemyTypeName));
            ReflectionTestUtilities.SetInstanceField(walkingEnemy, "enemyData", enemyData);
            ReflectionTestUtilities.SetInstanceField(walkingEnemy, "aiProfile", profile);
            ReflectionTestUtilities.SetInstanceField(walkingEnemy, "currentHealth", 10);
            enemy.SetActive(true);
            ((Behaviour)walkingEnemy).enabled = false;
            return enemy;
        }

        private GameObject CreateEnemyProjectile(int damage)
        {
            GameObject projectile = Fixture.CreateGameObject("Phase6EnemyProjectile");
            projectile.SetActive(false);
            projectile.AddComponent<Rigidbody2D>();
            projectile.AddComponent<CircleCollider2D>().isTrigger = true;
            Component script = projectile.AddComponent(ResolveRuntimeType(EnemyProjectileTypeName));
            ReflectionTestUtilities.SetInstanceField(script, "damage", damage);
            projectile.SetActive(true);
            return projectile;
        }

        private GameObject CreateBossProjectile(int damage)
        {
            GameObject projectile = Fixture.CreateGameObject("Phase6BossProjectile");
            projectile.SetActive(false);
            projectile.AddComponent<Rigidbody2D>();
            projectile.AddComponent<CircleCollider2D>().isTrigger = true;
            Component script = projectile.AddComponent(ResolveRuntimeType(BossProjectileTypeName));
            projectile.SetActive(true);
            ReflectionTestUtilities.InvokeInstance(script, "Initialize", damage, Vector2.left);
            return projectile;
        }

        private static ScriptableObject CreateData(string typeName)
        {
            return ScriptableObject.CreateInstance(ResolveRuntimeType(typeName));
        }

        private static ScriptableObject CreateEnemyData(int maxHealth)
        {
            ScriptableObject data = CreateData(EnemyDataTypeName);
            ReflectionTestUtilities.SetInstanceField(data, "maxHealth", maxHealth);
            ReflectionTestUtilities.SetInstanceField(data, "speed", 0f);
            return data;
        }

        private static void SetPlayerStatsForTest(ScriptableObject stats)
        {
            ReflectionTestUtilities.SetInstanceField(stats, "maxHealth", 5);
            ReflectionTestUtilities.SetInstanceField(stats, "iFramesDuration", 0.5f);
            ReflectionTestUtilities.SetInstanceField(stats, "damageForce", Vector2.zero);
            ReflectionTestUtilities.SetInstanceField(stats, "damageKnockbackDuration", 0.1f);
            ReflectionTestUtilities.SetInstanceField(stats, "contactDamage", 1);
        }

        private static int CurrentHealth(PlayerFixture player)
        {
            return ReflectionTestUtilities.GetProperty<int>(player.Health, "CurrentHealth");
        }

        private static float CurrentEssence(PlayerFixture player)
        {
            return ReflectionTestUtilities.GetProperty<float>(player.Essence, "CurrentEssence");
        }

        private static bool IsInvulnerable(PlayerFixture player)
        {
            return ReflectionTestUtilities.GetProperty<bool>(player.Health, "IsInvulnerable");
        }

        private static bool TakePlayerDamage(PlayerFixture player, int amount)
        {
            return ReflectionTestUtilities.InvokeInstance<bool>(
                player.Health,
                "TakeDamage",
                amount,
                Vector2.zero,
                Vector2.right);
        }

        private static void AddEssence(PlayerFixture player, bool perfectCatch)
        {
            ReflectionTestUtilities.InvokeInstance(
                player.Essence,
                "AddEssenceFromCatch",
                perfectCatch);
        }

        private static bool ConsumeEssence(PlayerFixture player, float amount)
        {
            return ReflectionTestUtilities.InvokeInstance<bool>(
                player.Essence,
                "ConsumeEssence",
                amount);
        }

        private static int EnemyHealth(GameObject enemy)
        {
            Component component = enemy.GetComponent(ResolveRuntimeType(WalkingEnemyTypeName));
            return ReflectionTestUtilities.GetInstanceField<int>(component, "currentHealth");
        }

        private static string SnapshotEssenceData(ScriptableObject data)
        {
            return string.Join(
                "|",
                new object[]
                {
                    ReflectionTestUtilities.GetInstanceField<float>(data, "maxEssence"),
                    ReflectionTestUtilities.GetInstanceField<float>(data, "essencePerBaseCatch"),
                    ReflectionTestUtilities.GetInstanceField<float>(data, "essencePerPerfectCatch")
                });
        }

        private static Type ResolveRuntimeType(string typeName)
        {
            return ReflectionTestUtilities.ResolveRuntimeType(typeName);
        }

        private static void DestroyPlayerData(PlayerFixture player)
        {
            for (int i = 0; i < player.OwnedData.Count; i++)
                DestroyObject(player.OwnedData[i]);
            player.OwnedData.Clear();
        }

        private static void DestroyObject(UnityEngine.Object instance)
        {
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        private sealed class PlayerFixture
        {
            public readonly GameObject Root;
            public readonly Component Player;
            public readonly Component Health;
            public readonly Component Essence;
            public readonly Component Dash;
            public readonly Component Pogo;
            public readonly Component Knockback;
            public readonly Rigidbody2D Rigidbody;
            public readonly Collider2D Collider;
            public readonly SpriteRenderer Sprite;
            public readonly ScriptableObject StatsData;
            public readonly ScriptableObject CombatData;
            public readonly ScriptableObject EssenceData;
            public readonly List<ScriptableObject> OwnedData;

            public PlayerFixture(
                GameObject root,
                Component player,
                Component health,
                Component essence,
                Component dash,
                Component pogo,
                Component knockback,
                Rigidbody2D rigidbody,
                Collider2D collider,
                SpriteRenderer sprite,
                ScriptableObject statsData,
                ScriptableObject combatData,
                ScriptableObject essenceData,
                List<ScriptableObject> ownedData)
            {
                Root = root;
                Player = player;
                Health = health;
                Essence = essence;
                Dash = dash;
                Pogo = pogo;
                Knockback = knockback;
                Rigidbody = rigidbody;
                Collider = collider;
                Sprite = sprite;
                StatsData = statsData;
                CombatData = combatData;
                EssenceData = essenceData;
                OwnedData = ownedData;
            }
        }
    }
}
