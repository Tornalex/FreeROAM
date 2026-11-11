using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Stats")]
public class PlayerStatsData : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 10f;
    public float runSpeed = 16f;

    [Tooltip("Horizontal input magnitude above which a player knockback can be interrupted.")]
    public float knockbackInterruptThreshold = 0.1f;

    [Tooltip("Horizontal input magnitude above which the player is considered to be moving.")]
    public float moveInputThreshold = 0.1f;

    [Header("Jump")]
    public float jumpForce = 24f;
    public float jumpCutMultiplier = 0.2f;
    public float jumpBufferTime = 0.1f;
    public float coyoteTime = 0.05f;

    [Header("Health")]
    public int maxHealth = 5;
    public float iFramesDuration = 0.75f;
    public Vector2 damageForce = new Vector2(15f, 10f);
    public float damageKnockbackDuration = 0.3f;

    [Tooltip("Damage dealt to the player by enemy contact.")]
    public int contactDamage = 1;

    [Tooltip("Interval between invulnerability flicker alpha steps.")]
    public float iFramesBlinkInterval = 0.05f;

    [Tooltip("Minimum sprite alpha used while invulnerable.")]
    public float iFramesBlinkMinAlpha = 0.2f;

    [Header("Ground Detection")]
    [Tooltip("Minimum contact normal Y for a collision to count as ground.")]
    public float groundNormalThreshold = 0.9f;

    [Tooltip("Maximum contact point offset above the feet for a ground contact.")]
    public float groundPointOffset = 0.2f;

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runSpeed = Mathf.Max(0f, runSpeed);
        knockbackInterruptThreshold = Mathf.Clamp01(knockbackInterruptThreshold);
        moveInputThreshold = Mathf.Clamp01(moveInputThreshold);
        jumpForce = Mathf.Max(0f, jumpForce);
        jumpCutMultiplier = Mathf.Clamp01(jumpCutMultiplier);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        maxHealth = Mathf.Max(1, maxHealth);
        iFramesDuration = Mathf.Max(0f, iFramesDuration);
        damageKnockbackDuration = Mathf.Max(0f, damageKnockbackDuration);
        contactDamage = Mathf.Max(0, contactDamage);
        iFramesBlinkInterval = Mathf.Max(0.01f, iFramesBlinkInterval);
        iFramesBlinkMinAlpha = Mathf.Clamp01(iFramesBlinkMinAlpha);
        groundNormalThreshold = Mathf.Clamp01(groundNormalThreshold);
        groundPointOffset = Mathf.Max(0f, groundPointOffset);
    }
}
