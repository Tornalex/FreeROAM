using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraSwitchTrigger : MonoBehaviour
{
    [Header("Camera Switch")]
    [SerializeField] private RoomCameraType cameraType = RoomCameraType.Exploration;

    [Tooltip("Required when Camera Type is Fixed. Place the anchor in the same room scene as this trigger.")]
    [SerializeField] private Transform fixedCameraAnchor;

    private readonly Dictionary<Player, HashSet<Collider2D>> _playerContacts =
        new Dictionary<Player, HashSet<Collider2D>>();

    private Collider2D _triggerCollider;
    private CameraManager _cameraManager;
    private bool _isRegistered;
    private bool _hasWarnedMissingManager;
    private bool _hasWarnedInvalidConfiguration;
    private bool _hasWarnedRegistrationFailure;
    private RoomCameraType _registeredCameraType;
    private Transform _registeredFixedCameraAnchor;

    private void Awake()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        if (colliders.Length != 1 || !colliders[0].isTrigger)
        {
            Debug.LogError(
                "[CameraSwitchTrigger] Use exactly one Collider2D on this GameObject and enable Is Trigger.",
                this);
            enabled = false;
            return;
        }

        _triggerCollider = colliders[0];
        ResolveCameraManager();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrackPlayerCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Reconcile occupancy if a collider or this trigger was re-enabled
        // while the Player was already overlapping.
        TrackPlayerCollider(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
            return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null ||
            !_playerContacts.TryGetValue(player, out HashSet<Collider2D> colliders))
        {
            return;
        }

        colliders.Remove(other);
        if (colliders.Count == 0)
            _playerContacts.Remove(player);

        SynchronizeCameraRequest();
    }

    private void Update()
    {
        RemoveStaleContacts();
        SynchronizeCameraRequest();
    }

    private void TrackPlayerCollider(Collider2D other)
    {
        if (other == null)
            return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.isActiveAndEnabled)
            return;

        if (!_playerContacts.TryGetValue(player, out HashSet<Collider2D> colliders))
        {
            colliders = new HashSet<Collider2D>();
            _playerContacts.Add(player, colliders);
        }

        colliders.Add(other);
        SynchronizeCameraRequest();
    }

    private void RemoveStaleContacts()
    {
        if (_triggerCollider == null ||
            !_triggerCollider.enabled ||
            !_triggerCollider.gameObject.activeInHierarchy)
        {
            _playerContacts.Clear();
            return;
        }

        List<Player> playersToRemove = null;
        foreach (KeyValuePair<Player, HashSet<Collider2D>> pair in _playerContacts)
        {
            Player player = pair.Key;
            if (player == null || !player.isActiveAndEnabled)
            {
                playersToRemove ??= new List<Player>();
                playersToRemove.Add(player);
                continue;
            }

            pair.Value.RemoveWhere(collider =>
                collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy);

            if (pair.Value.Count == 0)
            {
                playersToRemove ??= new List<Player>();
                playersToRemove.Add(player);
            }
        }

        if (playersToRemove == null)
            return;

        for (int i = 0; i < playersToRemove.Count; i++)
            _playerContacts.Remove(playersToRemove[i]);
    }

    private void SynchronizeCameraRequest()
    {
        bool hasPlayerContact = _playerContacts.Count > 0;

        if (_cameraManager == null)
            ResolveCameraManager();

        if (_isRegistered && _cameraManager == null)
            _isRegistered = false;

        if (!hasPlayerContact)
        {
            RemoveCameraRequest();
            return;
        }

        if (!HasValidCameraConfiguration())
        {
            RemoveCameraRequest();
            return;
        }

        bool requestChanged =
            !_isRegistered ||
            _registeredCameraType != cameraType ||
            _registeredFixedCameraAnchor != fixedCameraAnchor;

        if (!requestChanged || _cameraManager == null)
            return;

        if (_cameraManager.RegisterCameraSwitchTrigger(
                this,
                cameraType,
                cameraType == RoomCameraType.Fixed ? fixedCameraAnchor : null))
        {
            _isRegistered = true;
            _registeredCameraType = cameraType;
            _registeredFixedCameraAnchor = fixedCameraAnchor;
            _hasWarnedRegistrationFailure = false;
            return;
        }

        RemoveCameraRequest();
        if (!_hasWarnedRegistrationFailure)
        {
            Debug.LogWarning(
                "[CameraSwitchTrigger] CameraManager rejected this camera switch request.",
                this);
            _hasWarnedRegistrationFailure = true;
        }
    }

    private bool HasValidCameraConfiguration()
    {
        if (cameraType != RoomCameraType.Fixed)
        {
            _hasWarnedInvalidConfiguration = false;
            return true;
        }

        bool validAnchor =
            fixedCameraAnchor != null &&
            gameObject.scene.IsValid() &&
            fixedCameraAnchor.gameObject.scene.IsValid() &&
            fixedCameraAnchor.gameObject.scene.handle == gameObject.scene.handle;

        if (!validAnchor && !_hasWarnedInvalidConfiguration)
        {
            Debug.LogError(
                "[CameraSwitchTrigger] Fixed Camera requires an anchor in the same room scene.",
                this);
            _hasWarnedInvalidConfiguration = true;
        }
        else if (validAnchor)
        {
            _hasWarnedInvalidConfiguration = false;
        }

        return validAnchor;
    }

    private void ResolveCameraManager()
    {
        if (_cameraManager != null)
            return;

        _cameraManager = FindAnyObjectByType<CameraManager>();
        if (_cameraManager == null && !_hasWarnedMissingManager)
        {
            Debug.LogWarning(
                "[CameraSwitchTrigger] CameraManager was not found; the trigger will retry while the Player overlaps it.",
                this);
            _hasWarnedMissingManager = true;
        }
        else if (_cameraManager != null)
        {
            _hasWarnedMissingManager = false;
        }
    }

    private void RemoveCameraRequest()
    {
        if (!_isRegistered)
            return;

        if (_cameraManager != null)
            _cameraManager.UnregisterCameraSwitchTrigger(this);

        _isRegistered = false;
    }

    private void OnDisable()
    {
        RemoveCameraRequest();
        _playerContacts.Clear();
    }

    private void OnValidate()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        if (colliders.Length > 0 &&
            (colliders.Length != 1 || !colliders[0].isTrigger))
        {
            Debug.LogWarning(
                "[CameraSwitchTrigger] Use exactly one Collider2D on this GameObject and enable Is Trigger.",
                this);
        }

        if (cameraType == RoomCameraType.Fixed &&
            fixedCameraAnchor != null &&
            gameObject.scene.IsValid() &&
            fixedCameraAnchor.gameObject.scene.IsValid() &&
            fixedCameraAnchor.gameObject.scene.handle != gameObject.scene.handle)
        {
            Debug.LogWarning(
                "[CameraSwitchTrigger] Fixed Camera Anchor should belong to the same room scene as the trigger.",
                this);
        }
    }
}
