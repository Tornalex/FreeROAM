using UnityEngine;

/// <summary>
/// Shared authoring statistics for every enemy archetype. The values are read
/// by the runtime components; gameplay never writes them back, so all enemies
/// using the same asset keep independent runtime state.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Enemy/Data")]
public class EnemyData : ScriptableObject
{
    [Header("Health")]
    [SerializeField, Min(1)]
    [Tooltip("Health assigned to a new enemy instance.")]
    public int maxHealth = 3;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    [Tooltip("Ground movement speed. Used by archetypes that move at a single constant speed.")]
    public float speed = 2f;

    [Header("Damage Reaction")]
    [SerializeField]
    [Tooltip("Horizontal and vertical impulse applied when the enemy is damaged.")]
    public Vector2 knockbackForce = new Vector2(10f, 5f);

    [SerializeField, Min(0f)]
    [Tooltip("Time the knockback velocity is applied.")]
    public float knockbackDuration = 0.2f;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        speed = Mathf.Max(0f, speed);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
    }
}
