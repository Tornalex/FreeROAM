using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase1
{
    /// <summary>
    /// Phase 1 player architecture regression tests.
    /// These tests are read-only with respect to project assets: they may
    /// instantiate the Player prefab in memory, but they never save it or any
    /// ScriptableObject.
    /// </summary>
    [TestFixture]
    public class PlayerArchitectureTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Utilities/Player.prefab";
        private const string StatsPath = "Assets/Data/Player Data/Player Stats Data.asset";
        private const string EssencePath = "Assets/Data/Player Data/Player Essence Data.asset";

        [Test]
        public void PlayerPrefabComponentEnabledStatesMatchBaseline()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player prefab is missing.");

            Assert.IsFalse(prefab.GetComponent<PlayerDash>().enabled, "PlayerDash must start disabled.");
            Assert.IsFalse(
                prefab.GetComponent<PlayerRopeClimb>().enabled,
                "PlayerRopeClimb must start disabled.");
            Assert.IsFalse(prefab.GetComponent<PlayerPogo>().enabled, "PlayerPogo must start disabled.");

            Assert.IsTrue(prefab.GetComponent<Player>().enabled, "Player must be enabled.");
            Assert.IsTrue(prefab.GetComponent<PlayerMovement>().enabled, "PlayerMovement must be enabled.");
            Assert.IsTrue(prefab.GetComponent<PlayerJump>().enabled, "PlayerJump must be enabled.");
            Assert.IsTrue(prefab.GetComponent<PlayerCombat>().enabled, "PlayerCombat must be enabled.");
            Assert.IsTrue(prefab.GetComponent<PlayerHealth>().enabled, "PlayerHealth must be enabled.");
            Assert.IsTrue(prefab.GetComponent<Knockback>().enabled, "PlayerKnockback must be enabled.");
            Assert.IsTrue(
                prefab.GetComponent<PlayerInputHandler>().enabled,
                "PlayerInputHandler must be enabled.");
            Assert.IsTrue(prefab.GetComponent<PlayerInteract>().enabled, "PlayerInteract must be enabled.");
            Assert.IsTrue(prefab.GetComponent<PlayerEssence>().enabled, "PlayerEssence must be enabled.");

            PlayerFeet feet = prefab.GetComponentInChildren<PlayerFeet>(true);
            Assert.IsNotNull(feet, "PlayerFeet must be present.");
            Assert.IsTrue(feet.enabled, "PlayerFeet must be enabled.");
        }

        [Test]
        public void PlayerRuntimeStateDoesNotMutateSharedDataAssets()
        {
            // Load the real assets referenced by the Player prefab.
            PlayerStatsData stats = AssetDatabase.LoadAssetAtPath<PlayerStatsData>(StatsPath);
            PlayerEssenceData essence = AssetDatabase.LoadAssetAtPath<PlayerEssenceData>(EssencePath);
            Assert.IsNotNull(stats, "Player Stats Data asset is missing.");
            Assert.IsNotNull(essence, "Player Essence Data asset is missing.");

            // Snapshot every configurable value before any runtime activity.
            string statsBefore = Snapshot(stats);
            string essenceBefore = Snapshot(essence);

            GameObject instance = null;
            try
            {
                instance = GameObject.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));

                Assert.IsNotNull(instance, "Player prefab instance was not created.");

                Player player = instance.GetComponent<Player>();
                Assert.IsNotNull(player, "Player component is missing on the instance.");

                // EditMode instantiation does not reliably invoke Awake before
                // the assertion, so invoke the affected components explicitly.
                InvokeAwake(typeof(Player), player);
                InvokeAwake(typeof(PlayerHealth), player.Health);
                InvokeAwake(typeof(PlayerEssence), player.Essence);
                InvokeAwake(typeof(Knockback), player.Knockback);

                Assert.IsNotNull(player.Health, "PlayerHealth was not cached by Player.Awake.");
                Assert.IsNotNull(player.Essence, "PlayerEssence was not cached by Player.Awake.");

                // Runtime state starts from the shared configuration.
                int maxHealth = stats.maxHealth;
                Assert.AreEqual(
                    maxHealth,
                    player.Health.CurrentHealth,
                    "Runtime health did not start at the configured maximum.");

                // Actually damage the player, through the runtime component.
                int healthBeforeDamage = player.Health.CurrentHealth;
                bool damaged = player.Health.TakeDamage(1, Vector2.zero);
                Assert.IsTrue(damaged, "TakeDamage did not apply.");
                Assert.AreEqual(
                    healthBeforeDamage - 1,
                    player.Health.CurrentHealth,
                    "Runtime health did not decrease after damage.");
                Assert.Less(
                    player.Health.CurrentHealth,
                    maxHealth,
                    "Runtime health must be below the maximum before healing.");

                // Modify health and Essence through their runtime components.
                player.Health.Heal(1);
                Assert.AreEqual(
                    healthBeforeDamage,
                    player.Health.CurrentHealth,
                    "Runtime health did not increase after healing.");

                float essenceBeforeCatch = player.Essence.CurrentEssence;
                player.Essence.AddEssenceFromCatch(false);
                Assert.Greater(
                    player.Essence.CurrentEssence,
                    essenceBeforeCatch,
                    "Runtime essence did not increase after a catch.");

                // The shared authoring assets must be byte-for-byte identical.
                Assert.AreEqual(
                    statsBefore,
                    Snapshot(stats),
                    "Runtime state mutated the shared PlayerStatsData asset.");
                Assert.AreEqual(
                    essenceBefore,
                    Snapshot(essence),
                    "Runtime state mutated the shared PlayerEssenceData asset.");
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static string Snapshot(PlayerStatsData data)
        {
            return string.Join(
                "|",
                new[]
                {
                    F(data.walkSpeed),
                    F(data.runSpeed),
                    F(data.knockbackInterruptThreshold),
                    F(data.moveInputThreshold),
                    F(data.jumpForce),
                    F(data.jumpCutMultiplier),
                    F(data.jumpBufferTime),
                    F(data.coyoteTime),
                    data.maxHealth.ToString(CultureInfo.InvariantCulture),
                    F(data.iFramesDuration),
                    F(data.damageForce.x) + "," + F(data.damageForce.y),
                    F(data.damageKnockbackDuration),
                    data.contactDamage.ToString(CultureInfo.InvariantCulture),
                    F(data.iFramesBlinkInterval),
                    F(data.iFramesBlinkMinAlpha),
                    F(data.groundNormalThreshold),
                    F(data.groundPointOffset)
                });
        }

        private static string Snapshot(PlayerEssenceData data)
        {
            return string.Join(
                "|",
                new[]
                {
                    F(data.maxEssence),
                    F(data.essencePerBaseCatch),
                    F(data.essencePerPerfectCatch)
                });
        }

        private static string F(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void InvokeAwake(Type type, Component component)
        {
            MethodInfo awake = type.GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake, type.Name + ".Awake method is missing.");
            awake.Invoke(component, null);
        }

        [Test]
        public void PlayerRuntimeStateIsStoredOnComponentsNotAssets()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player prefab is missing.");

            PlayerHealth health = prefab.GetComponent<PlayerHealth>();
            PlayerEssence essence = prefab.GetComponent<PlayerEssence>();
            Assert.IsNotNull(health, "PlayerHealth component is missing.");
            Assert.IsNotNull(essence, "PlayerEssence component is missing.");

            FieldInfo currentHealthField = typeof(PlayerHealth).GetField(
                "_currentHealth",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo currentEssenceField = typeof(PlayerEssence).GetField(
                "_currentEssence",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(currentHealthField, "PlayerHealth._currentHealth field is missing.");
            Assert.IsNotNull(currentEssenceField, "PlayerEssence._currentEssence field is missing.");

            PlayerStatsData stats = AssetDatabase.LoadAssetAtPath<PlayerStatsData>(StatsPath);
            PlayerEssenceData essenceData = AssetDatabase.LoadAssetAtPath<PlayerEssenceData>(EssencePath);
            Assert.IsNotNull(stats, "Player Stats Data asset is missing.");
            Assert.IsNotNull(essenceData, "Player Essence Data asset is missing.");

            int maxHealthBefore = stats.maxHealth;
            float maxEssenceBefore = essenceData.maxEssence;

            int originalHealth = (int)currentHealthField.GetValue(health);
            float originalEssence = (float)currentEssenceField.GetValue(essence);

            try
            {
                currentHealthField.SetValue(health, originalHealth - 1);
                currentEssenceField.SetValue(essence, originalEssence + 10f);

                Assert.AreEqual(
                    maxHealthBefore,
                    stats.maxHealth,
                    "Runtime health state must not be written to the shared stats asset.");
                Assert.AreEqual(
                    maxEssenceBefore,
                    essenceData.maxEssence,
                    "Runtime essence state must not be written to the shared essence asset.");
            }
            finally
            {
                currentHealthField.SetValue(health, originalHealth);
                currentEssenceField.SetValue(essence, originalEssence);
            }
        }
    }
}
