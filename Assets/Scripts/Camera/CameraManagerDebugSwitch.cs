using UnityEngine;
using UnityEngine.InputSystem;

public class CameraManagerDebugSwitch : MonoBehaviour
{
    private CameraManager cameraManager;

    private void Awake()
    {
        cameraManager = GetComponent<CameraManager>();
    }

    private void Update()
    {
        if (Keyboard.current == null || cameraManager == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
            cameraManager.SelectCamera(RoomCameraType.Exploration);

        if (Keyboard.current.f2Key.wasPressedThisFrame)
            cameraManager.SelectCamera(RoomCameraType.HorizontalCorridor);

        if (Keyboard.current.f3Key.wasPressedThisFrame)
            cameraManager.SelectCamera(RoomCameraType.Fixed);
        if (Keyboard.current.f4Key.wasPressedThisFrame)
            cameraManager.SelectCamera(RoomCameraType.VerticalCorridor);
    }
}