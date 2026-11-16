using UnityEngine;
using System;

[DefaultExecutionOrder(100)]
public partial class Spear : MonoBehaviour
{
    public enum SpearState
    {
        Flying,
        BouncedOff,
        Embedded,
        Returning,
        Dropped
    }

    public SpearState currentState = SpearState.Flying;

    [Header("Authoring Configuration")]
    [SerializeField] private SpearConfig spearConfig;

    public event Action<Spear> OnSpearReturned;

    public bool HasHitEnemy { get; private set; }
    public SpearConfig Configuration => spearConfig;

    public float EffectiveMass => spearConfig != null ? spearConfig.RigidbodyMass : 2f;
    public float EffectiveGravityScale => spearConfig != null ? spearConfig.FlightGravityScale : 3f;
    public float EffectiveLinearDamping => spearConfig != null ? spearConfig.FlightLinearDamping : 0f;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private PlatformEffector2D _effector;

    private Collider2D _playerCollider;
    private bool _missingConfigWarningLogged;

    private float EnemyBounceSpeed =>
        spearConfig != null ? spearConfig.EnemyBounceSpeed : 15f;

    private float BounceAngle =>
        spearConfig != null ? spearConfig.BounceAngle : 90f;

    private float TipRaycastLength =>
        spearConfig != null ? spearConfig.TipRaycastLength : 1.5f;

    private float EmbedDepth =>
        spearConfig != null ? spearConfig.EmbedDepth : 0.15f;

    private float BossBounceSpeedMultiplier =>
        spearConfig != null ? spearConfig.BossBounceSpeedMultiplier : 0.6f;

    private float BossBounceGravityScale =>
        spearConfig != null ? spearConfig.BossBounceGravityScale : 1f;

    private float BossCollisionIgnoreDuration =>
        spearConfig != null ? spearConfig.BossCollisionIgnoreDuration : 0.5f;

    private float ReturnDuration =>
        spearConfig != null ? spearConfig.ReturnDuration : 0.5f;

    private float MaxReturnSpeed =>
        spearConfig != null ? spearConfig.MaxReturnSpeed : 50f;

    private float CatchDistance =>
        spearConfig != null ? spearConfig.CatchDistance : 0.7f;

    private float EvaluateReturnCurve(float normalizedTime)
    {
        return spearConfig != null
            ? spearConfig.EvaluateReturnCurve(normalizedTime)
            : 1f;
    }

    private float DroppedGravityScale =>
        spearConfig != null ? spearConfig.DroppedGravityScale : 1.5f;

    private float DroppedLinearDamping =>
        spearConfig != null ? spearConfig.DroppedLinearDamping : 2f;

    private float EnemyDeathHorizontalImpulse =>
        spearConfig != null ? spearConfig.EnemyDeathHorizontalImpulse : 6f;

    private float EnemyDeathUpwardImpulse =>
        spearConfig != null ? spearConfig.EnemyDeathUpwardImpulse : 10f;

    private Vector2 RopeAttachOffset =>
        spearConfig != null ? spearConfig.RopeAttachOffset : new Vector2(-1.25f, 0f);

    private float RopeWidth =>
        spearConfig != null ? spearConfig.RopeWidth : 0.08f;

    private Material RopeMaterial =>
        spearConfig != null ? spearConfig.RopeMaterial : null;

    private Color RopeStartColor =>
        spearConfig != null
            ? spearConfig.RopeStartColor
            : new Color(0.5f, 0.35f, 0.15f, 1f);

    private Color RopeEndColor =>
        spearConfig != null
            ? spearConfig.RopeEndColor
            : new Color(0.5f, 0.35f, 0.35f, 1f);

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();

        if (_rb != null)
        {
            // Missing-config defaults match the previously assigned
            // SpearV2 prefab Rigidbody2D values.
            _rb.mass = EffectiveMass;
            _rb.gravityScale = EffectiveGravityScale;
            _rb.linearDamping = EffectiveLinearDamping;
        }

        InitializePhysics();
    }

    void OnDestroy()
    {
        NotifyEmbeddableRemoved();
        DestroyRope();
    }

    public void Initialize(
        Collider2D playerCol,
        bool canSpawnRope,
        float ropeLen = 5f)
    {
        if (spearConfig == null && !_missingConfigWarningLogged)
        {
            Debug.LogWarning(
                "[Spear] SpearConfig is not assigned; using compatibility defaults.",
                this);
            _missingConfigWarningLogged = true;
        }

        ResetIgnoredWall();

        _playerCollider = playerCol;
        _playerTransform = playerCol.transform;

        CacheCameraImpulseSource();

        _collider.isTrigger = true;

        InitializeCombatState();

        _lastPosition = transform.position;
        _lastVelocity = Vector2.zero;

        _canSpawnRope = canSpawnRope;
        _ropeLength = ropeLen;

        IgnoreAllPlayerColliders(true);
    }

    void FixedUpdate()
    {
        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Returning)
        {
            _lastVelocity = _rb.linearVelocity;
        }

        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Returning)
        {
            CheckForTunneling();
        }

        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Returning ||
            currentState == SpearState.Dropped)
        {
            UpdateRotation();
        }

        if (currentState == SpearState.Returning)
        {
            MoveTowardsPlayer();
        }

        if (currentState == SpearState.Embedded)
        {
            MoveEmbeddedWithTarget();
        }
        else
        {
            _embeddedTargetRigidbody = null;
        }
    }
}
