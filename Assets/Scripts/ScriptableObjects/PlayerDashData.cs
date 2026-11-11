using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Dash")]
public class PlayerDashData : ScriptableObject
{
    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 0.2f;
    public float postDashInvincibility = 0.05f;

    [Header("Knockback")]
    public Vector2 knockbackForce = new Vector2(20f, 10f);
    public float knockbackDuration = 0.3f;

    [Header("Ready Feedback")]
    public float dashReadyBlinkInterval = 0.05f;
    public int dashReadyBlinkCount = 3;

    [Tooltip("Horizontal input magnitude above which a dash knockback can be interrupted.")]
    public float knockbackInterruptThreshold = 0.1f;

    private void OnValidate()
    {
        dashSpeed = Mathf.Max(0f, dashSpeed);
        dashDuration = Mathf.Max(0f, dashDuration);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        postDashInvincibility = Mathf.Max(0f, postDashInvincibility);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        dashReadyBlinkInterval = Mathf.Max(0.01f, dashReadyBlinkInterval);
        dashReadyBlinkCount = Mathf.Max(0, dashReadyBlinkCount);
        knockbackInterruptThreshold = Mathf.Clamp01(knockbackInterruptThreshold);
    }
}
