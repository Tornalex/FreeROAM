using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies a per-renderer Gaussian blur to a SpriteRenderer without modifying
/// shared materials.
///
/// The sprite image is convolved once (only when the sprite or a blur parameter
/// changes) into a temporary texture by a separable Gaussian kernel, then the
/// sprite material samples that texture. The blur radius is the kernel sigma,
/// expressed in source texture pixels.
/// </summary>
[AddComponentMenu("Rendering/2D/Sprite Blur")]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBlur : MonoBehaviour
{
    private const string BlurShaderName = "Spear/2D/SpriteBlur";
    private const string KernelShaderName = "Hidden/Spear/SpriteBlurKernel";

    private static readonly int BlurEnabledId = Shader.PropertyToID("_BlurEnabled");
    private static readonly int BlurTextureId = Shader.PropertyToID("_BlurTex");
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");

    private static readonly int KernelMainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int KernelSourceRectId = Shader.PropertyToID("_SourceRect");
    private static readonly int KernelTexelSizeId = Shader.PropertyToID("_TexelSize");
    private static readonly int KernelSourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
    private static readonly int KernelSigmaId = Shader.PropertyToID("_Sigma");
    private static readonly int KernelTapStepId = Shader.PropertyToID("_TapStep");
    private static readonly int KernelTapCountId = Shader.PropertyToID("_TapCount");

    private static Material s_SharedSpriteMaterial;

    [Header("Blur")]
    [SerializeField] private bool enableBlur = true;

    [Tooltip("Gaussian sigma, in source texture pixels.")]
    [SerializeField, Range(0f, 100f)] private float blurAmount = 4f;

    [Tooltip("Kernel sampling precision. Does not change the blur radius.")]
    [SerializeField] private SpriteBlurQuality blurQuality = SpriteBlurQuality.Medium;

    [SerializeField, HideInInspector] private Shader blurShader;
    [SerializeField, HideInInspector] private Shader kernelShader;

    private SpriteRenderer _spriteRenderer;

    private MaterialPropertyBlock _propertyBlock;
    private MaterialPropertyBlock _originalPropertyBlock;

    private Material _spriteMaterial;
    private Material _kernelMaterial;
    private Material _originalSharedMaterial;

    private CommandBuffer _commandBuffer;
    private RenderTexture _horizontalTarget;
    private RenderTexture _blurTexture;

    private bool _hasOriginalState;
    private bool _isApplied;
    private bool _blurReady;
    private bool _rebuildRequested;
    private bool _packingRotationWarningLogged;

    private Color _originalColor = Color.white;
    private Texture _originalMaskTexture;
    private Texture _originalNormalTexture;
    private bool _hasOriginalColor;
    private bool _hasOriginalMaskTexture;
    private bool _hasOriginalNormalTexture;

    private Sprite _cachedSprite;
    private Texture _cachedTexture;
    private Rect _cachedRect;
    private float _cachedAmount = -1f;
    private SpriteBlurQuality _cachedQuality;

    public enum SpriteBlurQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public bool EnableBlur
    {
        get => enableBlur;
        set
        {
            if (enableBlur == value)
                return;

            enableBlur = value;

            if (isActiveAndEnabled)
                SyncState();
        }
    }

    public float BlurAmount
    {
        get => blurAmount;
        set
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(blurAmount, clamped))
                return;

            blurAmount = clamped;
            _rebuildRequested = true;

            if (_isApplied)
                RebuildBlur();
        }
    }

    public SpriteBlurQuality BlurQuality
    {
        get => blurQuality;
        set
        {
            if (blurQuality == value)
                return;

            blurQuality = value;
            _rebuildRequested = true;

            if (_isApplied)
                RebuildBlur();
        }
    }

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _propertyBlock = new MaterialPropertyBlock();
        _originalPropertyBlock = new MaterialPropertyBlock();
        _commandBuffer = new CommandBuffer { name = "SpriteBlur" };
    }

    void OnEnable()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null)
        {
            Debug.LogError("SpriteBlur requires a SpriteRenderer.", this);
            enabled = false;
            return;
        }

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        if (_originalPropertyBlock == null)
            _originalPropertyBlock = new MaterialPropertyBlock();

        if (_commandBuffer == null)
            _commandBuffer = new CommandBuffer { name = "SpriteBlur" };

        CaptureOriginalState();
        SyncState();
    }

    void OnDisable()
    {
        RemoveBlur();
        _hasOriginalState = false;
    }

    void OnDestroy()
    {
        RemoveBlur();

        if (_kernelMaterial != null)
        {
            DestroyRuntimeObject(_kernelMaterial);
            _kernelMaterial = null;
        }

        if (_commandBuffer != null)
        {
            _commandBuffer.Release();
            _commandBuffer = null;
        }
    }

    void LateUpdate()
    {
        if (!_isApplied || _spriteRenderer == null)
            return;

        Sprite currentSprite = _spriteRenderer.sprite;

        if (currentSprite == null)
        {
            if (_cachedSprite != null || _rebuildRequested)
            {
                ResetCache();
                ReleaseTargets();
                _blurReady = false;
                PushProperties();
            }

            return;
        }

        bool spriteChanged = currentSprite != _cachedSprite ||
                             currentSprite.texture != _cachedTexture ||
                             currentSprite.textureRect != _cachedRect;

        bool parametersChanged = _rebuildRequested ||
                                 !Mathf.Approximately(blurAmount, _cachedAmount) ||
                                 blurQuality != _cachedQuality;

        if (spriteChanged || parametersChanged)
            RebuildBlur();
    }

    void Reset()
    {
        blurShader = Shader.Find(BlurShaderName);
        kernelShader = Shader.Find(KernelShaderName);
        blurAmount = Mathf.Max(0f, blurAmount);
    }

    void OnValidate()
    {
        blurAmount = Mathf.Max(0f, blurAmount);

        if (blurShader == null)
            blurShader = Shader.Find(BlurShaderName);

        if (kernelShader == null)
            kernelShader = Shader.Find(KernelShaderName);

        if (!Application.isPlaying || !isActiveAndEnabled || _spriteRenderer == null)
            return;

        RebuildBlur();
    }

    private void SyncState()
    {
        if (enableBlur)
            ApplyBlur();
        else
            RemoveBlur();
    }

    private void CaptureOriginalState()
    {
        _originalSharedMaterial = _spriteRenderer.sharedMaterial;
        _spriteRenderer.GetPropertyBlock(_originalPropertyBlock);

        _hasOriginalColor = false;
        _hasOriginalMaskTexture = false;
        _hasOriginalNormalTexture = false;

        if (_originalSharedMaterial != null)
        {
            if (_originalSharedMaterial.HasProperty(ColorId))
            {
                _originalColor = _originalSharedMaterial.GetColor(ColorId);
                _hasOriginalColor = true;
            }

            if (_originalSharedMaterial.HasProperty(MaskTexId))
            {
                _originalMaskTexture = _originalSharedMaterial.GetTexture(MaskTexId);
                _hasOriginalMaskTexture = _originalMaskTexture != null;
            }

            if (_originalSharedMaterial.HasProperty(NormalMapId))
            {
                _originalNormalTexture = _originalSharedMaterial.GetTexture(NormalMapId);
                _hasOriginalNormalTexture = _originalNormalTexture != null;
            }
        }

        _hasOriginalState = true;
    }

    private void ApplyBlur()
    {
        if (_spriteRenderer == null)
            return;

        if (!_hasOriginalState)
            CaptureOriginalState();

        if (!TryGetMaterials())
            return;

        if (_spriteRenderer.sharedMaterial != _spriteMaterial)
            _spriteRenderer.sharedMaterial = _spriteMaterial;

        _isApplied = true;
        _rebuildRequested = true;
        RebuildBlur();
    }

    private void RemoveBlur()
    {
        if (_isApplied && _spriteRenderer != null && _spriteRenderer.sharedMaterial == _spriteMaterial)
        {
            _spriteRenderer.sharedMaterial = _originalSharedMaterial;
            _spriteRenderer.SetPropertyBlock(_originalPropertyBlock);
        }

        _isApplied = false;
        _blurReady = false;
        _rebuildRequested = false;

        ReleaseTargets();
        ResetCache();
    }

    private bool TryGetMaterials()
    {
        if (blurShader == null)
            blurShader = Shader.Find(BlurShaderName);

        if (kernelShader == null)
            kernelShader = Shader.Find(KernelShaderName);

        if (blurShader == null)
        {
            Debug.LogError("SpriteBlur could not find shader \"" + BlurShaderName + "\".", this);
            return false;
        }

        if (kernelShader == null)
        {
            Debug.LogError("SpriteBlur could not find shader \"" + KernelShaderName + "\".", this);
            return false;
        }

        if (s_SharedSpriteMaterial == null)
        {
            s_SharedSpriteMaterial = new Material(blurShader)
            {
                name = "SpriteBlur (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (_kernelMaterial == null || _kernelMaterial.shader != kernelShader)
        {
            _kernelMaterial = new Material(kernelShader)
            {
                name = "SpriteBlur Kernel (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        _spriteMaterial = s_SharedSpriteMaterial;
        return true;
    }

    private void RebuildBlur()
    {
        _rebuildRequested = false;

        Sprite sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
        Texture texture = sprite != null ? sprite.texture : null;

        CacheCurrentState(sprite, texture);

        if (sprite == null || texture == null || blurAmount <= 0f || _kernelMaterial == null)
        {
            _blurReady = false;
            ReleaseTargets();
            PushProperties();
            return;
        }

        if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
        {
            if (!_packingRotationWarningLogged)
            {
                _packingRotationWarningLogged = true;
                Debug.LogWarning("SpriteBlur does not support sprites rotated inside a sprite atlas. " +
                                 "Blur is disabled for \"" + sprite.name + "\".", this);
            }

            _blurReady = false;
            ReleaseTargets();
            PushProperties();
            return;
        }

        Rect rect = sprite.textureRect;
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        int maxSize = SystemInfo.maxTextureSize;

        if (width > maxSize || height > maxSize || !EnsureTargets(width, height))
        {
            _blurReady = false;
            PushProperties();
            return;
        }

        float sigma = blurAmount;
        float tapStep = blurQuality == SpriteBlurQuality.Low ? 2f : 1f;
        float support = blurQuality == SpriteBlurQuality.High ? 4f : 3f;
        int tapCount = Mathf.Clamp(Mathf.CeilToInt(support * sigma / tapStep), 1, 96);

        float textureWidth = Mathf.Max(1f, texture.width);
        float textureHeight = Mathf.Max(1f, texture.height);

        Vector4 sourceRect = new Vector4(
            rect.x / textureWidth,
            rect.y / textureHeight,
            rect.width / textureWidth,
            rect.height / textureHeight);

        Vector4 texelSize = new Vector4(1f / width, 1f / height, 0f, 0f);
        Vector4 sourceTexelSize = new Vector4(1f / textureWidth, 1f / textureHeight, 0f, 0f);

        // Horizontal pass: sprite texture -> intermediate target, premultiplied alpha.
        _kernelMaterial.SetTexture(KernelMainTexId, texture);
        _kernelMaterial.SetVector(KernelSourceRectId, sourceRect);
        _kernelMaterial.SetVector(KernelTexelSizeId, texelSize);
        _kernelMaterial.SetVector(KernelSourceTexelSizeId, sourceTexelSize);
        _kernelMaterial.SetFloat(KernelSigmaId, sigma);
        _kernelMaterial.SetFloat(KernelTapStepId, tapStep);
        _kernelMaterial.SetInt(KernelTapCountId, tapCount);

        _commandBuffer.Clear();
        _commandBuffer.SetRenderTarget(_horizontalTarget);
        _commandBuffer.DrawProcedural(Matrix4x4.identity, _kernelMaterial, 0, MeshTopology.Triangles, 3);
        Graphics.ExecuteCommandBuffer(_commandBuffer);

        // Vertical pass: intermediate target -> final blurred texture, straight alpha.
        _kernelMaterial.SetTexture(KernelMainTexId, _horizontalTarget);
        _kernelMaterial.SetVector(KernelSourceRectId, new Vector4(0f, 0f, 1f, 1f));
        _kernelMaterial.SetVector(KernelSourceTexelSizeId, texelSize);

        _commandBuffer.Clear();
        _commandBuffer.SetRenderTarget(_blurTexture);
        _commandBuffer.DrawProcedural(Matrix4x4.identity, _kernelMaterial, 1, MeshTopology.Triangles, 3);
        Graphics.ExecuteCommandBuffer(_commandBuffer);

        _blurReady = true;
        PushProperties();
    }

    private void CacheCurrentState(Sprite sprite, Texture texture)
    {
        _cachedSprite = sprite;
        _cachedTexture = texture;
        _cachedRect = sprite != null ? sprite.textureRect : default;
        _cachedAmount = blurAmount;
        _cachedQuality = blurQuality;
    }

    private void ResetCache()
    {
        _cachedSprite = null;
        _cachedTexture = null;
        _cachedRect = default;
        _cachedAmount = -1f;
    }

    private bool EnsureTargets(int width, int height)
    {
        if (_horizontalTarget != null && _blurTexture != null &&
            _horizontalTarget.width == width && _horizontalTarget.height == height)
            return true;

        ReleaseTargets();

        _horizontalTarget = CreateTarget(width, height, "SpriteBlur Horizontal");
        _blurTexture = CreateTarget(width, height, "SpriteBlur Blurred");

        if (_horizontalTarget == null || _blurTexture == null)
        {
            ReleaseTargets();
            return false;
        }

        return true;
    }

    private static RenderTexture CreateTarget(int width, int height, string name)
    {
        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave
        };

        if (!target.Create())
        {
            DestroyRuntimeObject(target);
            return null;
        }

        return target;
    }

    private void ReleaseTargets()
    {
        if (_horizontalTarget != null)
        {
            DestroyRuntimeObject(_horizontalTarget);
            _horizontalTarget = null;
        }

        if (_blurTexture != null)
        {
            DestroyRuntimeObject(_blurTexture);
            _blurTexture = null;
        }
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (target is RenderTexture renderTexture)
            renderTexture.Release();

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void PushProperties()
    {
        if (!_isApplied || _spriteRenderer == null || _propertyBlock == null)
            return;

        _spriteRenderer.GetPropertyBlock(_propertyBlock);

        _propertyBlock.SetFloat(BlurEnabledId, _blurReady ? 1f : 0f);

        if (_blurReady && _blurTexture != null)
            _propertyBlock.SetTexture(BlurTextureId, _blurTexture);

        if (_hasOriginalColor)
            _propertyBlock.SetColor(ColorId, _originalColor);

        if (_hasOriginalMaskTexture)
            _propertyBlock.SetTexture(MaskTexId, _originalMaskTexture);

        if (_hasOriginalNormalTexture)
            _propertyBlock.SetTexture(NormalMapId, _originalNormalTexture);

        GetSpriteUVRect(_spriteRenderer.sprite, out Vector4 uvRect);
        _propertyBlock.SetVector(SpriteRectId, uvRect);

        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    private static void GetSpriteUVRect(Sprite sprite, out Vector4 uvRect)
    {
        if (sprite == null || sprite.texture == null)
        {
            uvRect = new Vector4(0f, 0f, 1f, 1f);
            return;
        }

        Texture texture = sprite.texture;
        float width = Mathf.Max(1, texture.width);
        float height = Mathf.Max(1, texture.height);
        Rect rect = sprite.textureRect;

        uvRect = new Vector4(
            rect.xMin / width,
            rect.yMin / height,
            rect.xMax / width,
            rect.yMax / height);
    }
}
