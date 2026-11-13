using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Heal")]
public class PlayerHealData : ScriptableObject
{
    [Tooltip("Time the heal button must remain held to complete one heal.")]
    public float healDuration = 1f;

    [Tooltip("Health restored by one completed heal.")]
    public int healthPerHeal = 2;

    [Tooltip("Essence consumed by one completed heal.")]
    public float essenceCostPerHeal = 50f;

    private void OnValidate()
    {
        healDuration = Mathf.Max(0f, healDuration);
        healthPerHeal = Mathf.Max(0, healthPerHeal);
        essenceCostPerHeal = Mathf.Max(0f, essenceCostPerHeal);
    }
}
