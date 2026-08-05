using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
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

    private bool isTemporarilyInvulnerable;
    private Coroutine invulnerabilityCoroutine;

    public bool IsTemporarilyInvulnerable =>
        isTemporarilyInvulnerable;

    [Header("Visual References")]
    [Tooltip("负责显示玩家身体并接收受伤、死亡颜色反馈")]
    [SerializeField]
    private SpriteRenderer bodySpriteRenderer;

    [Tooltip("负责玩家视觉位移和缩放，不包含物理组件与 Shadow")]
    [SerializeField]
    private Transform visualRoot;

    private Color originalColor;
    private Coroutine flashCoroutine;

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private Rigidbody2D rb;
    private Collider2D[] colliders;

    private Vector3 originalVisualScale;

    // 当前生命状态只读接口
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // 当前是否可以有效恢复生命值
    public bool CanRestoreHealth =>
        !isDead && currentHealth < maxHealth;

    private void Awake()
    {
        if (bodySpriteRenderer == null)
        {
            bodySpriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (bodySpriteRenderer == null)
        {
            bodySpriteRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (bodySpriteRenderer == null)
        {
            Debug.LogError(
                "PlayerHealth 找不到负责玩家身体显示的 SpriteRenderer。",
                this
            );

            enabled = false;
            return;
        }

        originalColor = bodySpriteRenderer.color;

        playerMovement = GetComponent<PlayerMovement>();
        playerShooting = GetComponent<PlayerShooting>();
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();

        originalVisualScale = visualRoot.localScale;

        currentHealth = maxHealth;
        isDead = false;

        isTemporarilyInvulnerable = false;
        invulnerabilityCoroutine = null;

        Debug.Log(
            $"Player Health Initialized: {currentHealth}/{maxHealth}"
        );
    }

    private void Start()
    {
        RefreshHealthUI();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        flashDuration = Mathf.Max(0f, flashDuration);

        deathScaleMultiplier =
            Mathf.Max(0.01f, deathScaleMultiplier);
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (isTemporarilyInvulnerable)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHealth -= damage;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0,
            maxHealth
        );

        Debug.Log(
            $"Player took {damage} damage. "
            + $"Current HP: {currentHealth}/{maxHealth}"
        );

        RefreshHealthUI();

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        PlayDamageFeedback();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlayerHurt();
        }

        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.PlayLightShake();
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

        currentHealth = Mathf.Clamp(
            currentHealth,
            0,
            maxHealth
        );

        Debug.Log(
            $"Max health upgraded by {amount}. "
            + $"Current HP: {currentHealth}/{maxHealth}"
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

        currentHealth = Mathf.Clamp(
            currentHealth,
            0,
            maxHealth
        );

        int actualRestoreAmount =
            currentHealth - healthBeforeRestore;

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

        flashCoroutine =
            StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        bodySpriteRenderer.color = damageColor;

        yield return new WaitForSeconds(flashDuration);

        if (!isDead)
        {
            bodySpriteRenderer.color = originalColor;
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

        CancelTemporaryInvulnerability();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlayerDeath();
        }

        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.PlayHeavyShake();
        }

        Debug.Log("Player died.");

        RefreshHealthUI();

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        bodySpriteRenderer.color = deathColor;

        visualRoot.localScale =
    originalVisualScale * deathScaleMultiplier;

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


    /// <summary>
    /// 让玩家在指定游戏时间内暂时免疫伤害。
    /// 使用缩放时间，所以暂停和升级期间计时停止。
    /// </summary>
    public void BeginTemporaryInvulnerability(
        float duration
    )
    {
        if (isDead || duration <= 0f)
        {
            return;
        }

        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(invulnerabilityCoroutine);
        }

        invulnerabilityCoroutine =
            StartCoroutine(
                TemporaryInvulnerabilityRoutine(
                    duration
                )
            );
    }

    private IEnumerator
        TemporaryInvulnerabilityRoutine(
            float duration
        )
    {
        isTemporarilyInvulnerable = true;

        yield return new WaitForSeconds(duration);

        isTemporarilyInvulnerable = false;
        invulnerabilityCoroutine = null;
    }

    /// <summary>
    /// 立即结束临时无敌。
    /// 用于冲刺中断、死亡或组件禁用。
    /// </summary>
    public void CancelTemporaryInvulnerability()
    {
        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(invulnerabilityCoroutine);
            invulnerabilityCoroutine = null;
        }

        isTemporarilyInvulnerable = false;
    }
    private void RefreshHealthUI()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateHealthUI(
                currentHealth,
                maxHealth
            );
        }
    }

    /// <summary>
    /// 输出当前生命值和生命恢复有效性。
    /// 仅用于本阶段调试。
    /// </summary>
    [ContextMenu("Debug/Print Current Health State")]
    private void PrintCurrentHealthState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请先进入 Play Mode，"
                + "再检查运行时生命状态。",
                this
            );

            return;
        }

        Debug.Log(
            "===== Current Health State =====\n"
            + "Current Health: "
            + currentHealth
            + "\nMax Health: "
            + maxHealth
            + "\nIs Dead: "
            + isDead
            + "\nCan Restore Health: "
            + CanRestoreHealth,
            this
        );
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

    private void OnDisable()
    {
        CancelTemporaryInvulnerability();
    }
}