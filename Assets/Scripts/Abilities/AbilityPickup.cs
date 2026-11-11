using UnityEngine;
using SpearWander.Abilities;

namespace SpearWander.Abilities
{
    public class AbilityPickup : MonoBehaviour, IInteractable
    {
        [Header("Ability Configuration")]
        [SerializeField] private AbilityData abilityData;

        [Header("Optional Override")]
        [SerializeField] private string overridePrompt;

        private void Start()
        {
            if (abilityData != null &&
                AbilityManager.Instance != null &&
                AbilityManager.Instance.IsUnlocked(abilityData.AbilityType))
            {
                Destroy(gameObject);
            }
        }

        public void Interact(Player player)
        {
            if (player == null)
                return;

            if (abilityData == null)
            {
                Debug.LogError("[AbilityPickup] No AbilityData assigned!");
                return;
            }

            AbilityManager manager = AbilityManager.Instance;
            if (manager != null && manager.IsUnlocked(abilityData.AbilityType))
            {
                Debug.Log($"[AbilityPickup] Ability {abilityData.AbilityType} already unlocked");
                RemoveFromPlayer(player);
                if (abilityData.DestroyOnPickup)
                    Destroy(gameObject);
                return;
            }

            if (!AbilityManager.TryUnlockAbility(abilityData, player))
                return;

            RemoveFromPlayer(player);

            if (abilityData.DestroyOnPickup)
                Destroy(gameObject);
        }

        private void RemoveFromPlayer(Player player)
        {
            var playerInteract = player.GetComponent<PlayerInteract>();
            if (playerInteract != null)
            {
                playerInteract.RemoveInteractableFromList(this);
            }
        }

        public string GetInteractPrompt()
        {
            if (!string.IsNullOrEmpty(overridePrompt)) return overridePrompt;
            return abilityData != null ? abilityData.PromptMessage : "Premi E per interagire";
        }
    }
}
