using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ExperienceOrb : MonoBehaviour
{
    [Header("Experience Settings")]
    [Tooltip("当前经验球提供的经验值")]
    [SerializeField] private int experienceAmount = 1;

    [Header("Debug Settings")]
    [Tooltip("是否在每次拾取时输出日志。性能测试时建议关闭")]
    [SerializeField] private bool logPickup;

    [Header("Runtime Debug")]
    [Tooltip("当前经验球是否已经被玩家拾取")]
    [SerializeField] private bool hasBeenCollected;

    [Tooltip("当前经验球是否已经被回收")]
    [SerializeField] private bool isReturned;

    [Tooltip("当前经验球是否由 ExperienceOrbPool 管理")]
    [SerializeField] private bool hasPool;

    private Collider2D orbCollider;
    private ExperienceOrbPool ownerPool;

    // 保存 Prefab 中配置的默认经验值。
    private int defaultExperienceAmount = 1;

    public int ExperienceAmount => experienceAmount;

    private void Awake()
    {
        orbCollider = GetComponent<Collider2D>();

        if (orbCollider == null)
        {
            Debug.LogError(
                "ExperienceOrb: Collider2D component was not found.",
                this
            );
        }

        defaultExperienceAmount =
            Mathf.Max(1, experienceAmount);

        ResetRuntimeState();
    }

    private void OnEnable()
    {
        // 每次从对象池中重新启用时，
        // 恢复可拾取状态和默认经验值。
        ResetRuntimeState();
    }

    /// <summary>
    /// 设置负责管理当前 ExperienceOrb 的对象池。
    /// </summary>
    public void SetPool(ExperienceOrbPool pool)
    {
        ownerPool = pool;
        hasPool = ownerPool != null;
    }

    /// <summary>
    /// 初始化一次刚从对象池取出的经验球。
    /// </summary>
    public void Initialize(int amount)
    {
        ResetRuntimeState();
        SetExperienceAmount(amount);
    }

    /// <summary>
    /// 设置当前经验球提供的经验值。
    /// 保留该方法以兼容现有 EnemyHealth 调用。
    /// </summary>
    public void SetExperienceAmount(int amount)
    {
        experienceAmount = Mathf.Max(1, amount);
    }

    /// <summary>
    /// 恢复经验球再次使用前的运行状态。
    /// </summary>
    private void ResetRuntimeState()
    {
        hasBeenCollected = false;
        isReturned = false;

        experienceAmount =
            Mathf.Max(1, defaultExperienceAmount);

        if (orbCollider != null)
        {
            orbCollider.enabled = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenCollected || isReturned)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        PlayerExperience playerExperience =
            other.GetComponentInParent<PlayerExperience>();

        if (playerExperience == null)
        {
            Debug.LogWarning(
                "ExperienceOrb touched Player, but "
                + "PlayerExperience component was not found.",
                other
            );

            return;
        }

        // 必须先锁定拾取状态，
        // 防止同一帧内重复触发经验增加。
        hasBeenCollected = true;

        if (orbCollider != null)
        {
            orbCollider.enabled = false;
        }

        playerExperience.AddExperience(experienceAmount);

        if (logPickup)
        {
            Debug.Log(
                "Player picked up Experience Orb. "
                + "Exp Amount: "
                + experienceAmount,
                this
            );
        }

        ReturnToPool();
    }

    /// <summary>
    /// 将经验球返回对象池。
    /// </summary>
    public void ReturnToPool()
    {
        // 碰撞回调或其他回收操作可能重复调用，
        // 使用 isReturned 作为第一层保护。
        if (isReturned)
        {
            return;
        }

        isReturned = true;

        if (orbCollider != null)
        {
            orbCollider.enabled = false;
        }

        if (ownerPool != null)
        {
            ownerPool.ReturnExperienceOrb(this);
        }
        else
        {
            // 下一步接入 EnemyHealth 前，
            // 非对象池生成的旧经验球只关闭，不再使用 Destroy。
            Debug.LogWarning(
                "ExperienceOrb: No owner pool was assigned. "
                + "The ExperienceOrb will be disabled.",
                this
            );

            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        experienceAmount =
            Mathf.Max(1, experienceAmount);
    }
}