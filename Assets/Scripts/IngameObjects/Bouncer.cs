using UnityEngine;

public class Bouncer : MonoBehaviour, IBounceable
{
    [SerializeField, Min(0f)] private float bounceMultiplier = 2.5f;

    [Tooltip("Se attivo, il giocatore rimbalza semplicemente toccandolo con i piedi, senza usare il Pogo.")]
    [SerializeField] private bool bounceOnTouch = true;

    public bool BounceOnTouch => bounceOnTouch;

    public float GetBounceMultiplier()
    {
        return bounceMultiplier;
    }

    public void OnPogoBounce()
    {
        //(SFX and VFX)
    }
}