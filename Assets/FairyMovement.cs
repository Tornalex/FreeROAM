using UnityEngine;

public class FairyMovement : MonoBehaviour
{
    [SerializeField] float FlySpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 _targetPatrolPoint;
    private Vector2 _startPosition;
    private const float PatrolRadius = 3f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        _startPosition = transform.position;
    }
    void Update()
    {
        rb.linearVelocity = new Vector2(Random.Range(-FlySpeed, FlySpeed), Random.Range(-FlySpeed, FlySpeed));
    }
        private void Patrol()
    {
        transform.position = Vector2.MoveTowards(transform.position, _targetPatrolPoint, FlySpeed * Time.fixedDeltaTime);
        if (Vector2.Distance(transform.position, _targetPatrolPoint) < 0.2f) SetNewPatrolPoint();
    }

    private void SetNewPatrolPoint()
    {
        _targetPatrolPoint = _startPosition + (Random.insideUnitCircle * PatrolRadius);
    }
}
