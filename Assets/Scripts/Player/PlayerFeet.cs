using UnityEngine;
using System.Collections.Generic;

public class PlayerFeet : MonoBehaviour
{
    private struct GroundContact
    {
        public Collider2D Collider;
        public Vector2 LastPosition;
        public Vector2 Displacement;
    }

    private bool _isGrounded;
    private Player _player;
    private Dictionary<EntityId, GroundContact> _groundContacts =
        new Dictionary<EntityId, GroundContact>();

    public Vector2 GroundVelocity
    {
        get
        {
            if (!_isGrounded)
                return Vector2.zero;

            Vector2 groundVelocity = Vector2.zero;
            float fastestGroundSpeed = 0f;

            foreach (GroundContact contact in _groundContacts.Values)
            {
                if (contact.Collider == null)
                    continue;

                Vector2 velocity = Time.fixedDeltaTime > 0f
                    ? contact.Displacement / Time.fixedDeltaTime
                    : Vector2.zero;
                if (velocity.sqrMagnitude > fastestGroundSpeed)
                {
                    groundVelocity = velocity;
                    fastestGroundSpeed = velocity.sqrMagnitude;
                }
            }

            return groundVelocity;
        }
    }

    void Awake()
    {
        _player = GetComponentInParent<Player>();
    }

    public bool IsGrounded()
    {
        // Remove contacts whose collider was destroyed or disabled without
        // raising OnCollisionExit2D, otherwise the player can stay grounded
        // on a platform that no longer exists.
        if (_groundContacts.Count > 0)
        {
            List<EntityId> staleContacts = null;

            foreach (KeyValuePair<EntityId, GroundContact> pair in _groundContacts)
            {
                Collider2D contactCollider = pair.Value.Collider;

                if (contactCollider == null ||
                    !contactCollider.enabled ||
                    !contactCollider.gameObject.activeInHierarchy)
                {
                    staleContacts ??= new List<EntityId>();
                    staleContacts.Add(pair.Key);
                }
            }

            if (staleContacts != null)
            {
                foreach (EntityId staleId in staleContacts)
                    _groundContacts.Remove(staleId);

                _isGrounded = _groundContacts.Count > 0;
            }
        }

        return _isGrounded;
    }
    private bool TryPassiveBounce(Collision2D collision)
    {
        if (_player == null || _player.Rb == null || _player.PogoStats == null)
            return false;

        if (!collision.collider.TryGetComponent(out Bouncer bouncer) || !bouncer.BounceOnTouch)
            return false;

        // Se sta già salendo non ribalzare di nuovo.
        if (_player.Rb.linearVelocity.y > 0.1f)
            return false;

        bouncer.OnPogoBounce();

        _player.transform.position += Vector3.up * _player.PogoStats.bounceOffset;

        float force = _player.PogoStats.bounceForce * bouncer.GetBounceMultiplier();
        _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, force);

        if (_player.Dash != null) _player.Dash.ResetAirDash();

        SFXManager.Instance?.PlaySFX(SFXType.PogoHit);
        return true;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        bool isGround = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y > _player.PlayerStats.groundNormalThreshold &&
                contact.point.y <= transform.position.y + _player.PlayerStats.groundPointOffset)
            {
                isGround = true;
                break;
            }
        }
        EntityId groundId = collision.collider.GetEntityId();
        if (!isGround)
        {
            _groundContacts.Remove(groundId);
            _isGrounded = _groundContacts.Count > 0;
            return;
        }

        Collider2D groundCollider = collision.collider;
        // Use the collider transform rather than attachedRigidbody velocity:
        // a support collider can move through a parent Rigidbody2D.
        Vector2 currentPosition = groundCollider.transform.position;
        Vector2 displacement = Vector2.zero;

        if (_groundContacts.TryGetValue(groundId, out GroundContact previousContact) &&
            previousContact.Collider == groundCollider)
        {
            displacement = currentPosition - previousContact.LastPosition;
        }

        _groundContacts[groundId] = new GroundContact
        {
            Collider = groundCollider,
            LastPosition = currentPosition,
            Displacement = displacement
        };
        _isGrounded = true;

        bool pogoActive = _player != null && _player.Pogo != null
                          && _player.Pogo.enabled && _player.Pogo.IsPlunging;

        if (pogoActive)
        {
            _player.Pogo.OnPogoHit(collision);
        }
        else if (TryPassiveBounce(collision))
        {
            // Stacca subito il contatto, così il player non risulta "grounded"
            // sul Bouncer mentre sta salendo.
            _groundContacts.Remove(groundId);
            _isGrounded = _groundContacts.Count > 0;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        _groundContacts.Remove(collision.collider.GetEntityId());
        _isGrounded = _groundContacts.Count > 0;
    }
}
