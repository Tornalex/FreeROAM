using UnityEngine;

/// <summary>
/// Shared authoring data for a boss.
///
/// Fundamental statistics (health, damage reaction, weak point and feedback)
/// are used by every boss. The movement and melee/ranged/anti-air blocks below
/// are the legacy configuration of the default <see cref="SpearWander.Boss.Boss"/>
/// controller: they are still serialized and still drive that controller, so
/// they are preserved until it is proven that no asset or prefab relies on
/// them. Free-roaming bosses read their movement and attacks from
/// <see cref="BossAIProfile"/> and <see cref="BossAttackData"/> instead.
///
/// Gameplay never writes to this asset: current health, timers, phase and
/// target stay on the runtime component.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/Data")]
public class BossData : ScriptableObject
{
    [Header("Health")]
    [SerializeField, Min(1)]
    [Tooltip("Health assigned to a new boss instance.")]
    public int maxHealth = 10;

    [Header("Damage Reaction")]
    [SerializeField]
    [Tooltip("Impulse applied to the boss when it is damaged.")]
    public Vector2 knockbackForce = new Vector2(15f, 10f);

    [SerializeField, Min(0f)]
    [Tooltip("Time the knockback impulse is applied.")]
    public float knockbackDuration = 0.3f;

    [Header("Weak Point")]
    [SerializeField, Min(1f)]
    [Tooltip("Damage multiplier applied when the weak point is hit.")]
    public float weakPointDamageMultiplier = 2f;

    [Header("Feedback")]
    [SerializeField] public VFXType hitVfxType = VFXType.HitGeneric;
    [SerializeField] public SFXType hitSfxType = SFXType.EnemyPierced;
    [SerializeField] public VFXType deathVfxType = VFXType.EnemyDeath;
    [SerializeField] public SFXType deathSfxType = SFXType.EnemyPierced;

    [Header("Legacy - Default Boss Controller")]
    [SerializeField]
    [Tooltip("Legacy: rotation speed of the default Boss controller. Unused by FreeRoamBoss.")]
    public float rotationSpeed = 360f; // degrees per second

    [SerializeField]
    [Tooltip("Legacy: horizontal speed of the default Boss controller. Unused by FreeRoamBoss.")]
    public float moveSpeed = 2f;

    [SerializeField]
    [Tooltip("Legacy: distance at which the default Boss controller starts following.")]
    public float chaseDistance = 10f;

    [Header("Legacy - Melee Attack")]
    [SerializeField] public float meleeAttackRange = 2f;
    [SerializeField] public float meleeAttackCooldown = 2f;
    [SerializeField, Min(0)] public int meleeDamage = 2;
    [SerializeField] public float meleeKnockbackForce = 15f;

    [Header("Legacy - Ranged Attack")]
    [SerializeField] public float rangedAttackRange = 8f;
    [SerializeField] public float rangedAttackCooldown = 3f;
    [SerializeField, Min(0)] public int rangedDamage = 1;
    [SerializeField] public GameObject projectilePrefab;
    [SerializeField] public float projectileSpeed = 10f;

    [Header("Legacy - Anti-Air Attack")]
    [SerializeField] public float antiAirRange = 4f;
    [SerializeField] public float antiAirCooldown = 2.5f;
    [SerializeField, Min(0)] public int antiAirDamage = 2;
    [SerializeField] public float antiAirVerticalForce = 20f;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        weakPointDamageMultiplier = Mathf.Max(1f, weakPointDamageMultiplier);

        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        chaseDistance = Mathf.Max(0f, chaseDistance);

        meleeAttackRange = Mathf.Max(0f, meleeAttackRange);
        meleeAttackCooldown = Mathf.Max(0f, meleeAttackCooldown);
        meleeDamage = Mathf.Max(0, meleeDamage);
        meleeKnockbackForce = Mathf.Max(0f, meleeKnockbackForce);

        rangedAttackRange = Mathf.Max(0f, rangedAttackRange);
        rangedAttackCooldown = Mathf.Max(0f, rangedAttackCooldown);
        rangedDamage = Mathf.Max(0, rangedDamage);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);

        antiAirRange = Mathf.Max(0f, antiAirRange);
        antiAirCooldown = Mathf.Max(0f, antiAirCooldown);
        antiAirDamage = Mathf.Max(0, antiAirDamage);
        antiAirVerticalForce = Mathf.Max(0f, antiAirVerticalForce);
    }
}
