using UnityEngine;

/// <summary>
/// Authoring data for the boss tongue lunge: telegraph, extend, retract,
/// recovery, reach and the runtime line visuals.
/// </summary>
[CreateAssetMenu(menuName = "Spear Wander/Boss/Attacks/Tongue")]
public class TongueAttackData : BossAttackData
{
    [Header("Timing")]
    [SerializeField, Min(0f)]
    [Tooltip("Telegraph before the tongue extends.")]
    private float windupTime = 0.45f;

    [SerializeField, Min(0f)]
    [Tooltip("Time the tongue takes to reach full extension.")]
    private float extendTime = 0.12f;

    [SerializeField, Min(0f)]
    [Tooltip("Time the tongue takes to retract.")]
    private float retractTime = 0.22f;

    [SerializeField, Min(0f)]
    [Tooltip("Recovery before the next action decision.")]
    private float recoveryTime = 0.3f;

    [Header("Hitbox")]
    [SerializeField, Min(0f)]
    [Tooltip("Maximum reach of the tongue.")]
    private float range = 6f;

    [SerializeField, Min(0.02f)]
    [Tooltip("Thickness of the tongue hitbox and visual.")]
    private float width = 0.4f;

    [Header("Visuals")]
    [SerializeField]
    [Tooltip("Color of the tongue line renderer.")]
    private Color tongueColor = new Color(0.9f, 0.3f, 0.4f, 1f);

    [SerializeField]
    [Tooltip("Sorting order of the tongue line renderer.")]
    private int sortingOrder = 10;

    public float WindupTime => windupTime;
    public float ExtendTime => extendTime;
    public float RetractTime => retractTime;
    public float RecoveryTime => recoveryTime;
    public float Range => range;
    public float Width => width;
    public Color TongueColor => tongueColor;
    public int SortingOrder => sortingOrder;

    protected override void OnValidate()
    {
        base.OnValidate();

        windupTime = Mathf.Max(0f, windupTime);
        extendTime = Mathf.Max(0f, extendTime);
        retractTime = Mathf.Max(0f, retractTime);
        recoveryTime = Mathf.Max(0f, recoveryTime);
        range = Mathf.Max(0f, range);
        width = Mathf.Max(0.02f, width);
    }
}
