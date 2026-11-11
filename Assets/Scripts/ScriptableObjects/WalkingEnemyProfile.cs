using UnityEngine;

/// <summary>
/// Tuning for the ground WalkingEnemy archetype: player detection, chase
/// direction stability and the obstacle probes that keep it on walkable ground.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Enemy/Profiles/Walking")]
public class WalkingEnemyProfile : EnemyAIProfile
{
    [Header("Chase")]
    [SerializeField, Min(0f)]
    [Tooltip("Horizontal dead zone used to avoid rapid direction changes while chasing.")]
    private float chaseDirectionDeadzone = 0.4f;

    [Header("Obstacle Detection")]
    [SerializeField, Min(0f)]
    [Tooltip("Extra distance beyond the enemy collider used for wall checks.")]
    private float wallCheckDistance = 0.1f;

    [SerializeField, Min(0f)]
    [Tooltip("Distance below the front of the enemy used to find ground.")]
    private float groundCheckDistance = 0.75f;

    [SerializeField, Min(0f)]
    [Tooltip("Forward offset from the enemy collider edge used for the ground check.")]
    private float groundCheckForwardOffset = 0.15f;

    public float ChaseDirectionDeadzone => chaseDirectionDeadzone;
    public float WallCheckDistance => wallCheckDistance;
    public float GroundCheckDistance => groundCheckDistance;
    public float GroundCheckForwardOffset => groundCheckForwardOffset;

    protected override void OnValidate()
    {
        base.OnValidate();

        chaseDirectionDeadzone = Mathf.Max(0f, chaseDirectionDeadzone);
        wallCheckDistance = Mathf.Max(0f, wallCheckDistance);
        groundCheckDistance = Mathf.Max(0f, groundCheckDistance);
        groundCheckForwardOffset = Mathf.Max(0f, groundCheckForwardOffset);
    }
}
