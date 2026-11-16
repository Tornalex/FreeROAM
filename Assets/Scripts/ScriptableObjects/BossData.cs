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

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        weakPointDamageMultiplier = Mathf.Max(1f, weakPointDamageMultiplier);
    }
}
