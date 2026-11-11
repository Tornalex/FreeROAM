using UnityEngine;

namespace SpearWander.Abilities
{
    /// <summary>
    /// Shared authoring configuration for one ability. Unlock state and the
    /// state of any configured Player component belong to AbilityManager and
    /// the Player instance, never to this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "New Ability", menuName = "Spear Wander/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private AbilityType abilityType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        [Header("Pickup")]
        [SerializeField] private string promptMessage = "Premi E per interagire";
        [SerializeField] private bool destroyOnPickup = true;

        [Header("Unlock Configuration")]
        [SerializeField] private string componentName;
        [SerializeField] private bool enableComponent = true;

        [Header("Legacy Compatibility")]
        [SerializeField, Tooltip("Retained in existing assets; not currently used by the unlock flow.")]
        private bool requiresPlayerCombat;

        [SerializeField, Tooltip("Retained in existing assets; not currently used by the unlock flow.")]
        private bool requiresRopeClimb;

        public AbilityType AbilityType => abilityType;
        public string DisplayName => displayName;
        public string Description => description;
        public string PromptMessage => promptMessage;
        public bool DestroyOnPickup => destroyOnPickup;
        public string ComponentName => componentName;
        public bool EnableComponent => enableComponent;
        public bool RequiresPlayerCombat => requiresPlayerCombat;
        public bool RequiresRopeClimb => requiresRopeClimb;
    }
}
