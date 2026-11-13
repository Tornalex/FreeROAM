using UnityEngine;

public enum RoomTransitionActivationMode
{
    Automatic = 0,
    Interact = 1
}

public class RoomTransition : MonoBehaviour
{
    [Header("Questa Porta")]
    [Tooltip("L'ID univoco di QUESTA porta all'interno di questa stanza (es: Porta_A, Porta_B)")]
    [SerializeField] private string doorID;

    [Header("Spawn")]
    [Tooltip("Child della porta dove il player appare quando arriva in questa stanza")]
    [SerializeField] private Transform _spawnPoint;

    [Header("Destinazione")]
    [Tooltip("Il nome esatto della scena Unity da caricare")]
    [SerializeField] private string targetSceneName;
    [Tooltip("L'ID della porta su cui il player deve apparire nella prossima scena")]
    [SerializeField] private string targetDoorID;

    [Header("Room Connectivity")]
    [Tooltip("Identità stabile della porta usata dagli endpoint di RoomConnection. Deve essere univoca nella stanza.")]
    [SerializeField] private string doorId;
    [Tooltip("Connessione che possiede questa porta. Assegnata dal creatore di connessioni.")]
    [SerializeField] private RoomConnection connection;

    [Header("Activation")]
    [Tooltip("Automatic: la transizione parte entrando nel trigger. Interact: richiede l'interazione del player.")]
    [SerializeField] private RoomTransitionActivationMode activationMode = RoomTransitionActivationMode.Automatic;

    public string DoorID => doorID;
    public Transform SpawnPoint => _spawnPoint;
    public string TargetSceneName => targetSceneName;
    public string TargetDoorID => targetDoorID;

    public string DoorId => doorId;
    public RoomConnection Connection => connection;
    public RoomTransitionActivationMode ActivationMode => activationMode;

    private void Awake()
    {
        if (activationMode != RoomTransitionActivationMode.Interact)
            return;

        if (GetComponent<RoomTransitionInteractable>() == null)
            gameObject.AddComponent<RoomTransitionInteractable>();
    }

    public void RequestTransition()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.RequestRoomTransition(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (activationMode != RoomTransitionActivationMode.Automatic)
            return;

        if (collision.CompareTag("Player"))
        {
            RequestTransition();
        }
    }
}
