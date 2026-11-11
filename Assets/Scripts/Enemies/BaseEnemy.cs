using UnityEngine;
using UnityEngine.SceneManagement;
using SpearWander.Abilities;

// Questa classe è "abstract" perché non la assegnerai mai direttamente a un nemico.
// È un "modello" da cui gli altri nemici prenderanno le funzioni.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public abstract class BaseEnemy : MonoBehaviour, IDamageable, IBounceable
{
    [SerializeField] protected EnemyData enemyData;
    protected int currentHealth;

    [Header("Persistent Death Tracking (Auto-Assigned)")]
    [SerializeField, HideInInspector] private string stableIdentity;
    [SerializeField] public string roomName;

    // Runtime-only room key used by DeadEnemyTracker. Resolved once in Start
    // and reused for both suppressions and death registration.
    private string roomTrackingKey;

    [HideInInspector] public bool isDead = false;

    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected HitFlash hitFlash;
    protected Knockback knockback;
    protected Animator animator;

    /// <summary>
    /// Resolves the current player reference once per archetype that needs it.
    /// Prefers the Player component and falls back to the player tag for scenes
    /// where the component hierarchy is not directly discoverable.
    /// </summary>
    protected static Player FindPlayerObject()
    {
        Player playerComponent = FindAnyObjectByType<Player>();
        if (playerComponent != null)
            return playerComponent;

        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        if (taggedPlayer == null)
            return null;

        return taggedPlayer.GetComponentInParent<Player>();
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();
        knockback = GetComponent<Knockback>();
        animator = GetComponent<Animator>();

        if (enemyData != null)
        {
            currentHealth = enemyData.maxHealth;
        }
        else
        {
            Debug.LogError($"[BaseEnemy] {name} has no EnemyData assigned.", this);
            currentHealth = 1;
        }
    }

    protected virtual void Start()
    {
        // Additive destination loading temporarily leaves the source room
        // loaded. Always describe this enemy through its own scene rather than
        // the manager's still-committed CurrentRoom during that overlap.
        Scene owningScene = gameObject.scene;
        bool hasRoomManager = GameSceneManager.Instance != null;
        string currentRoom = hasRoomManager ? GameSceneManager.Instance.CurrentRoom : null;

        // roomName stays the serialized scene-name compatibility field.
        roomName = EnemyRoomTrackingKey.ResolveLegacyKey(
            owningScene.name, currentRoom, hasRoomManager);

        // Resolve the tracking key once, before the dead check, so the same
        // key is used when this enemy is later registered as dead.
        roomTrackingKey = EnemyRoomTrackingKey.ResolveFromScene(
            owningScene.path, owningScene.name, currentRoom, hasRoomManager);

        if (IsTrackedAsDead())
            Destroy(gameObject);
    }

    /// <summary>
    /// Enemies without a stable identity cannot be tracked individually:
    /// treating an empty identity as a key would make every untracked enemy
    /// in the room disappear after a single kill.
    /// </summary>
    protected bool IsTrackedAsDead()
    {
        return !string.IsNullOrEmpty(stableIdentity) &&
               DeadEnemyTracker.IsDead(roomTrackingKey, stableIdentity);
    }

    protected void ReportDeath()
    {
        // Only persistent identities can be restored after a room reload.
        if (!string.IsNullOrEmpty(stableIdentity))
            DeadEnemyTracker.MarkDead(roomTrackingKey, stableIdentity);
    }

    public virtual bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        if (isDead) return false;

        currentHealth -= damage;

        if (enemyData != null)
        {
            knockback?.ApplyKnockback(damageSourcePosition, enemyData.knockbackForce, enemyData.knockbackDuration);
        }

        SFXManager.Instance?.PlaySFX(SFXType.EnemyPierced);
        VFXManager.Instance?.PlayVFX(VFXType.HitDash, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

        if (animator != null) animator.SetTrigger("TookHit");

        if (hitFlash != null) hitFlash.Flash();

        if (currentHealth <= 0) Die();

        return damage > 0;
    }

    public virtual float GetBounceMultiplier() => 1f;

    public virtual void OnPogoBounce()
    {
        if (hitFlash != null) hitFlash.Flash();
    }

    protected virtual void Die()
    {
        Spear[] attachedSpears = GetComponentsInChildren<Spear>();
        foreach (Spear spear in attachedSpears) spear.OnEnemyDeath();

        isDead = true;
        Debug.Log($"[BaseEnemy.Die] {name} - roomName='{roomName}', " +
                  $"roomTrackingKey='{roomTrackingKey}', stableIdentity='{stableIdentity}'");

        ReportDeath();

        Destroy(gameObject);
    }
}
