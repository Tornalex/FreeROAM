using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Focused runtime coverage for the horizontal tongue sweep:
    /// - the sampled horizontal direction is kept for the whole attack;
    /// - the lowered emission point is restored when the attack ends;
    /// - the sweep stops before a blocking obstacle;
    /// - the attack survives losing the player and restores on boss death.
    /// </summary>
    public class HorizontalTongueBossPlayModeTests : PlayModeTestBase
    {
        private const string BossType = "SpearWander.Boss.FreeRoamBoss";
        private const string BossDataType = "BossData";
        private const string BossAIProfileType = "BossAIProfile";
        private const string TongueAttackDataType = "TongueAttackData";
        private const string HorizontalTongueAttackDataType = "HorizontalTongueAttackData";
        private const string SporeAttackDataType = "SporeAttackData";

        [UnityTest]
        public IEnumerator SweepKeepsSampledDirectionAndRestoresOrigin()
        {
            ScriptableObject data = CreateBossData(10);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ScriptableObject horizontal = CreateHorizontalData();
            ScriptableObject spores = CreateAsset(SporeAttackDataType);
            ReflectionTestUtilities.SetInstanceField(profile, "horizontalTongueWeight", 2f);

            GameObject player = CreatePlayerProxy(new Vector3(5f, 0f, 0f));

            GameObject bossObject = null;
            try
            {
                bossObject = CreateBoss(
                    "HorizontalTongueBoss",
                    data,
                    profile,
                    tongue,
                    horizontal,
                    spores,
                    out Transform horizontalOrigin);

                bossObject.SetActive(true);
                yield return null;

                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossType));
                Vector3 baseLocalPosition = horizontalOrigin.localPosition;

                Assert.That(
                    ReflectionTestUtilities.GetProperty<float>(boss, "HorizontalTongueWeight"),
                    Is.EqualTo(2f).Within(0.0001f),
                    "The selection weight did not come from the AI profile.");

                ReflectionTestUtilities.InvokeInstance(boss, "StartHorizontalTongueAttack");

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<object>(boss, "state").ToString(),
                    Is.EqualTo("HorizontalTongueWindup"),
                    "The horizontal tongue did not enter its wind-up state.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(boss, "horizontalTongueDirection"),
                    Is.EqualTo(1f).Within(0.0001f),
                    "The attack did not sample the player's horizontal direction.");

                // The player crosses to the other side during the wind-up. The
                // attack must keep the direction sampled at the start.
                player.transform.position = new Vector3(-6f, 2f, 0f);
                yield return new WaitForSeconds(0.22f);

                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(boss, "horizontalTongueDirection"),
                    Is.EqualTo(1f).Within(0.0001f),
                    "The horizontal tongue retargeted after the player changed direction.");
                string launchState =
                    ReflectionTestUtilities.GetInstanceField<object>(boss, "state").ToString();
                Assert.That(
                    launchState == "HorizontalTongueExtend"
                        || launchState == "HorizontalTongueRetract",
                    Is.True,
                    "The horizontal tongue did not launch after the wind-up.");

                Vector2 target = ReflectionTestUtilities.GetInstanceField<Vector2>(
                    boss,
                    "tongueTarget");
                Assert.That(
                    target.x,
                    Is.GreaterThan(0.5f),
                    "The launch target followed the player instead of the sampled direction.");
                Assert.That(
                    horizontalOrigin.localPosition.y,
                    Is.LessThan(baseLocalPosition.y - 0.5f),
                    "The emission point was not lowered for the sweep.");

                // Losing the player mid-attack must not break the state machine.
                UnityEngine.Object.Destroy(player);
                player = null;
                yield return null;
                yield return new WaitForSeconds(1.6f);

                Assert.That(
                    Vector3.Distance(horizontalOrigin.localPosition, baseLocalPosition),
                    Is.LessThan(0.001f),
                    "The horizontal tongue origin was not restored after the attack.");
                Assert.That(
                    ReflectionTestUtilities.GetInstanceField<float>(
                        boss,
                        "horizontalTongueCooldownTimer"),
                    Is.GreaterThan(0f),
                    "The horizontal tongue cooldown was not started.");
                string finishedState =
                    ReflectionTestUtilities.GetInstanceField<object>(boss, "state").ToString();
                Assert.That(
                    finishedState != "HorizontalTongueWindup"
                        && finishedState != "HorizontalTongueExtend"
                        && finishedState != "HorizontalTongueRetract",
                    Is.True,
                    "The horizontal tongue state did not finish.");
            }
            finally
            {
                if (player != null)
                    UnityEngine.Object.Destroy(player);
                DestroyObject(bossObject);
                DestroyObject(spores);
                DestroyObject(horizontal);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator SweepStopsBeforeBlockingObstacle()
        {
            ScriptableObject data = CreateBossData(10);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ScriptableObject horizontal = CreateHorizontalData();
            ScriptableObject spores = CreateAsset(SporeAttackDataType);

            GameObject player = CreatePlayerProxy(new Vector3(5f, 0f, 0f));
            GameObject wall = Fixture.CreateGameObject("HorizontalTongueWall");
            wall.transform.position = new Vector3(3f, 0f, 0f);
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            wallCollider.size = new Vector2(0.2f, 6f);

            GameObject bossObject = null;
            try
            {
                bossObject = CreateBoss(
                    "HorizontalTongueWallBoss",
                    data,
                    profile,
                    tongue,
                    horizontal,
                    spores,
                    out _);

                bossObject.SetActive(true);
                yield return null;

                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossType));
                ReflectionTestUtilities.InvokeInstance(boss, "StartHorizontalTongueAttack");

                float reach = ReflectionTestUtilities.GetInstanceField<float>(
                    boss,
                    "horizontalTongueReach");
                Assert.That(
                    reach,
                    Is.GreaterThan(0.5f),
                    "The tongue found no reachable destination before the wall.");
                Assert.That(
                    reach,
                    Is.LessThan(3f),
                    "The tongue reach ignored the blocking wall.");

                Vector2 target = ReflectionTestUtilities.GetInstanceField<Vector2>(
                    boss,
                    "tongueTarget");
                Assert.That(
                    target.x,
                    Is.LessThan(3f),
                    "The tongue target crossed the blocking wall.");
            }
            finally
            {
                UnityEngine.Object.Destroy(player);
                UnityEngine.Object.Destroy(wall);
                DestroyObject(bossObject);
                DestroyObject(spores);
                DestroyObject(horizontal);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator BossDeathDuringSweepRestoresOrigin()
        {
            ScriptableObject data = CreateBossData(10);
            ScriptableObject profile = CreateAsset(BossAIProfileType);
            ScriptableObject tongue = CreateAsset(TongueAttackDataType);
            ScriptableObject horizontal = CreateHorizontalData();
            ScriptableObject spores = CreateAsset(SporeAttackDataType);

            GameObject player = CreatePlayerProxy(new Vector3(5f, 0f, 0f));
            GameObject bossObject = null;
            try
            {
                bossObject = CreateBoss(
                    "HorizontalTongueDeathBoss",
                    data,
                    profile,
                    tongue,
                    horizontal,
                    spores,
                    out Transform horizontalOrigin);

                bossObject.SetActive(true);
                yield return null;

                Component boss = bossObject.GetComponent(ResolveRuntimeType(BossType));
                Vector3 baseLocalPosition = horizontalOrigin.localPosition;

                ReflectionTestUtilities.InvokeInstance(boss, "StartHorizontalTongueAttack");
                yield return new WaitForSeconds(0.2f);

                Assert.That(
                    horizontalOrigin.localPosition.y,
                    Is.LessThan(baseLocalPosition.y - 0.5f),
                    "The emission point was not lowered before the death check.");

                ReflectionTestUtilities.InvokeInstance<bool>(
                    boss,
                    "TakeDamage",
                    999,
                    Vector2.zero,
                    new Vector2(-1f, 0f));

                Assert.That(
                    ReflectionTestUtilities.GetProperty<bool>(boss, "IsDead"),
                    Is.True,
                    "The boss did not die.");
                Assert.That(
                    Vector3.Distance(horizontalOrigin.localPosition, baseLocalPosition),
                    Is.LessThan(0.001f),
                    "Dying during the sweep did not restore the emission point.");
            }
            finally
            {
                UnityEngine.Object.Destroy(player);
                DestroyObject(bossObject);
                DestroyObject(spores);
                DestroyObject(horizontal);
                DestroyObject(tongue);
                DestroyObject(profile);
                DestroyObject(data);
            }
        }

        private ScriptableObject CreateHorizontalData()
        {
            ScriptableObject horizontal = CreateAsset(HorizontalTongueAttackDataType);
            ReflectionTestUtilities.SetInstanceField(horizontal, "damage", 3);
            ReflectionTestUtilities.SetInstanceField(horizontal, "cooldown", 2f);
            ReflectionTestUtilities.SetInstanceField(horizontal, "horizontalTongueWindupTime", 0.15f);
            ReflectionTestUtilities.SetInstanceField(horizontal, "horizontalTongueRecoveryTime", 0.1f);
            ReflectionTestUtilities.SetInstanceField(horizontal, "horizontalTongueMaxExtensionTime", 0.5f);
            ReflectionTestUtilities.SetInstanceField(horizontal, "horizontalTongueHeightOffset", 1f);
            ReflectionTestUtilities.SetInstanceField(horizontal, "horizontalTongueSpeed", 40f);
            return horizontal;
        }

        private GameObject CreateBoss(
            string name,
            ScriptableObject bossData,
            ScriptableObject profile,
            ScriptableObject tongue,
            ScriptableObject horizontal,
            ScriptableObject spores,
            out Transform horizontalOrigin)
        {
            GameObject bossObject = Fixture.CreateGameObject(name);
            bossObject.SetActive(false);
            bossObject.transform.position = Vector3.zero;

            bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            bossObject.AddComponent<SpriteRenderer>();
            bossObject.AddComponent<Animator>();

            GameObject originObject = new GameObject("Horizontal Tongue Origin");
            originObject.transform.SetParent(bossObject.transform, false);
            originObject.transform.localPosition = new Vector3(0f, 1f, 0f);
            horizontalOrigin = originObject.transform;

            Component boss = bossObject.AddComponent(ResolveRuntimeType(BossType));
            ReflectionTestUtilities.SetInstanceField(boss, "bossData", bossData);
            ReflectionTestUtilities.SetInstanceField(boss, "aiProfile", profile);
            ReflectionTestUtilities.SetInstanceField(boss, "tongueAttackData", tongue);
            ReflectionTestUtilities.SetInstanceField(
                boss,
                "horizontalTongueAttackData",
                horizontal);
            ReflectionTestUtilities.SetInstanceField(boss, "sporeAttackData", spores);
            ReflectionTestUtilities.SetInstanceField(
                boss,
                "horizontalTongueOrigin",
                horizontalOrigin);

            return bossObject;
        }

        private GameObject CreatePlayerProxy(Vector3 position)
        {
            GameObject player = Fixture.CreateGameObject("HorizontalTonguePlayerProxy");
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
