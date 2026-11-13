using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Essence")]
public class PlayerEssenceData : ScriptableObject
{
    public float maxEssence = 100f;
    public float essencePerBaseCatch = 10f;

    [Tooltip("Essence granted by a perfect spear catch.")]
    public float essencePerPerfectCatch = 40f;

    private void OnValidate()
    {
        maxEssence = Mathf.Max(0f, maxEssence);
        essencePerBaseCatch = Mathf.Max(0f, essencePerBaseCatch);
        essencePerPerfectCatch = Mathf.Max(0f, essencePerPerfectCatch);
    }
}
