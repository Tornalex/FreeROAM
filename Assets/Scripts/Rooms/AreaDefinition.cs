using UnityEngine;

[CreateAssetMenu(fileName = "AreaDefinition", menuName = "Project Spear/Rooms/Area Definition")]
public class AreaDefinition : ScriptableObject
{
    [SerializeField] private string title;
    [SerializeField] private string subtitle;
    [SerializeField] private bool showTitle = true;

    public string Title => title;
    public string Subtitle => subtitle;
    public bool ShowTitle => showTitle;
}
