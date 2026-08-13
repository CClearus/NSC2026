using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float hp = 100f;

    public void Damage(float damage)
    {
        hp -= damage;
        Debug.Log(hp);
    }
    void Update()
    {
        if (hp <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
