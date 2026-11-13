using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HitFlash : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.067f;

    [SerializeField] private Material hitFlashMaterial;

    // Optional: the renderer that is actually visible. Falls back to the
    // SpriteRenderer on this GameObject when left empty.
    [SerializeField] private SpriteRenderer targetRenderer;

    private Material _originalMaterial;
    private SpriteRenderer _spriteRenderer;
    private float _flashTimer;

    void Awake()
    {
        _spriteRenderer = targetRenderer != null
            ? targetRenderer
            : GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
            _originalMaterial = _spriteRenderer.material;
    }

    public void Flash()
    {
        if (hitFlashMaterial == null || _spriteRenderer == null)
            return;

        _flashTimer = flashDuration;
        _spriteRenderer.material = hitFlashMaterial;
    }

    void FixedUpdate()
    {
        if (_flashTimer > 0)
        {
            _flashTimer -= Time.fixedDeltaTime;
            if (_flashTimer <= 0)
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.material = _originalMaterial;
            }
        }
    }
}
