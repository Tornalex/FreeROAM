using UnityEngine;

/// <summary>
/// Single owner of the global hit-stop freeze.
///
/// The manager freezes gameplay by setting <see cref="Time.timeScale"/> to zero
/// and resumes it from an unscaled-time timer. It never touches
/// <see cref="Time.fixedDeltaTime"/> and never starts a freeze while the game is
/// already frozen, so a hit stop can not accidentally unpause a paused game.
/// </summary>
public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    [Header("Hit Stop")]
    [SerializeField] private bool enableHitStop = true;

    [Tooltip("Gameplay freeze duration in unscaled seconds.")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.06f;

    private bool _isActive;
    private float _remainingDuration;
    private float _restoreTimeScale = 1f;

    public bool IsActive => _isActive;
    public float HitStopDuration => hitStopDuration;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HitStopManager] Duplicate instance disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
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

        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        RestoreTimeScale();
    }

    private void Update()
    {
        if (!_isActive)
            return;

        _remainingDuration -= Time.unscaledDeltaTime;

        if (_remainingDuration <= 0f)
            RestoreTimeScale();
    }

    /// <summary>
    /// Requests a freeze using the configured duration. Repeated requests extend
    /// the current freeze instead of starting a competing timer.
    /// </summary>
    public void RequestHitStop()
    {
        RequestHitStop(hitStopDuration);
    }

    public void RequestHitStop(float duration)
    {
        if (!enableHitStop || !isActiveAndEnabled || duration <= 0f)
            return;

        if (!_isActive)
        {
            // Taking ownership of an already frozen game would later resume it
            // even though another system (pause, transition) stopped time.
            if (Time.timeScale <= 0f)
                return;

            _restoreTimeScale = Time.timeScale;
            _remainingDuration = duration;
            _isActive = true;
            Time.timeScale = 0f;
            return;
        }

        // Extend to the longest pending request without adding a second timer.
        _remainingDuration = Mathf.Max(_remainingDuration, duration);
    }

    private void RestoreTimeScale()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _remainingDuration = 0f;

        // Only release the freeze while the simulation is still stopped. If
        // another system changed time while the freeze was active, leave that
        // system in control.
        if (Time.timeScale <= 0f)
            Time.timeScale = _restoreTimeScale;
    }
}
