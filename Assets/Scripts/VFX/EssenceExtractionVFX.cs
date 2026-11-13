using UnityEngine;

/// <summary>
/// One directional Essence spray: viscous droplets and short liquid filaments
/// erupt from the confirmed impact point, biased opposite to the spear's
/// penetration direction.
///
/// The effect runs on unscaled time so it stays readable during hit stop. It
/// only moves its own visual children; no gameplay object is advanced here.
/// </summary>
public class EssenceExtractionVFX : MonoBehaviour
{
    private const int FilamentPointCount = 10;

    public struct Settings
    {
        public float Duration;
        public int DropletCount;
        public int FilamentCount;
        public float PrimarySprayFraction;
        public float PrimaryConeHalfAngle;
        public float SecondaryConeHalfAngle;
        public float SpeedMin;
        public float SpeedMax;
        public float Deceleration;
        public float Gravity;
        public float WobbleStrength;
        public float DropletSizeMin;
        public float DropletSizeMax;
        public float FilamentWidth;
        public float FilamentLengthMin;
        public float FilamentLengthMax;
        public Color CoreColor;
        public Color EdgeColor;
        public string SortingLayerName;
        public int SortingOrder;
        public Sprite DropletSprite;
        public Material Material;
    }

    private sealed class Droplet
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Direction;
        public float Speed;
        public float Deceleration;
        public float Size;
        public float Delay;
        public float Lifetime;
        public float Phase;
        public float WobbleFrequency;
        public Vector2 PreviousPosition;
        public bool HasPreviousPosition;
    }

    private sealed class Filament
    {
        public LineRenderer Line;
        public Vector2 Direction;
        public float Length;
        public float Delay;
        public float Lifetime;
        public float Phase;
        public float WaveCount;
        public float Amplitude;
        public float BaseWidth;
    }

    private Droplet[] _droplets;
    private Filament[] _filaments;

    private Vector2 _origin;
    private Vector2 _sprayDirection;

    private float _elapsed;
    private float _duration;
    private float _gravity;
    private float _wobbleStrength;

    private Color _coreColor;
    private Color _edgeColor;
    private string _sortingLayerName;
    private int _sortingOrder;

    public void Initialize(
        Vector2 impactPoint,
        Vector2 penetrationDirection,
        Settings settings)
    {
        _origin = impactPoint;

        Vector2 penetration = penetrationDirection.sqrMagnitude > 0.0001f
            ? penetrationDirection.normalized
            : Vector2.up;

        // Essence is expelled back out of the wound, opposite to the spear.
        _sprayDirection = -penetration;

        _duration = Mathf.Max(0.1f, settings.Duration);
        _gravity = Mathf.Max(0f, settings.Gravity);
        _wobbleStrength = Mathf.Max(0f, settings.WobbleStrength);
        _coreColor = settings.CoreColor;
        _edgeColor = settings.EdgeColor;
        _sortingLayerName = settings.SortingLayerName;
        _sortingOrder = settings.SortingOrder;

        BuildDroplets(settings);
        BuildFilaments(settings);
    }

    private void Update()
    {
        // Unscaled time keeps the spray readable while gameplay is frozen by
        // hit stop. Only this effect's visual children are moved.
        _elapsed += Time.unscaledDeltaTime;

        UpdateDroplets();
        UpdateFilaments();

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }

    private void BuildDroplets(Settings settings)
    {
        int count = Mathf.Max(0, settings.DropletCount);
        _droplets = new Droplet[count];

        float maxSize = Mathf.Max(
            settings.DropletSizeMin,
            settings.DropletSizeMax);

        for (int i = 0; i < count; i++)
        {
            GameObject dropletObject = new GameObject("Essence Droplet");
            dropletObject.transform.SetParent(transform, false);

            SpriteRenderer renderer = dropletObject.AddComponent<SpriteRenderer>();
            renderer.sprite = settings.DropletSprite;
            renderer.sharedMaterial = settings.Material;
            renderer.color = Color.white;
            renderer.enabled = false;
            ApplySorting(renderer);

            bool primary =
                Random.value < settings.PrimarySprayFraction;

            float angle;

            if (primary)
            {
                // Triangular distribution keeps the main cone dense around
                // the direction opposite the spear.
                angle =
                    (Random.value - Random.value) *
                    settings.PrimaryConeHalfAngle;
            }
            else
            {
                // Lateral and secondary droplets spread outside the main cone.
                float inner = Mathf.Max(settings.PrimaryConeHalfAngle, 5f);
                float outer = Mathf.Max(
                    settings.SecondaryConeHalfAngle,
                    inner + 1f);

                float magnitude = Random.Range(inner, outer);
                angle = (Random.value < 0.5f ? -1f : 1f) * magnitude;
            }

            float speed = Random.Range(
                settings.SpeedMin,
                Mathf.Max(settings.SpeedMin, settings.SpeedMax));

            if (!primary)
                speed *= Random.Range(0.55f, 0.85f);

            Droplet droplet = new Droplet();
            droplet.Transform = dropletObject.transform;
            droplet.Renderer = renderer;
            droplet.Direction = Rotate(_sprayDirection, angle);
            droplet.Speed = speed;
            droplet.Deceleration = Mathf.Max(
                0.01f,
                settings.Deceleration * Random.Range(0.8f, 1.25f));
            droplet.Size = Random.Range(settings.DropletSizeMin, maxSize)
                * (primary ? 1f : Random.Range(0.55f, 0.8f));
            droplet.Delay = _duration * Random.Range(0f, 0.08f);
            droplet.Lifetime = _duration * Random.Range(0.7f, 0.92f);
            droplet.Phase = Random.Range(0f, Mathf.PI * 2f);
            droplet.WobbleFrequency = Random.Range(2f, 4.5f);
            droplet.PreviousPosition = _origin;
            droplet.HasPreviousPosition = false;

            droplet.Transform.right = droplet.Direction;

            _droplets[i] = droplet;
        }
    }

    private void BuildFilaments(Settings settings)
    {
        int count = Mathf.Max(0, settings.FilamentCount);
        _filaments = new Filament[count];

        float maxLength = Mathf.Max(
            settings.FilamentLengthMin,
            settings.FilamentLengthMax);

        for (int i = 0; i < count; i++)
        {
            GameObject filamentObject = new GameObject("Essence Filament");
            filamentObject.transform.SetParent(transform, false);

            LineRenderer line = filamentObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = FilamentPointCount;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = settings.Material;
            line.enabled = false;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.55f, 0.6f),
                new Keyframe(1f, 0.04f));
            ApplySorting(line);

            // Filaments erupt in the dominant spray direction with only a
            // small angular deviation, so they never reach toward the spear.
            float angle =
                Random.Range(-1f, 1f) *
                settings.PrimaryConeHalfAngle *
                0.6f;

            Filament filament = new Filament();
            filament.Line = line;
            filament.Direction = Rotate(_sprayDirection, angle);
            filament.Length = Random.Range(
                settings.FilamentLengthMin,
                maxLength);
            filament.Delay = _duration * Random.Range(0f, 0.05f);
            filament.Lifetime = _duration * Random.Range(0.55f, 0.75f);
            filament.Phase = Random.Range(0f, Mathf.PI * 2f);
            filament.WaveCount = Random.Range(1.2f, 2.2f);
            filament.Amplitude = filament.Length * Random.Range(0.1f, 0.2f);
            filament.BaseWidth = Mathf.Max(0.005f, settings.FilamentWidth);
            line.widthMultiplier = filament.BaseWidth;

            _filaments[i] = filament;
        }
    }

    private void UpdateDroplets()
    {
        if (_droplets == null)
            return;

        float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

        for (int i = 0; i < _droplets.Length; i++)
        {
            Droplet droplet = _droplets[i];
            float age = _elapsed - droplet.Delay;

            if (age <= 0f)
            {
                droplet.Renderer.enabled = false;
                continue;
            }

            float t = Mathf.Clamp01(age / droplet.Lifetime);

            // Strong initial velocity that decelerates to a stop, then a
            // slight gravity pull as the liquid loses momentum.
            float stopTime = droplet.Speed / droplet.Deceleration;
            float travelAge = Mathf.Min(age, stopTime);
            float distance =
                droplet.Speed * travelAge -
                0.5f * droplet.Deceleration * travelAge * travelAge;

            Vector2 perpendicular = new Vector2(
                -droplet.Direction.y,
                droplet.Direction.x);

            float sway =
                Mathf.Sin(
                    age * droplet.WobbleFrequency * Mathf.PI * 2f +
                    droplet.Phase) *
                _wobbleStrength *
                Mathf.Clamp01(age / 0.08f);

            Vector2 position = _origin
                + droplet.Direction * distance
                + perpendicular * sway
                + Vector2.down * (0.5f * _gravity * age * age);

            if (droplet.HasPreviousPosition)
            {
                Vector2 velocity = position - droplet.PreviousPosition;

                if (velocity.sqrMagnitude > 0.0000001f)
                    droplet.Transform.right = velocity.normalized;

                float speed = velocity.magnitude / deltaTime;
                float stretch =
                    Mathf.Clamp01(speed * 0.12f) *
                    0.85f *
                    (1f - t * 0.6f);

                float size =
                    droplet.Size * Mathf.Lerp(1f, 0.25f, t);

                droplet.Transform.localScale = new Vector3(
                    size * (1f + stretch),
                    size * (1f - 0.35f * stretch),
                    1f);
            }
            else
            {
                droplet.Transform.localScale = Vector3.one * droplet.Size;
                droplet.HasPreviousPosition = true;
            }

            droplet.PreviousPosition = position;
            droplet.Transform.position = position;

            float alpha = t < 0.6f
                ? 1f
                : 1f - Mathf.InverseLerp(0.6f, 1f, t);

            Color color = Color.white;
            color.a = alpha;
            droplet.Renderer.color = color;
            droplet.Renderer.enabled = alpha > 0.01f;
        }
    }

    private void UpdateFilaments()
    {
        if (_filaments == null)
            return;

        for (int i = 0; i < _filaments.Length; i++)
        {
            Filament filament = _filaments[i];
            float age = _elapsed - filament.Delay;

            if (age <= 0f)
            {
                filament.Line.enabled = false;
                continue;
            }

            float t = Mathf.Clamp01(age / filament.Lifetime);

            // Shoot outward quickly, then slow down as the liquid thins.
            float grow = 1f - Mathf.Pow(1f - t, 2.4f);

            Vector2 start = _origin;
            Vector2 end =
                _origin + filament.Direction * (filament.Length * grow);

            Vector2 perpendicular = new Vector2(
                -filament.Direction.y,
                filament.Direction.x);

            float amplitude = filament.Amplitude * grow;

            for (int pointIndex = 0;
                 pointIndex < FilamentPointCount;
                 pointIndex++)
            {
                float u = pointIndex / (FilamentPointCount - 1f);
                Vector2 point = Vector2.Lerp(start, end, u);

                float wave =
                    Mathf.Sin(
                        u * Mathf.PI * filament.WaveCount +
                        filament.Phase +
                        age * 7f) *
                    amplitude *
                    Mathf.Sin(u * Mathf.PI);

                point += perpendicular * wave;
                filament.Line.SetPosition(pointIndex, point);
            }

            float fadeIn = Mathf.Clamp01(t * 6f);
            float fadeOut = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.55f, 1f, t));
            float alpha = fadeIn * fadeOut;

            filament.Line.widthMultiplier =
                filament.BaseWidth * (1f - 0.45f * t);

            // White core at the wound, cyan toward the thinning tip.
            Color startColor = _coreColor;
            startColor.a *= alpha * 0.9f;

            Color endColor = _edgeColor;
            endColor.a *= alpha;

            filament.Line.startColor = startColor;
            filament.Line.endColor = endColor;
            filament.Line.enabled = alpha > 0.01f;
        }
    }

    private void ApplySorting(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (!string.IsNullOrEmpty(_sortingLayerName))
            renderer.sortingLayerName = _sortingLayerName;

        renderer.sortingOrder = _sortingOrder;
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }
}
