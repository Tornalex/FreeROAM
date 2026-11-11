using UnityEngine;

/// <summary>
/// Tuning for the FlyingEnemy archetype: patrol/chase speeds, patrol radius
/// and the stun applied after a hit.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Enemy/Profiles/Flying")]
public class FlyingEnemyProfile : EnemyAIProfile
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    [Tooltip("Speed used while patrolling around the spawn point.")]
    private float flySpeed = 2.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Speed used while chasing the player.")]
    private float chaseSpeed = 4.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Maximum patrol offset from the spawn point.")]
    private float patrolRadius = 3f;

    [Header("Stun")]
    [SerializeField, Min(0f)]
    [Tooltip("Time the enemy stops moving after being hit or pogo bounced.")]
    private float stunDuration = 0.5f;

    public float FlySpeed => flySpeed;
    public float ChaseSpeed => chaseSpeed;
    public float PatrolRadius => patrolRadius;
    public float StunDuration => stunDuration;

    protected override void OnValidate()
    {
        base.OnValidate();

        flySpeed = Mathf.Max(0f, flySpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        patrolRadius = Mathf.Max(0f, patrolRadius);
        stunDuration = Mathf.Max(0f, stunDuration);
    }
}
