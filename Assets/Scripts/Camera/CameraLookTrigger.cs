using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraLookTrigger : MonoBehaviour
{
    [Header("Vertical Camera Offset")]
    [Tooltip("Signed vertical offset added to the Exploration Camera's base TargetOffset.y.")]
    [SerializeField] private float verticalOffset;

    [Tooltip("Smooth transition time, in seconds, when this trigger becomes active.")]
    [SerializeField, Min(0f)] private float enterTransitionDuration = 0.3f;

    [Tooltip("Smooth transition time, in seconds, when this trigger is exited.")]
    [SerializeField, Min(0f)] private float exitTransitionDuration = 0.3f;

    private readonly Dictionary<Player, HashSet<Collider2D>> _playerContacts =
        new Dictionary<Player, HashSet<Collider2D>>();

    private Collider2D _triggerCollider;
    private ExplorationCameraDirectionalLookahead _lookahead;
    private bool _isRegistered;
    private bool _hasWarnedMissingLookahead;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        if (_triggerCollider == null || !_triggerCollider.isTrigger)
        {
            Debug.LogError(
                "[CameraLookTrigger] An enabled Collider2D configured as a Trigger is required.",
                this);
            enabled = false;
            return;
        }

        ResolveLookahead();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrackPlayerCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Also reconciles contacts if a collider or trigger was re-enabled
        // while the player was already overlapping it.
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

        SynchronizeLookaheadRequest();
    }

    private void Update()
    {
        RemoveStaleContacts();
        SynchronizeLookaheadRequest();
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
        SynchronizeLookaheadRequest();
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

    private void SynchronizeLookaheadRequest()
    {
        bool hasPlayerContact = _playerContacts.Count > 0;

        if (_lookahead == null)
            ResolveLookahead();

        if (_isRegistered && _lookahead == null)
            _isRegistered = false;

        if (hasPlayerContact)
        {
            if (_isRegistered || _lookahead == null)
                return;

            _lookahead.RegisterVerticalLookTrigger(
                this,
                verticalOffset,
                enterTransitionDuration,
                exitTransitionDuration);
            _isRegistered = true;
            return;
        }

        if (!_isRegistered)
            return;

        if (_lookahead != null)
            _lookahead.UnregisterVerticalLookTrigger(this);

        _isRegistered = false;
    }

    private void ResolveLookahead()
    {
        if (_lookahead != null)
            return;

        _lookahead =
            FindFirstObjectByType<ExplorationCameraDirectionalLookahead>();

        if (_lookahead == null && !_hasWarnedMissingLookahead)
        {
            Debug.LogWarning(
                "[CameraLookTrigger] ExplorationCameraDirectionalLookahead was not found; " +
                "the trigger will retry while the player overlaps it.",
                this);
            _hasWarnedMissingLookahead = true;
        }
        else if (_lookahead != null)
        {
            _hasWarnedMissingLookahead = false;
        }
    }

    private void OnDisable()
    {
        if (_isRegistered && _lookahead != null)
            _lookahead.UnregisterVerticalLookTrigger(this);

        _isRegistered = false;
        _playerContacts.Clear();
    }

    private void OnValidate()
    {
        enterTransitionDuration = Mathf.Max(0f, enterTransitionDuration);
        exitTransitionDuration = Mathf.Max(0f, exitTransitionDuration);

        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                "[CameraLookTrigger] Its Collider2D must have Is Trigger enabled.",
                this);
        }
    }
}
