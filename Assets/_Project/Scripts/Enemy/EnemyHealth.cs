using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;

    [SerializeField] private int currentHealth;

    [Header("Hit Feedback")]
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Death Feedback")]
    [SerializeField] private Color deathColor = Color.red;
    [SerializeField] private float deathDelay = 0.15f;

    private bool isDead;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Coroutine hitFlashCoroutine;

    private void Awake()
    {
        if (maxHealth <= 0)
        {
            Debug.LogWarning($"{gameObject.name} 的 maxHealth 小于等于 0，已自动修正为 1。");
            maxHealth = 1;
        }

        currentHealth = maxHealth;
        isDead = false;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} 没有找到 SpriteRenderer，受击变色效果不会显示。");
        }
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

        PlayHitFeedback();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void PlayHitFeedback()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        spriteRenderer.color = hitColor;

        yield return new WaitForSeconds(hitFlashDuration);

        if (!isDead)
        {
            spriteRenderer.color = originalColor;
        }

        hitFlashCoroutine = null;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log($"{gameObject.name} 死亡");

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        EnemyMovement enemyMovement = GetComponent<EnemyMovement>();
        if (enemyMovement != null)
        {
            enemyMovement.enabled = false;
        }

        Collider2D enemyCollider = GetComponent<Collider2D>();
        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = deathColor;
            transform.localScale *= 1.2f;
        }

        yield return new WaitForSeconds(deathDelay);

        Destroy(gameObject);
    }
}