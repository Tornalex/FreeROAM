using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns the Essence spray for confirmed spear damage.
///
/// The visual is built from small sprite droplets and animated line filaments
/// that erupt from the impact point, away from the spear's penetration
/// direction. The manager owns the shared sprite/material assets and caps the
/// number of simultaneous effects.
/// </summary>
public class EssenceExtractionVFXManager : MonoBehaviour
{
    public static EssenceExtractionVFXManager Instance { get; private set; }

    [Header("Essence Spray")]
    [SerializeField] private bool enableEssenceExtraction = true;

    [Tooltip("Total visual duration in unscaled seconds.")]
    [SerializeField, Min(0.1f)] private float duration = 0.4f;

    [SerializeField, Range(1, 16)] private int dropletCount = 8;
    [SerializeField, Range(0, 3)] private int filamentCount = 2;

    [Header("Directional Spray")]
    [Tooltip("Fraction of droplets that travel inside the primary cone opposite the spear.")]
    [SerializeField, Range(0f, 1f)] private float primarySprayFraction = 0.7f;

    [Tooltip("Half-angle of the primary spray cone in degrees.")]
    [SerializeField, Range(5f, 80f)] private float primaryConeHalfAngle = 32f;

    [Tooltip("Half-angle limit for the lateral/secondary droplets in degrees.")]
    [SerializeField, Range(10f, 170f)] private float secondaryConeHalfAngle = 105f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float speedMin = 2.4f;
    [SerializeField, Min(0f)] private float speedMax = 4.4f;
    [SerializeField, Min(0.01f)] private float deceleration = 7f;
    [SerializeField, Min(0f)] private float gravity = 1.6f;
    [SerializeField, Min(0f)] private float wobbleStrength = 0.06f;

    [Header("Shape")]
    [SerializeField, Min(0.005f)] private float dropletSizeMin = 0.06f;
    [SerializeField, Min(0.005f)] private float dropletSizeMax = 0.12f;
    [SerializeField, Min(0.005f)] private float filamentWidth = 0.04f;
    [SerializeField, Min(0.01f)] private float filamentLengthMin = 0.4f;
    [SerializeField, Min(0.01f)] private float filamentLengthMax = 0.7f;

    [Header("Color")]
    [Tooltip("Almost white core of the Essence.")]
    [SerializeField] private Color coreColor = new Color(0.96f, 1f, 1f, 1f);

    [Tooltip("Cyan / light-blue outer edge of the Essence.")]
    [SerializeField] private Color edgeColor = new Color(0.45f, 0.88f, 1f, 1f);

    [Header("Rendering")]
    [Tooltip("Dedicated project VFX layer, so the spray stays readable over the spear and enemies.")]
    [SerializeField] private string sortingLayerName = "VFX";
    [SerializeField] private int sortingOrder = 100;

    [Header("Clutter Control")]
    [SerializeField, Range(1, 32)] private int maxConcurrentEffects = 12;

    private readonly List<EssenceExtractionVFX> activeEffects =
        new List<EssenceExtractionVFX>();

    private Sprite _dropletSprite;
    private Material _spriteMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EssenceExtractionVFXManager] Duplicate instance disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        RebuildSharedAssets();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DestroyActiveEffects();
        DestroySharedAssets();
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.1f, duration);
        primaryConeHalfAngle = Mathf.Clamp(primaryConeHalfAngle, 5f, 80f);
        secondaryConeHalfAngle = Mathf.Clamp(
            secondaryConeHalfAngle,
            primaryConeHalfAngle + 1f,
            170f);
        speedMin = Mathf.Max(0f, speedMin);
        speedMax = Mathf.Max(speedMin, speedMax);
        deceleration = Mathf.Max(0.01f, deceleration);
        gravity = Mathf.Max(0f, gravity);
        wobbleStrength = Mathf.Max(0f, wobbleStrength);
        dropletSizeMin = Mathf.Max(0.005f, dropletSizeMin);
        dropletSizeMax = Mathf.Max(dropletSizeMin, dropletSizeMax);
        filamentWidth = Mathf.Max(0.005f, filamentWidth);
        filamentLengthMin = Mathf.Max(0.01f, filamentLengthMin);
        filamentLengthMax = Mathf.Max(filamentLengthMin, filamentLengthMax);
        maxConcurrentEffects = Mathf.Max(1, maxConcurrentEffects);

        if (Application.isPlaying && isActiveAndEnabled)
            RebuildSharedAssets();
    }

    /// <summary>
    /// Plays one Essence spray that erupts from the confirmed impact point,
    /// opposite to the spear's pre-impact travel direction.
    /// </summary>
    public void Play(
        Vector2 impactPoint,
        Vector2 penetrationDirection)
    {
        if (!enableEssenceExtraction || !isActiveAndEnabled)
            return;

        if (penetrationDirection.sqrMagnitude <= 0.0001f)
            return;

        if (_dropletSprite == null || _spriteMaterial == null)
            return;

        PruneAndLimitActiveEffects();

        GameObject effectObject = new GameObject("Essence Spray VFX");
        EssenceExtractionVFX effect =
            effectObject.AddComponent<EssenceExtractionVFX>();

        effect.Initialize(
            impactPoint,
            penetrationDirection,
            BuildSettings());

        activeEffects.Add(effect);
    }

    private EssenceExtractionVFX.Settings BuildSettings()
    {
        EssenceExtractionVFX.Settings settings = new EssenceExtractionVFX.Settings();
        settings.Duration = duration;
        settings.DropletCount = dropletCount;
        settings.FilamentCount = filamentCount;
        settings.PrimarySprayFraction = primarySprayFraction;
        settings.PrimaryConeHalfAngle = primaryConeHalfAngle;
        settings.SecondaryConeHalfAngle = secondaryConeHalfAngle;
        settings.SpeedMin = speedMin;
        settings.SpeedMax = speedMax;
        settings.Deceleration = deceleration;
        settings.Gravity = gravity;
        settings.WobbleStrength = wobbleStrength;
        settings.DropletSizeMin = dropletSizeMin;
        settings.DropletSizeMax = dropletSizeMax;
        settings.FilamentWidth = filamentWidth;
        settings.FilamentLengthMin = filamentLengthMin;
        settings.FilamentLengthMax = filamentLengthMax;
        settings.CoreColor = coreColor;
        settings.EdgeColor = edgeColor;
        settings.SortingLayerName = sortingLayerName;
        settings.SortingOrder = sortingOrder;
        settings.DropletSprite = _dropletSprite;
        settings.Material = _spriteMaterial;
        return settings;
    }

    private void PruneAndLimitActiveEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i] == null)
                activeEffects.RemoveAt(i);
        }

        int limit = Mathf.Max(1, maxConcurrentEffects);
        while (activeEffects.Count >= limit)
        {
            EssenceExtractionVFX oldest = activeEffects[0];
            activeEffects.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest.gameObject);
        }
    }

    private void DestroyActiveEffects()
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i] != null)
                Destroy(activeEffects[i].gameObject);
        }

        activeEffects.Clear();
    }

    private void RebuildSharedAssets()
    {
        DestroySharedAssets();

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            Debug.LogError(
                "[EssenceExtractionVFXManager] Sprites/Default shader not found; " +
                "Essence spray visuals are disabled.",
                this);
            return;
        }

        _spriteMaterial = new Material(spriteShader);
        _spriteMaterial.name = "Essence Spray Material";
        _spriteMaterial.color = Color.white;
        _spriteMaterial.hideFlags = HideFlags.DontSave;

        _dropletSprite = CreateDropletSprite(coreColor, edgeColor);
    }

    private void DestroySharedAssets()
    {
        if (_dropletSprite != null)
        {
            Texture2D texture = _dropletSprite.texture;
            Destroy(_dropletSprite);
            _dropletSprite = null;

            if (texture != null)
                Destroy(texture);
        }

        if (_spriteMaterial != null)
        {
            Destroy(_spriteMaterial);
            _spriteMaterial = null;
        }
    }

    /// <summary>
    /// Builds a soft, irregular blob with a white core and a cyan rim. The
    /// silhouette is intentionally not a perfect circle so droplets read as
    /// viscous liquid rather than glowing dots.
    /// </summary>
    private static Sprite CreateDropletSprite(Color core, Color edge)
    {
        const int size = 64;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Essence Droplet";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.DontSave;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size) * 2f - 1f;
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx);

                // Low-frequency lobes keep the outline organic and slightly
                // gelatinous without making individual droplets noisy.
                float rim = 0.78f
                    + 0.055f * Mathf.Sin(angle * 3f + 1.4f)
                    + 0.035f * Mathf.Sin(angle * 5f - 0.6f)
                    + 0.02f * Mathf.Sin(angle * 8f + 2.1f);

                float alpha = Mathf.Clamp01((rim - radius) / 0.22f);
                alpha = alpha * alpha * (3f - 2f * alpha);

                float coreAmount = Mathf.Clamp01(1f - radius / 0.62f);
                coreAmount *= coreAmount;

                Color color = Color.Lerp(edge, core, coreAmount);
                color.a = alpha;
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect);
    }
}
