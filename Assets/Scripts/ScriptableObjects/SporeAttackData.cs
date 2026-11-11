using UnityEngine;

/// <summary>
/// Authoring data for the suspended spore cloud attack: telegraph, lifetime,
/// radius, drift, targeting range and the active cloud budget.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/Attacks/Spore")]
public class SporeAttackData : BossAttackData
{
    [Header("Timing")]
    [SerializeField, Min(0f)]
    [Tooltip("Telegraph before the cloud becomes active.")]
    private float windupTime = 0.6f;

    [SerializeField, Min(0f)]
    [Tooltip("Recovery before the next action decision.")]
    private float recoveryTime = 0.35f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Total lifetime of a spawned cloud.")]
    private float lifetime = 6f;

    [Header("Cloud")]
    [SerializeField, Min(0.05f)]
    [Tooltip("Damage radius of the cloud.")]
    private float radius = 0.9f;

    [SerializeField, Min(1)]
    [Tooltip("Maximum number of clouds that may be active at the same time.")]
    private int maxActiveSpores = 3;

    [SerializeField]
    [Tooltip("Drift applied to a spawn. X is randomized symmetrically.")]
    private Vector2 drift = new Vector2(0.35f, 0f);

    [Header("Targeting")]
    [SerializeField, Min(0f)]
    [Tooltip("Maximum distance at which the spore attack can be chosen.")]
    private float range = 9f;

    public float WindupTime => windupTime;
    public float RecoveryTime => recoveryTime;
    public float Lifetime => lifetime;
    public float Radius => radius;
    public int MaxActiveSpores => maxActiveSpores;
    public Vector2 Drift => drift;
    public float Range => range;

    protected override void OnValidate()
    {
        base.OnValidate();

        windupTime = Mathf.Max(0f, windupTime);
        recoveryTime = Mathf.Max(0f, recoveryTime);
        lifetime = Mathf.Max(0.1f, lifetime);
        radius = Mathf.Max(0.05f, radius);
        maxActiveSpores = Mathf.Max(1, maxActiveSpores);
        range = Mathf.Max(0f, range);
    }
}
