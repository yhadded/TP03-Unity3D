using System;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [SerializeField] private string displayName = "Character";
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int attackPower = 10;

    public string DisplayName => displayName;
    public int MaxHealth => maxHealth;
    public int AttackPower => attackPower;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public event Action<int, int> HealthChanged;
    public event Action Damaged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (IsDead) Died?.Invoke();
        else Damaged?.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
