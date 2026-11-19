using UnityEngine;
using SpearWander.Abilities;
using System.Collections;

namespace SpearWander.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Animator))]
    public class Boss : MonoBehaviour, IDamageable
    {
        [Header("Configuration")]
        [SerializeField] private BossData bossData;

        [Header("References")]
        [SerializeField] private WeakPoint weakPoint;
        [SerializeField] private Transform graphicsTransform; // The visual part that rotates
        [SerializeField] private Transform meleeAttackPoint;
        [SerializeField] private Transform rangedAttackPoint;
        [SerializeField] private Transform antiAirAttackPoint;

        [Header("Debug")]
#pragma warning disable 0414
        [SerializeField] private bool debugMode = false;
#pragma warning restore 0414

        // Components
        protected Rigidbody2D rb;
        protected Animator animator;
        protected HitFlash hitFlashComponent;
        protected Knockback knockbackComponent;

        // State
        protected int currentHealth;
        protected Transform playerTransform;
        protected bool isDead = false;

        // Facing direction
        protected bool facingRight = true;

        // Attack state
        protected bool isAttacking = false;
        protected float meleeAttackCooldownTimer = 0f;
        protected float rangedAttackCooldownTimer = 0f;
        protected float antiAirCooldownTimer = 0f;

        // Stun/knockback
        protected bool isStunned = false;
        protected float stunTimer = 0f;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => bossData?.maxHealth ?? 10;
        public WeakPoint WeakPoint => weakPoint;

        protected Transform GraphicsTransform => graphicsTransform;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            hitFlashComponent = GetComponent<HitFlash>();
            knockbackComponent = GetComponent<Knockback>();

            if (bossData != null)
            {
                currentHealth = bossData.maxHealth;
            }
            else
            {
                Debug.LogError("[Boss] BossData not assigned!");
                currentHealth = 10;
            }

            // Sync weak point damage multiplier from BossData
            if (weakPoint != null && bossData != null)
            {
                weakPoint.damageMultiplier = bossData.weakPointDamageMultiplier;
            }

            // Subscribe to weak point damage event
            if (weakPoint != null)
            {
                weakPoint.OnDamageReceived += OnWeakPointDamaged;
            }
        }

        protected virtual void Start()
        {
            // Find player
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogError("[Boss] Player with tag 'Player' not found! Boss will not rotate or move towards player.");
            }

            // Ensure weak point references boss
            if (weakPoint != null && weakPoint.Boss == null)
            {
                weakPoint.Boss = this;
            }
        }

        protected virtual void OnDestroy()
        {
            if (weakPoint != null)
            {
                weakPoint.OnDamageReceived -= OnWeakPointDamaged;
            }
        }

        protected virtual void FixedUpdate()
        {
            if (isDead) return;

            HandleStun();

            if (isStunned || isAttacking) return;
        }

        #region Rotation & Movement

        protected virtual void HandleRotation()
        {
            if (playerTransform == null) return;

            float directionToPlayer = playerTransform.position.x - transform.position.x;
            bool shouldFaceRight = directionToPlayer > 0;

            if (shouldFaceRight != facingRight)
            {
                RotateTowardsPlayer(shouldFaceRight);
            }
        }

        protected virtual void RotateTowardsPlayer(bool faceRight)
        {
            facingRight = faceRight;

            if (graphicsTransform != null)
            {
                // Smooth rotation
                Vector3 targetScale = graphicsTransform.localScale;
                targetScale.x = Mathf.Abs(targetScale.x) * (facingRight ? 1 : -1);
                graphicsTransform.localScale = targetScale;

                // Update weak point position after rotation
                UpdateWeakPointPosition();
            }
        }

        protected virtual void UpdateWeakPointPosition()
        {
            if (weakPoint == null || weakPoint.transform == null || graphicsTransform == null) return;

            // Weak point should be on the back side (opposite to facing direction)
            float backOffset = facingRight ? -1f : 1f;
            Vector3 localPos = weakPoint.transform.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * backOffset;
            weakPoint.transform.localPosition = localPos;

            // Also flip the weak point sprite if needed
            if (weakPoint.TryGetComponent(out SpriteRenderer sr))
            {
                sr.flipX = !facingRight;
            }
        }

        #endregion
        
        #region Damage & Death

        public virtual bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
        {
            if (isDead || bossData == null) return false;

            // Check if hit was on weak point
            bool hitWeakPoint = false;
            if (weakPoint != null && weakPoint.Collider != null)
            {
                hitWeakPoint = weakPoint.Collider.OverlapPoint(hitPoint);
            }

            // Apply damage (weak point multiplier is already applied by WeakPoint component if hit there)
            currentHealth -= damage;

            // Notify weak point for visual feedback
            if (hitWeakPoint && weakPoint != null)
            {
                weakPoint.TakeDamageDirect(damage, hitPoint, damageSourcePosition);
            }

            if (knockbackComponent != null)
            {
                knockbackComponent.ApplyKnockback(damageSourcePosition, bossData.knockbackForce, bossData.knockbackDuration);
            }

            SFXManager.Instance?.PlaySFX(bossData.hitSfxType);
            VFXManager.Instance?.PlayVFX(bossData.hitVfxType, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

            if (animator != null) animator.SetTrigger("TookHit");
            if (hitFlashComponent != null) hitFlashComponent.Flash();

            if (currentHealth <= 0)
            {
                Die();
            }

            return damage > 0;
        }

        protected virtual void Die()
        {
            isDead = true;

            SFXManager.Instance?.PlaySFX(bossData.deathSfxType);
            VFXManager.Instance?.PlayVFX(bossData.deathVfxType, transform.position, Vector2.zero);
            if (animator != null) animator.SetTrigger("Death");

            // Disable colliders
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders) col.enabled = false;

            // Disable physics
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;

            // Disable scripts
            enabled = false;
        }

        #endregion

        #region Stun

        protected virtual void HandleStun()
        {
            if (stunTimer > 0f)
            {
                stunTimer -= Time.fixedDeltaTime;
                if (stunTimer <= 0f)
                {
                    isStunned = false;
                }
            }
        }

        public void ApplyStun(float duration)
        {
            isStunned = true;
            stunTimer = duration;
            rb.linearVelocity = Vector2.zero;
        }

        #endregion

        #region Weak Point Events

        private void OnWeakPointDamaged(float damage, Vector2 hitPoint, Vector2 sourcePosition)
        {
            // Additional visual/audio feedback when weak point is hit
            if (hitFlashComponent != null) hitFlashComponent.Flash();
            if (animator != null) animator.SetTrigger("WeakPointHit");

            // Camera shake
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.Shake(CameraShakeType.Medium);
            }
        }

        #endregion
    }
}
