using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Combat")]
public class PlayerCombatData : ScriptableObject
{
    [Header("Spear")]
    public float shootForce = 100f;
    public float throwCooldown = 0f;
    public int spearImpactDamage = 1;
    public int spearRecallDamage = 1;

    [Header("Dash")]
    public int dashDamage = 1;

    [Header("Pogo")]
    public int pogoDamage = 1;

    [Header("Aiming")]
    [Tooltip("Gamepad aim magnitude below which the aim direction is considered neutral.")]
    public float aimDeadzone = 0.15f;

    [Tooltip("Higher values make gamepad aim converge faster.")]
    public float aimSmoothing = 12f;

    [Tooltip("Minimum gamepad aim magnitude required to enter controller aiming.")]
    public float aimInputThreshold = 0.1f;

    private void OnValidate()
    {
        shootForce = Mathf.Max(0f, shootForce);
        throwCooldown = Mathf.Max(0f, throwCooldown);
        spearImpactDamage = Mathf.Max(0, spearImpactDamage);
        spearRecallDamage = Mathf.Max(0, spearRecallDamage);
        dashDamage = Mathf.Max(0, dashDamage);
        pogoDamage = Mathf.Max(0, pogoDamage);
        aimDeadzone = Mathf.Clamp01(aimDeadzone);
        aimSmoothing = Mathf.Max(0f, aimSmoothing);
        aimInputThreshold = Mathf.Clamp01(aimInputThreshold);
    }
}
