using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Phase 2 runtime coverage for the enemy data separation:
    /// - shared EnemyData drives instance state without being mutated;
    /// - two enemies sharing one asset keep independent health;
    /// - damage, knockback and death still run through the real lifecycle;
    /// - RecallEnemy only reacts to Spears that are actually incoming.
    /// </summary>
    public class EnemyArchitecturePlayModeTests : PlayModeTestBase
    {
        private const string WalkingEnemyType = "WalkingEnemy";
        private const string RecallEnemyType = "RecallEnemy";
        private const string EnemyDataType = "EnemyData";
        private const string WalkingProfileType = "WalkingEnemyProfile";
        private const string KnockbackType = "Knockback";
        private const string SpearType = "Spear";

        [UnityTest]
        public IEnumerator WalkingEnemyUsesSharedDataSpeed()
        {
            ScriptableObject data = CreateEnemyData(maxHealth: 5, speed: 3f);
            ScriptableObject profile = CreateProfile(WalkingProfileType);

            GameObject enemyObject = null;
            try
            {
                enemyObject = CreateEnemyObject(
                    WalkingEnemyType,
                    data,
                    profile,
                    "Phase2WalkSpeed");
                Rigidbody2D body = enemyObject.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;

                enemyObject.SetActive(true);
                yield return null;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(
                    Mathf.Abs(body.linearVelocity.x),
                    Is.EqualTo(3f).Within(0.01f),
                    "WalkingEnemy did not move at the EnemyData speed.");
            }
            finally
            {
                DestroyObject(enemyObject);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator EnemyDamageKnockbackAndDeathUseRuntimeStateOnly()
        {
            const int maxHealth = 5;
            ScriptableObject data = CreateEnemyData(maxHealth, speed: 0f);
            ScriptableObject profile = CreateProfile(WalkingProfileType);

            GameObject firstObject = null;
            GameObject secondObject = null;
            try
            {
                firstObject = CreateEnemyObject(
                    WalkingEnemyType,
                    data,
                    profile,
                    "Phase2EnemyA",
                    withKnockback: true);
                secondObject = CreateEnemyObject(
                    WalkingEnemyType,
                    data,
                    profile,
                    "Phase2EnemyB");

                firstObject.SetActive(true);
                secondObject.SetActive(true);
                yield return null;

                Component first = firstObject.GetComponent(ResolveRuntimeType(WalkingEnemyType));
                Component second = secondObject.GetComponent(ResolveRuntimeType(WalkingEnemyType));

                Assert.That(GetHealth(first), Is.EqualTo(maxHealth));
                Assert.That(GetHealth(second), Is.EqualTo(maxHealth));

                bool damaged = ReflectionTestUtilities.InvokeInstance<bool>(
                    first,
                    "TakeDamage",
                    2,
                    Vector2.zero,
                    new Vector2(-1f, 0f));

                Assert.That(damaged, Is.True, "TakeDamage did not report applied damage.");
                Assert.That(GetHealth(first), Is.EqualTo(maxHealth - 2));
                Assert.That(
                    GetHealth(second),
                    Is.EqualTo(maxHealth),
                    "The second enemy sharing the same EnemyData changed state.");

                Component knockback = firstObject.GetComponent(
                    ResolveRuntimeType(KnockbackType));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<bool>(knockback, "IsKnockedBack"),
                    Is.True,
                    "Damage did not start the knockback.");

                Rigidbody2D firstBody = firstObject.GetComponent<Rigidbody2D>();
                yield return new WaitForFixedUpdate();
                Assert.That(
                    firstBody.linearVelocity.sqrMagnitude,
                    Is.GreaterThan(0.01f),
                    "Knockback did not move the enemy.");

                bool killed = ReflectionTestUtilities.InvokeInstance<bool>(
                    first,
                    "TakeDamage",
                    maxHealth,
                    Vector2.zero,
                    new Vector2(-1f, 0f));

                Assert.That(killed, Is.True);
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<bool>(first, "isDead"),
                    Is.True,
                    "Enemy was not marked dead at zero health.");

                yield return null;

                Assert.That(
                    firstObject == null,
                    Is.True,
                    "The dead enemy instance was not destroyed.");
                Assert.That(
                    GetHealth(second),
                    Is.EqualTo(maxHealth),
                    "The surviving enemy was affected by the death of its neighbour.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(data, "maxHealth"),
                    Is.EqualTo(maxHealth),
                    "Runtime combat mutated the shared EnemyData asset.");
            }
            finally
            {
                DestroyObject(firstObject);
                DestroyObject(secondObject);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator FlyingEnemyStunUsesProfileTuning()
        {
            const int maxHealth = 3;
            ScriptableObject data = CreateEnemyData(maxHealth, speed: 0f);
            ScriptableObject profile = CreateProfile("FlyingEnemyProfile");
            ReflectionTestUtilities.SetInstanceField(profile, "stunDuration", 0.25f);

            GameObject playerProxy = Fixture.CreateGameObject("Phase2PlayerProxy");
            GameObject enemyObject = null;
            try
            {
                enemyObject = CreateEnemyObject(
                    "FlyingEnemy",
                    data,
                    profile,
                    "Phase2FlyingEnemy",
                    withKnockback: true);

                Component flying = enemyObject.GetComponent(ResolveRuntimeType("FlyingEnemy"));
                ReflectionTestUtilities.SetInstanceField(
                    flying,
                    "_player",
                    playerProxy.transform);

                enemyObject.SetActive(true);
                yield return null;

                Rigidbody2D body = enemyObject.GetComponent<Rigidbody2D>();
                body.linearVelocity = new Vector2(1f, 1f);

                ReflectionTestUtilities.InvokeInstance<bool>(
                    flying,
                    "TakeDamage",
                    1,
                    Vector2.zero,
                    new Vector2(-1f, 0f));

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<bool>(flying, "_isStunned"),
                    Is.True,
                    "FlyingEnemy did not enter the stun state after damage.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(flying, "_stunTimer"),
                    Is.EqualTo(0.25f).Within(0.0001f),
                    "The stun duration did not come from the FlyingEnemyProfile.");
                Assert.That(
                    GetHealth(flying),
                    Is.EqualTo(maxHealth - 1),
                    "Runtime damage did not change the FlyingEnemy health.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(data, "maxHealth"),
                    Is.EqualTo(maxHealth),
                    "Runtime combat mutated the shared EnemyData asset.");
            }
            finally
            {
                DestroyObject(enemyObject);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator RecallEnemyRejectsSpearsThatAreNotIncoming()
        {
            Type recallType = ResolveRuntimeType(RecallEnemyType);
            Type spearType = ResolveRuntimeType(SpearType);

            GameObject recallObject = null;
            GameObject spearObject = null;
            try
            {
                recallObject = Fixture.CreateGameObject("Phase2RecallEnemy");
                recallObject.SetActive(false);
                recallObject.transform.position = Vector3.zero;
                Component recall = recallObject.AddComponent(recallType);
                ReflectionTestUtilities.SetInstanceField(recall, "dodgeDetectionDistance", 6f);
                ReflectionTestUtilities.SetInstanceField(recall, "spearDirectionThreshold", 0.98f);

                spearObject = Fixture.CreateGameObject("Phase2Spear");
                spearObject.SetActive(false);
                spearObject.transform.position = new Vector3(-2f, 0f, 0f);
                Rigidbody2D spearBody = spearObject.AddComponent<Rigidbody2D>();
                Component spear = spearObject.AddComponent(spearType);

                // Activate the Spear so its Rigidbody2D accepts a velocity.
                spearObject.SetActive(true);
                yield return null;

                object flying = GetSpearState(spear, "Flying");
                object returning = GetSpearState(spear, "Returning");

                // Incoming from the left: the enemy must react.
                spearObject.transform.position = new Vector3(-2f, 0f, 0f);
                spearBody.linearVelocity = new Vector2(10f, 0f);
                ReflectionTestUtilities.SetInstanceField(spear, "currentState", flying);
                Assert.That(
                    IsSpearThreatening(recall, spear),
                    Is.True,
                    "An incoming flying Spear was not detected.");

                // Moving away from the enemy: not a threat.
                spearBody.linearVelocity = new Vector2(-10f, 0f);
                Assert.That(
                    IsSpearThreatening(recall, spear),
                    Is.False,
                    "A Spear moving away was treated as a threat.");

                // Too far away: not a threat even when aligned.
                spearObject.transform.position = new Vector3(-10f, 0f, 0f);
                spearBody.linearVelocity = new Vector2(10f, 0f);
                Assert.That(
                    IsSpearThreatening(recall, spear),
                    Is.False,
                    "A Spear outside the dodge distance was treated as a threat.");

                // Returning Spears are deliberately ignored.
                spearObject.transform.position = new Vector3(-2f, 0f, 0f);
                ReflectionTestUtilities.SetInstanceField(spear, "currentState", returning);
                Assert.That(
                    IsSpearThreatening(recall, spear),
                    Is.False,
                    "A returning Spear was treated as a threat.");
            }
            finally
            {
                DestroyObject(spearObject);
                DestroyObject(recallObject);
            }
        }

        private GameObject CreateEnemyObject(
            string enemyTypeName,
            ScriptableObject data,
            ScriptableObject profile,
            string name,
            bool withKnockback = false)
        {
            GameObject gameObject = Fixture.CreateGameObject(name);
            gameObject.SetActive(false);

            gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<SpriteRenderer>();

            if (withKnockback)
                gameObject.AddComponent(ResolveRuntimeType(KnockbackType));

            Component enemy = gameObject.AddComponent(ResolveRuntimeType(enemyTypeName));
            ReflectionTestUtilities.SetInstanceField(enemy, "enemyData", data);

            if (profile != null)
                ReflectionTestUtilities.SetInstanceField(enemy, "aiProfile", profile);

            return gameObject;
        }

        private static ScriptableObject CreateEnemyData(int maxHealth, float speed)
        {
            Type dataType = ResolveRuntimeType(EnemyDataType);
            ScriptableObject data = ScriptableObject.CreateInstance(dataType);
            ReflectionTestUtilities.SetInstanceField(data, "maxHealth", maxHealth);
            ReflectionTestUtilities.SetInstanceField(data, "speed", speed);
            return data;
        }

        private static ScriptableObject CreateProfile(string profileTypeName)
        {
            Type profileType = ResolveRuntimeType(profileTypeName);
            return ScriptableObject.CreateInstance(profileType);
        }

        private static int GetHealth(Component enemy)
        {
            return ReflectionTestUtilities.GetInstanceField<int>(enemy, "currentHealth");
        }

        private static object GetSpearState(Component spear, string stateName)
        {
            object current = ReflectionTestUtilities.GetInstanceField<object>(spear, "currentState");
            return Enum.Parse(current.GetType(), stateName);
        }

        private static bool IsSpearThreatening(Component recall, Component spear)
        {
            return ReflectionTestUtilities.InvokeInstance<bool>(
                recall,
                "IsSpearThreatening",
                spear);
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
