using System.Collections.Generic;
using UnityEngine;

namespace SpearWander.Boss
{
    /// <summary>
    /// Single-phase, free-roaming boss that jumps toward/away from the player,
    /// closes long horizontal gaps with a low dash jump, and alternates between
    /// a non-grabbing tongue lunge and suspended spore clouds. Health, weak
    /// point, knockback, hit feedback and the death sequence are inherited from
    /// <see cref="Boss"/>.
    /// </summary>
    public class FreeRoamBoss : Boss
    {
        private const float DashCeilingCastRadius = 0.55f;

        private enum State
        {
            Idle,
            Jumping,
            DashJumpWindup,
            DashJumping,
            TongueWindup,
            TongueExtend,
            TongueRetract,
            Spores,
            Recover
        }

        private enum ActionType
        {
            None,
            JumpToward,
            JumpAway,
            DashJump,
            Tongue,
            Spores
        }

        [Header("Free Roam - References")]
        [SerializeField] private Transform tongueOrigin;
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private GameObject sporePrefab;

        [Header("Free Roam - Authoring Data")]
        [SerializeField]
        [Tooltip("Perception, jump/dash movement, decision timing and action weights.")]
        private BossAIProfile aiProfile;

        [SerializeField]
        [Tooltip("Timing, reach and visuals of the tongue lunge.")]
        private TongueAttackData tongueAttackData;

        [SerializeField]
        [Tooltip("Timing, radius and drift of the spore cloud attack.")]
        private SporeAttackData sporeAttackData;

        // Read-only views over the authoring assets. The literal fallbacks only
        // apply to runtime-created bosses without assets: every prefab and
        // scene instance assigns the real ScriptableObjects. They match the
        // values that were previously serialized on this component so a
        // missing asset cannot silently change the tuning.
        private float DetectionRange =>
            aiProfile != null ? aiProfile.DetectionRange : 14f;

        private LayerMask GroundLayers =>
            aiProfile != null ? aiProfile.GroundLayers : (LayerMask)1;

        private LayerMask PlayerLayers =>
            aiProfile != null ? aiProfile.PlayerLayers : (LayerMask)8;

        private float GroundCheckRadius =>
            aiProfile != null ? aiProfile.GroundCheckRadius : 0.22f;

        private LayerMask TongueObstructionLayers =>
            aiProfile != null ? aiProfile.TongueObstructionLayers : (LayerMask)1;

        private LayerMask SporeObstructionLayers =>
            aiProfile != null ? aiProfile.SporeObstructionLayers : (LayerMask)1;

        private LayerMask DashJumpObstructionLayers =>
            aiProfile != null ? aiProfile.DashJumpObstructionLayers : (LayerMask)1;

        private float JumpUpVelocity =>
            aiProfile != null ? aiProfile.JumpUpVelocity : 9f;

        private float JumpDistance =>
            aiProfile != null ? aiProfile.JumpDistance : 4f;

        private float JumpMaxHorizontalSpeed =>
            aiProfile != null ? aiProfile.JumpMaxHorizontalSpeed : 7f;

        private float LandingRecovery =>
            aiProfile != null ? aiProfile.LandingRecovery : 0.35f;

        private float PreferredDistance =>
            aiProfile != null ? aiProfile.PreferredDistance : 3.5f;

        private float RetreatDistance =>
            aiProfile != null ? aiProfile.RetreatDistance : 2f;

        private float DecisionInterval =>
            aiProfile != null ? aiProfile.DecisionInterval : 0.5f;

        private float DecisionIntervalJitter =>
            aiProfile != null ? aiProfile.DecisionIntervalJitter : 0.15f;

        private float MaxAirborneDuration =>
            aiProfile != null ? aiProfile.MaxAirborneDuration : 3f;

        private float RecoveryCooldown =>
            aiProfile != null ? aiProfile.RecoveryCooldown : 1f;

        private float JumpTowardWeight =>
            aiProfile != null ? aiProfile.JumpTowardWeight : 3f;

        private float JumpAwayWeight =>
            aiProfile != null ? aiProfile.JumpAwayWeight : 1.5f;

        private float DashJumpWeight =>
            aiProfile != null ? aiProfile.DashJumpWeight : 1.5f;

        private float TongueWeight =>
            aiProfile != null ? aiProfile.TongueWeight : 2f;

        private float SporeWeight =>
            aiProfile != null ? aiProfile.SporeWeight : 2f;

        private float RepeatWeightMultiplier =>
            aiProfile != null ? aiProfile.RepeatWeightMultiplier : 0.35f;

        private float AttackAfterAttackWeightMultiplier =>
            aiProfile != null ? aiProfile.AttackAfterAttackWeightMultiplier : 0.4f;

        private float DashJumpMinDistance =>
            aiProfile != null ? aiProfile.DashJumpMinDistance : 7f;

        private float DashJumpHorizontalSpeed =>
            aiProfile != null ? aiProfile.DashJumpHorizontalSpeed : 18f;

        private float DashJumpUpVelocity =>
            aiProfile != null ? aiProfile.DashJumpUpVelocity : 10f;

        private float DashJumpLandingOffset =>
            aiProfile != null ? aiProfile.DashJumpLandingOffset : 1f;

        private float DashJumpPreparationTime =>
            aiProfile != null ? aiProfile.DashJumpPreparationTime : 0.45f;

        private float DashJumpRecoveryTime =>
            aiProfile != null ? aiProfile.DashJumpRecoveryTime : 0.45f;

        private float DashJumpCooldown =>
            aiProfile != null ? aiProfile.DashJumpCooldown : 5f;

        private float DashJumpMaxAirborneDuration =>
            aiProfile != null ? aiProfile.DashJumpMaxAirborneDuration : 2f;

        private float TongueWindupTime =>
            tongueAttackData != null ? tongueAttackData.WindupTime : 0.45f;

        private float TongueExtendTime =>
            tongueAttackData != null ? tongueAttackData.ExtendTime : 0.12f;

        private float TongueRetractTime =>
            tongueAttackData != null ? tongueAttackData.RetractTime : 0.22f;

        private float TongueRecoveryTime =>
            tongueAttackData != null ? tongueAttackData.RecoveryTime : 0.3f;

        private float TongueRange =>
            tongueAttackData != null ? tongueAttackData.Range : 6f;

        private float TongueWidth =>
            tongueAttackData != null ? tongueAttackData.Width : 0.4f;

        private int TongueDamage =>
            tongueAttackData != null ? tongueAttackData.Damage : 2;

        private float TongueCooldown =>
            tongueAttackData != null ? tongueAttackData.Cooldown : 2.5f;

        private Color TongueColor =>
            tongueAttackData != null
                ? tongueAttackData.TongueColor
                : new Color(0.9f, 0.3f, 0.4f, 1f);

        private int TongueSortingOrder =>
            tongueAttackData != null ? tongueAttackData.SortingOrder : 10;

        private float SporeWindupTime =>
            sporeAttackData != null ? sporeAttackData.WindupTime : 0.6f;

        private float SporeRecoveryTime =>
            sporeAttackData != null ? sporeAttackData.RecoveryTime : 0.35f;

        private float SporeLifetime =>
            sporeAttackData != null ? sporeAttackData.Lifetime : 6f;

        private float SporeRadius =>
            sporeAttackData != null ? sporeAttackData.Radius : 0.9f;

        private int SporeDamage =>
            sporeAttackData != null ? sporeAttackData.Damage : 1;

        private float SporeCooldown =>
            sporeAttackData != null ? sporeAttackData.Cooldown : 4f;

        private int MaxActiveSpores =>
            sporeAttackData != null ? sporeAttackData.MaxActiveSpores : 3;

        private Vector2 SporeDrift =>
            sporeAttackData != null
                ? sporeAttackData.Drift
                : new Vector2(0.35f, 0f);

        private float SporeRange =>
            sporeAttackData != null ? sporeAttackData.Range : 9f;

        private State state = State.Idle;
        private ActionType lastAction = ActionType.None;
        private bool lastActionWasAttack;

        private float stateTimer;
        private float decisionTimer;
        private float tongueCooldownTimer;
        private float sporeCooldownTimer;
        private float dashJumpCooldownTimer;
        private float playerSearchTimer;
        private float jumpGroundGraceTimer;
        private float airborneTimer;
        private float recoveryCooldownTimer;

        private bool isGrounded;
        private bool tongueHitApplied;
        private bool suspensionActive;
        private bool suspensionChangedBodyType;

        private Vector2 tongueStart;
        private Vector2 tongueTarget;
        private Vector2 lastGroundedPosition;
        private RigidbodyType2D suspendedBodyType;

        private float dashDirection = 1f;
        private float dashDestinationX;

        private readonly List<SporeCloud> activeSpores = new List<SporeCloud>();
        private readonly List<Collider2D> tongueOverlapResults = new List<Collider2D>(8);
        private readonly List<Collider2D> groundOverlapResults = new List<Collider2D>(4);
        private readonly List<RaycastHit2D> obstructionHits = new List<RaycastHit2D>(4);
        private readonly List<RaycastHit2D> dashHits = new List<RaycastHit2D>(4);
        private readonly List<Collider2D> suspendedColliders = new List<Collider2D>(4);
        private readonly List<bool> suspendedColliderStates = new List<bool>(4);

        private ContactFilter2D playerFilter;
        private ContactFilter2D groundFilter;
        private ContactFilter2D tongueObstructionFilter;
        private ContactFilter2D dashJumpObstructionFilter;

        private LineRenderer tongueLine;
        private Material tongueMaterial;

        protected override void Awake()
        {
            base.Awake();

            if (aiProfile == null || tongueAttackData == null || sporeAttackData == null)
            {
                Debug.LogWarning(
                    "[FreeRoamBoss] Missing authoring data; using built-in defaults. " +
                    "Assign a BossAIProfile and the two BossAttackData assets.",
                    this);
            }

            playerFilter = CreateLayerFilter(PlayerLayers.value);
            groundFilter = CreateLayerFilter(GroundLayers.value & ~PlayerLayers.value);
            tongueObstructionFilter = CreateLayerFilter(
                TongueObstructionLayers.value & ~PlayerLayers.value);
            dashJumpObstructionFilter = CreateLayerFilter(
                DashJumpObstructionLayers.value & ~PlayerLayers.value);

            lastGroundedPosition = transform.position;

            CreateTongueVisual();
        }

        protected override void Start()
        {
            base.Start();

            decisionTimer = DecisionInterval;
            isGrounded = CheckGrounded();
            if (isGrounded)
            {
                lastGroundedPosition = transform.position;
            }
        }

        private void OnEnable()
        {
            if (isDead)
            {
                suspensionActive = false;
                suspensionChangedBodyType = false;
                suspendedColliders.Clear();
                suspendedColliderStates.Clear();
                return;
            }

            if (!suspensionActive) return;

            suspensionActive = false;
            RestoreSuspendedColliders();

            if (rb != null)
            {
                if (suspensionChangedBodyType)
                {
                    rb.bodyType = suspendedBodyType;
                }

                rb.linearVelocity = Vector2.zero;
            }

            suspensionChangedBodyType = false;

            HideTongue();
            tongueHitApplied = true;
            isAttacking = false;

            state = State.Recover;
            stateTimer = Mathf.Max(0.1f, LandingRecovery);
            decisionTimer = Mathf.Max(0f, DecisionInterval);
            airborneTimer = 0f;
            recoveryCooldownTimer = 0f;
            jumpGroundGraceTimer = 0f;

            isGrounded = CheckGrounded();
            if (isGrounded)
            {
                lastGroundedPosition = transform.position;
            }
        }

        private void OnDisable()
        {
            HideTongue();
            DespawnAllSpores();

            tongueHitApplied = true;
            isAttacking = false;
            state = State.Idle;
            stateTimer = 0f;
            ClearDashJumpState();

            if (isDead)
            {
                suspensionActive = false;
                suspensionChangedBodyType = false;
                suspendedColliders.Clear();
                suspendedColliderStates.Clear();
                return;
            }

            if (suspensionActive) return;

            suspensionActive = true;
            CacheAndDisableBodyColliders();

            if (rb != null)
            {
                suspendedBodyType = rb.bodyType;
                suspensionChangedBodyType = true;
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        protected override void OnDestroy()
        {
            DespawnAllSpores();
            HideTongue();

            if (tongueMaterial != null)
            {
                Destroy(tongueMaterial);
                tongueMaterial = null;
            }

            base.OnDestroy();
        }

        protected override void FixedUpdate()
        {
            if (isDead) return;

            HandleStun();
            if (isStunned) return;

            UpdatePlayerReference();
            UpdateGrounded();

            if (state == State.Idle || state == State.Jumping)
            {
                HandleRotation();
            }

            switch (state)
            {
                case State.Idle:
                    TickIdle();
                    break;

                case State.Jumping:
                    TickJumping();
                    break;

                case State.DashJumpWindup:
                    stateTimer -= Time.fixedDeltaTime;
                    if (stateTimer <= 0f)
                    {
                        LaunchDashJump();
                    }
                    break;

                case State.DashJumping:
                    TickDashJumping();
                    break;

                case State.TongueWindup:
                    stateTimer -= Time.fixedDeltaTime;
                    if (stateTimer <= 0f)
                    {
                        state = State.TongueExtend;
                        stateTimer = TongueExtendTime;
                    }
                    break;

                case State.TongueExtend:
                    TickTongueExtend();
                    break;

                case State.TongueRetract:
                    stateTimer -= Time.fixedDeltaTime;
                    if (stateTimer <= 0f)
                    {
                        FinishAttack(ActionType.Tongue, TongueRecoveryTime);
                    }
                    break;

                case State.Spores:
                    stateTimer -= Time.fixedDeltaTime;
                    if (stateTimer <= 0f)
                    {
                        FinishAttack(ActionType.Spores, SporeRecoveryTime);
                    }
                    break;

                case State.Recover:
                    stateTimer -= Time.fixedDeltaTime;
                    if (stateTimer <= 0f)
                    {
                        state = State.Idle;
                        decisionTimer = Mathf.Max(
                            0f,
                            DecisionInterval + Random.Range(
                                -DecisionIntervalJitter,
                                DecisionIntervalJitter));
                    }
                    break;
            }
        }

        void Update()
        {
            if (isDead) return;

            if (tongueCooldownTimer > 0f)
            {
                tongueCooldownTimer -= Time.deltaTime;
            }

            if (sporeCooldownTimer > 0f)
            {
                sporeCooldownTimer -= Time.deltaTime;
            }

            if (dashJumpCooldownTimer > 0f)
            {
                dashJumpCooldownTimer -= Time.deltaTime;
            }

            UpdateTongueVisual();
        }

        /// <summary>
        /// Damage must never interrupt the free-roam AI. The base implementation
        /// applies knockback, which zeroes the current velocity and pushes the
        /// boss. That side effect is undone here, while health, feedback and
        /// death keep running through <see cref="Boss.TakeDamage"/>.
        /// </summary>
        public override bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
        {
            if (rb == null)
            {
                return base.TakeDamage(damage, hitPoint, damageSourcePosition);
            }

            Vector2 velocityBeforeHit = rb.linearVelocity;
            bool damaged = base.TakeDamage(damage, hitPoint, damageSourcePosition);

            // Let the base death sequence keep its own velocity/physics cleanup.
            if (isDead)
            {
                return damaged;
            }

            rb.linearVelocity = velocityBeforeHit;

            if (knockbackComponent != null)
            {
                knockbackComponent.EndKnockback();
            }

            return damaged;
        }

        protected override void Die()
        {
            if (isDead) return;

            state = State.Idle;
            isAttacking = false;
            ClearDashJumpState();
            suspensionActive = false;
            suspensionChangedBodyType = false;
            suspendedColliders.Clear();
            suspendedColliderStates.Clear();
            HideTongue();
            DespawnAllSpores();

            base.Die();
        }

        #region Player & Ground

        private void UpdatePlayerReference()
        {
            if (playerTransform != null) return;

            playerSearchTimer -= Time.fixedDeltaTime;
            if (playerSearchTimer > 0f) return;

            playerSearchTimer = 1f;

            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
        }

        private void UpdateGrounded()
        {
            if (recoveryCooldownTimer > 0f)
            {
                recoveryCooldownTimer -= Time.fixedDeltaTime;
            }

            if (jumpGroundGraceTimer > 0f)
            {
                jumpGroundGraceTimer -= Time.fixedDeltaTime;
                isGrounded = false;
                return;
            }

            isGrounded = CheckGrounded();

            if (isGrounded && rb != null && rb.linearVelocity.y <= 0.1f)
            {
                lastGroundedPosition = transform.position;
                airborneTimer = 0f;
            }
        }

        private bool CheckGrounded()
        {
            Vector2 checkPosition = groundCheckPoint != null
                ? (Vector2)groundCheckPoint.position
                : (Vector2)transform.position + Vector2.down * 0.6f;

            groundOverlapResults.Clear();

            return Physics2D.OverlapCircle(
                checkPosition,
                GroundCheckRadius,
                groundFilter,
                groundOverlapResults) > 0;
        }

        #endregion

        #region Action Selection

        private void TickIdle()
        {
            if (!isGrounded)
            {
                state = State.Jumping;
                return;
            }

            if (playerTransform == null) return;

            float distance = Vector2.Distance(transform.position, playerTransform.position);
            if (distance > DetectionRange) return;

            decisionTimer -= Time.fixedDeltaTime;
            if (decisionTimer > 0f) return;

            ActionType action = ChooseAction(distance);
            ExecuteAction(action);
        }

        private void TickJumping()
        {
            airborneTimer += Time.fixedDeltaTime;

            if (airborneTimer >= Mathf.Max(0.5f, MaxAirborneDuration) &&
                recoveryCooldownTimer <= 0f)
            {
                RecoverFromFailedJump();
                return;
            }

            if (!isGrounded || rb.linearVelocity.y > 0.1f) return;

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            state = State.Idle;
            decisionTimer = LandingRecovery;
        }

        private ActionType ChooseAction(float distance)
        {
            bool canTongue = tongueCooldownTimer <= 0f && distance <= TongueRange;
            bool canSpore = sporeCooldownTimer <= 0f &&
                            distance <= SporeRange &&
                            CountActiveSpores() < MaxActiveSpores;
            // The dash is horizontal, so eligibility uses the horizontal gap:
            // a player directly above does not trigger it.
            float horizontalDistance = playerTransform != null
                ? Mathf.Abs(playerTransform.position.x - transform.position.x)
                : 0f;
            bool canDashJump = dashJumpCooldownTimer <= 0f &&
                               isGrounded &&
                               horizontalDistance >= DashJumpMinDistance;

            float towardWeight = JumpTowardWeight;
            if (distance > PreferredDistance)
            {
                towardWeight *= 1.8f;
            }
            else if (distance < RetreatDistance)
            {
                towardWeight *= 0.4f;
            }

            float awayWeight = JumpAwayWeight;
            if (distance < RetreatDistance)
            {
                awayWeight *= 2.2f;
            }
            else if (distance > PreferredDistance)
            {
                awayWeight *= 0.35f;
            }

            float dashActionWeight = canDashJump ? DashJumpWeight : 0f;
            float tongueActionWeight = canTongue ? TongueWeight : 0f;
            float sporeActionWeight = canSpore ? SporeWeight : 0f;

            towardWeight = ApplyHistory(ActionType.JumpToward, towardWeight);
            awayWeight = ApplyHistory(ActionType.JumpAway, awayWeight);
            dashActionWeight = ApplyHistory(ActionType.DashJump, dashActionWeight);
            tongueActionWeight = ApplyHistory(ActionType.Tongue, tongueActionWeight);
            sporeActionWeight = ApplyHistory(ActionType.Spores, sporeActionWeight);

            float total = towardWeight + awayWeight + dashActionWeight +
                          tongueActionWeight + sporeActionWeight;
            if (total <= 0f)
            {
                return ActionType.JumpToward;
            }

            float roll = Random.value * total;

            if ((roll -= towardWeight) < 0f) return ActionType.JumpToward;
            if ((roll -= awayWeight) < 0f) return ActionType.JumpAway;
            if ((roll -= dashActionWeight) < 0f) return ActionType.DashJump;
            if ((roll -= tongueActionWeight) < 0f) return ActionType.Tongue;
            return ActionType.Spores;
        }

        private float ApplyHistory(ActionType action, float weight)
        {
            if (weight <= 0f) return 0f;

            if (action == lastAction)
            {
                weight *= RepeatWeightMultiplier;
            }

            if (lastActionWasAttack && (action == ActionType.Tongue || action == ActionType.Spores))
            {
                weight *= AttackAfterAttackWeightMultiplier;
            }

            return Mathf.Max(0f, weight);
        }

        private void ExecuteAction(ActionType action)
        {
            switch (action)
            {
                case ActionType.JumpToward:
                    StartJump(true);
                    RecordAction(ActionType.JumpToward, false);
                    break;

                case ActionType.JumpAway:
                    StartJump(false);
                    RecordAction(ActionType.JumpAway, false);
                    break;

                case ActionType.DashJump:
                    StartDashJump();
                    RecordAction(ActionType.DashJump, false);
                    break;

                case ActionType.Tongue:
                    StartTongueAttack();
                    break;

                case ActionType.Spores:
                    StartSporeAttack();
                    break;
            }
        }

        private void RecordAction(ActionType action, bool wasAttack)
        {
            lastAction = action;
            lastActionWasAttack = wasAttack;
        }

        #endregion

        #region Jump Movement

        private void StartJump(bool towardPlayer)
        {
            float direction = playerTransform != null
                ? playerTransform.position.x - transform.position.x
                : (facingRight ? 1f : -1f);

            float sign = Mathf.Abs(direction) > 0.1f
                ? Mathf.Sign(direction)
                : (facingRight ? 1f : -1f);

            if (!towardPlayer)
            {
                sign = -sign;
            }
            animator.SetBool("hasJumped", true);

            float airTime = GetJumpAirTime();

            float horizontalSpeed = Mathf.Min(
                JumpMaxHorizontalSpeed,
                JumpDistance / airTime);

            rb.linearVelocity = new Vector2(sign * horizontalSpeed, JumpUpVelocity);

            isGrounded = false;
            airborneTimer = 0f;
            jumpGroundGraceTimer = 0.12f;
            state = State.Jumping;

            RotateTowardsPlayer(sign > 0f);
        }

        private float GetJumpAirTime()
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y);
            if (gravity <= 0.001f)
            {
                return 1f;
            }

            float verticalDelta = playerTransform != null
                ? playerTransform.position.y - transform.position.y
                : 0f;

            // The boss cannot land above its apex, so clamp unreachable targets
            // to just below the maximum jump height.
            float maxRise = (JumpUpVelocity * JumpUpVelocity) / (2f * gravity);
            verticalDelta = Mathf.Min(verticalDelta, maxRise * 0.95f);

            // Solve y(t) = up * t - 0.5 * g * t^2 for the descending landing time.
            float discriminant = Mathf.Max(
                0f,
                JumpUpVelocity * JumpUpVelocity - 2f * gravity * verticalDelta);

            float airTime = (JumpUpVelocity + Mathf.Sqrt(discriminant)) / gravity;
            return Mathf.Max(0.2f, airTime);
        }

        private void RecoverFromFailedJump()
        {
            Vector2 targetPosition = lastGroundedPosition;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = targetPosition;
            }

            transform.position = targetPosition;

            isGrounded = CheckGrounded();
            if (isGrounded)
            {
                lastGroundedPosition = transform.position;
                animator.SetBool("hasJumped", false);
            }

            airborneTimer = 0f;
            recoveryCooldownTimer = Mathf.Max(0f, RecoveryCooldown);
            jumpGroundGraceTimer = 0f;

            tongueHitApplied = true;
            isAttacking = false;
            HideTongue();

            state = State.Recover;
            stateTimer = Mathf.Max(0.1f, LandingRecovery);
            decisionTimer = Mathf.Max(0f, DecisionInterval);
        }

        #endregion

        #region Dash Jump

        private void StartDashJump()
        {
            if (rb == null || playerTransform == null)
            {
                StartDashRecovery();
                return;
            }

            // Hold position during the readable preparation phase.
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            HideTongue();

            float horizontalDelta = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(horizontalDelta) > 0.05f)
            {
                RotateTowardsPlayer(horizontalDelta > 0f);
            }

            dashJumpCooldownTimer = Mathf.Max(0f, DashJumpCooldown);
            isAttacking = true;
            state = State.DashJumpWindup;
            stateTimer = Mathf.Max(0.05f, DashJumpPreparationTime);
        }

        private void LaunchDashJump()
        {
            if (rb == null || playerTransform == null || !isGrounded)
            {
                StartDashRecovery();
                return;
            }

            Vector2 start = rb.position;

            // Sample the player exactly once for the destination.
            // The destination is never recalculated during flight.
            Vector2 sampledPlayerPosition = playerTransform.position;

            float horizontalDelta = sampledPlayerPosition.x - start.x;
            if (Mathf.Abs(horizontalDelta) <= 0.5f)
            {
                // A purely vertical target is not a horizontal dash.
                StartDashRecovery();
                return;
            }

            float direction = Mathf.Sign(horizontalDelta);

            float requestedTravel = Mathf.Abs(
                sampledPlayerPosition.x +
                direction * Mathf.Max(0f, DashJumpLandingOffset) - start.x);

            if (requestedTravel <= 0.1f)
            {
                StartDashRecovery();
                return;
            }

            if (!TryClampDashPath(start, direction, requestedTravel, out float travelDistance) ||
                travelDistance <= 0.5f)
            {
                StartDashRecovery();
                return;
            }

            float destinationX = start.x + direction * travelDistance;

            if (!TryGetDashLandingY(destinationX, sampledPlayerPosition.y, out float landingY))
            {
                StartDashRecovery();
                return;
            }

            float gravity = GetEffectiveGravity();
            if (gravity <= 0.01f)
            {
                StartDashRecovery();
                return;
            }

            // Choose a low arc that reaches the destination ground at the
            // configured horizontal cruise speed. The configured up velocity
            // is a minimum so short dashes still read as jumps.
            float targetTime = travelDistance / Mathf.Max(0.01f, DashJumpHorizontalSpeed);
            float verticalDelta = landingY - start.y;
            float requiredUp = (verticalDelta + 0.5f * gravity * targetTime * targetTime) /
                               targetTime;
            float launchUp = Mathf.Max(DashJumpUpVelocity, requiredUp);

            float maxLaunchUp = Mathf.Max(DashJumpUpVelocity, JumpUpVelocity);
            if (launchUp > maxLaunchUp)
            {
                // The destination is too high for a low dash. Stay grounded
                // rather than turning this into a normal jump.
                StartDashRecovery();
                return;
            }

            float apexRise = (launchUp * launchUp) / (2f * gravity);
            if (!HasDashCeilingClearance(start, apexRise))
            {
                StartDashRecovery();
                return;
            }

            float discriminant = launchUp * launchUp - 2f * gravity * verticalDelta;
            if (discriminant < 0f)
            {
                StartDashRecovery();
                return;
            }

            float flightTime = (launchUp + Mathf.Sqrt(discriminant)) / gravity;
            if (flightTime <= 0.05f)
            {
                StartDashRecovery();
                return;
            }

            float horizontalSpeed = Mathf.Min(
                Mathf.Max(0.01f, DashJumpHorizontalSpeed),
                travelDistance / flightTime);

            dashDirection = direction;
            dashDestinationX = destinationX;

            isGrounded = false;
            airborneTimer = 0f;
            jumpGroundGraceTimer = 0.12f;

            rb.linearVelocity = new Vector2(direction * horizontalSpeed, launchUp);

            isAttacking = true;
            state = State.DashJumping;

            RotateTowardsPlayer(direction > 0f);
        }

        private void TickDashJumping()
        {
            if (rb == null)
            {
                StartDashRecovery();
                return;
            }

            airborneTimer += Time.fixedDeltaTime;

            // Stop before the sampled destination. The step is checked before
            // the physics step, so the boss never crosses the destination.
            float remaining = (dashDestinationX - rb.position.x) * dashDirection;
            float horizontalStep = Mathf.Abs(rb.linearVelocity.x) * Time.fixedDeltaTime;

            if (remaining <= 0f || horizontalStep >= remaining)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }

            if (airborneTimer >= Mathf.Max(0.5f, DashJumpMaxAirborneDuration))
            {
                RecoverFromFailedDash();
                return;
            }

            if (!isGrounded || rb.linearVelocity.y > 0.1f) return;

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            FinishDashJump();
        }

        private bool TryClampDashPath(
            Vector2 start,
            float direction,
            float requestedDistance,
            out float travelDistance)
        {
            travelDistance = requestedDistance;
            if (requestedDistance <= 0f) return false;

            // A box narrower than the body catches walls and steps without
            // grazing the floor the boss is standing on.
            dashHits.Clear();
            int hitCount = Physics2D.BoxCast(
                start,
                new Vector2(1.2f, 0.8f),
                0f,
                new Vector2(direction, 0f),
                dashJumpObstructionFilter,
                dashHits,
                requestedDistance);

            float nearest = requestedDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = dashHits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;

                nearest = Mathf.Min(nearest, hit.distance);
            }

            travelDistance = Mathf.Clamp(nearest, 0f, requestedDistance);
            return travelDistance > 0f;
        }

        private bool TryGetDashLandingY(
            float destinationX,
            float sampledPlayerY,
            out float landingY)
        {
            landingY = 0f;

            Vector2 origin = new Vector2(destinationX, sampledPlayerY + 0.1f);
            dashHits.Clear();

            int hitCount = Physics2D.Raycast(
                origin,
                Vector2.down,
                groundFilter,
                dashHits,
                30f);

            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = dashHits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;

                if (hit.distance >= nearestDistance) continue;

                nearestDistance = hit.distance;
                landingY = hit.point.y;
            }

            if (nearestDistance == float.MaxValue)
            {
                return false;
            }

            landingY += GetGroundClearance();
            return true;
        }

        private bool HasDashCeilingClearance(Vector2 start, float apexRise)
        {
            if (apexRise <= 0.01f) return true;

            dashHits.Clear();
            int hitCount = Physics2D.CircleCast(
                start,
                DashCeilingCastRadius,
                Vector2.up,
                dashJumpObstructionFilter,
                dashHits,
                apexRise + 0.1f);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = dashHits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;

                return false;
            }

            return true;
        }

        private void FinishDashJump()
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }

            isAttacking = false;
            HideTongue();
            airborneTimer = 0f;
            jumpGroundGraceTimer = 0f;

            state = State.Recover;
            stateTimer = Mathf.Max(0.1f, DashJumpRecoveryTime);
        }

        private void StartDashRecovery()
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }

            isAttacking = false;
            HideTongue();
            airborneTimer = 0f;
            jumpGroundGraceTimer = 0f;

            state = State.Recover;
            stateTimer = Mathf.Max(0.1f, DashJumpRecoveryTime);
        }

        private void RecoverFromFailedDash()
        {
            RecoverFromFailedJump();
            stateTimer = Mathf.Max(0.1f, DashJumpRecoveryTime);
        }

        private void ClearDashJumpState()
        {
            dashDirection = 0f;
            dashDestinationX = 0f;
        }

        private float GetEffectiveGravity()
        {
            float gravityScale = rb != null ? Mathf.Max(0.01f, rb.gravityScale) : 1f;
            return Mathf.Abs(Physics2D.gravity.y) * gravityScale;
        }

        private float GetGroundClearance()
        {
            if (groundCheckPoint != null)
            {
                return Mathf.Max(0.05f, Mathf.Abs(groundCheckPoint.localPosition.y));
            }

            return 0.6f;
        }

        #endregion

        #region Tongue Attack

        private void StartTongueAttack()
        {
            Vector2 origin = GetTongueOrigin();

            // Sample the target once: the tongue does not track or grab.
            Vector2 targetPosition = playerTransform != null
                ? (Vector2)playerTransform.position
                : origin + new Vector2(facingRight ? 1f : -1f, 0f);

            Vector2 delta = targetPosition - origin;
            Vector2 direction = delta.sqrMagnitude > 0.0001f
                ? delta.normalized
                : new Vector2(facingRight ? 1f : -1f, 0f);

            float reach = Mathf.Min(delta.magnitude, TongueRange);
            reach = ClampTongueReach(origin, direction, reach);

            tongueStart = origin;
            tongueTarget = origin + direction * reach;
            tongueHitApplied = false;

            state = State.TongueWindup;
            stateTimer = TongueWindupTime;
            isAttacking = true;

            RotateTowardsPlayer(direction.x >= 0f);
            TrySetTrigger("TongueWindup");
        }

        private void TickTongueExtend()
        {
            stateTimer -= Time.fixedDeltaTime;

            float progress = 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.0001f, TongueExtendTime));
            Vector2 tip = Vector2.Lerp(tongueStart, tongueTarget, progress);

            CheckTongueHit(tip);

            if (stateTimer <= 0f)
            {
                state = State.TongueRetract;
                stateTimer = TongueRetractTime;
            }
        }

        private float ClampTongueReach(Vector2 origin, Vector2 direction, float reach)
        {
            if (reach <= 0f) return 0f;

            float radius = Mathf.Max(0.05f, TongueWidth * 0.5f);
            obstructionHits.Clear();

            int hitCount = Physics2D.CircleCast(
                origin,
                radius,
                direction,
                tongueObstructionFilter,
                obstructionHits,
                reach);

            float safeReach = reach;
            for (int i = 0; i < hitCount; i++)
            {
                safeReach = Mathf.Min(safeReach, obstructionHits[i].distance);
            }

            return Mathf.Clamp(safeReach, 0f, reach);
        }

        private void CheckTongueHit(Vector2 tip)
        {
            if (tongueHitApplied) return;

            float radius = Mathf.Max(0.05f, TongueWidth * 0.5f);
            tongueOverlapResults.Clear();

            Vector2 delta = tip - tongueStart;
            float length = delta.magnitude;

            if (length < 0.01f)
            {
                Physics2D.OverlapCircle(
                    tongueStart,
                    radius,
                    playerFilter,
                    tongueOverlapResults);
            }
            else
            {
                // Unity 6.4 takes the capsule center/size/angle, not two endpoints.
                Vector2 center = (tongueStart + tip) * 0.5f;
                float diameter = radius * 2f;
                Vector2 size = new Vector2(length + diameter, diameter);
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                Physics2D.OverlapCapsule(
                    center,
                    size,
                    CapsuleDirection2D.Horizontal,
                    angle,
                    playerFilter,
                    tongueOverlapResults);
            }

            for (int i = 0; i < tongueOverlapResults.Count; i++)
            {
                Player player = GetPlayerFromCollider(tongueOverlapResults[i]);
                if (player == null) continue;

                IDamageable damageable = player.Health;
                if (damageable == null) continue;

                Vector2 hitPoint = tongueOverlapResults[i].ClosestPoint(
                    Vector2.Lerp(tongueStart, tip, 0.5f));

                if (damageable.TakeDamage(TongueDamage, hitPoint, tongueStart))
                {
                    tongueHitApplied = true;
                    break;
                }
            }
        }

        private Vector2 GetTongueOrigin()
        {
            if (tongueOrigin != null)
            {
                return tongueOrigin.position;
            }

            return (Vector2)transform.position + Vector2.up * 0.25f;
        }

        private void CreateTongueVisual()
        {
            GameObject lineObject = new GameObject("Tongue Visual");
            lineObject.transform.SetParent(transform, false);

            tongueLine = lineObject.AddComponent<LineRenderer>();
            tongueLine.useWorldSpace = true;
            tongueLine.positionCount = 2;
            tongueLine.startWidth = Mathf.Max(0.02f, TongueWidth);
            tongueLine.endWidth = Mathf.Max(0.01f, TongueWidth * 0.65f);
            tongueLine.numCapVertices = 4;
            tongueLine.numCornerVertices = 2;
            tongueLine.textureMode = LineTextureMode.Stretch;
            tongueLine.sortingOrder = TongueSortingOrder;
            tongueLine.enabled = false;

            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader != null)
            {
                tongueMaterial = new Material(lineShader);
                tongueLine.material = tongueMaterial;
            }
        }

        private void UpdateTongueVisual()
        {
            if (tongueLine == null) return;

            if (state != State.TongueWindup &&
                state != State.TongueExtend &&
                state != State.TongueRetract)
            {
                if (tongueLine.enabled)
                {
                    tongueLine.enabled = false;
                }
                return;
            }

            Vector2 tip;
            float alpha;

            switch (state)
            {
                case State.TongueWindup:
                {
                    float progress = 1f - Mathf.Clamp01(
                        stateTimer / Mathf.Max(0.0001f, TongueWindupTime));
                    tip = tongueTarget;
                    alpha = Mathf.Lerp(0.18f, 0.4f, progress);
                    break;
                }

                case State.TongueExtend:
                {
                    float progress = 1f - Mathf.Clamp01(
                        stateTimer / Mathf.Max(0.0001f, TongueExtendTime));
                    tip = Vector2.Lerp(tongueStart, tongueTarget, progress);
                    alpha = 1f;
                    break;
                }

                default:
                {
                    float progress = 1f - Mathf.Clamp01(
                        stateTimer / Mathf.Max(0.0001f, TongueRetractTime));
                    tip = Vector2.Lerp(tongueTarget, tongueStart, progress);
                    alpha = Mathf.Lerp(1f, 0f, progress);
                    break;
                }
            }

            tongueLine.enabled = true;
            tongueLine.SetPosition(0, tongueStart);
            tongueLine.SetPosition(1, tip);

            Color color = TongueColor;
            color.a *= alpha;
            tongueLine.startColor = color;
            tongueLine.endColor = color;
        }

        private void HideTongue()
        {
            if (tongueLine != null)
            {
                tongueLine.enabled = false;
            }
        }

        #endregion

        #region Spore Attack

        private void StartSporeAttack()
        {
            // Exactly one cloud, always at the boss's world-space center.
            // Spawning does not depend on the player position or facing.
            Vector2 position = transform.position;

            Vector2 drift = new Vector2(
                Random.Range(-Mathf.Abs(SporeDrift.x), Mathf.Abs(SporeDrift.x)),
                SporeDrift.y);

            SporeCloud cloud = SpawnSpore(position, drift);
            if (cloud != null)
            {
                activeSpores.Add(cloud);
            }

            state = State.Spores;
            stateTimer = SporeWindupTime;
            isAttacking = true;

            TrySetTrigger("SporeAttack");
        }

        private SporeCloud SpawnSpore(Vector2 position, Vector2 drift)
        {
            GameObject instance;

            if (sporePrefab != null)
            {
                instance = Instantiate(sporePrefab, position, Quaternion.identity);
            }
            else
            {
                instance = new GameObject("Spore Cloud");
                instance.transform.position = position;
            }

            SporeCloud cloud = instance.GetComponent<SporeCloud>();
            if (cloud == null)
            {
                cloud = instance.AddComponent<SporeCloud>();
            }

            cloud.Initialize(
                SporeDamage,
                SporeLifetime,
                SporeRadius,
                drift,
                SporeWindupTime,
                SporeObstructionLayers);

            return cloud;
        }

        private int CountActiveSpores()
        {
            for (int i = activeSpores.Count - 1; i >= 0; i--)
            {
                if (activeSpores[i] == null)
                {
                    activeSpores.RemoveAt(i);
                }
            }

            return activeSpores.Count;
        }

        private void DespawnAllSpores()
        {
            for (int i = activeSpores.Count - 1; i >= 0; i--)
            {
                if (activeSpores[i] != null)
                {
                    activeSpores[i].Despawn();
                }
            }

            activeSpores.Clear();
        }

        #endregion

        #region Helpers

        private static ContactFilter2D CreateLayerFilter(int layerMask)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.SetLayerMask(layerMask);
            return filter;
        }

        private void CacheAndDisableBodyColliders()
        {
            suspendedColliders.Clear();
            suspendedColliderStates.Clear();

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || collider.isTrigger) continue;

                suspendedColliders.Add(collider);
                suspendedColliderStates.Add(collider.enabled);
                collider.enabled = false;
            }
        }

        private void RestoreSuspendedColliders()
        {
            for (int i = 0; i < suspendedColliders.Count; i++)
            {
                Collider2D collider = suspendedColliders[i];
                if (collider == null) continue;

                collider.enabled =
                    i < suspendedColliderStates.Count && suspendedColliderStates[i];
            }

            suspendedColliders.Clear();
            suspendedColliderStates.Clear();
        }

        private void FinishAttack(ActionType action, float recoveryTime)
        {
            HideTongue();

            isAttacking = false;
            RecordAction(action, true);

            if (action == ActionType.Tongue)
            {
                tongueCooldownTimer = TongueCooldown;
            }
            else if (action == ActionType.Spores)
            {
                sporeCooldownTimer = SporeCooldown;
            }

            state = State.Recover;
            stateTimer = Mathf.Max(0f, recoveryTime);
        }

        private static Player GetPlayerFromCollider(Collider2D collider)
        {
            if (collider == null) return null;

            Player player = collider.GetComponentInParent<Player>();
            if (player == null || player.Collider == null) return null;

            // Ignore child colliders such as ground checks and interaction ranges.
            return collider == player.Collider ? player : null;
        }

        private void TrySetTrigger(string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName)) return;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                    parameters[i].name == triggerName)
                {
                    animator.SetTrigger(triggerName);
                    return;
                }
            }
        }

        #endregion
    }
}
