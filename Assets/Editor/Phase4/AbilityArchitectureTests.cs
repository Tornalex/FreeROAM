using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpearWander.Abilities;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase4
{
    [TestFixture]
    public class AbilityArchitectureTests
    {
        private const string AbilityDataGuid = "e32f7f8b2c96c134eaf1bfcdcceb8a41";
        private const string AbilityPickupGuid = "ec1ccb40b1413994995905c101e40f1f";
        private const string SpearDataPath = "Assets/Data/Ability Data/Spear Ability Data.asset";
        private const string DashDataPath = "Assets/Data/Ability Data/Dash Ability Data.asset";
        private const string SpearPickupPath = "Assets/Prefabs/Props/SpearPickup.prefab";
        private const string AbilityPickupPath = "Assets/Prefabs/Props/Ability Pickup.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/Utilities/Player.prefab";

        [Test]
        public void AbilityDataAssetsMatchSerializedAuthoringBaseline()
        {
            AbilityData spear = Load<AbilityData>(SpearDataPath);
            Assert.That(spear.AbilityType, Is.EqualTo(AbilityType.Spear));
            Assert.That(spear.DisplayName, Is.EqualTo("Spear"));
            Assert.That(
                spear.Description,
                Is.EqualTo("Press Fire to throw; press Fire again to recall."));
            Assert.That(spear.PromptMessage, Is.EqualTo("Interact - E"));
            Assert.That(spear.ComponentName, Is.EqualTo("PlayerCombat"));
            Assert.IsTrue(spear.EnableComponent);
            Assert.IsTrue(spear.DestroyOnPickup);

            AbilityData dash = Load<AbilityData>(DashDataPath);
            Assert.That(dash.AbilityType, Is.EqualTo(AbilityType.Dash));
            Assert.That(dash.DisplayName, Is.EqualTo("Dash"));
            Assert.That(dash.Description, Is.EqualTo("Left Shift to Use"));
            Assert.That(dash.PromptMessage, Is.EqualTo("Interact - E"));
            Assert.That(dash.ComponentName, Is.EqualTo("PlayerDash"));
            Assert.IsTrue(dash.EnableComponent);
            Assert.IsTrue(dash.DestroyOnPickup);

            AssertAssetContains(
                SpearDataPath,
                "abilityType: 1", "displayName: Spear", "componentName: PlayerCombat",
                "enableComponent: 1", "destroyOnPickup: 1");
            AssertAssetContains(
                DashDataPath,
                "abilityType: 2", "displayName: Dash", "componentName: PlayerDash",
                "enableComponent: 1", "destroyOnPickup: 1");
        }

        [Test]
        public void AbilityDataContainsConfigurationButNoRuntimeUnlockState()
        {
            Type dataType = typeof(AbilityData);
            Assert.IsNull(dataType.GetMethod("Unlock"), "AbilityData must not own unlock behavior.");
            Assert.IsNull(dataType.GetMethod("IsUnlocked"), "AbilityData must not own unlock state.");

            string[] runtimeStateNames =
            {
                "unlocked", "isUnlocked", "isEquipped", "isActive", "collected"
            };

            foreach (string stateName in runtimeStateNames)
            {
                Assert.IsNull(
                    dataType.GetField(
                        stateName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    "AbilityData must not store runtime state '" + stateName + "'.");
            }

            AssertFieldExists("abilityType");
            AssertFieldExists("displayName");
            AssertFieldExists("description");
            AssertFieldExists("promptMessage");
            AssertFieldExists("componentName");
            AssertFieldExists("enableComponent");
        }

        [Test]
        public void ThereIsOnlyOneAbilityPickupImplementation()
        {
            Type[] implementations = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => type.Name == "AbilityPickup")
                .ToArray();

            Assert.That(implementations.Length, Is.EqualTo(1));
            Assert.That(implementations[0], Is.EqualTo(typeof(AbilityPickup)));
            Assert.That(
                AssetDatabase.GUIDToAssetPath(AbilityPickupGuid),
                Is.EqualTo("Assets/Scripts/Abilities/AbilityPickup.cs"));
        }

        [Test]
        public void AbilityPickupPrefabAndSpearPickupPrefabUseTheAuthoritativeComponent()
        {
            GameObject abilityPickupPrefab = Load<GameObject>(AbilityPickupPath);
            GameObject spearPickupPrefab = Load<GameObject>(SpearPickupPath);

            AbilityPickup abilityPickup = abilityPickupPrefab.GetComponent<AbilityPickup>();
            AbilityPickup spearPickup = spearPickupPrefab.GetComponent<AbilityPickup>();
            Assert.IsNotNull(abilityPickup);
            Assert.IsNotNull(spearPickup);
            Assert.That(
                GetObjectReference(abilityPickup, "abilityData"),
                Is.SameAs(Load<AbilityData>(SpearDataPath)));
            Assert.That(
                GetObjectReference(spearPickup, "abilityData"),
                Is.SameAs(Load<AbilityData>(SpearDataPath)));
            Assert.That(
                GetString(spearPickup, "overridePrompt"),
                Is.EqualTo("Premi E per raccogliere"));

            Collider2D collider = spearPickupPrefab.GetComponent<Collider2D>();
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.isTrigger, "Spear pickup trigger collider was not preserved.");
        }

        [Test]
        public void AllSerializedAbilityPickupScriptsUseTheAuthoritativeGuid()
        {
            string[] files = Directory.GetFiles(
                Application.dataPath,
                "*.*",
                SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string path in files)
            {
                string text = File.ReadAllText(path);
                Assert.IsFalse(
                    text.Contains("df77003ad3a4d7c4ba2ab8484f532a4b"),
                    "Removed global AbilityPickup GUID is still referenced in " + path);
            }

            Assert.That(
                File.ReadAllText(Path.Combine(Application.dataPath, "Prefabs/Props/SpearPickup.prefab"))
                    .Contains("guid: " + AbilityPickupGuid),
                Is.True,
                "SpearPickup.prefab does not reference the authoritative script GUID.");

            Assert.That(
                File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "Scenes/Rooms/Legacy/2. Spear/R_Spear_6.unity"))
                    .Contains("guid: " + AbilityPickupGuid),
                Is.True,
                "The legacy scene pickup script reference was not preserved.");
        }

        [Test]
        public void MasterProgressionRemainsSpearThenDashOnly()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes/Master.unity");
            string text = File.ReadAllText(scenePath);
            int listStart = text.IndexOf("  allAbilities:", StringComparison.Ordinal);
            Assert.That(listStart, Is.GreaterThanOrEqualTo(0));

            int listEnd = text.IndexOf("--- !u!", listStart, StringComparison.Ordinal);
            Assert.That(listEnd, Is.GreaterThan(listStart));
            string listText = text.Substring(listStart, listEnd - listStart);
            string spearGuid = "guid: f7e172528dbfe8042a4acc44acdcb08e";
            string dashGuid = "guid: 80fc42830c66a4e4bb190deb93cfdbef";

            int spearIndex = listText.IndexOf(spearGuid, StringComparison.Ordinal);
            int dashIndex = listText.IndexOf(dashGuid, StringComparison.Ordinal);
            Assert.That(spearIndex, Is.GreaterThanOrEqualTo(0), "Spear data is missing.");
            Assert.That(dashIndex, Is.GreaterThan(spearIndex), "Dash must follow Spear.");
            Assert.That(
                listText.Split(new[] { "guid:" }, StringSplitOptions.None).Length - 1,
                Is.EqualTo(2));
            Assert.IsFalse(listText.Contains("Ability_Rope"));
            Assert.IsFalse(listText.Contains("Ability_Pogo"));
        }

        [Test]
        public void PlayerAbilityComponentEnabledStatesRemainAtBaseline()
        {
            GameObject playerPrefab = Load<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(playerPrefab.GetComponent<PlayerDash>());
            Assert.IsNotNull(playerPrefab.GetComponent<PlayerRopeClimb>());
            Assert.IsNotNull(playerPrefab.GetComponent<PlayerPogo>());
            Assert.IsFalse(playerPrefab.GetComponent<PlayerDash>().enabled);
            Assert.IsFalse(playerPrefab.GetComponent<PlayerRopeClimb>().enabled);
            Assert.IsFalse(playerPrefab.GetComponent<PlayerPogo>().enabled);
            Assert.IsTrue(playerPrefab.GetComponent<PlayerCombat>().enabled);
        }

        [Test]
        public void AbilityReferencesResolveAndLegacyAbilityAssetsAreNotDeleted()
        {
            Assert.That(AssetDatabase.GUIDToAssetPath(AbilityDataGuid), Is.Not.Empty);
            Assert.That(AssetDatabase.GUIDToAssetPath(AbilityPickupGuid), Is.Not.Empty);

            // Retain the older, currently unreferenced authored assets rather
            // than deleting them during this consolidation.
            foreach (string path in new[]
                     {
                         "Assets/Scripts/ScriptableObjects/Ability_Spear.asset",
                         "Assets/Scripts/ScriptableObjects/Ability_Dash.asset",
                         "Assets/Scripts/ScriptableObjects/Ability_Rope.asset",
                         "Assets/Scripts/ScriptableObjects/Ability_Pogo.asset"
                     })
            {
                Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, path.Substring("Assets/".Length))));
            }
        }

        private static void AssertFieldExists(string name)
        {
            FieldInfo field = typeof(AbilityData).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Serialized AbilityData configuration field missing: " + name);
        }

        private static UnityEngine.Object GetObjectReference(Component component, string field)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.Update();
            return serialized.FindProperty(field).objectReferenceValue;
        }

        private static string GetString(Component component, string field)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(field);
            Assert.IsNotNull(property, "Serialized property not found: " + field);
            return property.stringValue;
        }

        private static void AssertAssetContains(string path, params string[] expectedLines)
        {
            string fullPath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            string[] lines = File.ReadAllText(fullPath).Split('\n');
            foreach (string expectedLine in expectedLines)
            {
                Assert.That(
                    Array.Exists(lines, line => line.Trim() == expectedLine),
                    Is.True,
                    path + " does not serialize '" + expectedLine + "'.");
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).ToArray();
            }
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, "Missing asset: " + path);
            return asset;
        }
    }
}
