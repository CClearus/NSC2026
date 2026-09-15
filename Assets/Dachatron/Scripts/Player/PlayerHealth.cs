using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float MaxHealth = 100f;
    [Tooltip("Runtime value - use TakeDamage()/Heal()/SetHealth() to change it, don't edit directly.")]
    [SerializeField] private float currentHealth;

    [Header("On Death")]
    [Tooltip("Locked (SetMovementLocked(true)) as soon as health hits 0. Leave empty to auto-find on this GameObject.")]
    public PlayerMovement playerMovement;

    public float CurrentHealth => currentHealth;
    public float Health01 => MaxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / MaxHealth);
    public bool IsDead { get; private set; }

    private void Awake()
    {
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        currentHealth = MaxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        SetHealth(currentHealth - amount);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        SetHealth(currentHealth + amount);
    }

    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, MaxHealth);

        if (currentHealth <= 0f && !IsDead)
        {
            Die();
        }
        else if (currentHealth > 0f && IsDead)
        {
            Revive(); // e.g. a respawn/checkpoint calling SetHealth() after death
        }
    }

    private void Die()
    {
        IsDead = true;
        Debug.Log("Player died.");
        if (playerMovement != null) playerMovement.SetMovementLocked(true);
    }

    private void Revive()
    {
        IsDead = false;
        if (playerMovement != null) playerMovement.SetMovementLocked(false);
    }
}
