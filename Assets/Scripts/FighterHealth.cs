using UnityEngine;

public class FighterHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool autoClamp = true;
    [SerializeField] private bool debugLog = false;

    public int CurrentHealth { get; private set; }
    public PieceTeam Team { get; private set; }

    public System.Action OnDeath;
    public System.Action<int,int> OnHealthChanged; // current, max

    private bool dead = false;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void Initialize(PieceTeam team)
    {
        Team = team;
    }

    public void ResetHealth()
    {
        dead = false;
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void ApplyDamage(int amount)
    {
        if (dead) return;
        if (amount <= 0) return;
        CurrentHealth -= amount;
        if (autoClamp && CurrentHealth < 0) CurrentHealth = 0;
        if (debugLog) Debug.Log($"[FighterHealth] {gameObject.name} took {amount} dmg -> {CurrentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth <= 0)
        {
            dead = true;
            if (debugLog) Debug.Log($"[FighterHealth] {gameObject.name} died");
            OnDeath?.Invoke();
        }
    }
}
