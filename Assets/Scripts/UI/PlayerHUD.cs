using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Player player;

    [Header("Health UI")]
    [SerializeField] private GameObject[] nuts;

    [Header("Essence UI")]
    [SerializeField] private GameObject[] essencePoints;

    [Header("Spear UI")]
    [SerializeField] private TextMeshProUGUI spearStateText;

    private int _lastHealth = -1;
    private int _lastMaxHealth = -1;
    private float _lastEssence = -1f;
    private float _lastMaxEssence = -1f;
    private PlayerCombat.SpearUIState _lastSpearState = PlayerCombat.SpearUIState.Ready;

    void Start()
    {
        if (player == null)
        {
#if UNITY_2023_1_OR_NEWER
            player = FindAnyObjectByType<Player>();
#else
            player = FindObjectOfType<Player>();
#endif
        }

        if (player == null)
        {
            Debug.LogError("PlayerHUD: Nessun componente 'Player' trovato nella scena!");
        }
    }

    void Update()
    {
        if (player == null) return;

        UpdateHealthUI();
        UpdateEssenceUI();
        UpdateSpearUI();
    }
private void UpdateHealthUI()
{
    if (player.Health == null) return;

    int currentHealth = player.Health.CurrentHealth;
    int maxHealth = player.Health.MaxHealth;

    if (currentHealth == _lastHealth &&
        maxHealth == _lastMaxHealth)
        return;

    _lastHealth = currentHealth;
    _lastMaxHealth = maxHealth;

    if (nuts == null) return;

    for (int i = 0; i < nuts.Length; i++)
    {
        if (nuts[i] == null) continue;
        nuts[i].SetActive(i < maxHealth && i < currentHealth);
    }
}

    private void UpdateEssenceUI()
    {
        if (player.Essence == null) return;

        float currentEssence = player.Essence.CurrentEssence;
        float maxEssence = player.Essence.MaxEssence;

        if (currentEssence == _lastEssence &&
            maxEssence == _lastMaxEssence)
            return;

        _lastEssence = currentEssence;
        _lastMaxEssence = maxEssence;

        if (essencePoints == null) return;

        for (int i = 0; i < essencePoints.Length; i++)
        {
            if (essencePoints[i] == null) continue;
            essencePoints[i].SetActive(i < maxEssence && i < currentEssence);
        }
    }

    private void UpdateSpearUI()
    {
        if (player.Combat == null) return;

        var currentState = player.Combat.CurrentSpearUIState;

        if (currentState != _lastSpearState)
        {
            _lastSpearState = currentState;

            if (spearStateText != null)
            {
                spearStateText.text = currentState switch
                {
                    PlayerCombat.SpearUIState.Ready => "Spear: Ready",
                    PlayerCombat.SpearUIState.Thrown => "Spear: Thrown",
                    PlayerCombat.SpearUIState.Returning => "Spear: Returning",
                    _ => "Spear: Ready"
                };
            }
        }
    }
}
