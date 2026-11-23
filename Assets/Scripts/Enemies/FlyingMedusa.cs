using UnityEngine;

public class FlyingMedusa : MonoBehaviour
{
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    void Update()
    {
        //if (animator.tri("Jump") == true)
        {
            ToJump();
        }
    }
    private void ToJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        animator.SetTrigger("Jump");
    }
    private void ToIdle()
    {
        animator.SetTrigger("Idle");
    }
    private void ToIdle2()
    {
        animator.SetTrigger("Idle2");
    }
}
