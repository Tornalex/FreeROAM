using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using SpearWander.Abilities;
using Yarn.Unity;

[DefaultExecutionOrder(-100)]
public class Player : MonoBehaviour
{
    // --- Unity Standard Components ---
    public Rigidbody2D Rb { get; private set; }
    public Collider2D Collider { get; private set; }
    public SpriteRenderer Sprite { get; private set; }
    public Animator Animator { get; private set; }
    public CinemachineImpulseSource ImpulseSource { get; private set; }

    // --- Scene Services ---
    public DialogueRunner DialogueRunner { get; private set; }
    public GameOverUI GameOverUI { get; private set; }

    // --- Custom Player Components ---
    public PlayerInputHandler Input { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PlayerJump Jump { get; private set; }
    public PlayerDash Dash { get; private set; }
    public PlayerPogo Pogo { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public PlayerHealth Health { get; private set; }
    public Knockback Knockback { get; private set; }
    public PlayerRopeClimb RopeClimb { get; private set; }
    public PlayerEssence Essence { get; private set; }
    public PlayerFeet Feet { get; private set; }

    // --- HasControl System ---
    public enum ControlReason
    {
        Death,
        Respawn,
        Cutscene,
        Transition,
        Pickup,
        Pause,
        TextBox
    }
    private HashSet<ControlReason> _activeControlReasons = new();

    public bool HasControl => _activeControlReasons.Count == 0;

    public bool HasControlRequest(ControlReason reason) => _activeControlReasons.Contains(reason);

    public void AddControlRequest(ControlReason reason)
    {
        _activeControlReasons.Add(reason);

        if (RopeClimb != null && RopeClimb.IsClimbing)
            RopeClimb.StopClimbing();
    }

    public void RemoveControlRequest(ControlReason reason) => _activeControlReasons.Remove(reason);

    [Header("Scriptable Objects")]
    [SerializeField] private PlayerStatsData playerStats;
    [SerializeField] private PlayerCombatData combatStats;
    [SerializeField] private PlayerEssenceData essenceStats;
    [SerializeField] private PlayerDashData dashStats;
    [SerializeField] private PlayerRopeData ropeStats;
    [SerializeField] private PlayerPogoData pogoStats;

    public PlayerStatsData PlayerStats => playerStats;
    public PlayerCombatData CombatStats => combatStats;
    public PlayerEssenceData EssenceStats => essenceStats;
    public PlayerDashData DashStats => dashStats;
    public PlayerRopeData RopeStats => ropeStats;
    public PlayerPogoData PogoStats => pogoStats;

    void Awake()
    {
        // Cache Unity standard components
        Rb = GetComponent<Rigidbody2D>();
        Collider = GetComponent<Collider2D>();
        Sprite = GetComponentInChildren<SpriteRenderer>();
        Animator = GetComponentInChildren<Animator>();
        ImpulseSource = GetComponent<CinemachineImpulseSource>();
        DialogueRunner = FindFirstObjectByType<DialogueRunner>();
        GameOverUI = FindFirstObjectByType<GameOverUI>();

        ValidateConfiguration();

        // Cache custom components
        Input = GetComponent<PlayerInputHandler>();
        Movement = GetComponent<PlayerMovement>();
        Jump = GetComponent<PlayerJump>();
        Dash = GetComponent<PlayerDash>();
        Pogo = GetComponent<PlayerPogo>();
        Combat = GetComponent<PlayerCombat>();
        Health = GetComponent<PlayerHealth>();
        Knockback = GetComponent<Knockback>();
        RopeClimb = GetComponent<PlayerRopeClimb>();
        Essence = GetComponent<PlayerEssence>();
        Feet = GetComponentInChildren<PlayerFeet>();
    }

    private void ValidateConfiguration()
    {
        if (playerStats == null)
            Debug.LogError("[Player] PlayerStatsData is not assigned.", this);
        if (combatStats == null)
            Debug.LogError("[Player] PlayerCombatData is not assigned.", this);
        if (essenceStats == null)
            Debug.LogError("[Player] PlayerEssenceData is not assigned.", this);
        if (dashStats == null)
            Debug.LogError("[Player] PlayerDashData is not assigned.", this);
        if (ropeStats == null)
            Debug.LogError("[Player] PlayerRopeData is not assigned.", this);
        if (pogoStats == null)
            Debug.LogError("[Player] PlayerPogoData is not assigned.", this);
    }

    public void ResetState()
    {
        TextBoxUI.Instance?.Close();

        Health.ResetState();
        Essence.ResetState();
        Combat.ResetState();
        
        if (AbilityManager.Instance != null)
        {
            AbilityManager.Instance.RestoreAbilities(this);
        }
        
        _activeControlReasons.Clear();
    }
}
