using UnityEngine;

/// <summary>
/// Interaction target added at runtime by a <see cref="RoomTransition"/> whose
/// activation mode is <see cref="RoomTransitionActivationMode.Interact"/>.
/// Automatic doors never receive this component, so entering their trigger
/// does not register an interaction target or show a prompt.
/// </summary>
public class RoomTransitionInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptMessage = "INTERAGISCI";

    private RoomTransition _transition;

    private void Awake()
    {
        _transition = GetComponent<RoomTransition>();
    }

    public void Interact(Player player)
    {
        if (_transition == null)
            _transition = GetComponent<RoomTransition>();

        if (_transition != null)
            _transition.RequestTransition();
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }
}
