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
    [Tooltip("敌人死亡后掉落的经验值")]
    [SerializeField] private int experienceAmount = 1;

    [Tooltip("经验球相对于敌人死亡位置的随机偏移")]
    [SerializeField] private float dropRandomOffset = 0.15f;

    private bool isDead;
    private bool hasRegisteredKill;
    private bool hasDroppedExperience;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private Coroutine hitFlashCoroutine;
    private Coroutine deathCoroutine;

    private PooledEnemy pooledEnemy;

    private Collider2D[] enemyColliders;
    private Vector3 scaleBeforeDeath;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int ExperienceAmount => experienceAmount;
    public bool IsDead => isDead;

    private void Awake()
    {
        ValidateHealthSettings();

        currentHealth = maxHealth;

        isDead = false;
        hasRegisteredKill = false;
        hasDroppedExperience = false;

        deathCoroutine = null;

        pooledEnemy =
            GetComponent<PooledEnemy>();

        enemyColliders =
    GetComponents<Collider2D>();

        scaleBeforeDeath =
            transform.localScale;

        spriteRenderer =
            GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        else
        {
            Debug.LogWarning(
                gameObject.name
                + " 没有找到 SpriteRenderer，"
                + "受击变色效果不会显示。",
                this
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
                gameObject.name
                + " 收到了无效的最大生命值："
                + newMaxHealth
                + "，已自动修正为 1。",
                this
            );

            newMaxHealth = 1;
        }

        maxHealth = newMaxHealth;
        currentHealth = maxHealth;
        isDead = false;

        Debug.Log(
            gameObject.name
            + " 生命值初始化完成："
            + currentHealth
            + "/"
            + maxHealth,
            this
        );
    }

    /// <summary>
    /// 在敌人生成后初始化该敌人死亡时提供的经验值。
    /// </summary>
    public void InitializeExperienceAmount(
        int newExperienceAmount
    )
    {
        if (newExperienceAmount <= 0)
        {
            Debug.LogWarning(
                gameObject.name
                + " 收到了无效经验值："
                + newExperienceAmount
                + "，已自动修正为 1。",
                this
            );

            newExperienceAmount = 1;
        }

        experienceAmount = newExperienceAmount;

        Debug.Log(
            gameObject.name
            + " 经验掉落初始化完成："
            + experienceAmount,
            this
        );
    }

    /// <summary>
    /// 初始化敌人的常态显示颜色。
    ///
    /// 同时更新 originalColor，确保敌人受击闪白结束后，
    /// 能恢复到当前敌人类型对应的颜色，
    /// 而不是恢复到 Prefab 原来的颜色。
    /// </summary>
    public void InitializeVisualColor(
        Color newVisualColor
    )
    {
        if (spriteRenderer == null)
        {
            Debug.LogWarning(
                gameObject.name
                + " 没有找到 SpriteRenderer，"
                + "无法初始化敌人颜色。",
                this
            );

            return;
        }

        originalColor = newVisualColor;
        spriteRenderer.color = originalColor;

        Debug.Log(
            gameObject.name
            + " 视觉颜色初始化完成。",
            this
        );
    }

    /// <summary>
    /// 每次从 EnemyPool 取出时重置生命组件状态。
    ///
    /// 最新最大生命值和类型颜色随后仍会由
    /// EnemyDefinition 重新初始化。
    /// </summary>
    public void PrepareForSpawn()
    {
        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }

        isDead = false;
        hasRegisteredKill = false;
        hasDroppedExperience = false;

        currentHealth = maxHealth;

        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                originalColor;
        }

        transform.localScale =
            scaleBeforeDeath;

        if (enemyColliders == null
            || enemyColliders.Length == 0)
        {
            enemyColliders =
                GetComponents<Collider2D>();
        }

        for (int i = 0;
            i < enemyColliders.Length;
            i++)
        {
            if (enemyColliders[i] != null)
            {
                enemyColliders[i].enabled = true;
            }
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
            Debug.LogWarning(
                gameObject.name
                + " 收到了无效伤害："
                + damage,
                this
            );

            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log(
            gameObject.name
            + " 受到 "
            + damage
            + " 点伤害，当前血量："
            + currentHealth
            + "/"
            + maxHealth,
            this
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

        hitFlashCoroutine =
            StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        spriteRenderer.color = hitColor;

        yield return new WaitForSeconds(
            hitFlashDuration
        );

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

        if (!hasRegisteredKill)
        {
            hasRegisteredKill = true;
            RegisterKillCount();
        }

        Debug.Log(
            gameObject.name + " 死亡",
            this
        );

        if (deathCoroutine == null)
        {
            deathCoroutine =
                StartCoroutine(DeathRoutine());
        }
    }
    /// <summary>
    /// 向 GameManager 登记击杀数量。
    /// </summary>
    private void RegisterKillCount()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterEnemyKilled();
        }
        else
        {
            Debug.LogWarning(
                "场景中没有找到 GameManager，"
                + "无法增加击杀数。",
                this
            );
        }
    }

    private IEnumerator DeathRoutine()
    {
        EnemyMovement enemyMovement =
            GetComponent<EnemyMovement>();

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

        if (enemyColliders == null
            || enemyColliders.Length == 0)
        {
            enemyColliders =
                GetComponents<Collider2D>();
        }

        for (int i = 0;
            i < enemyColliders.Length;
            i++)
        {
            if (enemyColliders[i] != null)
            {
                enemyColliders[i].enabled = false;
            }
        }

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        scaleBeforeDeath =
            transform.localScale;

        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                deathColor;
        }

        transform.localScale =
            scaleBeforeDeath * 1.2f;

        if (!hasDroppedExperience)
        {
            hasDroppedExperience = true;
            DropExperienceOrb();
        }

        yield return new WaitForSeconds(
            deathDelay
        );

        deathCoroutine = null;

        if (pooledEnemy != null
            && pooledEnemy.HasOwningPool)
        {
            pooledEnemy.ReturnToPool();
            yield break;
        }

        Debug.LogWarning(
            gameObject.name
            + " 死亡时没有可用的 EnemyPool，"
            + "将使用 Destroy 兼容处理。",
            this
        );

        Destroy(gameObject);
    }

    /// <summary>
    /// 从 ExperienceOrbPool 获取经验球。
    /// 不再直接 Instantiate ExperienceOrb Prefab。
    /// </summary>
    private void DropExperienceOrb()
    {
        if (ExperienceOrbPool.Instance == null)
        {
            Debug.LogWarning(
                gameObject.name
                + " 死亡时没有找到 ExperienceOrbPool，"
                + "无法掉落经验球。",
                this
            );

            return;
        }

        Vector2 randomOffset =
            Random.insideUnitCircle
            * dropRandomOffset;

        Vector3 spawnPosition =
            transform.position
            + new Vector3(
                randomOffset.x,
                randomOffset.y,
                0f
            );

        ExperienceOrb experienceOrb =
            ExperienceOrbPool.Instance.GetExperienceOrb(
                spawnPosition,
                Quaternion.identity,
                experienceAmount
            );

        if (experienceOrb == null)
        {
            Debug.LogWarning(
                gameObject.name
                + " 未能从 ExperienceOrbPool "
                + "取得经验球。",
                this
            );
        }
    }

    private void ValidateHealthSettings()
    {
        if (maxHealth <= 0)
        {
            Debug.LogWarning(
                gameObject.name
                + " 的 maxHealth 小于等于 0，"
                + "已自动修正为 1。",
                this
            );

            maxHealth = 1;
        }

        experienceAmount =
            Mathf.Max(1, experienceAmount);
    }

    private void OnValidate()
    {
        maxHealth =
            Mathf.Max(1, maxHealth);

        hitFlashDuration =
            Mathf.Max(0f, hitFlashDuration);

        deathDelay =
            Mathf.Max(0f, deathDelay);

        experienceAmount =
            Mathf.Max(1, experienceAmount);

        dropRandomOffset =
            Mathf.Max(0f, dropRandomOffset);
    }

    [ContextMenu("Test Initialize Health To 6")]
    private void TestInitializeHealthTo6()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试敌人生命值初始化。",
                this
            );

            return;
        }

        InitializeHealth(6);
    }

    [ContextMenu("Test Initialize Experience To 2")]
    private void TestInitializeExperienceTo2()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试敌人经验值初始化。",
                this
            );

            return;
        }

        InitializeExperienceAmount(2);
    }

    [ContextMenu("Test Initialize Visual Color To Cyan")]
    private void TestInitializeVisualColorToCyan()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试敌人颜色初始化。",
                this
            );

            return;
        }

        InitializeVisualColor(Color.cyan);
    }
}