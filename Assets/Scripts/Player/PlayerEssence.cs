using UnityEngine;

// Runs before the other player action components so the healing state has
// priority over any action pressed on the same frame.
[DefaultExecutionOrder(-99)]
public class PlayerEssence : MonoBehaviour
{
    private float _currentEssence;
    private float _healTimer;
    private Player _player;

    public float CurrentEssence => _currentEssence;
    public float MaxEssence => _player.EssenceStats.maxEssence;

    void Awake()
    {
        _currentEssence = 0f;
        _player = GetComponent<Player>();
    }

    void Update()
    {
        // Healing is a timed hold state: a fresh press starts the channel and
        // the button must remain held until the configured duration elapses.
        if (_player.IsHealing)
        {
            if (!_player.HasControl || !_player.Input.IsHealHeld())
            {
                _player.EndHealing();
                _healTimer = 0f;
                return;
            }

            _healTimer += Time.deltaTime;

            if (_healTimer >= _player.HealStats.healDuration)
            {
                TryHeal();
                _player.EndHealing();
                _healTimer = 0f;
            }

            return;
        }

        if (!_player.HasControl || !_player.Input.HealTriggered)
            return;

        if (!CanHeal())
            return;

        _player.BeginHealing();
        _healTimer = 0f;
    }

    private void OnDisable()
    {
        // Never leave the player locked if this component is turned off.
        if (_player != null && _player.IsHealing)
            _player.EndHealing();

        _healTimer = 0f;
    }

    public void AddEssenceFromCatch(bool perfectCatch)
    {
        if (!_player.HasControl) return;

        float amountToAdd = perfectCatch ? _player.EssenceStats.essencePerPerfectCatch : _player.EssenceStats.essencePerBaseCatch;
        _currentEssence = Mathf.Clamp(_currentEssence + amountToAdd, 0f, _player.EssenceStats.maxEssence);
    }

    public bool ConsumeEssence(float amount)
    {
        // Negative costs would increase the runtime balance and bypass the
        // maximum-essence cap. No gameplay caller uses negative costs.
        if (amount < 0f)
            return false;

        if (_currentEssence >= amount)
        {
            _currentEssence -= amount;
            return true;
        }
        return false;
    }

    private bool CanHeal()
    {
        return _player.Health.CurrentHealth < _player.Health.MaxHealth &&
               _currentEssence >= _player.HealStats.essenceCostPerHeal;
    }

    private void TryHeal()
    {
        if (CanHeal())
        {
            _currentEssence -= _player.HealStats.essenceCostPerHeal;
            _player.Health.Heal(_player.HealStats.healthPerHeal);
        }
    }

    public void Refill()
    {
        _currentEssence = _player.EssenceStats.maxEssence;
    }

    public void ResetState()
    {
        if (_player != null && _player.IsHealing)
            _player.EndHealing();

        _healTimer = 0f;
        _currentEssence = 0f;
    }
}
