using UnityEngine;

public class CloudMoving : MonoBehaviour
{
    Rigidbody2D rb;
    [SerializeField] Vector2 speed = new Vector2(1f, 0f);
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    void Update()
    {
        rb.linearVelocity = new Vector2(speed.x * Time.deltaTime, speed.y * Time.deltaTime);
    }
}
