using UnityEngine;

/// <summary>
/// Enemy designed to teach the player the Spear Recall mechanic.
///
/// Behavior:
/// - Maintains a preferred distance from the player.
/// - Periodically shoots projectiles at the player.
/// - Flashes red before shooting.
/// - Jumps when a thrown Spear is actually approaching it.
/// - Does not react to a Spear during Recall.
///
/// Intended interaction:
///
/// Throw Spear
///     ↓
/// Enemy jumps
///     ↓
/// Spear lands behind enemy
///     ↓
/// Recall
///     ↓
/// Spear passes through enemy
///     ↓
/// Recall damage
/// </summary>
public class RecallEnemy : BaseEnemy
{
    [Header("Movement")]

    [Tooltip("Preferred distance from the player.")]
    [SerializeField, Min(0f)] private float preferredDistance = 6f;

    [Tooltip("Allowed distance around the preferred distance.")]
    [SerializeField, Min(0f)] private float distanceTolerance = 1f;

    [Tooltip("Horizontal movement speed.")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;


    [Header("Spear Dodge")]

    [Tooltip("Vertical velocity applied when dodging the Spear.")]
    [SerializeField, Min(0f)] private float dodgeJumpForce = 8f;

    [Tooltip("Maximum distance at which the enemy reacts to the Spear.")]
    [SerializeField, Min(0f)] private float dodgeDetectionDistance = 5f;

    [Tooltip("Minimum time between two dodge reactions.")]
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.75f;

    [Tooltip("How strongly the Spear must be moving toward the enemy.")]
    [Range(0f, 1f)]
    [SerializeField] private float spearDirectionThreshold = 0.7f;


    [Header("Ranged Attack")]

    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] private Transform firePoint;

    [Tooltip("Time between shots.")]
    [SerializeField, Min(0f)] private float attackInterval = 2f;

    [Tooltip("Projectile movement speed.")]
    [SerializeField, Min(0f)] private float projectileSpeed = 7f;


    [Header("Ranged Attack Telegraph")]

    [Tooltip("How long the enemy warns the player before shooting.")]
    [SerializeField, Min(0f)] private float attackTelegraphDuration = 0.5f;

    [Tooltip("Time between each red/normal flash.")]
    [SerializeField, Min(0f)] private float flashInterval = 0.1f;

    [Tooltip("Color used during the attack warning.")]
    [SerializeField] private Color telegraphColor = Color.red;


    private Transform player;
    private Player playerComponent;

    private float attackTimer;
    private float dodgeTimer;

    private Spear trackedSpear;

    private bool isTelegraphing;
    private float telegraphTimer;
    private float flashTimer;
    private bool isFlashing;


    protected override void Awake()
    {
        base.Awake();

        Player resolvedPlayer = FindPlayerObject();

        if (resolvedPlayer != null)
        {
            player = resolvedPlayer.transform;
            playerComponent = resolvedPlayer;
        }
    }

    private void OnValidate()
    {
        preferredDistance = Mathf.Max(0f, preferredDistance);
        distanceTolerance = Mathf.Max(0f, distanceTolerance);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        dodgeJumpForce = Mathf.Max(0f, dodgeJumpForce);
        dodgeDetectionDistance = Mathf.Max(0f, dodgeDetectionDistance);
        dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
        attackInterval = Mathf.Max(0f, attackInterval);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        attackTelegraphDuration = Mathf.Max(0f, attackTelegraphDuration);
        flashInterval = Mathf.Max(0f, flashInterval);
    }


    protected override void Start()
    {
        base.Start();

        attackTimer = attackInterval;
    }


    private void Update()
    {
        if (isDead || player == null)
            return;

        UpdateTimers();

        MaintainDistance();

        CheckForIncomingSpear();

        HandleRangedAttack();
    }

    private void UpdateTimers()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (dodgeTimer > 0f)
            dodgeTimer -= Time.deltaTime;
    }


    // ============================================================
    // MOVEMENT
    // ============================================================

    private void MaintainDistance()
    {
        if (rb == null)
            return;

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );


        // Player is too close.
        // Move away from the player.
        if (distance < preferredDistance - distanceTolerance)
        {
            Vector2 direction =
                ((Vector2)transform.position -
                 (Vector2)player.position).normalized;

            rb.linearVelocity = new Vector2(
                direction.x * moveSpeed,
                rb.linearVelocity.y
            );

            return;
        }


        // Player is too far.
        // Move toward the player.
        if (distance > preferredDistance + distanceTolerance)
        {
            Vector2 direction =
                ((Vector2)player.position -
                 (Vector2)transform.position).normalized;

            rb.linearVelocity = new Vector2(
                direction.x * moveSpeed,
                rb.linearVelocity.y
            );

            return;
        }


        // Player is inside the preferred distance range.
        // Stop horizontal movement.
        rb.linearVelocity = new Vector2(
            0f,
            rb.linearVelocity.y
        );
    }


    // ============================================================
    // SPEAR DODGE
    // ============================================================

    private void CheckForIncomingSpear()
    {
        // We are currently tracking a Spear.
        // Check whether it is still valid.
        if (trackedSpear != null &&
            trackedSpear.currentState != Spear.SpearState.Flying)
        {
            trackedSpear = null;
        }

        // No Spear currently tracked.
        if (trackedSpear == null)
        {
            trackedSpear = FindIncomingSpear();
        }

        if (trackedSpear == null)
            return;

        // Cannot dodge again while on cooldown.
        if (dodgeTimer > 0f)
            return;

        // Keep tracking a Spear that is not currently threatening: it may
        // become dangerous again before it stops flying.
        if (IsSpearThreatening(trackedSpear))
        {
            DodgeSpear();
        }
    }

    private Spear FindIncomingSpear()
    {
        if (playerComponent == null || playerComponent.Combat == null)
            return null;

        // The game uses a single player spear: reading it from PlayerCombat
        // avoids a per-frame FindObjectsByType allocation.
        Spear spear = playerComponent.Combat.CurrentSpear;

        // IMPORTANT:
        // Only react to a thrown Spear.
        // Returning is deliberately ignored.
        return IsSpearThreatening(spear) ? spear : null;
    }

    /// <summary>
    /// Shared threat evaluation used both when acquiring a Spear and when
    /// deciding whether an already tracked Spear still justifies a dodge.
    /// </summary>
    private bool IsSpearThreatening(Spear spear)
    {
        if (spear == null || spear.currentState != Spear.SpearState.Flying)
            return false;

        Rigidbody2D spearRb = spear.GetComponent<Rigidbody2D>();
        if (spearRb == null)
            return false;

        Vector2 spearVelocity = spearRb.linearVelocity;

        // Spear is not moving.
        if (spearVelocity.sqrMagnitude < 0.01f)
            return false;

        Vector2 toEnemy =
            (Vector2)transform.position -
            (Vector2)spear.transform.position;

        float distance = toEnemy.magnitude;

        // Too far away to react.
        if (distance > dodgeDetectionDistance)
            return false;

        float alignment = Vector2.Dot(
            spearVelocity.normalized,
            toEnemy.normalized);

        // Positive value = Spear is moving toward us.
        //
        // 1.0 = directly toward the enemy
        // 0.0 = perpendicular
        // -1.0 = directly away
        return alignment >= spearDirectionThreshold;
    }


    private void DodgeSpear()
    {
        dodgeTimer = dodgeCooldown;

        // Preserve horizontal movement.
        // Only add the vertical dodge.
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            dodgeJumpForce
        );

        trackedSpear = null;
    }

    // ============================================================
    // RANGED ATTACK
    // ============================================================

    private void HandleRangedAttack()
    {
        if (isTelegraphing)
        {
            UpdateAttackTelegraph();
            return;
        }


        if (attackTimer > 0f)
            return;


        StartAttackTelegraph();
    }


    private void StartAttackTelegraph()
    {
        isTelegraphing = true;

        telegraphTimer =
            attackTelegraphDuration;

        flashTimer = 0f;
        animator.SetBool("isAttacking", true); //animazione attacco

        isFlashing = false;
    }


    private void UpdateAttackTelegraph()
    {
        telegraphTimer -= Time.deltaTime;
        flashTimer -= Time.deltaTime;


        if (flashTimer <= 0f)
        {
            flashTimer = flashInterval;

            isFlashing = !isFlashing;

            spriteRenderer.color =
                isFlashing
                    ? telegraphColor
                    : Color.white;
        }


        if (telegraphTimer <= 0f)
        {
            isTelegraphing = false;

            spriteRenderer.color = Color.white;

            Shoot();

            attackTimer = attackInterval;
        }
    }


    private void Shoot()
    {
        if (projectilePrefab == null)
            return;

        if (firePoint == null)
            return;

        if (player == null)
            return;


        GameObject projectile =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.identity
            );


        Vector2 direction =
            (
                (Vector2)player.position -
                (Vector2)firePoint.position
            ).normalized;


        EnemyProjectile enemyProjectile =
            projectile.GetComponent<EnemyProjectile>();


        if (enemyProjectile != null)
        {
            enemyProjectile.Launch(
                direction,
                projectileSpeed
            );
        }
        animator.SetBool("isAttacking", false); //fine animazione attacco
    }
}