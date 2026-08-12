using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Statystyki Gracza")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    // Zdarzenie powiadamiające UI o zmianie HP (currentHealth, maxHealth)
    public static event Action<float, float> OnHealthChanged;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // Wywołanie startowe, aby UI zainicjalizowało pełne zdrowie
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    private bool hasWarnedLowHealth = false;
    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (currentHealth <= 0) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        
        // Powiadamiamy UI o aktualizacji HP
        OnHealthChanged?.Invoke(currentHealth, maxHealth);


        if (currentHealth <= 25f && currentHealth > 0 && !hasWarnedLowHealth) {
             hasWarnedLowHealth = true;
             NotificationManager.Show("Uwaga: Twój poziom zdrowia jest niski!", NotificationType.Warning);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}