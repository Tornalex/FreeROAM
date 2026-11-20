using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachinePositionComposer))]
public sealed class ExplorationCameraDirectionalLookahead : MonoBehaviour
{
    private sealed class VerticalLookRequest
    {
        public CameraLookTrigger Trigger;
        public float Offset;
        public float EnterDuration;
        public float ExitDuration;
    }

    [Header("Directional Look-ahead")]
    [Tooltip("World-space distance to anticipate in front of the Player.")]
    [SerializeField, Min(0f)] private float lookAheadDistance = 1.5f;

    [Tooltip("SmoothDamp time when the Player changes facing direction.")]
    [SerializeField, Min(0.01f)] private float directionTransitionTime = 0.2f;

    private CinemachinePositionComposer _positionComposer;
    private Transform _trackingTarget;
    private float _baseTargetOffsetX;
    private float _baseTargetOffsetY;
    private float _baseTargetOffsetZ;
    private float _currentWorldOffset;
    private float _offsetVelocity;
    private float _lastAppliedTargetOffsetX;
    private float _lastAppliedTargetOffsetY;
    private float _lastAppliedTargetOffsetZ;
    private float _lastFacingSign = 1f;
    private bool _hasCapturedBaseOffset;
    private bool _hasAppliedOffset;

    private readonly List<VerticalLookRequest> _verticalLookRequests =
        new List<VerticalLookRequest>();

    private float _currentVerticalOffset;
    private float _verticalTransitionStart;
    private float _verticalTransitionTarget;
    private float _verticalTransitionDuration;
    private float _verticalTransitionElapsed;
    private bool _isVerticalTransitionActive;

    private void Awake()
    {
        _positionComposer = GetComponent<CinemachinePositionComposer>();
        CinemachineCamera camera = GetComponent<CinemachineCamera>();
        _trackingTarget = camera != null ? camera.Follow : null;

        if (_positionComposer == null || _trackingTarget == null)
        {
            Debug.LogError(
                "[ExplorationCameraDirectionalLookahead] A Position Composer and a tracking target are required.",
                this);
            enabled = false;
            return;
        }

        _baseTargetOffsetX = _positionComposer.TargetOffset.x;
        _baseTargetOffsetY = _positionComposer.TargetOffset.y;
        _baseTargetOffsetZ = _positionComposer.TargetOffset.z;
        _lastAppliedTargetOffsetX = _positionComposer.TargetOffset.x;
        _lastAppliedTargetOffsetY = _positionComposer.TargetOffset.y;
        _lastAppliedTargetOffsetZ = _positionComposer.TargetOffset.z;
        _hasCapturedBaseOffset = true;
    }

    private void OnEnable()
    {
        if (!_hasCapturedBaseOffset)
            return;

        StartVerticalTransition(
            _verticalTransitionTarget,
            _verticalTransitionDuration);
    }

    private void FixedUpdate()
    {
        if (_positionComposer == null || _trackingTarget == null)
            return;

        RemoveInactiveLookTriggers();

        Vector3 targetOffset = _positionComposer.TargetOffset;

        // Preserve changes made by another system to the base offset; this
        // component owns the directional X and contextual Y contributions.
        if (_hasAppliedOffset)
        {
            _baseTargetOffsetX += targetOffset.x - _lastAppliedTargetOffsetX;
            _baseTargetOffsetY += targetOffset.y - _lastAppliedTargetOffsetY;
            _baseTargetOffsetZ += targetOffset.z - _lastAppliedTargetOffsetZ;
        }

        float facingSign = GetFacingSign();
        float targetWorldOffset = facingSign * lookAheadDistance;
        float deltaTime = Time.deltaTime;

        if (deltaTime > 0f)
        {
            _currentWorldOffset = Mathf.SmoothDamp(
                _currentWorldOffset,
                targetWorldOffset,
                ref _offsetVelocity,
                directionTransitionTime,
                Mathf.Infinity,
                deltaTime);
        }

        UpdateVerticalTransition(deltaTime);

        // TargetOffset is expressed in the tracking target's local space.
        // Convert the smoothed world-X offset into that space so the camera
        // moves continuously even though PlayerMovement flips the target 180°.
        targetOffset.x = _baseTargetOffsetX + facingSign * _currentWorldOffset;
        targetOffset.y = _baseTargetOffsetY + _currentVerticalOffset;
        targetOffset.z = _baseTargetOffsetZ;
        _positionComposer.TargetOffset = targetOffset;
        _lastAppliedTargetOffsetX = targetOffset.x;
        _lastAppliedTargetOffsetY = targetOffset.y;
        _lastAppliedTargetOffsetZ = targetOffset.z;
        _hasAppliedOffset = true;
    }

    /// <summary>
    /// Registers a vertical offset request. The newest active trigger becomes
    /// the current request; repeated registration of the same trigger is
    /// idempotent and does not change its place in the LIFO order.
    /// </summary>
    public void RegisterVerticalLookTrigger(
        CameraLookTrigger trigger,
        float verticalOffset,
        float enterDuration,
        float exitDuration)
    {
        if (object.ReferenceEquals(trigger, null))
            return;

        int existingIndex = FindVerticalLookRequest(trigger);
        if (existingIndex >= 0)
        {
            VerticalLookRequest existing = _verticalLookRequests[existingIndex];
            existing.Offset = verticalOffset;
            existing.EnterDuration = enterDuration;
            existing.ExitDuration = exitDuration;

            if (existingIndex == _verticalLookRequests.Count - 1)
                StartVerticalTransition(verticalOffset, enterDuration);

            return;
        }

        _verticalLookRequests.Add(new VerticalLookRequest
        {
            Trigger = trigger,
            Offset = verticalOffset,
            EnterDuration = enterDuration,
            ExitDuration = exitDuration
        });

        StartVerticalTransition(verticalOffset, enterDuration);
    }

    /// <summary>
    /// Removes a trigger request. If it was the active request, its own exit
    /// duration is used to transition to the preceding request or base offset.
    /// </summary>
    public void UnregisterVerticalLookTrigger(CameraLookTrigger trigger)
    {
        if (object.ReferenceEquals(trigger, null))
            return;

        int index = FindVerticalLookRequest(trigger);
        if (index < 0)
            return;

        bool wasActive = index == _verticalLookRequests.Count - 1;
        float exitDuration = _verticalLookRequests[index].ExitDuration;
        _verticalLookRequests.RemoveAt(index);

        if (!wasActive)
            return;

        float nextOffset = _verticalLookRequests.Count > 0
            ? _verticalLookRequests[_verticalLookRequests.Count - 1].Offset
            : 0f;

        StartVerticalTransition(nextOffset, exitDuration);
    }

    private int FindVerticalLookRequest(CameraLookTrigger trigger)
    {
        for (int i = 0; i < _verticalLookRequests.Count; i++)
        {
            if (object.ReferenceEquals(_verticalLookRequests[i].Trigger, trigger))
                return i;
        }

        return -1;
    }

    private void RemoveInactiveLookTriggers()
    {
        for (int i = _verticalLookRequests.Count - 1; i >= 0; i--)
        {
            VerticalLookRequest request = _verticalLookRequests[i];
            if (request.Trigger != null && request.Trigger.isActiveAndEnabled)
                continue;

            bool wasActive = i == _verticalLookRequests.Count - 1;
            float exitDuration = request.ExitDuration;
            _verticalLookRequests.RemoveAt(i);

            if (wasActive)
            {
                float nextOffset = _verticalLookRequests.Count > 0
                    ? _verticalLookRequests[_verticalLookRequests.Count - 1].Offset
                    : 0f;
                StartVerticalTransition(nextOffset, exitDuration);
            }
        }
    }

    private void StartVerticalTransition(float targetOffset, float duration)
    {
        _verticalTransitionStart = _currentVerticalOffset;
        _verticalTransitionTarget = targetOffset;
        _verticalTransitionDuration = Mathf.Max(0f, duration);
        _verticalTransitionElapsed = 0f;

        if (_verticalTransitionDuration <= 0f ||
            Mathf.Approximately(_currentVerticalOffset, targetOffset))
        {
            _currentVerticalOffset = targetOffset;
            _verticalTransitionStart = targetOffset;
            _verticalTransitionElapsed = _verticalTransitionDuration;
            _isVerticalTransitionActive = false;
            return;
        }

        _isVerticalTransitionActive = true;
    }

    private void UpdateVerticalTransition(float deltaTime)
    {
        if (!_isVerticalTransitionActive || deltaTime <= 0f)
            return;

        _verticalTransitionElapsed = Mathf.Min(
            _verticalTransitionElapsed + deltaTime,
            _verticalTransitionDuration);

        float normalizedTime =
            _verticalTransitionElapsed / _verticalTransitionDuration;
        float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);

        _currentVerticalOffset = Mathf.Lerp(
            _verticalTransitionStart,
            _verticalTransitionTarget,
            easedTime);

        if (_verticalTransitionElapsed >= _verticalTransitionDuration)
        {
            _currentVerticalOffset = _verticalTransitionTarget;
            _isVerticalTransitionActive = false;
        }
    }

    private float GetFacingSign()
    {
        float facingX = _trackingTarget.right.x;
        if (Mathf.Abs(facingX) > 0.001f)
            _lastFacingSign = Mathf.Sign(facingX);

        return _lastFacingSign;
    }

    private void OnDisable()
    {
        if (_positionComposer == null || !_hasCapturedBaseOffset)
            return;

        Vector3 targetOffset = _positionComposer.TargetOffset;
        if (_hasAppliedOffset)
        {
            _baseTargetOffsetX += targetOffset.x - _lastAppliedTargetOffsetX;
            _baseTargetOffsetY += targetOffset.y - _lastAppliedTargetOffsetY;
            _baseTargetOffsetZ += targetOffset.z - _lastAppliedTargetOffsetZ;
        }

        targetOffset.x = _baseTargetOffsetX;
        targetOffset.y = _baseTargetOffsetY;
        targetOffset.z = _baseTargetOffsetZ;
        _positionComposer.TargetOffset = targetOffset;

        _currentWorldOffset = 0f;
        _offsetVelocity = 0f;
        _currentVerticalOffset = 0f;
        _verticalTransitionStart = 0f;
        _verticalTransitionElapsed = 0f;
        _isVerticalTransitionActive = false;
        _lastAppliedTargetOffsetX = _baseTargetOffsetX;
        _lastAppliedTargetOffsetY = _baseTargetOffsetY;
        _lastAppliedTargetOffsetZ = _baseTargetOffsetZ;
        _hasAppliedOffset = false;
    }

    private void OnValidate()
    {
        lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
        directionTransitionTime = Mathf.Max(0.01f, directionTransitionTime);
    }
}
