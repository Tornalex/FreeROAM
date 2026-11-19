using UnityEngine;

/// <summary>
/// Authoring data for the boss dash jump: distance gate, launch arc, landing
/// offset, preparation/recovery timing, cooldown and obstruction layers.
///
/// Selection weight stays in <see cref="BossAIProfile"/> with the other action
/// weights.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/Attacks/Dash Jump")]
public class DashJumpAttackData : ScriptableObject
{
    [Header("Targeting")]
    [SerializeField, Min(0f)]
    [Tooltip("Minimum horizontal distance to the player required to consider a dash jump.")]
    private float dashJumpMinDistance = 7f;

    [Header("Movement")]
    [SerializeField, Min(0.01f)]
    [Tooltip("Horizontal cruise speed of a dash jump.")]
    private float dashJumpHorizontalSpeed = 18f;

    [SerializeField, Min(0f)]
    [Tooltip("Minimum upward velocity of a dash jump.")]
    private float dashJumpUpVelocity = 10f;

    [SerializeField, Min(0f)]
    [Tooltip("Extra landing distance beyond the sampled player position.")]
    private float dashJumpLandingOffset = 1f;

    [Header("Timing")]
    [SerializeField, Min(0.05f)]
    [Tooltip("Readable wind-up before the dash jump launches.")]
    private float dashJumpPreparationTime = 0.45f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Recovery time after a dash jump (successful or aborted).")]
    private float dashJumpRecoveryTime = 0.45f;

    [SerializeField, Min(0f)]
    [Tooltip("Cooldown before another dash jump is allowed.")]
    private float dashJumpCooldown = 5f;

    [SerializeField, Min(0.5f)]
    [Tooltip("Maximum airborne time for a dash jump before a forced recovery.")]
    private float dashJumpMaxAirborneDuration = 2f;

    [Header("Obstruction Layers")]
    [SerializeField]
    [Tooltip("Layers that stop a dash jump.")]
    private LayerMask dashJumpObstructionLayers = 1;

    public float DashJumpMinDistance => dashJumpMinDistance;
    public float DashJumpHorizontalSpeed => dashJumpHorizontalSpeed;
    public float DashJumpUpVelocity => dashJumpUpVelocity;
    public float DashJumpLandingOffset => dashJumpLandingOffset;
    public float DashJumpPreparationTime => dashJumpPreparationTime;
    public float DashJumpRecoveryTime => dashJumpRecoveryTime;
    public float DashJumpCooldown => dashJumpCooldown;
    public float DashJumpMaxAirborneDuration => dashJumpMaxAirborneDuration;
    public LayerMask DashJumpObstructionLayers => dashJumpObstructionLayers;

    private void OnValidate()
    {
        dashJumpMinDistance = Mathf.Max(0f, dashJumpMinDistance);
        dashJumpHorizontalSpeed = Mathf.Max(0.01f, dashJumpHorizontalSpeed);
        dashJumpUpVelocity = Mathf.Max(0f, dashJumpUpVelocity);
        dashJumpLandingOffset = Mathf.Max(0f, dashJumpLandingOffset);
        dashJumpPreparationTime = Mathf.Max(0.05f, dashJumpPreparationTime);
        dashJumpRecoveryTime = Mathf.Max(0.1f, dashJumpRecoveryTime);
        dashJumpCooldown = Mathf.Max(0f, dashJumpCooldown);
        dashJumpMaxAirborneDuration = Mathf.Max(0.5f, dashJumpMaxAirborneDuration);
    }
}
