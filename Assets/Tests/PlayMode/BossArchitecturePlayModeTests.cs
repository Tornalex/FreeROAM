using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Phase 3 runtime coverage for the boss data separation:
    /// - BossData keeps health on the instance and is never mutated;
    /// - two bosses sharing the same assets keep independent state;
    /// - damage still runs through the real lifecycle without interrupting the boss;
    /// - death still runs through the real lifecycle;
    /// - the weak point multiplier reaches the boss health;
    /// - the tongue and spore attacks read their timings, reach and cooldowns
    ///   from the assigned attack assets.
    /// </summary>
    public class BossArchitecturePlayModeTests : PlayModeTestBase
    {
        private const string BossType = "SpearWander.Boss.FreeRoamBoss";
        private const string WeakPointType = "SpearWander.Boss.WeakPoint";
        private const string KnockbackType = "Knockback";
        private const string BossDataType = "BossData";
        private const string BossAIProfileType = "BossAIProfile";
        private const string TongueAttackDataType = "TongueAttackData";
        private const string SporeAttackDataType = "SporeAttackData";

        [UnityTest]
        public IEnumerator BossDamageAndDeathUseRuntimeStateOnly()
        {
            ScriptableObject data = CreateBossData(maxHealth: 6);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ScriptableObject spores = CreateAsset(SporeAttackDataType);

            CreatePlayerProxy(new Vector3(20f, 0f, 0f));

            GameObject firstObject = null;
            GameObject secondObject = null;
            try
            {
                firstObject = CreateBoss(
                    "Phase3BossA",
                    data,
                    profile,
                    tongue,
                    spores,
                    withKnockback: true);
                secondObject = CreateBoss(
                    "Phase3BossB",
                    data,
                    profile,
                    tongue,
                    spores);

                firstObject.SetActive(true);
                secondObject.SetActive(true);
                yield return null;

                Component first = firstObject.GetComponent(ResolveRuntimeType(BossType));
                Component second = secondObject.GetComponent(ResolveRuntimeType(BossType));

                Assert.That(GetHealth(first), Is.EqualTo(6));
                Assert.That(GetHealth(second), Is.EqualTo(6));

                bool damaged = ReflectionTestUtilities.InvokeInstance<bool>(
                    first,
                    "TakeDamage",
                    2,
                    Vector2.zero,
                    new Vector2(-1f, 0f));

                Assert.That(damaged, Is.True, "TakeDamage did not report applied damage.");
                Assert.That(GetHealth(first), Is.EqualTo(4));
                Assert.That(
                    GetHealth(second),
                    Is.EqualTo(6),
                    "The second boss sharing the same BossData changed health.");

                Component knockback = firstObject.GetComponent(
                    ResolveRuntimeType(KnockbackType));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<bool>(knockback, "IsKnockedBack"),
                    Is.False,
                    "Damage started knockback; the free-roam boss must ignore hit interruption.");

                bool killed = ReflectionTestUtilities.InvokeInstance<bool>(
                    first,
                    "TakeDamage",
                    10,
                    Vector2.zero,
                    new Vector2(-1f, 0f));

                Assert.That(killed, Is.True);
                Assert.That(
                    ReflectionTestUtilities.GetProperty<bool>(first, "IsDead"),
                    Is.True,
                    "Boss was not marked dead at zero health.");
                Assert.That(GetHealth(first), Is.LessThanOrEqualTo(0));

                Assert.That(
                    GetHealth(second),
                    Is.EqualTo(6),
                    "The surviving boss was affected by its neighbour's death.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(data, "maxHealth"),
                    Is.EqualTo(6),
                    "Runtime combat mutated the shared BossData asset.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(
                        data,
                        "weakPointDamageMultiplier"),
                    Is.EqualTo(5f).Within(0.0001f),
                    "Runtime combat mutated the shared BossData asset.");
            }
            finally
            {
                DestroyObject(firstObject);
                DestroyObject(secondObject);
                DestroyObject(spores);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator WeakPointForwardsMultipliedDamageToTheBoss()
        {
            const int maxHealth = 12;
            const float multiplier = 3f;
            ScriptableObject data = CreateBossData(maxHealth);
            ReflectionTestUtilities.SetInstanceField(
                data,
                "weakPointDamageMultiplier",
                multiplier);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ScriptableObject spores = CreateAsset(SporeAttackDataType);

            CreatePlayerProxy(new Vector3(20f, 0f, 0f));

            GameObject bossObject = null;
            try
            {
                bossObject = CreateBoss(
                    "Phase3WeakPointBoss",
                    data,
                    profile,
                    tongue,
                    spores,
                    withWeakPoint: true);
                bossObject.SetActive(true);
                yield return null;

                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossType));
                Component weakPoint = bossObject.GetComponentInChildren(
                    ResolveRuntimeType(WeakPointType),
                    true);

                Assert.IsNotNull(weakPoint, "WeakPoint was not created.");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<float>(weakPoint, "DamageMultiplier"),
                    Is.EqualTo(multiplier).Within(0.0001f),
                    "The boss did not sync BossData.weakPointDamageMultiplier.");

                Vector2 hitPoint = weakPoint.transform.position;
                bool hit = ReflectionTestUtilities.InvokeInstance<bool>(
                    weakPoint,
                    "TakeDamage",
                    1,
                    hitPoint,
                    hitPoint + Vector2.left);

                Assert.That(hit, Is.True, "WeakPoint.TakeDamage did not report the hit.");
                Assert.That(
                    GetHealth(boss),
                    Is.EqualTo(maxHealth - 3),
                    "WeakPoint damage did not forward baseDamage * multiplier to the boss.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(
                        data,
                        "weakPointDamageMultiplier"),
                    Is.EqualTo(multiplier).Within(0.0001f),
                    "The weak point hit mutated the shared BossData asset.");
            }
            finally
            {
                DestroyObject(bossObject);
                DestroyObject(spores);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator TongueAttackUsesTongueAttackData()
        {
            const float windup = 0.8f;
            const float cooldown = 1.75f;
            const float range = 2.5f;

            ScriptableObject data = CreateBossData(maxHealth: 10);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ReflectionTestUtilities.SetInstanceField(tongue, "damage", 7);
            ReflectionTestUtilities.SetInstanceField(tongue, "cooldown", cooldown);
            ReflectionTestUtilities.SetInstanceField(tongue, "windupTime", windup);
            ReflectionTestUtilities.SetInstanceField(tongue, "extendTime", 0.1f);
            ReflectionTestUtilities.SetInstanceField(tongue, "retractTime", 0.1f);
            ReflectionTestUtilities.SetInstanceField(tongue, "recoveryTime", 0.2f);
            ReflectionTestUtilities.SetInstanceField(tongue, "range", range);
            ReflectionTestUtilities.SetInstanceField(tongue, "width", 0.2f);

            ScriptableObject spores = CreateAsset(SporeAttackDataType);

            CreatePlayerProxy(new Vector3(10f, 0f, 0f));

            GameObject bossObject = null;
            try
            {
                bossObject = CreateBoss(
                    "Phase3TongueBoss",
                    data,
                    profile,
                    tongue,
                    spores);
                bossObject.SetActive(true);
                yield return null;

                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossType));

                Assert.That(
                    ReflectionTestUtilities.GetProperty<float>(boss, "TongueWindupTime"),
                    Is.EqualTo(windup).Within(0.0001f));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(boss, "TongueDamage"),
                    Is.EqualTo(7));
                Assert.That(
                    ReflectionTestUtilities.GetProperty<float>(boss, "TongueCooldown"),
                    Is.EqualTo(cooldown).Within(0.0001f));

                ReflectionTestUtilities.InvokeInstance(boss, "StartTongueAttack");

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(boss, "state").ToString(),
                    Is.EqualTo("TongueWindup"),
                    "The tongue attack did not enter its wind-up state.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(boss, "stateTimer"),
                    Is.EqualTo(windup).Within(0.0001f),
                    "The tongue wind-up did not come from TongueAttackData.");

                Vector2 start = ReflectionTestUtilities.GetInstanceField<Vector2>(
                    boss,
                    "tongueStart");
                Vector2 target = ReflectionTestUtilities.GetInstanceField<Vector2>(
                    boss,
                    "tongueTarget");
                Assert.That(
                    Vector2.Distance(start, target),
                    Is.EqualTo(range).Within(0.01f),
                    "The tongue reach did not come from TongueAttackData.");

                object tongueAction = GetActionType(boss, "Tongue");
                ReflectionTestUtilities.InvokeInstance(boss, "FinishAttack", tongueAction, 0f);

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(
                        boss,
                        "tongueCooldownTimer"),
                    Is.EqualTo(cooldown).Within(0.0001f),
                    "The tongue cooldown did not come from TongueAttackData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(tongue, "damage"),
                    Is.EqualTo(7),
                    "Runtime attack state mutated the shared TongueAttackData asset.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(tongue, "cooldown"),
                    Is.EqualTo(cooldown).Within(0.0001f),
                    "Runtime attack state mutated the shared TongueAttackData asset.");
            }
            finally
            {
                DestroyObject(bossObject);
                DestroyObject(spores);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator SporeAttackUsesSporeAttackData()
        {
            const int damage = 4;
            const float lifetime = 1.5f;
            const float radius = 2.5f;
            const float windup = 0.25f;
            const float cooldown = 3.25f;

            ScriptableObject data = CreateBossData(maxHealth: 10);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ScriptableObject spores = CreateAsset(SporeAttackDataType);
            ReflectionTestUtilities.SetInstanceField(spores, "damage", damage);
            ReflectionTestUtilities.SetInstanceField(spores, "lifetime", lifetime);
            ReflectionTestUtilities.SetInstanceField(spores, "radius", radius);
            ReflectionTestUtilities.SetInstanceField(spores, "windupTime", windup);
            ReflectionTestUtilities.SetInstanceField(spores, "cooldown", cooldown);
            ReflectionTestUtilities.SetInstanceField(spores, "maxActiveSpores", 2);
            ReflectionTestUtilities.SetInstanceField(spores, "range", 12f);
            ReflectionTestUtilities.SetInstanceField(spores, "drift", Vector2.zero);

            CreatePlayerProxy(new Vector3(10f, 0f, 0f));

            GameObject bossObject = null;
            List<Component> spawnedSpores = new List<Component>();
            try
            {
                bossObject = CreateBoss(
                    "Phase3SporeBoss",
                    data,
                    profile,
                    tongue,
                    spores);
                bossObject.SetActive(true);
                yield return null;

                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossType));

                ReflectionTestUtilities.InvokeInstance(boss, "StartSporeAttack");

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(boss, "state").ToString(),
                    Is.EqualTo("Spores"),
                    "The spore attack did not enter its state.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(boss, "stateTimer"),
                    Is.EqualTo(windup).Within(0.0001f),
                    "The spore wind-up did not come from SporeAttackData.");

                Component cloud = GetSingleSpawnedSpore(boss, spawnedSpores);
                Assert.IsNotNull(cloud, "The spore attack spawned no cloud.");

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(cloud, "damage"),
                    Is.EqualTo(damage),
                    "The spore damage did not come from SporeAttackData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(cloud, "lifetime"),
                    Is.EqualTo(lifetime).Within(0.0001f),
                    "The spore lifetime did not come from SporeAttackData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(cloud, "radius"),
                    Is.EqualTo(radius).Within(0.0001f),
                    "The spore radius did not come from SporeAttackData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(cloud, "activationDelay"),
                    Is.EqualTo(windup).Within(0.0001f),
                    "The spore activation delay did not come from SporeAttackData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<Vector2>(cloud, "driftVelocity"),
                    Is.EqualTo(Vector2.zero),
                    "The spore drift did not come from SporeAttackData.");

                object sporeAction = GetActionType(boss, "Spores");
                ReflectionTestUtilities.InvokeInstance(boss, "FinishAttack", sporeAction, 0f);

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(
                        boss,
                        "sporeCooldownTimer"),
                    Is.EqualTo(cooldown).Within(0.0001f),
                    "The spore cooldown did not come from SporeAttackData.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<int>(spores, "damage"),
                    Is.EqualTo(damage),
                    "Runtime attack state mutated the shared SporeAttackData asset.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(spores, "cooldown"),
                    Is.EqualTo(cooldown).Within(0.0001f),
                    "Runtime attack state mutated the shared SporeAttackData asset.");
            }
            finally
            {
                DestroySpores(spawnedSpores);
                DestroyObject(bossObject);
                DestroyObject(spores);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        private GameObject CreateBoss(
            string name,
            ScriptableObject bossData,
            ScriptableObject profile,
            ScriptableObject tongue,
            ScriptableObject spores,
            bool withWeakPoint = false,
            bool withKnockback = false)
        {
            GameObject bossObject = Fixture.CreateGameObject(name);
            bossObject.SetActive(false);
            bossObject.transform.position = Vector3.zero;

            bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            bossObject.AddComponent<SpriteRenderer>();
            bossObject.AddComponent<Animator>();

            if (withKnockback)
                bossObject.AddComponent(ResolveRuntimeType(KnockbackType));

            Component boss = bossObject.AddComponent(ResolveRuntimeType(BossType));
            ReflectionTestUtilities.SetInstanceField(boss, "bossData", bossData);
            ReflectionTestUtilities.SetInstanceField(boss, "aiProfile", profile);
            ReflectionTestUtilities.SetInstanceField(boss, "tongueAttackData", tongue);
            ReflectionTestUtilities.SetInstanceField(boss, "sporeAttackData", spores);

            if (withWeakPoint)
            {
                GameObject weakObject = new GameObject(name + " WeakPoint");
                weakObject.transform.SetParent(bossObject.transform, false);
                weakObject.AddComponent<CircleCollider2D>();
                weakObject.AddComponent<SpriteRenderer>();
                Component weakPoint = weakObject.AddComponent(
                    ResolveRuntimeType(WeakPointType));
                ReflectionTestUtilities.SetInstanceField(boss, "weakPoint", weakPoint);
            }

            return bossObject;
        }

        private GameObject CreatePlayerProxy(Vector3 position)
        {
            GameObject player = Fixture.CreateGameObject("Phase3PlayerProxy");
            player.tag = "Player";
            player.transform.position = position;
            return player;
        }

        private static ScriptableObject CreateBossData(int maxHealth)
        {
            ScriptableObject data = CreateAsset(BossDataType);
            ReflectionTestUtilities.SetInstanceField(data, "maxHealth", maxHealth);
            ReflectionTestUtilities.SetInstanceField(
                data,
                "knockbackForce",
                new Vector2(10f, 5f));
            ReflectionTestUtilities.SetInstanceField(data, "knockbackDuration", 0.1f);
            ReflectionTestUtilities.SetInstanceField(
                data,
                "weakPointDamageMultiplier",
                5f);
            return data;
        }

        private static ScriptableObject CreateAsset(string typeName)
        {
            return ScriptableObject.CreateInstance(ResolveRuntimeType(typeName));
        }

        private static int GetHealth(Component boss)
        {
            return ReflectionTestUtilities.GetProperty<int>(boss, "CurrentHealth");
        }

        private static object GetActionType(Component boss, string actionName)
        {
            Type actionType = boss.GetType().GetNestedType(
                "ActionType",
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(actionType, "FreeRoamBoss.ActionType was not found.");
            return Enum.Parse(actionType, actionName);
        }

        private static Component GetSingleSpawnedSpore(
            Component boss,
            List<Component> collected)
        {
            object list = ReflectionTestUtilities.GetInstanceField(boss, "activeSpores");
            Assert.That(list, Is.InstanceOf<IEnumerable>(), "activeSpores is not enumerable.");

            Component found = null;
            foreach (object item in (IEnumerable)list)
            {
                Component cloud = item as Component;
                if (cloud == null)
                    continue;

                collected.Add(cloud);
                if (found == null)
                    found = cloud;
            }

            return found;
        }

        private static void DestroySpores(List<Component> spores)
        {
            for (int i = 0; i < spores.Count; i++)
            {
                if (spores[i] != null)
                    UnityEngine.Object.Destroy(spores[i].gameObject);
            }

            spores.Clear();
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
