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

        Debug.Log($"Player took {damage} damage. Current HP: {currentHealth}/{maxHealth}");

        PlayDamageFeedback();

        if (currentHealth <= 0)
        {
            Die();
        }
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
    }

    [ContextMenu("Test Take 1 Damage")]
    private void TestTakeOneDamage()
    {
        TakeDamage(1);
    }
}