using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Essence")]
public class PlayerEssenceData : ScriptableObject
{
    public float maxEssence = 100f;
    public float essencePerBaseCatch = 10f;
    public float essenceCostPerHeal = 50f;
    public int healthPerHeal = 2;

    [Tooltip("Essence granted by a perfect spear catch.")]
    public float essencePerPerfectCatch = 40f;

    private void OnValidate()
    {
        maxEssence = Mathf.Max(0f, maxEssence);
        essencePerBaseCatch = Mathf.Max(0f, essencePerBaseCatch);
        essenceCostPerHeal = Mathf.Max(0f, essenceCostPerHeal);
        healthPerHeal = Mathf.Max(0, healthPerHeal);
        essencePerPerfectCatch = Mathf.Max(0f, essencePerPerfectCatch);
    }
}
