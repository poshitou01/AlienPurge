using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    [Header("Contact Damage Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float damageInterval = 1f;

    private float nextDamageTime;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other.gameObject);
    }

    private void TryDamagePlayer(GameObject target)
    {
        if (Time.time < nextDamageTime)
        {
            return;
        }

        if (!target.CompareTag("Player"))
        {
            return;
        }

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogWarning("Enemy touched Player, but PlayerHealth was not found.");
            return;
        }

        if (playerHealth.IsDead)
        {
            return;
        }

        playerHealth.TakeDamage(damage);

        nextDamageTime = Time.time + damageInterval;
    }
}