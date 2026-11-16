using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    public class PlayerEnemyCollisionBypassPlayModeTests : PlayModeTestBase
    {
        private const string PlayerTypeName = "Player";
        private const string PlayerHealthTypeName = "PlayerHealth";
        private const string EnemyDataTypeName = "EnemyData";
        private const string BossDataTypeName = "BossData";

        private readonly List<Object> _temporaries = new List<Object>();

        [UnityTearDown]
        public IEnumerator DestroyTemporaries()
        {
            for (int i = 0; i < _temporaries.Count; i++)
            {
                if (_temporaries[i] != null)
                    Object.Destroy(_temporaries[i]);
            }

            _temporaries.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageBypassesBodyCollisionBeyondIFramesAndCanHitAgain()
        {
            PlayerFixture player = CreatePlayer();
            yield return null;

            GameObject enemy = CreateEnemy("WalkingEnemy", Vector2.zero);
            Collider2D enemyBody = enemy.GetComponent<Collider2D>();
            Physics2D.SyncTransforms();

            Assert.That(
                ReflectionTestUtilities.InvokeInstance<bool>(
                    player.Health,
                    "TakeDamage",
                    1,
                    Vector2.right),
                Is.True);
            Assert.That(GetHealth(player.Health), Is.EqualTo(4));
            Assert.That(Physics2D.GetIgnoreCollision(player.Body, enemyBody), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(player.Feet, enemyBody), Is.True);

            SetIFrames(player.Health, 0f);
            RefreshBypass(player.Health);

            Assert.That(
                ReflectionTestUtilities.GetProperty<bool>(player.Health, "IsInvulnerable"),
                Is.False);
            Assert.That(
                Physics2D.GetIgnoreCollision(player.Body, enemyBody),
                Is.True,
                "The physical bypass should outlive i-frames while overlap remains.");

            ReflectionTestUtilities.InvokeInstance(player.Health, "FixedUpdate");

            Assert.That(GetHealth(player.Health), Is.EqualTo(3));
            Assert.That(
                ReflectionTestUtilities.GetProperty<bool>(player.Health, "IsInvulnerable"),
                Is.True,
                "A new contact hit should start a fresh i-frame period.");
            Assert.That(Physics2D.GetIgnoreCollision(player.Body, enemyBody), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(player.Feet, enemyBody), Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerCanPhysicallyCrossEnemyBodyDuringIFrames()
        {
            PlayerFixture player = CreatePlayer(dynamicBody: true);
            yield return null;

            GameObject enemy = CreateEnemy("WalkingEnemy", new Vector2(0.8f, 0f));
            Collider2D enemyBody = enemy.GetComponent<Collider2D>();
            Physics2D.SyncTransforms();

            Assert.That(
                ReflectionTestUtilities.InvokeInstance<bool>(
                    player.Health,
                    "TakeDamage",
                    1,
                    Vector2.left),
                Is.True);
            player.Rigidbody.linearVelocity = new Vector2(4f, 0f);

            for (int i = 0; i < 40; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(
                player.Root.transform.position.x,
                Is.GreaterThan(enemy.transform.position.x + 1f),
                "The Player did not pass through the enemy body.");
            Assert.That(GetHealth(player.Health), Is.EqualTo(4));
            Assert.That(Physics2D.GetIgnoreCollision(player.Body, enemyBody), Is.False);
        }

        [UnityTest]
        public IEnumerator OverlappingEnemiesAreRestoredIndependently()
        {
            PlayerFixture player = CreatePlayer();
            yield return null;

            GameObject firstEnemy = CreateEnemy("WalkingEnemy", new Vector2(-0.2f, 0f));
            GameObject secondEnemy = CreateEnemy("FlyingEnemy", new Vector2(0.2f, 0f));
            Collider2D firstBody = firstEnemy.GetComponent<Collider2D>();
            Collider2D secondBody = secondEnemy.GetComponent<Collider2D>();
            SetIFrames(player.Health, 1f);
            Physics2D.SyncTransforms();

            RefreshBypass(player.Health);

            Assert.That(Physics2D.GetIgnoreCollision(player.Body, firstBody), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(player.Body, secondBody), Is.True);

            secondEnemy.transform.position = new Vector2(5f, 0f);
            Physics2D.SyncTransforms();
            RefreshBypass(player.Health);

            Assert.That(
                Physics2D.GetIgnoreCollision(player.Body, firstBody),
                Is.True,
                "Exiting one body must not restore another overlapping body pair.");
            Assert.That(Physics2D.GetIgnoreCollision(player.Body, secondBody), Is.False);

            player.Root.transform.position = new Vector2(8f, 0f);
            Physics2D.SyncTransforms();
            RefreshBypass(player.Health);

            Assert.That(Physics2D.GetIgnoreCollision(player.Body, firstBody), Is.False);
        }

        [UnityTest]
        public IEnumerator DeactivatedAndDestroyedEnemiesDoNotLeaveIgnoredPairs()
        {
            PlayerFixture player = CreatePlayer();
            yield return null;

            GameObject disabledEnemy = CreateEnemy("RecallEnemy", Vector2.zero);
            GameObject destroyedEnemy = CreateEnemy("WalkingEnemy", new Vector2(0.25f, 0f));
            Collider2D disabledBody = disabledEnemy.GetComponent<Collider2D>();
            Collider2D destroyedBody = destroyedEnemy.GetComponent<Collider2D>();
            SetIFrames(player.Health, 1f);
            Physics2D.SyncTransforms();
            RefreshBypass(player.Health);

            Assert.That(Physics2D.GetIgnoreCollision(player.Body, disabledBody), Is.True);
            Assert.That(Physics2D.GetIgnoreCollision(player.Body, destroyedBody), Is.True);

            disabledEnemy.SetActive(false);
            Object.Destroy(destroyedEnemy);
            yield return null;
            Physics2D.SyncTransforms();
            RefreshBypass(player.Health);

            Assert.That(GetBypassedPairCount(player.Health), Is.Zero);
        }

        [UnityTest]
        public IEnumerator OnlyEnemyBodyCollidersAreBypassedAcrossEnemyArchetypes()
        {
            PlayerFixture player = CreatePlayer();
            yield return null;

            string[] archetypes =
            {
                "WalkingEnemy",
                "FlyingEnemy",
                "RecallEnemy",
                "SpearWander.Boss.Boss"
            };
            List<Collider2D> enemyBodies = new List<Collider2D>();
            List<GameObject> attackTriggers = new List<GameObject>();

            for (int i = 0; i < archetypes.Length; i++)
            {
                GameObject enemy = CreateEnemy(archetypes[i], new Vector2(i * 0.15f, 0f));
                enemyBodies.Add(enemy.GetComponent<Collider2D>());

                GameObject attackTrigger = new GameObject("Attack Trigger");
                _temporaries.Add(attackTrigger);
                attackTrigger.transform.SetParent(enemy.transform, false);
                BoxCollider2D triggerCollider = attackTrigger.AddComponent<BoxCollider2D>();
                triggerCollider.isTrigger = true;
                attackTriggers.Add(attackTrigger);
            }

            GameObject wall = new GameObject("Test Wall");
            _temporaries.Add(wall);
            wall.transform.position = new Vector2(0f, -3f);
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();

            SetIFrames(player.Health, 1f);
            Physics2D.SyncTransforms();
            RefreshBypass(player.Health);

            for (int i = 0; i < enemyBodies.Count; i++)
            {
                Assert.That(
                    Physics2D.GetIgnoreCollision(player.Body, enemyBodies[i]),
                    Is.True,
                    $"The body collider for {archetypes[i]} was not bypassed.");

                Assert.That(
                    Physics2D.GetIgnoreCollision(
                        player.Body,
                        attackTriggers[i].GetComponent<Collider2D>()),
                    Is.False,
                    "Attack triggers must not be changed by body bypass.");
            }

            Assert.That(Physics2D.GetIgnoreCollision(player.Body, wallCollider), Is.False);
        }

        private PlayerFixture CreatePlayer(bool dynamicBody = false)
        {
            GameObject root = new GameObject("Test Player");
            _temporaries.Add(root);
            root.SetActive(false);
            root.tag = "Player";

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = dynamicBody
                ? RigidbodyType2D.Dynamic
                : RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            GameObject feet = new GameObject("Player Feet Collider");
            feet.transform.SetParent(root.transform, false);
            feet.transform.localPosition = new Vector2(0f, -0.45f);
            BoxCollider2D feetCollider = feet.AddComponent<BoxCollider2D>();
            feetCollider.size = new Vector2(0.3f, 0.1f);

            root.AddComponent<SpriteRenderer>();
            root.AddComponent<Animator>();

            System.Type playerType =
                ReflectionTestUtilities.ResolveRuntimeType(PlayerTypeName);
            Component playerComponent = root.AddComponent(playerType);

            string[] dataFields =
            {
                "playerStats", "combatStats", "essenceStats", "healStats",
                "dashStats", "ropeStats", "pogoStats"
            };
            string[] dataTypes =
            {
                "PlayerStatsData", "PlayerCombatData", "PlayerEssenceData",
                "PlayerHealData", "PlayerDashData", "PlayerRopeData",
                "PlayerPogoData"
            };
            for (int i = 0; i < dataFields.Length; i++)
            {
                Object data = ScriptableObject.CreateInstance(
                    ReflectionTestUtilities.ResolveRuntimeType(dataTypes[i]));
                _temporaries.Add(data);
                ReflectionTestUtilities.SetInstanceField(playerComponent, dataFields[i], data);
            }

            object playerStats =
                ReflectionTestUtilities.GetInstanceField(playerComponent, "playerStats");
            ReflectionTestUtilities.SetInstanceField(playerStats, "iFramesDuration", 0.5f);
            ReflectionTestUtilities.SetInstanceField(playerStats, "damageForce", Vector2.zero);
            ReflectionTestUtilities.SetInstanceField(playerStats, "damageKnockbackDuration", 0f);

            Component dash = root.AddComponent(
                ReflectionTestUtilities.ResolveRuntimeType("PlayerDash"));
            ((Behaviour)dash).enabled = false;
            Component pogo = root.AddComponent(
                ReflectionTestUtilities.ResolveRuntimeType("PlayerPogo"));
            ((Behaviour)pogo).enabled = false;
            Component movement = root.AddComponent(
                ReflectionTestUtilities.ResolveRuntimeType("PlayerMovement"));
            ((Behaviour)movement).enabled = false;
            root.AddComponent(
                ReflectionTestUtilities.ResolveRuntimeType("Knockback"));
            Component health = root.AddComponent(
                ReflectionTestUtilities.ResolveRuntimeType(PlayerHealthTypeName));

            root.SetActive(true);

            return new PlayerFixture(root, body, collider, feetCollider, health);
        }

        private GameObject CreateEnemy(string runtimeTypeName, Vector2 position)
        {
            GameObject enemy = new GameObject(runtimeTypeName);
            _temporaries.Add(enemy);
            enemy.SetActive(false);
            enemy.transform.position = position;
            enemy.tag = "Enemy";

            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            BoxCollider2D collider = enemy.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<Animator>();

            System.Type componentType =
                ReflectionTestUtilities.ResolveRuntimeType(runtimeTypeName);
            Component enemyComponent = enemy.AddComponent(componentType);

            if (runtimeTypeName == "SpearWander.Boss.Boss")
            {
                Object data = ScriptableObject.CreateInstance(
                    ReflectionTestUtilities.ResolveRuntimeType(BossDataTypeName));
                _temporaries.Add(data);
                ReflectionTestUtilities.SetInstanceField(enemyComponent, "bossData", data);
            }
            else
            {
                Object data = ScriptableObject.CreateInstance(
                    ReflectionTestUtilities.ResolveRuntimeType(EnemyDataTypeName));
                _temporaries.Add(data);
                ReflectionTestUtilities.SetInstanceField(enemyComponent, "enemyData", data);

                if (runtimeTypeName == "WalkingEnemy")
                {
                    Object profile = ScriptableObject.CreateInstance(
                        ReflectionTestUtilities.ResolveRuntimeType("WalkingEnemyProfile"));
                    _temporaries.Add(profile);
                    ReflectionTestUtilities.SetInstanceField(enemyComponent, "aiProfile", profile);
                }
                else if (runtimeTypeName == "FlyingEnemy")
                {
                    Object profile = ScriptableObject.CreateInstance(
                        ReflectionTestUtilities.ResolveRuntimeType("FlyingEnemyProfile"));
                    _temporaries.Add(profile);
                    ReflectionTestUtilities.SetInstanceField(enemyComponent, "aiProfile", profile);
                }
            }

            ((Behaviour)enemyComponent).enabled = false;
            enemy.SetActive(true);
            return enemy;
        }

        private static void RefreshBypass(object health)
        {
            ReflectionTestUtilities.InvokeInstance(
                health,
                "RefreshEnemyBodyCollisionBypass");
        }

        private static void SetIFrames(object health, float duration)
        {
            ReflectionTestUtilities.SetInstanceField(health, "_iFramesTimer", duration);
        }

        private static int GetHealth(object health)
        {
            return ReflectionTestUtilities.GetProperty<int>(health, "CurrentHealth");
        }

        private static int GetBypassedPairCount(object health)
        {
            object pairs = ReflectionTestUtilities.GetInstanceField(
                health,
                "_bypassedEnemyBodyCollisions");
            return (int)pairs.GetType().GetProperty("Count").GetValue(pairs);
        }

        private readonly struct PlayerFixture
        {
            public readonly GameObject Root;
            public readonly Rigidbody2D Rigidbody;
            public readonly Collider2D Body;
            public readonly Collider2D Feet;
            public readonly Component Health;

            public PlayerFixture(
                GameObject root,
                Rigidbody2D rigidbody,
                Collider2D body,
                Collider2D feet,
                Component health)
            {
                Root = root;
                Rigidbody = rigidbody;
                Body = body;
                Feet = feet;
                Health = health;
            }
        }
    }
}
