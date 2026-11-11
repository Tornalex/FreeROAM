using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Min(0)]
    [Tooltip("Damage dealt to the player on contact.")]
    private int damage = 1;

    [SerializeField, Min(0f)]
    [Tooltip("Seconds before the projectile is destroyed automatically.")]
    private float lifetime = 5f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        lifetime = Mathf.Max(0f, lifetime);
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Launch(Vector2 direction, float speed)
    {
        if (rb == null)
            return;

        rb.linearVelocity = direction.normalized * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Player
        Player player = other.GetComponentInParent<Player>();

        if (player != null)
        {
            // Ignore child colliders such as Ground Check and Interact Range.
            if (other != player.Collider)
                return;

            IDamageable damageable = player.Health;

            if (damageable != null)
            {
                damageable.TakeDamage(
                    damage,
                    transform.position,
                    transform.position
                );
            }

            Destroy(gameObject);
            return;
        }

        // Ground / walls
        if (other.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}