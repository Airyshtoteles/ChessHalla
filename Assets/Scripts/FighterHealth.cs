using UnityEngine;
using System;

// Health component used by arena fighters and bots
public class FighterHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;
    public bool destroyOnDeath = false;
    [Tooltip("Tampilkan log saat damage/tewas")] public bool debugLog = false;

    public bool IsAlive => currentHealth > 0;

    // Opsional: simpan tim untuk integrasi Arena
    public PieceTeam Team { get; private set; }

    // Event callback
    public Action OnDeath; // invoked when hp reaches 0
    public Action<int,int> OnHealthChanged; // current, max

    void Awake()
    {
        if (currentHealth <= 0) currentHealth = maxHealth;
    }

    public void Initialize(PieceTeam team)
    {
        Team = team;
    }

    public void ApplyDamage(int amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, amount));
        if (debugLog) Debug.Log($"[FighterHealth] {gameObject.name} -{amount} => {currentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0)
        {
            OnDeath?.Invoke();
            if (destroyOnDeath) Destroy(gameObject);
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ResetHP()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
