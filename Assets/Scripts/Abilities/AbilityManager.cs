using System.Collections.Generic;
using UnityEngine;

namespace SpearWander.Abilities
{
    /// <summary>
    /// Owns the runtime set of unlocked abilities and applies their configured
    /// component state when a Player is acquired, restored or reset.
    /// </summary>
    public class AbilityManager : MonoBehaviour
    {
        public static AbilityManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private List<AbilityData> allAbilities = new List<AbilityData>();

        private readonly HashSet<AbilityType> unlockedAbilities =
            new HashSet<AbilityType>();

        public IReadOnlyCollection<AbilityType> UnlockedAbilities => unlockedAbilities;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeFromPlayer(FindAnyObjectByType<Player>());
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void InitializeFromPlayer(Player player)
        {
            if (player == null)
                return;

            for (int i = 0; i < allAbilities.Count; i++)
            {
                AbilityData ability = allAbilities[i];
                if (ability != null && IsAbilityApplied(ability, player))
                    unlockedAbilities.Add(ability.AbilityType);
            }
        }

        /// <summary>
        /// Unlocks a registered ability and applies it to the Player.
        /// Returns false when the request is invalid, unconfigured or already
        /// unlocked. The runtime unlocked set is the sole progression state.
        /// </summary>
        public bool UnlockAbility(AbilityType type, Player player)
        {
            if (player == null)
                return false;

            if (unlockedAbilities.Contains(type))
                return false;

            AbilityData abilityData = GetAbilityData(type);
            if (abilityData == null)
            {
                Debug.LogWarning($"[AbilityManager] No AbilityData found for type: {type}");
                return false;
            }

            if (!ApplyAbility(abilityData, player))
                return false;

            unlockedAbilities.Add(type);
            Debug.Log($"[AbilityManager] Unlocked ability: {type}");
            return true;
        }

        /// <summary>
        /// Common pickup entry point. If no manager exists (for isolated
        /// legacy scenes), it still applies the same authoring configuration
        /// without creating a second source of progression state.
        /// </summary>
        public static bool TryUnlockAbility(AbilityData abilityData, Player player)
        {
            if (abilityData == null || player == null)
                return false;

            if (Instance != null)
                return Instance.UnlockAbility(abilityData.AbilityType, player);

            return ApplyAbility(abilityData, player);
        }

        public bool IsUnlocked(AbilityType type)
        {
            return unlockedAbilities.Contains(type);
        }

        public AbilityData GetAbilityData(AbilityType type)
        {
            for (int i = 0; i < allAbilities.Count; i++)
            {
                AbilityData ability = allAbilities[i];
                if (ability != null && ability.AbilityType == type)
                    return ability;
            }

            return null;
        }

        public void ResetAll(Player player = null)
        {
            if (player != null)
            {
                for (int i = 0; i < allAbilities.Count; i++)
                {
                    AbilityData ability = allAbilities[i];
                    if (ability == null)
                        continue;

                    Behaviour component = GetTargetComponent(ability, player);
                    if (component != null)
                        component.enabled = !ability.EnableComponent;
                }
            }

            unlockedAbilities.Clear();
        }

        public void RestoreAbilities(Player player)
        {
            if (player == null)
                return;

            foreach (AbilityType type in unlockedAbilities)
            {
                AbilityData ability = GetAbilityData(type);
                if (ability != null)
                    ApplyAbility(ability, player);
            }
        }

        private static bool IsAbilityApplied(AbilityData ability, Player player)
        {
            Behaviour component = GetTargetComponent(ability, player);
            return component != null && component.enabled == ability.EnableComponent;
        }

        private static bool ApplyAbility(AbilityData ability, Player player)
        {
            Behaviour component = GetTargetComponent(ability, player);
            if (component == null)
            {
                Debug.LogWarning(
                    $"[AbilityManager] Component '{ability.ComponentName}' not found on Player " +
                    $"for ability {ability.AbilityType}");
                return false;
            }

            component.enabled = ability.EnableComponent;
            return true;
        }

        private static Behaviour GetTargetComponent(AbilityData ability, Player player)
        {
            if (ability == null || player == null || string.IsNullOrEmpty(ability.ComponentName))
                return null;

            // Player caches its gameplay components in Awake. Prefer those
            // references; retain the name-based fallback for compatibility
            // with existing assets and future authored abilities.
            Behaviour cachedComponent = null;
            switch (ability.AbilityType)
            {
                case AbilityType.Spear:
                    if (ability.ComponentName == nameof(PlayerCombat))
                        cachedComponent = player.Combat;
                    break;
                case AbilityType.Dash:
                    if (ability.ComponentName == nameof(PlayerDash))
                        cachedComponent = player.Dash;
                    break;
                case AbilityType.Rope:
                    if (ability.ComponentName == nameof(PlayerRopeClimb))
                        cachedComponent = player.RopeClimb;
                    break;
                case AbilityType.Pogo:
                    if (ability.ComponentName == nameof(PlayerPogo))
                        cachedComponent = player.Pogo;
                    break;
            }

            return cachedComponent != null
                ? cachedComponent
                : player.GetComponent(ability.ComponentName) as Behaviour;
        }
    }
}
