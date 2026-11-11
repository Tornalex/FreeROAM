using UnityEngine;

/// <summary>
/// Shared AI tuning for enemy archetypes that detect the player by distance.
/// This is authoring data only: runtime state (current target, timers, AI
/// state, velocity) stays on the enemy component instance.
/// </summary>
public abstract class EnemyAIProfile : ScriptableObject
{
    [Header("Detection")]
    [SerializeField, Min(0f)]
    [Tooltip("Distance at which the player can be detected.")]
    private float detectionRadius = 6f;

    [SerializeField, Min(0f)]
    [Tooltip("Distance after which a detected player is lost. Never lower than the detection radius.")]
    private float loseRadius = 10f;

    public float DetectionRadius => detectionRadius;
    public float LoseRadius => loseRadius;

    protected virtual void OnValidate()
    {
        detectionRadius = Mathf.Max(0f, detectionRadius);
        loseRadius = Mathf.Max(detectionRadius, loseRadius);
    }
}
