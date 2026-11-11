using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Exercises the unified ability pickup and manager pipeline without
    /// changing the project progression assets.
    /// </summary>
    public class AbilityArchitecturePlayModeTests : PlayModeTestBase
    {
        private const string AbilityManagerTypeName = "SpearWander.Abilities.AbilityManager";
        private const string AbilityDataTypeName = "SpearWander.Abilities.AbilityData";
        private const string AbilityPickupTypeName = "SpearWander.Abilities.AbilityPickup";
        private const string AbilityTypeName = "SpearWander.Abilities.AbilityType";

        [UnityTest]
        public IEnumerator SharedDataPickupUnlocksDashOnceAndKeepsStateOnManager()
        {
            ScriptableObject data = CreateAbilityData(
                "Dash",
                "PlayerDash",
                "Dash",
                enableComponent: true);
            ManagerScope managerScope = CreateManager(data);
            GameObject playerObject = CreatePlayerWithDisabledDash();
            GameObject firstPickupObject = CreatePickup(data);
            GameObject duplicatePickupObject = CreatePickup(data);

            string serializedDataBefore = SnapshotAbilityData(data);
            try
            {
                Component player = playerObject.GetComponent(ResolveRuntimeType("Player"));
                Behaviour dash = (Behaviour)playerObject.GetComponent(ResolveRuntimeType("PlayerDash"));
                Component firstPickup = firstPickupObject.GetComponent(
                    ResolveRuntimeType(AbilityPickupTypeName));
                Component duplicatePickup = duplicatePickupObject.GetComponent(
                    ResolveRuntimeType(AbilityPickupTypeName));

                Assert.IsFalse(dash.enabled, "Dash must begin locked on the Player.");
                ReflectionTestUtilities.InvokeInstance(firstPickup, "Interact", player);

                Assert.IsTrue(dash.enabled, "The pickup did not enable PlayerDash.");
                Assert.IsTrue(IsUnlocked(managerScope.Manager, "Dash"));

                // A second pickup sharing this AbilityData must not create a
                // second unlock or mutate the shared authoring asset.
                ReflectionTestUtilities.InvokeInstance(duplicatePickup, "Interact", player);
                Assert.IsTrue(dash.enabled);
                Assert.IsTrue(IsUnlocked(managerScope.Manager, "Dash"));
                object unlockedAbilities = ReflectionTestUtilities.GetProperty(
                    managerScope.Manager,
                    "UnlockedAbilities");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(unlockedAbilities, "Count"),
                    Is.EqualTo(1));
                Assert.That(SnapshotAbilityData(data), Is.EqualTo(serializedDataBefore));

                yield return null;
            }
            finally
            {
                managerScope.Dispose();
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator ManagerRestoresAndResetsTheRuntimeUnlockState()
        {
            ScriptableObject data = CreateAbilityData(
                "Dash",
                "PlayerDash",
                "Dash",
                enableComponent: true);
            ManagerScope managerScope = CreateManager(data);
            GameObject playerObject = CreatePlayerWithDisabledDash();

            try
            {
                Component player = playerObject.GetComponent(ResolveRuntimeType("Player"));
                Behaviour dash = (Behaviour)playerObject.GetComponent(ResolveRuntimeType("PlayerDash"));

                bool unlocked = ReflectionTestUtilities.InvokeInstance<bool>(
                    managerScope.Manager,
                    "UnlockAbility",
                    Enum.Parse(ResolveRuntimeType(AbilityTypeName), "Dash"),
                    player);
                Assert.IsTrue(unlocked);
                Assert.IsTrue(dash.enabled);

                dash.enabled = false;
                ReflectionTestUtilities.InvokeInstance(
                    managerScope.Manager,
                    "RestoreAbilities",
                    player);
                Assert.IsTrue(dash.enabled, "RestoreAbilities did not reapply the unlocked ability.");
                Assert.IsTrue(IsUnlocked(managerScope.Manager, "Dash"));

                ReflectionTestUtilities.InvokeInstance(
                    managerScope.Manager,
                    "ResetAll",
                    player);
                Assert.IsFalse(dash.enabled, "ResetAll did not restore the locked component state.");
                Assert.IsFalse(IsUnlocked(managerScope.Manager, "Dash"));

                yield return null;
            }
            finally
            {
                managerScope.Dispose();
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator ManagerInitializesUnlockedAbilitiesFromPlayerComponentState()
        {
            ScriptableObject data = CreateAbilityData(
                "Dash",
                "PlayerDash",
                "Dash",
                enableComponent: true);
            ManagerScope managerScope = CreateManager(data);

            GameObject playerObject = Fixture.CreateGameObject("Phase4InitializedPlayer");
            playerObject.SetActive(false);
            Component player = playerObject.AddComponent(ResolveRuntimeType("Player"));
            Behaviour dash = (Behaviour)playerObject.AddComponent(
                ResolveRuntimeType("PlayerDash"));
            dash.enabled = true;

            try
            {
                ReflectionTestUtilities.InvokeInstance(
                    managerScope.Manager,
                    "InitializeFromPlayer",
                    player);

                Assert.IsTrue(IsUnlocked(managerScope.Manager, "Dash"));
                object unlockedAbilities = ReflectionTestUtilities.GetProperty(
                    managerScope.Manager,
                    "UnlockedAbilities");
                Assert.That(
                    ReflectionTestUtilities.GetProperty<int>(unlockedAbilities, "Count"),
                    Is.EqualTo(1));
                yield return null;
            }
            finally
            {
                managerScope.Dispose();
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator SpearAbilityUnlocksTheExistingPlayerCombatComponent()
        {
            ScriptableObject data = CreateAbilityData(
                "Spear",
                "PlayerCombat",
                "Spear",
                enableComponent: true);
            ManagerScope managerScope = CreateManager(data);

            GameObject playerObject = Fixture.CreateGameObject("Phase4SpearPlayer");
            playerObject.SetActive(false);
            Component player = playerObject.AddComponent(ResolveRuntimeType("Player"));
            Behaviour combat = (Behaviour)playerObject.AddComponent(
                ResolveRuntimeType("PlayerCombat"));
            combat.enabled = false;
            GameObject pickupObject = CreatePickup(data);

            try
            {
                Component pickup = pickupObject.GetComponent(
                    ResolveRuntimeType(AbilityPickupTypeName));
                ReflectionTestUtilities.InvokeInstance(pickup, "Interact", player);

                Assert.IsTrue(combat.enabled, "Spear pickup did not enable PlayerCombat.");
                Assert.IsTrue(IsUnlocked(managerScope.Manager, "Spear"));
                yield return null;
            }
            finally
            {
                managerScope.Dispose();
                DestroyObject(data);
            }
        }

        [UnityTest]
        public IEnumerator PickupWithoutManagerUsesTheSameAbilityConfiguration()
        {
            ScriptableObject data = CreateAbilityData(
                "Dash",
                "PlayerDash",
                "Dash",
                enableComponent: true);
            string serializedDataBefore = SnapshotAbilityData(data);
            object previousManager = GetCurrentManager();
            SetCurrentManager(null);
            GameObject playerObject = CreatePlayerWithDisabledDash();

            try
            {
                Component player = playerObject.GetComponent(ResolveRuntimeType("Player"));
                Behaviour dash = (Behaviour)playerObject.GetComponent(ResolveRuntimeType("PlayerDash"));
                Assert.IsFalse(dash.enabled);

                bool unlocked = ReflectionTestUtilities.InvokeStatic<bool>(
                    ResolveRuntimeType(AbilityManagerTypeName),
                    "TryUnlockAbility",
                    data,
                    player);

                Assert.IsTrue(unlocked);
                Assert.IsTrue(dash.enabled);
                Assert.That(SnapshotAbilityData(data), Is.EqualTo(serializedDataBefore));
                yield return null;
            }
            finally
            {
                SetCurrentManager(previousManager);
                DestroyObject(data);
            }
        }

        private GameObject CreatePlayerWithDisabledDash()
        {
            GameObject playerObject = Fixture.CreateGameObject("Phase4Player");
            playerObject.SetActive(false);
            playerObject.AddComponent(ResolveRuntimeType("Player"));
            Behaviour dash = (Behaviour)playerObject.AddComponent(ResolveRuntimeType("PlayerDash"));
            dash.enabled = false;
            return playerObject;
        }

        private GameObject CreatePickup(ScriptableObject data)
        {
            GameObject pickupObject = Fixture.CreateGameObject("Phase4AbilityPickup");
            pickupObject.SetActive(false);
            Component pickup = pickupObject.AddComponent(ResolveRuntimeType(AbilityPickupTypeName));
            ReflectionTestUtilities.SetInstanceField(pickup, "abilityData", data);
            return pickupObject;
        }

        private ManagerScope CreateManager(ScriptableObject data)
        {
            Type managerType = ResolveRuntimeType(AbilityManagerTypeName);
            object previous = GetCurrentManager();

            GameObject managerObject = Fixture.CreateGameObject("Phase4AbilityManager");
            managerObject.SetActive(false);
            Component manager = managerObject.AddComponent(managerType);

            IList configuredAbilities = (IList)ReflectionTestUtilities.GetInstanceField(
                manager,
                "allAbilities");
            configuredAbilities.Add(data);
            SetCurrentManager(manager);

            return new ManagerScope(manager, previous, managerType);
        }

        private static ScriptableObject CreateAbilityData(
            string abilityType,
            string componentName,
            string displayName,
            bool enableComponent)
        {
            Type dataType = ResolveRuntimeType(AbilityDataTypeName);
            ScriptableObject data = ScriptableObject.CreateInstance(dataType);
            ReflectionTestUtilities.SetInstanceField(
                data,
                "abilityType",
                Enum.Parse(ResolveRuntimeType(AbilityTypeName), abilityType));
            ReflectionTestUtilities.SetInstanceField(data, "componentName", componentName);
            ReflectionTestUtilities.SetInstanceField(data, "displayName", displayName);
            ReflectionTestUtilities.SetInstanceField(data, "description", "Phase 4 test data");
            ReflectionTestUtilities.SetInstanceField(data, "promptMessage", "Interact");
            ReflectionTestUtilities.SetInstanceField(data, "destroyOnPickup", true);
            ReflectionTestUtilities.SetInstanceField(data, "enableComponent", enableComponent);
            return data;
        }

        private static string SnapshotAbilityData(ScriptableObject data)
        {
            return string.Join(
                "|",
                new object[]
                {
                    ReflectionTestUtilities.GetProperty(data, "DisplayName"),
                    ReflectionTestUtilities.GetProperty(data, "Description"),
                    ReflectionTestUtilities.GetProperty(data, "EnableComponent"),
                    ReflectionTestUtilities.GetProperty(data, "DestroyOnPickup"),
                    ReflectionTestUtilities.GetProperty(data, "ComponentName")
                });
        }

        private static bool IsUnlocked(Component manager, string abilityType)
        {
            return ReflectionTestUtilities.InvokeInstance<bool>(
                manager,
                "IsUnlocked",
                Enum.Parse(ResolveRuntimeType(AbilityTypeName), abilityType));
        }

        private static object GetCurrentManager()
        {
            return ReflectionTestUtilities.GetStaticProperty(
                ResolveRuntimeType(AbilityManagerTypeName),
                "Instance");
        }

        private static void SetCurrentManager(object manager)
        {
            ReflectionTestUtilities.SetStaticField(
                ResolveRuntimeType(AbilityManagerTypeName),
                "<Instance>k__BackingField",
                manager);
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

        private sealed class ManagerScope
        {
            public readonly Component Manager;
            private readonly object _previous;
            private readonly Type _managerType;
            private bool _disposed;

            public ManagerScope(Component manager, object previous, Type managerType)
            {
                Manager = manager;
                _previous = previous;
                _managerType = managerType;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                ReflectionTestUtilities.SetStaticField(
                    _managerType,
                    "<Instance>k__BackingField",
                    _previous);
            }
        }
    }
}
