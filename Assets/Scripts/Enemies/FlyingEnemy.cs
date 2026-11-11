using UnityEngine;

public class FlyingEnemy : BaseEnemy
{
    [Header("AI Profile")]
    [SerializeField]
    [Tooltip("Authoring tuning for patrol, chase and stun behaviour.")]
    private FlyingEnemyProfile aiProfile;

    // Runtime-created enemies without a profile still work with the values that
    // were previously serialized on the component. Prefabs always assign a profile.
    private const float DefaultFlySpeed = 2.5f;
    private const float DefaultChaseSpeed = 4.5f;
    private const float DefaultPatrolRadius = 3f;
    private const float DefaultDetectionRadius = 7f;
    private const float DefaultLoseRadius = 12f;
    private const float DefaultStunDuration = 0.5f;

    private Vector2 _startPosition;
    private Vector2 _targetPatrolPoint;
    private Transform _player;
    private bool _isStunned = false;
    private bool _isChasing = false;
    private float _stunTimer = 0f;

    private float FlySpeed => aiProfile != null ? aiProfile.FlySpeed : DefaultFlySpeed;
    private float ChaseSpeed => aiProfile != null ? aiProfile.ChaseSpeed : DefaultChaseSpeed;
    private float PatrolRadius =>
        aiProfile != null ? aiProfile.PatrolRadius : DefaultPatrolRadius;
    private float DetectionRadius =>
        aiProfile != null ? aiProfile.DetectionRadius : DefaultDetectionRadius;
    private float LoseRadius => aiProfile != null ? aiProfile.LoseRadius : DefaultLoseRadius;
    private float StunDuration =>
        aiProfile != null ? aiProfile.StunDuration : DefaultStunDuration;

    protected override void Awake()
    {
        base.Awake();

        if (aiProfile == null)
        {
            Debug.LogWarning(
                $"[FlyingEnemy] {name} has no FlyingEnemyProfile assigned; using built-in defaults.",
                this);
        }

        _startPosition = transform.position;

        Player playerComponent = FindPlayerObject();
        if (playerComponent != null) _player = playerComponent.transform;

        SetNewPatrolPoint();
    }

    void FixedUpdate()
    {
        if (isDead || _player == null) return;

        if (_isStunned)
        {
            _stunTimer -= Time.fixedDeltaTime;
            if (_stunTimer <= 0) _isStunned = false;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        _isChasing = distanceToPlayer < (_isChasing ? LoseRadius : DetectionRadius);

        if (_isChasing) ChasePlayer();
        else Patrol();

        FlipSprite();
    }

    public override bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        bool damageApplied =
            base.TakeDamage(damage, hitPoint, damageSourcePosition); // Esegue logica base (vita, flash, knockback)

        if (!isDead && _player != null)
        {
            ApplyStun();
            if (Vector2.Distance(transform.position, _player.position) < LoseRadius) _isChasing = true;
        }

        return damageApplied;
    }

    public override void OnPogoBounce()
    {
        base.OnPogoBounce();
        ApplyStun();
    }

    private void ApplyStun()
    {
        _isStunned = true;
        _stunTimer = StunDuration;
        rb.linearVelocity = Vector2.zero;
    }

    private void Patrol()
    {
        transform.position = Vector2.MoveTowards(transform.position, _targetPatrolPoint, FlySpeed * Time.fixedDeltaTime);
        if (Vector2.Distance(transform.position, _targetPatrolPoint) < 0.2f) SetNewPatrolPoint();
    }

    private void SetNewPatrolPoint()
    {
        _targetPatrolPoint = _startPosition + (Random.insideUnitCircle * PatrolRadius);
    }

    private void ChasePlayer()
    {
        transform.position = Vector2.MoveTowards(transform.position, _player.position, ChaseSpeed * Time.fixedDeltaTime);
    }

    private void FlipSprite()
    {
        float dirX = _isChasing ? (_player.position.x - transform.position.x) : (_targetPatrolPoint.x - transform.position.x);
        if (Mathf.Abs(dirX) > 0.1f) spriteRenderer.flipX = dirX < 0; // 'spriteRenderer' viene da BaseEnemy
    }
}
