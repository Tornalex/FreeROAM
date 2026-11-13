using UnityEngine;

/// <summary>
/// Movimento ambientale autonomo per oggetti decorativi (es. lucciole).
/// Implementa un pattugliamento casuale attorno alla posizione di partenza,
/// con direzione di movimento rotata gradualmente e lievi variazioni di velocita.
/// Senza dipendenze dal Player, da BaseEnemy o da sistemi di combattimento.
/// </summary>
public class FireflyMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    [Tooltip("Velocita base di spostamento.")]
    private float flySpeed = 2.5f;

    [SerializeField, Min(0f)]
    [Tooltip("Raggio massimo dalla posizione di partenza entro cui generare i punti di pattugliamento.")]
    private float patrolRadius = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("Distanza sotto cui il punto e' considerato raggiunto e ne viene generato uno nuovo. La lucciola non si ferma: cambia semplicemente direzione.")]
    private float arrivalThreshold = 0.2f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)]
    [Tooltip("Velocita di rotazione della direzione di movimento verso il nuovo punto. Valori piu alti = cambi di direzione piu rapidi. Zero mantiene la direzione fissa.")]
    private float rotationSpeed = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("Entita della variazione casuale della velocita (0 = nessuna variazione).")]
    [Range(0f, 1f)]
    private float speedVariation = 0.2f;

    [SerializeField, Min(0f)]
    [Tooltip("Frequenza della variazione casuale della velocita.")]
    private float speedVariationFrequency = 1f;

    private Vector2 _startPosition;
    private Vector2 _targetPatrolPoint;
    private Vector2 _currentDirection;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _startPosition = transform.position;
        _rb = GetComponent<Rigidbody2D>();
        SetNewPatrolPoint();
        _currentDirection = (_targetPatrolPoint - (Vector2)transform.position).normalized;
        if (_currentDirection.sqrMagnitude == 0f)
            _currentDirection = Vector2.right;
    }

    private void FixedUpdate()
    {
        Vector2 position = transform.position;
        Vector2 toTarget = _targetPatrolPoint - position;
        float distance = toTarget.magnitude;

        if (distance < arrivalThreshold)
        {
            SetNewPatrolPoint();
            toTarget = _targetPatrolPoint - position;
        }

        Vector2 desiredDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : _currentDirection;

        // Rotazione graduale della direzione verso il punto bersaglio
        _currentDirection = Vector2.MoveTowards(_currentDirection, desiredDirection, rotationSpeed * Time.fixedDeltaTime);
        _currentDirection.Normalize();

        // Variazione leggera e naturale della velocita
        float noise = Mathf.PerlinNoise(0f, Time.time * speedVariationFrequency);
        float speedMultiplier = 1f + (noise - 0.5f) * 2f * speedVariation;
        float currentSpeed = flySpeed * speedMultiplier;

        Vector2 nextPosition = position + _currentDirection * currentSpeed * Time.fixedDeltaTime;

        if (_rb != null)
        {
            _rb.MovePosition(nextPosition);
        }
        else
        {
            transform.position = nextPosition;
        }
    }

    private void SetNewPatrolPoint()
    {
        _targetPatrolPoint = _startPosition + (Random.insideUnitCircle * patrolRadius);
    }
}
