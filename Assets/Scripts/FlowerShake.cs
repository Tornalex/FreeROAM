using UnityEngine;

public class FlowerShake : MonoBehaviour
{
    public Transform target;
    public Transform orbitingObject;
    public Vector3 orbitAxis = Vector3.up;
    public float degreesToRotate = 360f;
    public float duration = 3f;

    private Transform pivot;

    void Start()
    {
        pivot = new GameObject("OrbitPivot").transform;
        pivot.position = target.position;

        orbitingObject.SetParent(pivot, worldPositionStays: true);

        LeanTween.rotateAround(pivot.gameObject, orbitAxis, degreesToRotate, duration)
            .setRepeat(-1);
    }
}
