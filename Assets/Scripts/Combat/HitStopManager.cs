using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Single owner of the global hit-stop freeze.
///
/// The manager freezes gameplay by setting <see cref="Time.timeScale"/> to zero
/// and resumes it from an unscaled-time timer. It never touches
/// <see cref="Time.fixedDeltaTime"/> and never starts a freeze while the game is
/// already frozen, so a hit stop can not accidentally unpause a paused game.
///
/// While the freeze is active the Cinemachine brains are kept on an update path
/// that still runs with a stopped physics clock, so impulse-driven camera shake
/// starts at the impact instead of waiting for gameplay to resume.
/// </summary>
public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    [Header("Hit Stop")]
    [SerializeField] private bool enableHitStop = true;

    [Tooltip("Gameplay freeze duration in unscaled seconds.")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.06f;

    private struct BrainUpdateOverride
    {
        public CinemachineBrain Brain;
        public CinemachineBrain.UpdateMethods OriginalMethod;
    }

    private readonly List<BrainUpdateOverride> _brainOverrides =
        new List<BrainUpdateOverride>();

    private readonly List<CinemachineBrain> _brains =
        new List<CinemachineBrain>();

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

        // Impulse events are timestamped and evaluated against
        // CinemachineCore.CurrentTime, which follows the scaled clock. While the
        // freeze holds Time.timeScale at zero that clock stops, so an impulse
        // created at the impact would not start moving until gameplay resumes.
        // The impulse system's unscaled clock lets the shake play during the
        // freeze and continue seamlessly once the freeze ends.
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
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
            BeginCameraUpdatesDuringFreeze();
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

        EndCameraUpdatesDuringFreeze();
    }

    /// <summary>
    /// A CinemachineBrain set to FixedUpdate is only stepped from the physics
    /// update, and physics does not run while time is stopped. During the freeze
    /// the brains are moved to LateUpdate, where the camera pipeline (and its
    /// impulse listener) keeps evaluating even with a stopped physics clock.
    /// The original update method is restored as soon as the freeze ends.
    /// </summary>
    private void BeginCameraUpdatesDuringFreeze()
    {
        _brainOverrides.Clear();
        RefreshBrains();

        for (int i = 0; i < _brains.Count; i++)
        {
            CinemachineBrain brain = _brains[i];

            if (brain == null ||
                !brain.isActiveAndEnabled ||
                brain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate)
            {
                continue;
            }

            _brainOverrides.Add(new BrainUpdateOverride
            {
                Brain = brain,
                OriginalMethod = brain.UpdateMethod
            });

            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        }
    }

    private void EndCameraUpdatesDuringFreeze()
    {
        for (int i = 0; i < _brainOverrides.Count; i++)
        {
            BrainUpdateOverride entry = _brainOverrides[i];

            // Do not fight another system that changed the mode in the meantime.
            if (entry.Brain != null &&
                entry.Brain.UpdateMethod == CinemachineBrain.UpdateMethods.LateUpdate)
            {
                entry.Brain.UpdateMethod = entry.OriginalMethod;
            }
        }

        _brainOverrides.Clear();
    }

    private void RefreshBrains()
    {
        bool needsRefresh = _brains.Count == 0;

        for (int i = 0; i < _brains.Count && !needsRefresh; i++)
            needsRefresh = _brains[i] == null;

        if (!needsRefresh)
            return;

        _brains.Clear();

        _brains.AddRange(
            FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude));
    }
}
