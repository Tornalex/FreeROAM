using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using SpearWander.Abilities;
using SpearWander.Boss;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    private int _currentHealth;
    private float _iFramesTimer;

    private readonly List<Collider2D> _playerBodyColliders =
        new List<Collider2D>();
    private readonly List<Collider2D> _overlapBuffer =
        new List<Collider2D>(8);
    private readonly HashSet<Collider2D> _currentEnemyBodyOverlaps =
        new HashSet<Collider2D>(ColliderReferenceComparer.Instance);
    private readonly HashSet<Collider2D> _bypassedEnemyBodyColliders =
        new HashSet<Collider2D>(ColliderReferenceComparer.Instance);
    private readonly Dictionary<CollisionPair, bool> _bypassedEnemyBodyCollisions =
        new Dictionary<CollisionPair, bool>();
    private readonly List<CollisionPair> _staleBypassedCollisions =
        new List<CollisionPair>();

    private ContactFilter2D _enemyBodyOverlapFilter;

    private Player _player;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _player.PlayerStats.maxHealth;
    public bool IsInvulnerable => _iFramesTimer > 0;


    void Awake()
    {
        _player = GetComponent<Player>();
        _currentHealth = _player.PlayerStats.maxHealth;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
                _playerBodyColliders.Add(colliders[i]);
        }

        _enemyBodyOverlapFilter = new ContactFilter2D();
        _enemyBodyOverlapFilter.SetLayerMask(Physics2D.AllLayers);
        _enemyBodyOverlapFilter.useTriggers = false;
    }


    void FixedUpdate()
    {
        if (_iFramesTimer > 0)
        {
            _iFramesTimer -= Time.fixedDeltaTime;

            float alpha =
                (Mathf.Floor(_iFramesTimer / _player.PlayerStats.iFramesBlinkInterval) % 2 == 0)
                    ? _player.PlayerStats.iFramesBlinkMinAlpha
                    : 1f;

            _player.Sprite.color =
                new Color(1f, 1f, 1f, alpha);
        }
        else
        {
            if (_player.Sprite.color.a < 1f)
                _player.Sprite.color = Color.white;
        }

        Collider2D overlappingEnemyBody =
            RefreshEnemyBodyCollisionBypass();

        if (_iFramesTimer <= 0 &&
            overlappingEnemyBody != null &&
            CanTakeContactDamage())
        {
            TakeDamage(
                _player.PlayerStats.contactDamage,
                overlappingEnemyBody.bounds.center);
        }
    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsEnemyBodyCollider(collision.collider))
            return;

        // Collision callbacks preserve the existing immediate contact-damage
        // timing. The overlap query also finds bodies contacted while pairs
        // have already been ignored.
        RefreshEnemyBodyCollisionBypass();

        if (_iFramesTimer <= 0 && CanTakeContactDamage())
        {
            TakeDamage(
                _player.PlayerStats.contactDamage,
                collision.collider.bounds.center);
        }
    }


    private bool CanTakeContactDamage()
    {
        return !_player.Dash.IsDashing &&
               !_player.Dash.HasPostDashProtection &&
               !_player.Pogo.IsPlunging &&
               !_player.Pogo.HasPostPogoProtection;
    }


    /// <summary>
    /// Ignores only overlapping, non-trigger body colliders belonging to an
    /// enemy or boss. Kept pairs remain ignored until the overlap ends, even
    /// after the independent i-frame timer expires.
    /// </summary>
    private Collider2D RefreshEnemyBodyCollisionBypass()
    {
        _currentEnemyBodyOverlaps.Clear();
        Collider2D firstOverlappingBody = null;

        for (int i = 0; i < _playerBodyColliders.Count; i++)
        {
            Collider2D playerBody = _playerBodyColliders[i];
            if (!IsColliderAvailable(playerBody))
                continue;

            _overlapBuffer.Clear();
            playerBody.Overlap(
                _enemyBodyOverlapFilter,
                _overlapBuffer);

            for (int j = 0; j < _overlapBuffer.Count; j++)
            {
                Collider2D enemyBody = _overlapBuffer[j];
                if (!IsEnemyBodyCollider(enemyBody))
                    continue;

                _currentEnemyBodyOverlaps.Add(enemyBody);

                if (firstOverlappingBody == null)
                    firstOverlappingBody = enemyBody;

                if (_iFramesTimer > 0 ||
                    _bypassedEnemyBodyColliders.Contains(enemyBody))
                {
                    _bypassedEnemyBodyColliders.Add(enemyBody);

                    // Keep every Player body pair ignored until all of its
                    // physical colliders have left this enemy body.
                    for (int k = 0; k < _playerBodyColliders.Count; k++)
                    {
                        Collider2D playerCollider = _playerBodyColliders[k];
                        if (IsColliderAvailable(playerCollider))
                        {
                            IgnoreEnemyBodyCollision(
                                new CollisionPair(playerCollider, enemyBody));
                        }
                    }
                }
            }
        }

        _staleBypassedCollisions.Clear();
        foreach (KeyValuePair<CollisionPair, bool> bypassedPair in
                 _bypassedEnemyBodyCollisions)
        {
            if (!_currentEnemyBodyOverlaps.Contains(
                    bypassedPair.Key.EnemyBody))
                _staleBypassedCollisions.Add(bypassedPair.Key);
        }

        for (int i = 0; i < _staleBypassedCollisions.Count; i++)
            RestoreEnemyBodyCollision(_staleBypassedCollisions[i]);

        _bypassedEnemyBodyColliders.RemoveWhere(
            enemyBody => !_currentEnemyBodyOverlaps.Contains(enemyBody));

        return firstOverlappingBody;
    }


    private void IgnoreEnemyBodyCollision(CollisionPair pair)
    {
        _bypassedEnemyBodyColliders.Add(pair.EnemyBody);

        if (!_bypassedEnemyBodyCollisions.ContainsKey(pair))
        {
            bool wasIgnored =
                Physics2D.GetIgnoreCollision(
                    pair.PlayerBody,
                    pair.EnemyBody);

            _bypassedEnemyBodyCollisions.Add(pair, wasIgnored);
        }

        if (IsColliderAvailable(pair.PlayerBody) &&
            IsColliderAvailable(pair.EnemyBody))
        {
            Physics2D.IgnoreCollision(
                pair.PlayerBody,
                pair.EnemyBody,
                true);
        }
    }


    private void RestoreEnemyBodyCollision(CollisionPair pair)
    {
        if (!_bypassedEnemyBodyCollisions.TryGetValue(
                pair,
                out bool wasIgnored))
        {
            return;
        }

        if (IsColliderAvailable(pair.PlayerBody) &&
            IsColliderAvailable(pair.EnemyBody))
        {
            Physics2D.IgnoreCollision(
                pair.PlayerBody,
                pair.EnemyBody,
                wasIgnored);
        }

        _bypassedEnemyBodyCollisions.Remove(pair);
    }


    private static bool IsColliderAvailable(Collider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy;
    }


    private static bool IsEnemyBodyCollider(Collider2D collider)
    {
        if (!IsColliderAvailable(collider) || collider.isTrigger)
            return false;

        BaseEnemy enemy = collider.GetComponentInParent<BaseEnemy>();
        if (enemy != null)
            return !enemy.isDead;

        Boss boss = collider.GetComponentInParent<Boss>();
        return boss != null && !boss.IsDead;
    }


    private void ClearEnemyBodyCollisionBypass()
    {
        _staleBypassedCollisions.Clear();
        foreach (CollisionPair pair in _bypassedEnemyBodyCollisions.Keys)
            _staleBypassedCollisions.Add(pair);

        for (int i = 0; i < _staleBypassedCollisions.Count; i++)
            RestoreEnemyBodyCollision(_staleBypassedCollisions[i]);

        _bypassedEnemyBodyColliders.Clear();
    }


    private void OnDisable()
    {
        ClearEnemyBodyCollisionBypass();
    }


    private readonly struct CollisionPair : System.IEquatable<CollisionPair>
    {
        public readonly Collider2D PlayerBody;
        public readonly Collider2D EnemyBody;

        public CollisionPair(Collider2D playerBody, Collider2D enemyBody)
        {
            PlayerBody = playerBody;
            EnemyBody = enemyBody;
        }

        public bool Equals(CollisionPair other)
        {
            return ReferenceEquals(PlayerBody, other.PlayerBody) &&
                   ReferenceEquals(EnemyBody, other.EnemyBody);
        }

        public override bool Equals(object obj)
        {
            return obj is CollisionPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(PlayerBody) * 397) ^
                       RuntimeHelpers.GetHashCode(EnemyBody);
            }
        }
    }


    private sealed class ColliderReferenceComparer :
        IEqualityComparer<Collider2D>
    {
        public static readonly ColliderReferenceComparer Instance =
            new ColliderReferenceComparer();

        public bool Equals(Collider2D first, Collider2D second)
        {
            return ReferenceEquals(first, second);
        }

        public int GetHashCode(Collider2D collider)
        {
            return RuntimeHelpers.GetHashCode(collider);
        }
    }


    // ============================================================
    // IDamageable
    // ============================================================

    public bool TakeDamage(
        int amount,
        Vector2 hitPoint,
        Vector2 damageSourcePosition)
    {
        return TakeDamage(amount, damageSourcePosition);
    }


    // ============================================================
    // PLAYER DAMAGE
    // ============================================================

    public bool TakeDamage(int amount, Vector2 sourcePos)
    {
        if (_currentHealth <= 0)
            return false;

        if (_iFramesTimer > 0)
            return false;

        _currentHealth -= amount;

        _iFramesTimer =
            _player.PlayerStats.iFramesDuration;

        if (_currentHealth > 0)
            RefreshEnemyBodyCollisionBypass();

        if (_player.ImpulseSource != null)
        {
            _player.ImpulseSource.GenerateImpulse();
        }

        if (_player.Dash.IsDashing)
        {
            _player.Dash.StopDash();
        }

        _player.Knockback.ApplyKnockback(
            sourcePos,
            _player.PlayerStats.damageForce,
            _player.PlayerStats.damageKnockbackDuration
        );

        _player.Movement.StartPlayerKnockback(
            _player.PlayerStats.damageKnockbackDuration
        );

        VFXManager.Instance?.PlayVFX(
            VFXType.HitGeneric,
            transform.position,
            Vector2.zero
        );

        if (_currentHealth <= 0)
        {
            ClearEnemyBodyCollisionBypass();

            _player.AddControlRequest(
                Player.ControlReason.Death
            );
            _player.Combat.ResetState();

            _player.Collider.enabled = false;
            _player.Rb.bodyType = RigidbodyType2D.Kinematic;
            _player.Rb.linearVelocity = Vector2.zero;

            if (GameSceneManager.Instance != null)
            {
                string room =
                    GameSceneManager.Instance.CurrentRoom;

                Debug.Log(
                    $"[PlayerHealth.TakeDamage] Player died in room '{room}', calling ClearAll"
                );

                DeadEnemyTracker.ClearAll();
            }

            if (_player.GameOverUI != null)
            {
                _player.GameOverUI.Show();
            }
        }

        return amount > 0;
    }


    public void Heal(int amount)
    {
        _currentHealth =
            Mathf.Clamp(
                _currentHealth + amount,
                0,
                _player.PlayerStats.maxHealth
            );
    }


    public void ResetState()
    {
        _currentHealth =
            _player.PlayerStats.maxHealth;

        ClearEnemyBodyCollisionBypass();

        _iFramesTimer =
            _player.PlayerStats.iFramesDuration;

        _player.Sprite.color = Color.white;
        _player.Collider.enabled = true;
        RefreshEnemyBodyCollisionBypass();
    }
}
