using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpearWander.Abilities;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Captures the current ability data, the current progression list in the
    /// Master scene, and the current duplicate pickup wiring. Progression is
    /// intentionally preserved as-is; Rope and Pogo are not added here.
    /// </summary>
    [TestFixture]
    public class AbilityAndProgressionBaselineTests
    {
        private const string SpearAbilityPath = "Assets/Data/Ability Data/Spear Ability Data.asset";
        private const string DashAbilityPath = "Assets/Data/Ability Data/Dash Ability Data.asset";
        private const string SpearPickupPrefabPath = "Assets/Prefabs/Props/SpearPickup.prefab";
        private const string AbilityPickupPrefabPath = "Assets/Prefabs/Props/Ability Pickup.prefab";

        [Test]
        public void SpearAbilityDataMatchesCapturedBaseline()
        {
            AbilityData data = BaselineTestSupport.LoadAsset<AbilityData>(SpearAbilityPath);

            Assert.That(data.AbilityType, Is.EqualTo(AbilityType.Spear), "abilityType");
            Assert.That(data.DisplayName, Is.EqualTo("Spear"), "displayName");
            Assert.That(
                data.Description,
                Is.EqualTo("Press Fire to throw; press Fire again to recall."),
                "description");
            Assert.That(data.PromptMessage, Is.EqualTo("Interact - E"), "promptMessage");
            Assert.IsTrue(data.DestroyOnPickup, "destroyOnPickup");
            Assert.That(data.ComponentName, Is.EqualTo("PlayerCombat"), "componentName");
            Assert.IsTrue(data.EnableComponent, "enableComponent");
            Assert.IsTrue(data.RequiresPlayerCombat, "requiresPlayerCombat");
            Assert.IsFalse(data.RequiresRopeClimb, "requiresRopeClimb");
        }

        [Test]
        public void DashAbilityDataMatchesCapturedBaseline()
        {
            AbilityData data = BaselineTestSupport.LoadAsset<AbilityData>(DashAbilityPath);

            Assert.That(data.AbilityType, Is.EqualTo(AbilityType.Dash), "abilityType");
            Assert.That(data.DisplayName, Is.EqualTo("Dash"), "displayName");
            Assert.That(data.Description, Is.EqualTo("Left Shift to Use"), "description");
            Assert.That(data.PromptMessage, Is.EqualTo("Interact - E"), "promptMessage");
            Assert.IsTrue(data.DestroyOnPickup, "destroyOnPickup");
            Assert.That(data.ComponentName, Is.EqualTo("PlayerDash"), "componentName");
            Assert.IsTrue(data.EnableComponent, "enableComponent");
            Assert.IsFalse(data.RequiresPlayerCombat, "requiresPlayerCombat");
            Assert.IsFalse(data.RequiresRopeClimb, "requiresRopeClimb");
        }

        [Test]
        public void MasterSceneAllAbilitiesListMatchesCurrentProgression()
        {
            List<string> abilityPaths = ReadAllAbilityPaths();

            CollectionAssert.AreEqual(
                new[] { SpearAbilityPath, DashAbilityPath },
                abilityPaths,
                "The current progression baseline must remain Spear followed by Dash. " +
                "Rope and Pogo are intentionally not part of this list.");
        }

        [Test]
        public void SpearPickupPrefabUsesTheAuthoritativeAbilityPickup()
        {
            GameObject prefab = BaselineTestSupport.LoadPrefab(SpearPickupPrefabPath);
            Component pickup = prefab.GetComponent(typeof(SpearWander.Abilities.AbilityPickup));

            Assert.IsNotNull(
                pickup,
                "SpearPickup.prefab must use the authoritative AbilityPickup component.");

            BaselineTestSupport.AssertReference(
                pickup,
                "abilityData",
                BaselineTestSupport.LoadAsset<AbilityData>(SpearAbilityPath));
            Assert.That(
                BaselineTestSupport.Text(pickup, "overridePrompt"),
                Is.EqualTo("Premi E per raccogliere"),
                "overridePrompt");
        }

        [Test]
        public void AbilityPickupPrefabStillUsesTheNamespacedPickupComponent()
        {
            GameObject prefab = BaselineTestSupport.LoadPrefab(AbilityPickupPrefabPath);
            Component pickup = prefab.GetComponent(typeof(SpearWander.Abilities.AbilityPickup));

            Assert.IsNotNull(
                pickup,
                "Ability Pickup.prefab must keep using the namespaced AbilityPickup component.");

            BaselineTestSupport.AssertReference(
                pickup,
                "abilityData",
                BaselineTestSupport.LoadAsset<AbilityData>(SpearAbilityPath));
            Assert.That(
                BaselineTestSupport.Text(pickup, "overridePrompt"),
                Is.EqualTo(string.Empty),
                "overridePrompt");
        }

        private static List<string> ReadAllAbilityPaths()
        {
            string absolutePath = Path.Combine(
                Application.dataPath,
                "Scenes",
                "Master.unity");
            Assert.IsTrue(File.Exists(absolutePath), "Master scene not found: " + absolutePath);

            string sceneText = File.ReadAllText(absolutePath);
            Match block = Regex.Match(
                sceneText,
                @"(?m)^\s*allAbilities:\s*\r?\n((?:\s*-\s*\{[^\r\n]*\}\s*\r?\n)+)");

            Assert.IsTrue(block.Success, "The allAbilities list was not found in Master.unity.");

            MatchCollection references = Regex.Matches(
                block.Groups[1].Value,
                @"guid:\s*([0-9a-fA-F]{32})");
            Assert.That(references.Count, Is.EqualTo(2), "allAbilities GUID count");

            List<string> paths = new List<string>(references.Count);
            foreach (Match reference in references)
            {
                string path = AssetDatabase.GUIDToAssetPath(reference.Groups[1].Value);
                Assert.IsFalse(
                    string.IsNullOrEmpty(path),
                    "allAbilities contains an unresolved GUID: " +
                    reference.Groups[1].Value);
                paths.Add(path);
            }

            return paths;
        }
    }
}
