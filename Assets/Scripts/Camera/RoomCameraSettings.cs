using UnityEngine;

public enum RoomCameraType
{
    Exploration,
    Corridor,
    Fixed
}

/// <summary>
/// Optional camera configuration stored alongside a room's CameraBounds.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomCameraSettings : MonoBehaviour
{
    [SerializeField] private RoomCameraType cameraType = RoomCameraType.Exploration;
    [SerializeField] private Transform fixedCameraAnchor;

    public RoomCameraType CameraType => cameraType;
    public Transform FixedCameraAnchor => fixedCameraAnchor;
    public Collider2D CameraBounds => GetComponent<Collider2D>();
}
