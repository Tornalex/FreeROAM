using System.Collections.Generic;
using UnityEngine;

namespace SpearWander.Boss
{
    /// <summary>
    /// Suspended spore cloud spawned by <see cref="FreeRoamBoss"/>.
    /// It expires on its own, fades out, drifts slowly and damages the player
    /// on contact. No physical collider is used, so it does not block movement.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SporeCloud : MonoBehaviour
    {
        [Header("Cloud")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private float radius = 0.9f;
        [SerializeField] private Vector2 driftVelocity = Vector2.zero;
        [SerializeField] private float activationDelay = 0f;

        [Header("Visuals")]
        [SerializeField] private Color cloudColor = new Color(0.55f, 0.85f, 0.45f, 0.75f);
        [SerializeField] private int sortingOrder = 5;
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField] private float pulseAmount = 0.06f;
        [SerializeField] private float fadeDuration = 1.25f;
        [SerializeField] private float warningAlpha = 0.18f;

        [Header("Contact")]
        [SerializeField] private LayerMask playerLayers = 8;
        [SerializeField] private LayerMask obstructionLayers = 1;
        [SerializeField] private float damageCooldown = 0.4f;

        private SpriteRenderer spriteRenderer;
        private ContactFilter2D playerFilter;
        private ContactFilter2D obstructionFilter;
        private readonly List<Collider2D> overlapResults = new List<Collider2D>(4);
        private readonly List<RaycastHit2D> obstructionHits = new List<RaycastHit2D>(4);

        private float lifeTimer;
        private float activationTimer;
        private float hitTimer;
        private float baseScale = 1f;
        private float totalLifetime;
        private float pulsePhase;
        private bool active;
        private bool despawning;

        private static Sprite fallbackSprite;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = GetFallbackSprite();
            }

            if (spriteRenderer.sortingOrder == 0)
            {
                spriteRenderer.sortingOrder = sortingOrder;
            }

            pulsePhase = Random.Range(0f, Mathf.PI * 2f);

            ConfigureFilters();

            totalLifetime = Mathf.Max(0.1f, lifetime);
            lifeTimer = totalLifetime;
            activationTimer = Mathf.Max(0f, activationDelay);
            active = activationTimer <= 0f;

            ApplyVisualScale();
            UpdateVisual();
        }

        public void Initialize(
            int damageAmount,
            float cloudLifetime,
            float cloudRadius,
            Vector2 drift,
            float delay,
            LayerMask cloudObstructionLayers)
        {
            damage = Mathf.Max(0, damageAmount);
            lifetime = Mathf.Max(0.1f, cloudLifetime);
            radius = Mathf.Max(0.05f, cloudRadius);
            driftVelocity = drift;
            activationDelay = Mathf.Max(0f, delay);
            obstructionLayers = cloudObstructionLayers;

            totalLifetime = lifetime;
            lifeTimer = totalLifetime;
            activationTimer = activationDelay;
            active = activationTimer <= 0f;
            hitTimer = 0f;

            ConfigureFilters();

            ApplyVisualScale();
            UpdateVisual();
        }

        private void ConfigureFilters()
        {
            playerFilter = CreateLayerFilter(playerLayers.value);
            obstructionFilter = CreateLayerFilter(
                obstructionLayers.value & ~playerLayers.value);
        }

        private static ContactFilter2D CreateLayerFilter(int layerMask)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.SetLayerMask(layerMask);
            return filter;
        }

        private void FixedUpdate()
        {
            if (despawning) return;

            float deltaTime = Time.fixedDeltaTime;

            if (!active && activationDelay > 0f)
            {
                activationTimer -= deltaTime;
                if (activationTimer <= 0f)
                {
                    activationTimer = 0f;
                    active = true;
                }
            }

            if (!active) return;

            lifeTimer -= deltaTime;
            if (lifeTimer <= 0f)
            {
                Despawn();
                return;
            }

            if (driftVelocity.sqrMagnitude > 0f)
            {
                MoveWithObstruction(deltaTime);
            }

            if (hitTimer > 0f)
            {
                hitTimer -= deltaTime;
            }

            if (!IsFadingOut() && hitTimer <= 0f)
            {
                CheckPlayerContact();
            }
        }

        private void Update()
        {
            if (despawning) return;
            UpdateVisual();
        }

        private void MoveWithObstruction(float deltaTime)
        {
            Vector2 displacement = driftVelocity * deltaTime;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f) return;

            Vector2 direction = displacement / distance;
            obstructionHits.Clear();

            int hitCount = Physics2D.CircleCast(
                transform.position,
                radius,
                direction,
                obstructionFilter,
                obstructionHits,
                distance);

            if (hitCount <= 0)
            {
                transform.position += (Vector3)displacement;
                return;
            }

            float blockedDistance = distance;
            for (int i = 0; i < hitCount; i++)
            {
                blockedDistance = Mathf.Min(blockedDistance, obstructionHits[i].distance);
            }

            float safeDistance = Mathf.Max(0f, blockedDistance - 0.01f);
            if (safeDistance > 0f)
            {
                transform.position += (Vector3)(direction * safeDistance);
            }

            driftVelocity = Vector2.zero;
        }

        private void CheckPlayerContact()
        {
            overlapResults.Clear();

            Physics2D.OverlapCircle(
                transform.position,
                radius,
                playerFilter,
                overlapResults);

            for (int i = 0; i < overlapResults.Count; i++)
            {
                Player player = GetPlayerFromCollider(overlapResults[i]);
                if (player == null || player.Health == null) continue;

                Vector2 targetPoint = overlapResults[i].ClosestPoint(transform.position);
                if (IsObstructedTo(targetPoint)) continue;

                if (player.Health.TakeDamage(damage, transform.position, transform.position))
                {
                    hitTimer = Mathf.Max(0.05f, damageCooldown);
                    break;
                }
            }
        }

        private bool IsObstructedTo(Vector2 targetPoint)
        {
            Vector2 origin = transform.position;
            Vector2 delta = targetPoint - origin;
            float distance = delta.magnitude;
            if (distance <= 0.0001f) return false;

            obstructionHits.Clear();

            int hitCount = Physics2D.Raycast(
                origin,
                delta / distance,
                obstructionFilter,
                obstructionHits,
                distance);

            return hitCount > 0;
        }

        private static Player GetPlayerFromCollider(Collider2D collider)
        {
            if (collider == null) return null;

            Player player = collider.GetComponentInParent<Player>();
            if (player == null || player.Collider == null) return null;

            return collider == player.Collider ? player : null;
        }

        public void Despawn()
        {
            if (despawning) return;

            despawning = true;
            Destroy(gameObject);
        }

        private float GetFadeStartTime()
        {
            return Mathf.Max(
                0.0001f,
                Mathf.Min(fadeDuration, totalLifetime));
        }

        private bool IsFadingOut()
        {
            return active && lifeTimer <= GetFadeStartTime();
        }

        private void ApplyVisualScale()
        {
            if (spriteRenderer == null) return;

            float spriteWidth = spriteRenderer.sprite != null
                ? Mathf.Max(0.0001f, spriteRenderer.sprite.bounds.size.x)
                : 1f;

            baseScale = (radius * 2f) / spriteWidth;
            transform.localScale = Vector3.one * baseScale;
        }

        private void UpdateVisual()
        {
            if (spriteRenderer == null) return;

            float alpha = cloudColor.a;
            float scale = 1f;

            if (!active && activationDelay > 0f)
            {
                float progress = 1f - Mathf.Clamp01(
                    activationTimer / Mathf.Max(0.0001f, activationDelay));

                alpha = Mathf.Lerp(
                    Mathf.Min(warningAlpha, cloudColor.a),
                    cloudColor.a,
                    progress);

                scale = Mathf.Lerp(0.65f, 1f, progress);
            }
            else
            {
                float fadeStart = GetFadeStartTime();

                if (lifeTimer <= fadeStart)
                {
                    alpha = cloudColor.a * Mathf.Clamp01(lifeTimer / fadeStart);
                }

                scale = 1f + Mathf.Sin(Time.time * pulseSpeed + pulsePhase) * pulseAmount;
            }

            transform.localScale = Vector3.one * (baseScale * Mathf.Max(0.01f, scale));

            Color color = cloudColor;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "SporeCloudRuntime";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.DontSave;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            fallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);

            fallbackSprite.name = "SporeCloudRuntime";
            fallbackSprite.hideFlags = HideFlags.DontSave;

            return fallbackSprite;
        }
    }
}
