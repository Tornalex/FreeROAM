using UnityEngine;

/// <summary>
/// Authoring tuning for the free-roaming boss AI: perception, decision cadence,
/// action weights and references to the per-attack authoring assets.
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

    [Header("Attack Data")]
    [SerializeField]
    [Tooltip("Configuration for the normal jump action.")]
    private JumpAttackData jumpAttackData;

    [SerializeField]
    [Tooltip("Configuration for the dash jump action.")]
    private DashJumpAttackData dashJumpAttackData;

    [Header("Obstruction Layers")]
    [SerializeField]
    [Tooltip("Layers that stop the tongue attack.")]
    private LayerMask tongueObstructionLayers = 1;

    [SerializeField]
    [Tooltip("Layers that stop drifting spore clouds.")]
    private LayerMask sporeObstructionLayers = 1;

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

    [SerializeField, Min(0f)]
    [Tooltip("Weight of the horizontal tongue sweep when it is available.")]
    private float horizontalTongueWeight = 2f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Multiplier applied to the weight of the action that was just used.")]
    private float repeatWeightMultiplier = 0.35f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Multiplier applied to an attack that directly follows another attack.")]
    private float attackAfterAttackWeightMultiplier = 0.4f;

    public float DetectionRange => detectionRange;
    public LayerMask GroundLayers => groundLayers;
    public LayerMask PlayerLayers => playerLayers;
    public float GroundCheckRadius => groundCheckRadius;

    public JumpAttackData JumpAttackData => jumpAttackData;
    public DashJumpAttackData DashJumpAttackData => dashJumpAttackData;

    public LayerMask TongueObstructionLayers => tongueObstructionLayers;
    public LayerMask SporeObstructionLayers => sporeObstructionLayers;

    public float PreferredDistance => preferredDistance;
    public float RetreatDistance => retreatDistance;

    public float DecisionInterval => decisionInterval;
    public float DecisionIntervalJitter => decisionIntervalJitter;

    public float JumpTowardWeight => jumpTowardWeight;
    public float JumpAwayWeight => jumpAwayWeight;
    public float DashJumpWeight => dashJumpWeight;
    public float TongueWeight => tongueWeight;
    public float SporeWeight => sporeWeight;
    public float HorizontalTongueWeight => horizontalTongueWeight;
    public float RepeatWeightMultiplier => repeatWeightMultiplier;
    public float AttackAfterAttackWeightMultiplier => attackAfterAttackWeightMultiplier;

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0f, detectionRange);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);

        preferredDistance = Mathf.Max(0f, preferredDistance);
        retreatDistance = Mathf.Max(0f, retreatDistance);

        decisionInterval = Mathf.Max(0f, decisionInterval);
        decisionIntervalJitter = Mathf.Max(0f, decisionIntervalJitter);

        jumpTowardWeight = Mathf.Max(0f, jumpTowardWeight);
        jumpAwayWeight = Mathf.Max(0f, jumpAwayWeight);
        dashJumpWeight = Mathf.Max(0f, dashJumpWeight);
        tongueWeight = Mathf.Max(0f, tongueWeight);
        sporeWeight = Mathf.Max(0f, sporeWeight);
        horizontalTongueWeight = Mathf.Max(0f, horizontalTongueWeight);
        repeatWeightMultiplier = Mathf.Clamp01(repeatWeightMultiplier);
        attackAfterAttackWeightMultiplier =
            Mathf.Clamp01(attackAfterAttackWeightMultiplier);
    }
}
