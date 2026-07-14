using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 5;

    [Header("Damage Feedback")]
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private float flashDuration = 0.12f;

    [Header("Death Feedback")]
    [SerializeField] private Color deathColor = Color.gray;
    [SerializeField] private float deathScaleMultiplier = 1.15f;

    private int currentHealth;
    private bool isDead;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private Rigidbody2D rb;
    private Collider2D[] colliders;

    private Vector3 originalScale;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;

        playerMovement = GetComponent<PlayerMovement>();
        playerShooting = GetComponent<PlayerShooting>();
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();

        originalScale = transform.localScale;

        currentHealth = maxHealth;
        isDead = false;

        Debug.Log($"Player Health Initialized: {currentHealth}/{maxHealth}");
    }

    private void Start()
    {
        RefreshHealthUI();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        flashDuration = Mathf.Max(0f, flashDuration);
        deathScaleMultiplier = Mathf.Max(0.01f, deathScaleMultiplier);
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log(
            $"Player took {damage} damage. Current HP: {currentHealth}/{maxHealth}"
        );

        RefreshHealthUI();
        PlayDamageFeedback();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 增加玩家最大生命值。
    /// 增加最大生命值时，同时恢复同等数量的生命值。
    /// </summary>
    public void IncreaseMaxHealth(int amount)
    {
        if (isDead)
        {
            return;
        }

        if (amount <= 0)
        {
            return;
        }

        maxHealth += amount;
        currentHealth += amount;

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log(
            $"Max health upgraded by {amount}. Current HP: {currentHealth}/{maxHealth}"
        );

        RefreshHealthUI();
    }

    /// <summary>
    /// 恢复玩家生命值，但不会超过最大生命值。
    /// </summary>
    public void RestoreHealth(int amount)
    {
        if (isDead)
        {
            return;
        }

        if (amount <= 0)
        {
            return;
        }

        int healthBeforeRestore = currentHealth;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        int actualRestoreAmount = currentHealth - healthBeforeRestore;

        Debug.Log(
            $"Player restored {actualRestoreAmount} health. "
            + $"Current HP: {currentHealth}/{maxHealth}"
        );

        RefreshHealthUI();
    }

    private void PlayDamageFeedback()
    {
        if (isDead)
        {
            return;
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        spriteRenderer.color = damageColor;

        yield return new WaitForSeconds(flashDuration);

        if (!isDead)
        {
            spriteRenderer.color = originalColor;
        }

        flashCoroutine = null;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log("Player died.");

        RefreshHealthUI();

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        spriteRenderer.color = deathColor;
        transform.localScale = originalScale * deathScaleMultiplier;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerShooting != null)
        {
            playerShooting.enabled = false;
        }

        foreach (Collider2D col in colliders)
        {
            if (col != null)
            {
                col.enabled = false;
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
    }

    private void RefreshHealthUI()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateHealthUI(currentHealth, maxHealth);
        }
    }

    [ContextMenu("Test Take 1 Damage")]
    private void TestTakeOneDamage()
    {
        TakeDamage(1);
    }

    [ContextMenu("Test Increase Max Health By 1")]
    private void TestIncreaseMaxHealth()
    {
        IncreaseMaxHealth(1);
    }

    [ContextMenu("Test Restore 2 Health")]
    private void TestRestoreHealth()
    {
        RestoreHealth(2);
    }
}