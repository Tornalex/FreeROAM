using UnityEngine;

/// <summary>
/// Shared authoring data for a single boss attack.
///
/// Only the parameters that every attack really shares live here (damage and
/// cooldown). Timing, range and visuals stay on the concrete attack assets so
/// that the Inspector remains focused and no attack is forced into a uniform
/// shape it does not need.
/// </summary>
public abstract class BossAttackData : ScriptableObject
{
    [Header("Shared")]
    [SerializeField, Min(0)]
    [Tooltip("Damage dealt by a successful hit.")]
    private int damage = 1;

    [SerializeField, Min(0f)]
    [Tooltip("Time before this attack can be chosen again.")]
    private float cooldown = 2f;

    public int Damage => damage;
    public float Cooldown => cooldown;

    protected virtual void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        cooldown = Mathf.Max(0f, cooldown);
    }
}
