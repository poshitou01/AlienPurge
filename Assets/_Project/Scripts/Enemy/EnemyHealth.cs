using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;

    [SerializeField] private int currentHealth;

    private bool isDead;

    private void Awake()
    {
        if (maxHealth <= 0)
        {
            Debug.LogWarning($"{gameObject.name} 的 maxHealth 小于等于 0，已自动修正为 1。");
            maxHealth = 1;
        }

        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            Debug.LogWarning($"{gameObject.name} 收到了无效伤害：{damage}");
            return;
        }

        currentHealth -= damage;

        Debug.Log($"{gameObject.name} 受到 {damage} 点伤害，当前血量：{currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log($"{gameObject.name} 死亡");

        Destroy(gameObject);
    }
}