using Unity.Cinemachine;
using UnityEngine;

public partial class Spear : MonoBehaviour
{
    private CinemachineImpulseSource _cameraImpulseSource;
    private float _lastCameraImpulseTime =
        float.NegativeInfinity;

    private float ThrowImpactImpulse =>
        spearConfig != null
            ? spearConfig.ThrowImpactImpulse
            : 0.12f;

    private float ReturnImpactImpulse =>
        spearConfig != null
            ? spearConfig.ReturnImpactImpulse
            : 0.07f;

    private float BossDeflectImpulse =>
        spearConfig != null
            ? spearConfig.BossDeflectImpulse
            : 0.16f;

    private float CameraImpulseMinInterval =>
        spearConfig != null
            ? spearConfig.CameraImpulseMinInterval
            : 0.15f;

    /// <summary>
    /// Caches the impulse source already owned by the player. No impulse
    /// source is added or created by the spear.
    /// </summary>
    private void CacheCameraImpulseSource()
    {
        _cameraImpulseSource =
            _playerCollider != null
                ? _playerCollider.GetComponentInParent<CinemachineImpulseSource>()
                : null;

        _lastCameraImpulseTime =
            float.NegativeInfinity;
    }

    /// <summary>
    /// Feedback for damage the spear actually delivered to an enemy. The
    /// throw and return use different strengths, but share the same limiter
    /// so multiple contacts cannot stack into a continuous shake.
    /// </summary>
    private void GenerateEnemyHitCameraImpulse(
        Vector2 hitPoint,
        Vector2 travelDirection)
    {
        if (currentState == SpearState.Returning)
        {
            GenerateCameraImpulse(
                hitPoint,
                travelDirection,
                ReturnImpactImpulse);

            return;
        }

        // A dropped or embedded spear touching an enemy is not a combat
        // impact and must not shake the camera.
        if (currentState != SpearState.Flying &&
            currentState != SpearState.BouncedOff)
        {
            return;
        }

        GenerateCameraImpulse(
            hitPoint,
            travelDirection,
            ThrowImpactImpulse);
    }

    private void GenerateBossDeflectCameraImpulse(
        Vector2 hitPoint,
        Vector2 incomingVelocity)
    {
        GenerateCameraImpulse(
            hitPoint,
            incomingVelocity,
            BossDeflectImpulse);
    }

    /// <summary>
    /// Sends a single impulse through the player's existing source. Only the
    /// per-event position and velocity are overridden; the shared
    /// ImpulseDefinition is never modified.
    /// </summary>
    private void GenerateCameraImpulse(
        Vector2 hitPoint,
        Vector2 direction,
        float strength)
    {
        if (_cameraImpulseSource == null ||
            strength <= 0f)
        {
            return;
        }

        float minInterval =
            CameraImpulseMinInterval;

        if (minInterval > 0f &&
            Time.unscaledTime - _lastCameraImpulseTime < minInterval)
        {
            return;
        }

        _lastCameraImpulseTime =
            Time.unscaledTime;

        Vector2 impulseDirection =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.down;

        _cameraImpulseSource.GenerateImpulseAtPositionWithVelocity(
            hitPoint,
            new Vector3(
                impulseDirection.x,
                impulseDirection.y,
                0f) *
            strength);
    }
}
