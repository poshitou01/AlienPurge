using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
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

    [Header("Experience Drop")]
    [SerializeField] private GameObject experienceOrbPrefab;
    [SerializeField] private int experienceAmount = 1;
    [SerializeField] private float dropRandomOffset = 0.15f;

    private bool isDead;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Coroutine hitFlashCoroutine;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        ValidateHealthSettings();

        currentHealth = maxHealth;
        isDead = false;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        else
        {
            Debug.LogWarning(
                $"{gameObject.name} 没有找到 SpriteRenderer，受击变色效果不会显示。"
            );
        }
    }

    /// <summary>
    /// 在敌人生成后初始化本次敌人的最大生命值。
    /// 当前生命值会同时恢复为新的最大生命值。
    /// </summary>
    public void InitializeHealth(int newMaxHealth)
    {
        if (newMaxHealth <= 0)
        {
            Debug.LogWarning(
                $"{gameObject.name} 收到了无效的最大生命值：{newMaxHealth}，已自动修正为 1。"
            );

            newMaxHealth = 1;
        }

        maxHealth = newMaxHealth;
        currentHealth = maxHealth;
        isDead = false;

        Debug.Log(
            $"{gameObject.name} 生命值初始化完成：{currentHealth}/{maxHealth}"
        );
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            Debug.LogWarning(
                $"{gameObject.name} 收到了无效伤害：{damage}"
            );

            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log(
            $"{gameObject.name} 受到 {damage} 点伤害，当前血量：{currentHealth}/{maxHealth}"
        );

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

        RegisterKillCount();

        Debug.Log($"{gameObject.name} 死亡");

        StartCoroutine(DeathRoutine());
    }

    private void RegisterKillCount()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterEnemyKilled();
        }
        else
        {
            Debug.LogWarning(
                "场景中没有找到 GameManager，无法增加击杀数。"
            );
        }
    }

    private IEnumerator DeathRoutine()
    {
        EnemyMovement enemyMovement = GetComponent<EnemyMovement>();

        if (enemyMovement != null)
        {
            enemyMovement.enabled = false;
        }

        EnemyContactDamage enemyContactDamage =
            GetComponent<EnemyContactDamage>();

        if (enemyContactDamage != null)
        {
            enemyContactDamage.enabled = false;
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

        DropExperienceOrb();

        yield return new WaitForSeconds(deathDelay);

        Destroy(gameObject);
    }

    private void DropExperienceOrb()
    {
        if (experienceOrbPrefab == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} 没有绑定 ExperienceOrb Prefab，无法掉落经验球。"
            );

            return;
        }

        Vector2 randomOffset =
            Random.insideUnitCircle * dropRandomOffset;

        Vector3 spawnPosition =
            transform.position +
            new Vector3(randomOffset.x, randomOffset.y, 0f);

        GameObject orb = Instantiate(
            experienceOrbPrefab,
            spawnPosition,
            Quaternion.identity
        );

        ExperienceOrb experienceOrb =
            orb.GetComponent<ExperienceOrb>();

        if (experienceOrb != null)
        {
            experienceOrb.SetExperienceAmount(experienceAmount);
        }
        else
        {
            Debug.LogWarning(
                "生成的经验球上没有找到 ExperienceOrb 脚本。"
            );
        }
    }

    private void ValidateHealthSettings()
    {
        if (maxHealth <= 0)
        {
            Debug.LogWarning(
                $"{gameObject.name} 的 maxHealth 小于等于 0，已自动修正为 1。"
            );

            maxHealth = 1;
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
        deathDelay = Mathf.Max(0f, deathDelay);
        experienceAmount = Mathf.Max(0, experienceAmount);
        dropRandomOffset = Mathf.Max(0f, dropRandomOffset);
    }

    [ContextMenu("Test Initialize Health To 6")]
    private void TestInitializeHealthTo6()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试敌人生命值初始化。"
            );

            return;
        }

        InitializeHealth(6);
    }
}