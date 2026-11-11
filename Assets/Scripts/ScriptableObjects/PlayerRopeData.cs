using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Rope")]
public class PlayerRopeData : ScriptableObject
{
    public float climbSpeed = 10f;
    public float dismountJumpForce = 12f;
    public float dismountCooldown = 0.5f;
    public float ropeLength = 5f;

    [Header("Input Thresholds")]
    [Tooltip("Horizontal input magnitude that dismounts the player from a rope.")]
    public float horizontalDismountThreshold = 0.5f;

    [Tooltip("Vertical input magnitude that starts or stops climbing.")]
    public float verticalInputThreshold = 0.1f;

    [Tooltip("Distance below the rope top at which the player auto-dismounts.")]
    public float autoDismountOffset = 0.15f;

    [Tooltip("Vertical input magnitude that moves the player while climbing.")]
    public float climbInputThreshold = 0.1f;

    private void OnValidate()
    {
        climbSpeed = Mathf.Max(0f, climbSpeed);
        dismountJumpForce = Mathf.Max(0f, dismountJumpForce);
        dismountCooldown = Mathf.Max(0f, dismountCooldown);
        ropeLength = Mathf.Max(0f, ropeLength);
        horizontalDismountThreshold = Mathf.Clamp01(horizontalDismountThreshold);
        verticalInputThreshold = Mathf.Clamp01(verticalInputThreshold);
        autoDismountOffset = Mathf.Max(0f, autoDismountOffset);
        climbInputThreshold = Mathf.Clamp01(climbInputThreshold);
    }
}
