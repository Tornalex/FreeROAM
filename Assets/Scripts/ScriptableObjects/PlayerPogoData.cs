using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Pogo")]
public class PlayerPogoData : ScriptableObject
{
    public float plungeSpeed = 25f;
    public float bounceForce = 24f;
    public float postPogoInvincibility = 0.05f;
    public float pogoStunDuration = 0.15f;

    [Tooltip("Vertical offset applied to the player after a pogo bounce.")]
    public float bounceOffset = 0.2f;

    private void OnValidate()
    {
        plungeSpeed = Mathf.Max(0f, plungeSpeed);
        bounceForce = Mathf.Max(0f, bounceForce);
        postPogoInvincibility = Mathf.Max(0f, postPogoInvincibility);
        pogoStunDuration = Mathf.Max(0f, pogoStunDuration);
        bounceOffset = Mathf.Max(0f, bounceOffset);
    }
}
