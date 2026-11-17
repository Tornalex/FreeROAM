using UnityEngine;

/// <summary>
/// Authoring data for the boss horizontal tongue sweep: lowering of the
/// dedicated emission point, telegraph, sweep speed and recovery.
///
/// Damage and cooldown come from <see cref="BossAttackData"/>, the same shared
/// fields used by the other boss attacks. Selection weight lives in the
/// <see cref="BossAIProfile"/> with the other action weights.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/Attacks/Horizontal Tongue")]
public class HorizontalTongueAttackData : BossAttackData
{
    [Header("Timing")]
    [SerializeField, Min(0f)]
    [Tooltip("Total preparation time. The origin descends during the first half and holds for the rest.")]
    private float horizontalTongueWindupTime = 0.55f;

    [SerializeField, Min(0f)]
    [Tooltip("Recovery before the next action decision.")]
    private float horizontalTongueRecoveryTime = 0.35f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Safety cap for the extension. Also caps the maximum horizontal reach: Speed * MaxExtensionTime.")]
    private float horizontalTongueMaxExtensionTime = 2.5f;

    [Header("Emission")]
    [SerializeField, Min(0f)]
    [Tooltip("Local downward offset applied to HorizontalTongueOrigin during the wind-up.")]
    private float horizontalTongueHeightOffset = 1f;

    [Header("Sweep")]
    [SerializeField, Min(0.01f)]
    [Tooltip("Horizontal travel speed of the tongue in world units per second.")]
    private float horizontalTongueSpeed = 24f;

    public float WindupTime => horizontalTongueWindupTime;
    public float RecoveryTime => horizontalTongueRecoveryTime;
    public float MaxExtensionTime => horizontalTongueMaxExtensionTime;
    public float HeightOffset => horizontalTongueHeightOffset;
    public float Speed => horizontalTongueSpeed;

    protected override void OnValidate()
    {
        base.OnValidate();

        horizontalTongueWindupTime = Mathf.Max(0f, horizontalTongueWindupTime);
        horizontalTongueRecoveryTime = Mathf.Max(0f, horizontalTongueRecoveryTime);
        horizontalTongueMaxExtensionTime = Mathf.Max(0.01f, horizontalTongueMaxExtensionTime);
        horizontalTongueHeightOffset = Mathf.Max(0f, horizontalTongueHeightOffset);
        horizontalTongueSpeed = Mathf.Max(0.01f, horizontalTongueSpeed);
    }
}
