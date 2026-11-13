using UnityEngine;
using System.Collections;
using SpearWander.Boss;

public partial class Spear : MonoBehaviour
{
private void OnCollisionEnter2D(
    Collision2D collision)
{
    if (currentState != SpearState.Flying &&
        currentState != SpearState.Dropped)
    {
        return;
    }

    Boss boss =
        collision.collider.GetComponentInParent<Boss>();

    if (boss != null &&
        currentState == SpearState.Flying)
    {
        WeakPoint weakPoint =
            collision.collider.GetComponent<WeakPoint>();

        if (weakPoint != null)
            return;

        BounceOffBoss(collision);
        return;
    }

    if (currentState == SpearState.Dropped)
    {
        Vector2 hitPoint =
            collision.contacts[0].point;

        Vector2 normal =
            collision.contacts[0].normal;

        // The contact normal points out of the enemy toward the spear, so its
        // inverse is the penetration direction. The cached velocity is stale
        // in the Dropped state, so it must not be used here.
        Vector2 travelDirection =
            normal.sqrMagnitude > 0.0001f
                ? -normal.normalized
                : Vector2.zero;

        ProcessHit(
            collision.collider,
            hitPoint,
            normal,
            true,
            travelDirection
        );
    }
}

    public void OnTipHit(Collider2D other)
    {
        if (currentState == SpearState.Embedded)
            return;

        if (!IsTipTriggerCandidate(other, _playerCollider))
            return;

        Vector2 hitPoint =
            other.ClosestPoint(transform.position);

        Vector2 flyDir =
            _lastVelocity.sqrMagnitude > 0.01f
                ? _lastVelocity.normalized
                : (Vector2)transform.right;

        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                hitPoint - flyDir * 0.5f,
                flyDir,
                TipRaycastLength,
                1 << other.gameObject.layer
            );

        RaycastHit2D actualColliderHit = default;
        bool foundActualColliderHit = false;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != other)
                continue;

            if (!foundActualColliderHit ||
                hit.distance < actualColliderHit.distance)
            {
                actualColliderHit = hit;
                foundActualColliderHit = true;
            }
        }

        Vector2 normal =
            foundActualColliderHit
                ? actualColliderHit.normal
                : Vector2.up;

        ProcessHit(
            other,
            hitPoint,
            normal,
            foundActualColliderHit,
            flyDir
        );
    }

    /// <summary>
    /// Applies the same candidate exclusions used by the live tip trigger.
    /// Exposed for the trajectory predictor; this method has no side effects.
    /// </summary>
    public bool IsTipTriggerCandidate(
        Collider2D candidate,
        Collider2D playerCollider)
    {
        int spearLayer =
            _spearLayer == 0 ? gameObject.layer : _spearLayer;

        if (candidate == null ||
            candidate.gameObject.layer == spearLayer ||
            candidate.transform.IsChildOf(transform))
        {
            return false;
        }

        if (candidate.CompareTag(cameraBoundsTag) ||
            candidate.CompareTag(directionChangerTag))
        {
            return false;
        }

        return playerCollider == null ||
               !candidate.transform.IsChildOf(playerCollider.transform);
    }

    private void BounceOffBoss(
        Collision2D collision)
    {
        Vector2 normal =
            collision.contacts[0].normal;

        Vector2 velocity =
            _rb.linearVelocity;

        float dot =
            Vector2.Dot(
                velocity,
                normal
            );

        Vector2 bounceDirection =
            velocity -
            2 * dot * normal;

        Vector2 bounceVelocity =
            bounceDirection *
            BossBounceSpeedMultiplier;

        _rb.linearVelocity =
            bounceVelocity;

        if (bounceVelocity.sqrMagnitude > 0.01f)
        {
            transform.right =
                bounceVelocity.normalized;
        }

        _rb.bodyType =
            RigidbodyType2D.Dynamic;

        _rb.gravityScale =
            BossBounceGravityScale;

        _collider.isTrigger =
            false;

        currentState =
            SpearState.Dropped;

        if (collision.collider != null &&
            _collider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                collision.collider,
                true
            );

            StartCoroutine(
                ReenableCollisionWithBoss(
                    collision.collider,
                    BossCollisionIgnoreDuration
                )
            );
        }
    }

    private IEnumerator ReenableCollisionWithBoss(
        Collider2D bossCollider,
        float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bossCollider != null &&
            _collider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                bossCollider,
                false
            );
        }
    }
}
