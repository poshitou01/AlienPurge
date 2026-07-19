using System.Collections.Generic;
using UnityEngine;

public class HitEffectPool : MonoBehaviour
{
    public static HitEffectPool Instance { get; private set; }
    [Header("Pool Settings")]
    [Tooltip("对象池使用的 HitEffect Prefab")]
    [SerializeField] private HitEffect hitEffectPrefab;

    [Tooltip("游戏开始时预生成的 HitEffect 数量")]
    [SerializeField] private int initialPoolSize = 20;

    [Tooltip("池中没有可用 HitEffect 时是否允许自动扩容")]
    [SerializeField] private bool allowExpansion = true;

    [Tooltip("每次扩容时增加的 HitEffect 数量")]
    [SerializeField] private int expansionAmount = 5;

    [Header("Runtime Debug")]
    [Tooltip("对象池目前创建的 HitEffect 总数量")]
    [SerializeField] private int totalCount;

    [Tooltip("当前正在播放的 HitEffect 数量")]
    [SerializeField] private int activeCount;

    [Tooltip("当前可供使用的 HitEffect 数量")]
    [SerializeField] private int availableCount;

    // 存放当前可用 HitEffect 的队列。
    private readonly Queue<HitEffect> availableHitEffects =
        new Queue<HitEffect>();

    // 记录已经位于可用队列中的 HitEffect，
    // 用于防止重复回收。
    private readonly HashSet<HitEffect> availableHitEffectLookup =
        new HashSet<HitEffect>();

    // 记录当前正在使用的 HitEffect。
    private readonly HashSet<HitEffect> activeHitEffectLookup =
        new HashSet<HitEffect>();

    // 记录所有由当前对象池创建的 HitEffect。
    private readonly HashSet<HitEffect> allHitEffectLookup =
        new HashSet<HitEffect>();

    private Transform availableContainer;
    private Transform activeContainer;

    public int TotalCount => totalCount;
    public int ActiveCount => activeCount;
    public int AvailableCount => availableCount;

    private void Awake()
    {
        // 当前场景只允许存在一个 HitEffectPool。
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "HitEffectPool: More than one HitEffectPool "
                + "exists in the current scene.",
                this
            );

            enabled = false;
            return;
        }

        Instance = this;

        CreateRuntimeContainers();
        PrewarmPool();
        RefreshDebugCounts();
    }

    /// <summary>
    /// 创建运行时对象层级。
    /// </summary>
    private void CreateRuntimeContainers()
    {
        GameObject availableContainerObject =
            new GameObject("AvailableHitEffects");

        availableContainerObject.transform.SetParent(
            transform,
            false
        );

        availableContainer =
            availableContainerObject.transform;

        // 整个可用容器保持关闭状态，
        // 池中的特效不会执行 Update。
        availableContainerObject.SetActive(false);

        GameObject activeContainerObject =
            new GameObject("ActiveHitEffects");

        activeContainerObject.transform.SetParent(
            transform,
            false
        );

        activeContainer =
            activeContainerObject.transform;
    }

    /// <summary>
    /// 游戏开始时预生成 HitEffect。
    /// </summary>
    private void PrewarmPool()
    {
        if (hitEffectPrefab == null)
        {
            Debug.LogError(
                "HitEffectPool: HitEffect Prefab has not been assigned.",
                this
            );

            return;
        }

        CreateHitEffects(
            Mathf.Max(1, initialPoolSize)
        );
    }

    /// <summary>
    /// 创建指定数量的 HitEffect，并放入可用队列。
    /// </summary>
    private void CreateHitEffects(int amount)
    {
        if (hitEffectPrefab == null)
        {
            Debug.LogError(
                "HitEffectPool: Cannot create HitEffects because "
                + "HitEffect Prefab has not been assigned.",
                this
            );

            return;
        }

        amount = Mathf.Max(1, amount);

        for (int i = 0; i < amount; i++)
        {
            HitEffect hitEffect = Instantiate(
                hitEffectPrefab,
                availableContainer
            );

            hitEffect.SetPool(this);

            hitEffect.name =
                hitEffectPrefab.name
                + "_Pooled_"
                + allHitEffectLookup.Count;

            hitEffect.gameObject.SetActive(false);

            availableHitEffects.Enqueue(hitEffect);
            availableHitEffectLookup.Add(hitEffect);
            allHitEffectLookup.Add(hitEffect);
        }

        RefreshDebugCounts();
    }

    /// <summary>
    /// 从对象池中取得一个 HitEffect。
    /// </summary>
    public HitEffect GetHitEffect(
        Vector3 spawnPosition,
        Quaternion spawnRotation)
    {
        HitEffect hitEffect =
            GetNextAvailableHitEffect();

        if (hitEffect == null)
        {
            Debug.LogWarning(
                "HitEffectPool: No HitEffect is currently available.",
                this
            );

            return null;
        }

        if (!allHitEffectLookup.Contains(hitEffect))
        {
            Debug.LogError(
                "HitEffectPool: An unregistered HitEffect was "
                + "found in the available queue.",
                this
            );

            return null;
        }

        availableHitEffectLookup.Remove(hitEffect);

        if (!activeHitEffectLookup.Add(hitEffect))
        {
            Debug.LogError(
                "HitEffectPool: Attempted to get an already "
                + "active HitEffect: "
                + hitEffect.name,
                hitEffect
            );

            availableHitEffects.Enqueue(hitEffect);
            availableHitEffectLookup.Add(hitEffect);

            RefreshDebugCounts();
            return null;
        }

        // 确保设置位置和层级时不会提前执行 Update。
        hitEffect.gameObject.SetActive(false);

        hitEffect.transform.SetParent(
            activeContainer,
            false
        );

        hitEffect.transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        hitEffect.gameObject.SetActive(true);

        RefreshDebugCounts();

        return hitEffect;
    }

    /// <summary>
    /// 获取下一项可用 HitEffect。
    /// 池为空时根据配置决定是否扩容。
    /// </summary>
    private HitEffect GetNextAvailableHitEffect()
    {
        while (availableHitEffects.Count > 0)
        {
            HitEffect hitEffect =
                availableHitEffects.Dequeue();

            if (hitEffect != null)
            {
                return hitEffect;
            }
        }

        if (!allowExpansion)
        {
            RefreshDebugCounts();
            return null;
        }

        CreateHitEffects(
            Mathf.Max(1, expansionAmount)
        );

        if (availableHitEffects.Count <= 0)
        {
            return null;
        }

        return availableHitEffects.Dequeue();
    }

    /// <summary>
    /// 将 HitEffect 放回对象池。
    /// </summary>
    public void ReturnHitEffect(HitEffect hitEffect)
    {
        if (hitEffect == null)
        {
            return;
        }

        // 只接受由当前对象池创建的对象。
        if (!allHitEffectLookup.Contains(hitEffect))
        {
            Debug.LogError(
                "HitEffectPool: Attempted to return a HitEffect "
                + "that does not belong to this pool: "
                + hitEffect.name,
                hitEffect
            );

            return;
        }

        // 第二层重复回收保护。
        if (availableHitEffectLookup.Contains(hitEffect))
        {
            Debug.LogWarning(
                "HitEffectPool: Attempted to return the same "
                + "HitEffect twice: "
                + hitEffect.name,
                hitEffect
            );

            return;
        }

        if (!activeHitEffectLookup.Remove(hitEffect))
        {
            Debug.LogWarning(
                "HitEffectPool: Returned HitEffect was not "
                + "recorded as active: "
                + hitEffect.name,
                hitEffect
            );
        }

        hitEffect.gameObject.SetActive(false);

        hitEffect.transform.SetParent(
            availableContainer,
            false
        );

        availableHitEffects.Enqueue(hitEffect);
        availableHitEffectLookup.Add(hitEffect);

        RefreshDebugCounts();
    }

    /// <summary>
    /// 更新 Inspector 中的调试数量。
    /// </summary>
    private void RefreshDebugCounts()
    {
        totalCount = allHitEffectLookup.Count;
        activeCount = activeHitEffectLookup.Count;
        availableCount = availableHitEffects.Count;
    }

    [ContextMenu("Debug/Print Pool Status")]
    private void PrintPoolStatus()
    {
        RefreshDebugCounts();

        Debug.Log(
            "===== HitEffect Pool Status =====\n"
            + "Total Count: "
            + totalCount
            + "\nActive Count: "
            + activeCount
            + "\nAvailable Count: "
            + availableCount
            + "\nAllow Expansion: "
            + allowExpansion
            + "\nExpansion Amount: "
            + expansionAmount,
            this
        );
    }

    /// <summary>
    /// 检查队列、集合、数量和 Hierarchy 是否一致。
    /// </summary>
    [ContextMenu("Debug/Validate Pool State")]
    private void ValidatePoolState()
    {
        RefreshDebugCounts();

        int availableLookupCount =
            availableHitEffectLookup.Count;

        int activeLookupCount =
            activeHitEffectLookup.Count;

        int availableHierarchyCount =
            availableContainer != null
                ? availableContainer.childCount
                : -1;

        int activeHierarchyCount =
            activeContainer != null
                ? activeContainer.childCount
                : -1;

        bool totalCountIsValid =
            totalCount ==
            activeCount + availableCount;

        bool availableRecordsAreValid =
            availableCount ==
            availableLookupCount;

        bool activeRecordsAreValid =
            activeCount ==
            activeLookupCount;

        bool hierarchyIsValid =
            availableHierarchyCount == availableCount
            && activeHierarchyCount == activeCount;

        bool collectionsDoNotOverlap =
            !activeHitEffectLookup.Overlaps(
                availableHitEffectLookup
            );

        bool isValid =
            totalCountIsValid
            && availableRecordsAreValid
            && activeRecordsAreValid
            && hierarchyIsValid
            && collectionsDoNotOverlap;

        Debug.Log(
            "===== HitEffect Pool Validation =====\n"
            + "Result: "
            + (isValid ? "PASS" : "FAIL")
            + "\nTotal = Active + Available: "
            + totalCountIsValid
            + "\nQueue Count = Available Lookup Count: "
            + availableRecordsAreValid
            + "\nActive Debug Count = Active Lookup Count: "
            + activeRecordsAreValid
            + "\nHierarchy Counts Match: "
            + hierarchyIsValid
            + "\nActive And Available Do Not Overlap: "
            + collectionsDoNotOverlap
            + "\nTotal Count: "
            + totalCount
            + "\nActive Count: "
            + activeCount
            + "\nAvailable Count: "
            + availableCount
            + "\nAvailable Hierarchy Children: "
            + availableHierarchyCount
            + "\nActive Hierarchy Children: "
            + activeHierarchyCount,
            this
        );
    }

    /// <summary>
    /// 在对象池位置生成一个持续 1 秒的测试特效。
    /// </summary>
    [ContextMenu("Debug/Test Spawn Hit Effect")]
    private void TestSpawnHitEffect()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "HitEffectPool: Please enter Play Mode before "
                + "testing HitEffect spawning.",
                this
            );

            return;
        }

        HitEffect hitEffect = GetHitEffect(
            transform.position,
            Quaternion.identity
        );

        if (hitEffect != null)
        {
            // 测试时使用 1 秒生命周期，
            // 方便观察 Active Count 变化。
            hitEffect.Initialize(1f);
        }
    }

    /// <summary>
    /// 手动生成一个扩容批次。
    /// </summary>
    [ContextMenu("Debug/Create Expansion Batch")]
    private void CreateExpansionBatch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "HitEffectPool: Please enter Play Mode before "
                + "creating an expansion batch.",
                this
            );

            return;
        }

        CreateHitEffects(
            Mathf.Max(1, expansionAmount)
        );

        Debug.Log(
            "HitEffectPool: Expansion batch created. "
            + "Current total count: "
            + totalCount,
            this
        );
    }

    private void OnValidate()
    {
        initialPoolSize =
            Mathf.Max(1, initialPoolSize);

        expansionAmount =
            Mathf.Max(1, expansionAmount);
    }
    private void OnDestroy()
    {
        // 场景重新加载或返回主菜单时，
        // 清除旧场景留下的静态引用。
        if (Instance == this)
        {
            Instance = null;
        }
    }
}