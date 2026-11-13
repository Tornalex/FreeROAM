using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Player player;

    [Header("Essence UI")]
    [SerializeField] private GameObject[] nuts;

    [Header("Essence UI")]
    [SerializeField] private Slider essenceSlider;
    [SerializeField] private TextMeshProUGUI essenceText;

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

        if (!Mathf.Approximately(currentEssence, _lastEssence) || !Mathf.Approximately(maxEssence, _lastMaxEssence))
        {
            _lastEssence = currentEssence;
            _lastMaxEssence = maxEssence;

            if (essenceSlider != null)
            {
                essenceSlider.maxValue = maxEssence;
                essenceSlider.value = currentEssence;
            }

            if (essenceText != null)
            {
                int essencePercentage = maxEssence > 0f
                    ? Mathf.RoundToInt((currentEssence / maxEssence) * 100f)
                    : 0;
                essenceText.text = $"Essence: {essencePercentage}%";
            }
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
