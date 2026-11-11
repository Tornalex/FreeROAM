using UnityEngine;

/// <summary>
/// Shared authoring configuration for a spear instance. This asset contains
/// tuning only; flight state, damage values assigned for a throw, rope length,
/// timers and targets remain on the runtime Spear/Player components.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Spear/Config")]
public sealed class SpearConfig : ScriptableObject
{
    [Header("Flight and Impact")]
    [SerializeField, Min(0f), Tooltip("Speed used when the spear bounces after hitting an enemy.")]
    private float enemyBounceSpeed = 10f;

    [SerializeField, Range(0f, 360f), Tooltip("Total width of the random enemy-bounce cone in degrees.")]
    private float bounceAngle = 90f;

    [SerializeField, Min(0.01f), Tooltip("Length of the tip raycast used to recover the impact surface normal.")]
    private float tipRaycastLength = 1.5f;

    [SerializeField, Min(0.001f), Tooltip("Distance the spear is offset into a surface when embedded. The old serialized zero was clamped to 0.15 at runtime.")]
    private float embedDepth = 0.15f;

    [Header("Flight Rigidbody")]
    [SerializeField, Min(0.01f), Tooltip("Mass used by the live spear and trajectory preview.")]
    private float rigidbodyMass = 2f;

    [SerializeField, Min(0f), Tooltip("Gravity scale while the spear is in flight.")]
    private float flightGravityScale = 3f;

    [SerializeField, Min(0f), Tooltip("Linear damping while the spear is in flight.")]
    private float flightLinearDamping;

    [SerializeField, Min(0f), Tooltip("Velocity multiplier applied when the spear is deflected by a boss.")]
    private float bossBounceSpeedMultiplier = 0.6f;

    [SerializeField, Min(0f), Tooltip("Gravity scale applied after the spear is deflected by a boss.")]
    private float bossBounceGravityScale = 1f;

    [SerializeField, Min(0f), Tooltip("Time the spear ignores the boss collider after being deflected.")]
    private float bossCollisionIgnoreDuration = 0.5f;

    [Header("Return")]
    [SerializeField, Tooltip("Speed progression over the normalized return duration.")]
    private AnimationCurve returnCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));

    [SerializeField, Min(0.0001f), Tooltip("Duration used to normalize the return-speed curve.")]
    private float returnDuration = 1f;

    [SerializeField, Min(0f), Tooltip("Maximum speed of the returning spear.")]
    private float maxReturnSpeed = 120f;

    [SerializeField, Min(0f), Tooltip("Distance from the player at which the spear is caught.")]
    private float catchDistance = 0.75f;

    [Header("Dropped Spear")]
    [SerializeField, Min(0f), Tooltip("Gravity scale when the spear becomes a dropped physics object.")]
    private float droppedGravityScale = 1.5f;

    [SerializeField, Min(0f), Tooltip("Linear damping when the spear is dropped after return or target removal.")]
    private float droppedLinearDamping = 2f;

    [SerializeField, Min(0f), Tooltip("Maximum horizontal impulse range when an embedded enemy dies.")]
    private float enemyDeathHorizontalImpulse = 6f;

    [SerializeField, Min(0f), Tooltip("Upward impulse when an embedded enemy dies.")]
    private float enemyDeathUpwardImpulse = 10f;

    [Header("Rope Appearance")]
    [SerializeField, Tooltip("Local rope attachment offset on the spear handle.")]
    private Vector2 ropeAttachOffset = new Vector2(-0.8f, 0f);

    [SerializeField, Min(0.001f), Tooltip("Width of the generated rope LineRenderer.")]
    private float ropeWidth = 0.1f;

    [SerializeField, Tooltip("Optional material assigned to the generated rope LineRenderer.")]
    private Material ropeMaterial;

    [SerializeField, Tooltip("Color at the spear end of the rope.")]
    private Color ropeStartColor = new Color(0.5f, 0.35f, 0.15f, 1f);

    [SerializeField, Tooltip("Color at the player end of the rope.")]
    private Color ropeEndColor = new Color(0.5f, 0.35f, 0.35f, 1f);

    public float EnemyBounceSpeed => enemyBounceSpeed;
    public float BounceAngle => bounceAngle;
    public float TipRaycastLength => tipRaycastLength;
    public float EmbedDepth => embedDepth;
    public float RigidbodyMass => rigidbodyMass;
    public float FlightGravityScale => flightGravityScale;
    public float FlightLinearDamping => flightLinearDamping;
    public float BossBounceSpeedMultiplier => bossBounceSpeedMultiplier;
    public float BossBounceGravityScale => bossBounceGravityScale;
    public float BossCollisionIgnoreDuration => bossCollisionIgnoreDuration;

    public float ReturnDuration => returnDuration;
    public float MaxReturnSpeed => maxReturnSpeed;
    public float CatchDistance => catchDistance;
    public float EvaluateReturnCurve(float normalizedTime)
    {
        return returnCurve != null ? returnCurve.Evaluate(normalizedTime) : 1f;
    }

    public float DroppedGravityScale => droppedGravityScale;
    public float DroppedLinearDamping => droppedLinearDamping;
    public float EnemyDeathHorizontalImpulse => enemyDeathHorizontalImpulse;
    public float EnemyDeathUpwardImpulse => enemyDeathUpwardImpulse;

    public Vector2 RopeAttachOffset => ropeAttachOffset;
    public float RopeWidth => ropeWidth;
    public Material RopeMaterial => ropeMaterial;
    public Color RopeStartColor => ropeStartColor;
    public Color RopeEndColor => ropeEndColor;

    private void OnValidate()
    {
        enemyBounceSpeed = Mathf.Max(0f, enemyBounceSpeed);
        bounceAngle = Mathf.Clamp(bounceAngle, 0f, 360f);
        tipRaycastLength = Mathf.Max(0.01f, tipRaycastLength);
        embedDepth = Mathf.Max(0.001f, embedDepth);
        rigidbodyMass = Mathf.Max(0.01f, rigidbodyMass);
        flightGravityScale = Mathf.Max(0f, flightGravityScale);
        flightLinearDamping = Mathf.Max(0f, flightLinearDamping);
        bossBounceSpeedMultiplier = Mathf.Max(0f, bossBounceSpeedMultiplier);
        bossBounceGravityScale = Mathf.Max(0f, bossBounceGravityScale);
        bossCollisionIgnoreDuration = Mathf.Max(0f, bossCollisionIgnoreDuration);

        returnDuration = Mathf.Max(0.0001f, returnDuration);
        maxReturnSpeed = Mathf.Max(0f, maxReturnSpeed);
        catchDistance = Mathf.Max(0f, catchDistance);

        droppedGravityScale = Mathf.Max(0f, droppedGravityScale);
        droppedLinearDamping = Mathf.Max(0f, droppedLinearDamping);
        enemyDeathHorizontalImpulse = Mathf.Max(0f, enemyDeathHorizontalImpulse);
        enemyDeathUpwardImpulse = Mathf.Max(0f, enemyDeathUpwardImpulse);

        ropeWidth = Mathf.Max(0.001f, ropeWidth);
    }
}
