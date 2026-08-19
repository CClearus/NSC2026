using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int CurrentHealth;
    public int MaxHealth = 100;
    public bool AlDie = false;

    private void Start()
    {
        CurrentHealth = MaxHealth;
    }
    

    private void Update()
    {
        if (CurrentHealth == 0 && !AlDie)
        {
            Die();
        }
    }
    private void Die()
    {
        Debug.Log("Player Die ez");
        AlDie = true;
    }
}