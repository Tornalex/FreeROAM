using UnityEngine;

/// <summary>
/// Authoring tuning for the free-roaming boss AI: perception, jump and dash
/// movement, decision cadence and action weights.
///
/// This is shared, read-only authoring data. Runtime state (current state,
/// timers, cooldowns, target, velocity) stays on
/// <see cref="SpearWander.Boss.FreeRoamBoss"/> so that two instances using the
/// same profile never contaminate each other.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/AI Profile")]
public class BossAIProfile : ScriptableObject
{
    [Header("Detection")]
    [SerializeField, Min(0f)]
    [Tooltip("Distance at which the boss notices the player and starts choosing actions.")]
    private float detectionRange = 14f;

    [SerializeField]
    [Tooltip("Layers considered walkable ground.")]
    private LayerMask groundLayers = 1;

    [SerializeField]
    [Tooltip("Layer that contains the player collider.")]
    private LayerMask playerLayers = 8;

    [SerializeField, Min(0.01f)]
    [Tooltip("Radius of the ground probe used by the grounded check.")]
    private float groundCheckRadius = 0.22f;

    [Header("Obstruction Layers")]
    [SerializeField]
    [Tooltip("Layers that stop the tongue attack.")]
    private LayerMask tongueObstructionLayers = 1;

    [SerializeField]
    [Tooltip("Layers that stop drifting spore clouds.")]
    private LayerMask sporeObstructionLayers = 1;

    [SerializeField]
    [Tooltip("Layers that stop a dash jump.")]
    private LayerMask dashJumpObstructionLayers = 1;

    [Header("Jump Movement")]
    [SerializeField, Min(0f)]
    [Tooltip("Upward velocity applied when the boss starts a normal jump.")]
    private float jumpUpVelocity = 9f;

    [SerializeField, Min(0f)]
    [Tooltip("Horizontal distance the boss tries to cover with a normal jump.")]
    private float jumpDistance = 4f;

    [SerializeField, Min(0f)]
    [Tooltip("Upper clamp for the horizontal speed of a normal jump.")]
    private float jumpMaxHorizontalSpeed = 7f;

    [SerializeField, Min(0f)]
    [Tooltip("Pause after landing before the next decision.")]
    private float landingRecovery = 0.35f;

    [Header("Positioning")]
    [SerializeField, Min(0f)]
    [Tooltip("Distance the boss tries to keep from the player.")]
    private float preferredDistance = 3.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Below this distance the boss prefers jumping away.")]
    private float retreatDistance = 2f;

    [Header("Decision Timing")]
    [SerializeField, Min(0f)]
    [Tooltip("Time between two action decisions while grounded.")]
    private float decisionInterval = 0.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Random variation applied to the decision interval.")]
    private float decisionIntervalJitter = 0.15f;

    [SerializeField, Min(0.5f)]
    [Tooltip("Maximum time the boss may stay airborne before a forced recovery.")]
    private float maxAirborneDuration = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("Cooldown before the boss may recover from a failed jump again.")]
    private float recoveryCooldown = 1f;

    [Header("Action Weights")]
    [SerializeField, Min(0f)]
    [Tooltip("Weight of a jump toward the player.")]
    private float jumpTowardWeight = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("Weight of a jump away from the player.")]
    private float jumpAwayWeight = 1.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Weight of a dash jump when the player is far enough.")]
    private float dashJumpWeight = 1.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Weight of the tongue attack when it is available.")]
    private float tongueWeight = 2f;

    [SerializeField, Min(0f)]
    [Tooltip("Weight of the spore attack when it is available.")]
    private float sporeWeight = 2f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Multiplier applied to the weight of the action that was just used.")]
    private float repeatWeightMultiplier = 0.35f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Multiplier applied to an attack that directly follows another attack.")]
    private float attackAfterAttackWeightMultiplier = 0.4f;

    [Header("Dash Jump")]
    [SerializeField, Min(0f)]
    [Tooltip("Minimum horizontal distance to the player required to consider a dash jump.")]
    private float dashJumpMinDistance = 7f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Horizontal cruise speed of a dash jump.")]
    private float dashJumpHorizontalSpeed = 18f;

    [SerializeField, Min(0f)]
    [Tooltip("Minimum upward velocity of a dash jump.")]
    private float dashJumpUpVelocity = 10f;

    [SerializeField, Min(0f)]
    [Tooltip("Extra landing distance beyond the sampled player position.")]
    private float dashJumpLandingOffset = 1f;

    [SerializeField, Min(0.05f)]
    [Tooltip("Readable wind-up before the dash jump launches.")]
    private float dashJumpPreparationTime = 0.45f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Recovery time after a dash jump (successful or aborted).")]
    private float dashJumpRecoveryTime = 0.45f;

    [SerializeField, Min(0f)]
    [Tooltip("Cooldown before another dash jump is allowed.")]
    private float dashJumpCooldown = 5f;

    [SerializeField, Min(0.5f)]
    [Tooltip("Maximum airborne time for a dash jump before a forced recovery.")]
    private float dashJumpMaxAirborneDuration = 2f;

    public float DetectionRange => detectionRange;
    public LayerMask GroundLayers => groundLayers;
    public LayerMask PlayerLayers => playerLayers;
    public float GroundCheckRadius => groundCheckRadius;

    public LayerMask TongueObstructionLayers => tongueObstructionLayers;
    public LayerMask SporeObstructionLayers => sporeObstructionLayers;
    public LayerMask DashJumpObstructionLayers => dashJumpObstructionLayers;

    public float JumpUpVelocity => jumpUpVelocity;
    public float JumpDistance => jumpDistance;
    public float JumpMaxHorizontalSpeed => jumpMaxHorizontalSpeed;
    public float LandingRecovery => landingRecovery;

    public float PreferredDistance => preferredDistance;
    public float RetreatDistance => retreatDistance;

    public float DecisionInterval => decisionInterval;
    public float DecisionIntervalJitter => decisionIntervalJitter;
    public float MaxAirborneDuration => maxAirborneDuration;
    public float RecoveryCooldown => recoveryCooldown;

    public float JumpTowardWeight => jumpTowardWeight;
    public float JumpAwayWeight => jumpAwayWeight;
    public float DashJumpWeight => dashJumpWeight;
    public float TongueWeight => tongueWeight;
    public float SporeWeight => sporeWeight;
    public float RepeatWeightMultiplier => repeatWeightMultiplier;
    public float AttackAfterAttackWeightMultiplier => attackAfterAttackWeightMultiplier;

    public float DashJumpMinDistance => dashJumpMinDistance;
    public float DashJumpHorizontalSpeed => dashJumpHorizontalSpeed;
    public float DashJumpUpVelocity => dashJumpUpVelocity;
    public float DashJumpLandingOffset => dashJumpLandingOffset;
    public float DashJumpPreparationTime => dashJumpPreparationTime;
    public float DashJumpRecoveryTime => dashJumpRecoveryTime;
    public float DashJumpCooldown => dashJumpCooldown;
    public float DashJumpMaxAirborneDuration => dashJumpMaxAirborneDuration;

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0f, detectionRange);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);

        jumpUpVelocity = Mathf.Max(0f, jumpUpVelocity);
        jumpDistance = Mathf.Max(0f, jumpDistance);
        jumpMaxHorizontalSpeed = Mathf.Max(0f, jumpMaxHorizontalSpeed);
        landingRecovery = Mathf.Max(0f, landingRecovery);

        preferredDistance = Mathf.Max(0f, preferredDistance);
        retreatDistance = Mathf.Max(0f, retreatDistance);

        decisionInterval = Mathf.Max(0f, decisionInterval);
        decisionIntervalJitter = Mathf.Max(0f, decisionIntervalJitter);
        maxAirborneDuration = Mathf.Max(0.5f, maxAirborneDuration);
        recoveryCooldown = Mathf.Max(0f, recoveryCooldown);

        jumpTowardWeight = Mathf.Max(0f, jumpTowardWeight);
        jumpAwayWeight = Mathf.Max(0f, jumpAwayWeight);
        dashJumpWeight = Mathf.Max(0f, dashJumpWeight);
        tongueWeight = Mathf.Max(0f, tongueWeight);
        sporeWeight = Mathf.Max(0f, sporeWeight);
        repeatWeightMultiplier = Mathf.Clamp01(repeatWeightMultiplier);
        attackAfterAttackWeightMultiplier =
            Mathf.Clamp01(attackAfterAttackWeightMultiplier);

        dashJumpMinDistance = Mathf.Max(0f, dashJumpMinDistance);
        dashJumpHorizontalSpeed = Mathf.Max(0.01f, dashJumpHorizontalSpeed);
        dashJumpUpVelocity = Mathf.Max(0f, dashJumpUpVelocity);
        dashJumpLandingOffset = Mathf.Max(0f, dashJumpLandingOffset);
        dashJumpPreparationTime = Mathf.Max(0.05f, dashJumpPreparationTime);
        dashJumpRecoveryTime = Mathf.Max(0.1f, dashJumpRecoveryTime);
        dashJumpCooldown = Mathf.Max(0f, dashJumpCooldown);
        dashJumpMaxAirborneDuration = Mathf.Max(0.5f, dashJumpMaxAirborneDuration);
    }
}
