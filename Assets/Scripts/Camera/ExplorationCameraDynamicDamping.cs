using System;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachinePositionComposer))]
public sealed class ExplorationCameraDynamicDamping : MonoBehaviour
{
    private enum VerticalMotion
    {
        Grounded,
        Rising,
        Falling
    }

    [Serializable]
    private struct DampingProfile
    {
        [Min(0f)] public float horizontal;
        [Min(0f)] public float vertical;
    }

    [Header("Damping Profiles (seconds)")]
    [SerializeField] private DampingProfile groundedProfile =
        new DampingProfile { horizontal = 0.75f, vertical = 2f };
    [SerializeField] private DampingProfile risingProfile =
        new DampingProfile { horizontal = 0.75f, vertical = 2.8f };
    [SerializeField] private DampingProfile fallingProfile =
        new DampingProfile { horizontal = 0.75f, vertical = 0.65f };

    [Tooltip("Horizontal damping used while Dash is active. Vertical damping stays with the current airborne profile.")]
    [SerializeField, Min(0f)] private float dashHorizontalDamping = 0.25f;

    [Header("Transitions")]
    [Tooltip("SmoothDamp time used when changing damping profiles.")]
    [SerializeField, Min(0.01f)] private float transitionSmoothTime = 0.16f;

    [Header("Vertical Motion Detection")]
    [Tooltip("Vertical Rigidbody2D speed required to leave the stable profile.")]
    [SerializeField, Min(0f)] private float verticalSpeedThreshold = 0.35f;

    [Tooltip("Extra speed required to switch directly between Rising and Falling.")]
    [SerializeField, Min(0f)] private float verticalSpeedHysteresis = 0.15f;

    private CinemachinePositionComposer _positionComposer;
    private Player _player;
    private VerticalMotion _verticalMotion = VerticalMotion.Grounded;
    private Vector3 _currentDamping;
    private Vector3 _dampingVelocity;
    private float _dampingDepth;

    private void Awake()
    {
        _positionComposer = GetComponent<CinemachinePositionComposer>();

        CinemachineCamera camera = GetComponent<CinemachineCamera>();
        Transform followTarget = camera != null ? camera.Follow : null;
        _player = followTarget != null
            ? followTarget.GetComponentInParent<Player>()
            : null;

        if (_positionComposer == null || _player == null)
        {
            Debug.LogError(
                "[ExplorationCameraDynamicDamping] A Position Composer and a Player tracking target are required.",
                this);
            enabled = false;
            return;
        }

        _currentDamping = _positionComposer.Damping;
        _dampingDepth = _currentDamping.z;
    }

    private void FixedUpdate()
    {
        if (_player == null || _player.Rb == null)
            return;

        bool isGrounded = _player.Feet != null && _player.Feet.IsGrounded();
        bool isDashing = _player.Dash != null && _player.Dash.IsDashing;
        float verticalSpeed = _player.Rb.linearVelocity.y;

        // Ground contact takes priority. Dash owns horizontal tracking, but its
        // forced zero vertical velocity must not replace the airborne profile.
        if (isGrounded)
        {
            _verticalMotion = VerticalMotion.Grounded;
        }
        else if (!isDashing)
        {
            bool stationaryOnRope =
                _player.RopeClimb != null &&
                _player.RopeClimb.IsClimbing &&
                Mathf.Abs(verticalSpeed) <= verticalSpeedThreshold;

            if (stationaryOnRope)
                _verticalMotion = VerticalMotion.Grounded;
            else
                UpdateVerticalMotion(verticalSpeed);
        }

        DampingProfile verticalProfile = GetVerticalProfile();
        Vector3 targetDamping = new Vector3(
            isDashing ? dashHorizontalDamping : verticalProfile.horizontal,
            verticalProfile.vertical,
            _dampingDepth);

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        _currentDamping = Vector3.SmoothDamp(
            _currentDamping,
            targetDamping,
            ref _dampingVelocity,
            transitionSmoothTime,
            Mathf.Infinity,
            deltaTime);

        _positionComposer.Damping = _currentDamping;
    }

    private void UpdateVerticalMotion(float verticalSpeed)
    {
        float threshold = verticalSpeedThreshold;
        float reversalThreshold = threshold + verticalSpeedHysteresis;

        switch (_verticalMotion)
        {
            case VerticalMotion.Grounded:
                if (verticalSpeed > threshold)
                    _verticalMotion = VerticalMotion.Rising;
                else if (verticalSpeed < -threshold)
                    _verticalMotion = VerticalMotion.Falling;
                break;

            case VerticalMotion.Rising:
                if (verticalSpeed < -reversalThreshold)
                    _verticalMotion = VerticalMotion.Falling;
                break;

            case VerticalMotion.Falling:
                if (verticalSpeed > reversalThreshold)
                    _verticalMotion = VerticalMotion.Rising;
                break;
        }
    }

    private DampingProfile GetVerticalProfile()
    {
        return _verticalMotion switch
        {
            VerticalMotion.Rising => risingProfile,
            VerticalMotion.Falling => fallingProfile,
            _ => groundedProfile
        };
    }

    private void OnValidate()
    {
        groundedProfile.horizontal = Mathf.Max(0f, groundedProfile.horizontal);
        groundedProfile.vertical = Mathf.Max(0f, groundedProfile.vertical);
        risingProfile.horizontal = Mathf.Max(0f, risingProfile.horizontal);
        risingProfile.vertical = Mathf.Max(0f, risingProfile.vertical);
        fallingProfile.horizontal = Mathf.Max(0f, fallingProfile.horizontal);
        fallingProfile.vertical = Mathf.Max(0f, fallingProfile.vertical);
        dashHorizontalDamping = Mathf.Max(0f, dashHorizontalDamping);
        transitionSmoothTime = Mathf.Max(0.01f, transitionSmoothTime);
        verticalSpeedThreshold = Mathf.Max(0f, verticalSpeedThreshold);
        verticalSpeedHysteresis = Mathf.Max(0f, verticalSpeedHysteresis);
    }
}
