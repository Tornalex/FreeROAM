using UnityEngine;

/// <summary>
/// Authoring data for the boss jump action: launch velocity, travel distance,
/// landing recovery and the airborne watchdog used when a jump fails.
///
/// Selection weights stay in <see cref="BossAIProfile"/> with the other action
/// weights.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/Attacks/Jump")]
public class JumpAttackData : ScriptableObject
{
    [Header("Jump")]
    [SerializeField, Min(0f)]
    [Tooltip("Upward velocity applied when the boss starts a normal jump.")]
    private float jumpUpVelocity = 9f;

    [SerializeField, Min(0f)]
    [Tooltip("Horizontal distance the boss tries to cover with a normal jump.")]
    private float jumpDistance = 4f;

    [SerializeField, Min(0f)]
    [Tooltip("Upper clamp for the horizontal speed of a normal jump.")]
    private float jumpMaxHorizontalSpeed = 7f;

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    [Tooltip("Pause after landing before the next decision.")]
    private float landingRecovery = 0.35f;

    [SerializeField, Min(0.5f)]
    [Tooltip("Maximum time the boss may stay airborne before a forced recovery.")]
    private float maxAirborneDuration = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("Cooldown before the boss may recover from a failed jump again.")]
    private float recoveryCooldown = 1f;

    public float JumpUpVelocity => jumpUpVelocity;
    public float JumpDistance => jumpDistance;
    public float JumpMaxHorizontalSpeed => jumpMaxHorizontalSpeed;
    public float LandingRecovery => landingRecovery;
    public float MaxAirborneDuration => maxAirborneDuration;
    public float RecoveryCooldown => recoveryCooldown;

    private void OnValidate()
    {
        jumpUpVelocity = Mathf.Max(0f, jumpUpVelocity);
        jumpDistance = Mathf.Max(0f, jumpDistance);
        jumpMaxHorizontalSpeed = Mathf.Max(0f, jumpMaxHorizontalSpeed);
        landingRecovery = Mathf.Max(0f, landingRecovery);
        maxAirborneDuration = Mathf.Max(0.5f, maxAirborneDuration);
        recoveryCooldown = Mathf.Max(0f, recoveryCooldown);
    }
}
