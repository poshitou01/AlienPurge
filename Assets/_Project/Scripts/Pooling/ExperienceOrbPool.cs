using System.Collections.Generic;
using UnityEngine;

public class ExperienceOrbPool : MonoBehaviour
{
    public static ExperienceOrbPool Instance
    {
        get;
        private set;
    }

    [Header("Pool Settings")]
    [Tooltip("对象池使用的 ExperienceOrb Prefab")]
    [SerializeField] private ExperienceOrb experienceOrbPrefab;

    [Tooltip("游戏开始时预生成的 ExperienceOrb 数量")]
    [SerializeField] private int initialPoolSize = 30;

    [Tooltip("池中没有可用 ExperienceOrb 时是否允许自动扩容")]
    [SerializeField] private bool allowExpansion = true;

    [Tooltip("每次扩容时增加的 ExperienceOrb 数量")]
    [SerializeField] private int expansionAmount = 10;

    [Header("Runtime Debug")]
    [Tooltip("对象池目前创建的 ExperienceOrb 总数量")]
    [SerializeField] private int totalCount;

    [Tooltip("当前场景中正在使用的 ExperienceOrb 数量")]
    [SerializeField] private int activeCount;

    [Tooltip("当前可供使用的 ExperienceOrb 数量")]
    [SerializeField] private int availableCount;

    private readonly Queue<ExperienceOrb> availableExperienceOrbs =
        new Queue<ExperienceOrb>();

    // 记录已经位于可用队列中的经验球，
    // 用于防止重复回收。
    private readonly HashSet<ExperienceOrb>
        availableExperienceOrbLookup =
            new HashSet<ExperienceOrb>();

    // 记录当前正在场景中使用的经验球。
    private readonly HashSet<ExperienceOrb>
        activeExperienceOrbLookup =
            new HashSet<ExperienceOrb>();

    // 记录所有由当前对象池创建的经验球。
    private readonly HashSet<ExperienceOrb>
        allExperienceOrbLookup =
            new HashSet<ExperienceOrb>();

    private Transform availableContainer;
    private Transform activeContainer;

    public int TotalCount => totalCount;
    public int ActiveCount => activeCount;
    public int AvailableCount => availableCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "ExperienceOrbPool: More than one "
                + "ExperienceOrbPool exists in the current scene.",
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
            new GameObject("AvailableExperienceOrbs");

        availableContainerObject.transform.SetParent(
            transform,
            false
        );

        availableContainer =
            availableContainerObject.transform;

        // 回收池整体关闭，
        // 使可用经验球停止执行碰撞检测。
        availableContainerObject.SetActive(false);

        GameObject activeContainerObject =
            new GameObject("ActiveExperienceOrbs");

        activeContainerObject.transform.SetParent(
            transform,
            false
        );

        activeContainer =
            activeContainerObject.transform;
    }

    /// <summary>
    /// 游戏开始时预生成 ExperienceOrb。
    /// </summary>
    private void PrewarmPool()
    {
        if (experienceOrbPrefab == null)
        {
            Debug.LogError(
                "ExperienceOrbPool: Experience Orb Prefab "
                + "has not been assigned.",
                this
            );

            return;
        }

        CreateExperienceOrbs(
            Mathf.Max(1, initialPoolSize)
        );
    }

    /// <summary>
    /// 创建指定数量的 ExperienceOrb。
    /// </summary>
    private void CreateExperienceOrbs(int amount)
    {
        if (experienceOrbPrefab == null)
        {
            Debug.LogError(
                "ExperienceOrbPool: Cannot create ExperienceOrbs "
                + "because the Prefab has not been assigned.",
                this
            );

            return;
        }

        amount = Mathf.Max(1, amount);

        for (int i = 0; i < amount; i++)
        {
            ExperienceOrb experienceOrb = Instantiate(
                experienceOrbPrefab,
                availableContainer
            );

            experienceOrb.SetPool(this);

            experienceOrb.name =
                experienceOrbPrefab.name
                + "_Pooled_"
                + allExperienceOrbLookup.Count;

            experienceOrb.gameObject.SetActive(false);

            availableExperienceOrbs.Enqueue(
                experienceOrb
            );

            availableExperienceOrbLookup.Add(
                experienceOrb
            );

            allExperienceOrbLookup.Add(
                experienceOrb
            );
        }

        RefreshDebugCounts();
    }

    /// <summary>
    /// 从对象池中取得一个 ExperienceOrb。
    /// </summary>
    public ExperienceOrb GetExperienceOrb(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        int experienceAmount)
    {
        ExperienceOrb experienceOrb =
            GetNextAvailableExperienceOrb();

        if (experienceOrb == null)
        {
            Debug.LogWarning(
                "ExperienceOrbPool: No ExperienceOrb "
                + "is currently available.",
                this
            );

            return null;
        }

        if (!allExperienceOrbLookup.Contains(experienceOrb))
        {
            Debug.LogError(
                "ExperienceOrbPool: An unregistered ExperienceOrb "
                + "was found in the available queue.",
                this
            );

            return null;
        }

        availableExperienceOrbLookup.Remove(
            experienceOrb
        );

        if (!activeExperienceOrbLookup.Add(experienceOrb))
        {
            Debug.LogError(
                "ExperienceOrbPool: Attempted to get an already "
                + "active ExperienceOrb: "
                + experienceOrb.name,
                experienceOrb
            );

            availableExperienceOrbs.Enqueue(
                experienceOrb
            );

            availableExperienceOrbLookup.Add(
                experienceOrb
            );

            RefreshDebugCounts();
            return null;
        }

        experienceOrb.gameObject.SetActive(false);

        experienceOrb.transform.SetParent(
            activeContainer,
            false
        );

        experienceOrb.transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        experienceOrb.gameObject.SetActive(true);

        // 重置经验值、Collider 和拾取状态。
        experienceOrb.Initialize(
            Mathf.Max(1, experienceAmount)
        );

        RefreshDebugCounts();

        return experienceOrb;
    }

    /// <summary>
    /// 获取下一颗可用的 ExperienceOrb。
    /// 池为空时根据设置决定是否扩容。
    /// </summary>
    private ExperienceOrb GetNextAvailableExperienceOrb()
    {
        while (availableExperienceOrbs.Count > 0)
        {
            ExperienceOrb experienceOrb =
                availableExperienceOrbs.Dequeue();

            if (experienceOrb != null)
            {
                return experienceOrb;
            }
        }

        if (!allowExpansion)
        {
            RefreshDebugCounts();
            return null;
        }

        CreateExperienceOrbs(
            Mathf.Max(1, expansionAmount)
        );

        if (availableExperienceOrbs.Count <= 0)
        {
            return null;
        }

        return availableExperienceOrbs.Dequeue();
    }

    /// <summary>
    /// 将 ExperienceOrb 放回对象池。
    /// </summary>
    public void ReturnExperienceOrb(
        ExperienceOrb experienceOrb)
    {
        if (experienceOrb == null)
        {
            return;
        }

        // 只允许回收当前池创建的对象。
        if (!allExperienceOrbLookup.Contains(experienceOrb))
        {
            Debug.LogError(
                "ExperienceOrbPool: Attempted to return an "
                + "ExperienceOrb that does not belong to this pool: "
                + experienceOrb.name,
                experienceOrb
            );

            return;
        }

        // 第二层重复回收保护。
        if (availableExperienceOrbLookup.Contains(experienceOrb))
        {
            Debug.LogWarning(
                "ExperienceOrbPool: Attempted to return the same "
                + "ExperienceOrb twice: "
                + experienceOrb.name,
                experienceOrb
            );

            return;
        }

        if (!activeExperienceOrbLookup.Remove(experienceOrb))
        {
            Debug.LogWarning(
                "ExperienceOrbPool: Returned ExperienceOrb was not "
                + "recorded as active: "
                + experienceOrb.name,
                experienceOrb
            );
        }

        experienceOrb.gameObject.SetActive(false);

        experienceOrb.transform.SetParent(
            availableContainer,
            false
        );

        availableExperienceOrbs.Enqueue(
            experienceOrb
        );

        availableExperienceOrbLookup.Add(
            experienceOrb
        );

        RefreshDebugCounts();
    }

    /// <summary>
    /// 更新 Inspector 调试数量。
    /// </summary>
    private void RefreshDebugCounts()
    {
        totalCount =
            allExperienceOrbLookup.Count;

        activeCount =
            activeExperienceOrbLookup.Count;

        availableCount =
            availableExperienceOrbs.Count;
    }

    [ContextMenu("Debug/Print Pool Status")]
    private void PrintPoolStatus()
    {
        RefreshDebugCounts();

        Debug.Log(
            "===== ExperienceOrb Pool Status =====\n"
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
            availableExperienceOrbLookup.Count;

        int activeLookupCount =
            activeExperienceOrbLookup.Count;

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
            !activeExperienceOrbLookup.Overlaps(
                availableExperienceOrbLookup
            );

        bool isValid =
            totalCountIsValid
            && availableRecordsAreValid
            && activeRecordsAreValid
            && hierarchyIsValid
            && collectionsDoNotOverlap;

        Debug.Log(
            "===== ExperienceOrb Pool Validation =====\n"
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
    /// 在对象池右侧生成一颗测试经验球。
    /// </summary>
    [ContextMenu("Debug/Test Spawn Experience Orb")]
    private void TestSpawnExperienceOrb()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "ExperienceOrbPool: Please enter Play Mode "
                + "before testing ExperienceOrb spawning.",
                this
            );

            return;
        }

        Vector3 testPosition =
            transform.position
            + Vector3.right * 2.5f;

        ExperienceOrb experienceOrb =
            GetExperienceOrb(
                testPosition,
                Quaternion.identity,
                5
            );

        if (experienceOrb != null)
        {
            Debug.Log(
                "ExperienceOrbPool: Spawned a test orb "
                + "worth 5 experience.",
                experienceOrb
            );
        }
    }

    /// <summary>
    /// 自动请求超过当前可用数量的经验球，
    /// 验证池为空后是否按 expansionAmount 扩容。
    /// </summary>
    [ContextMenu("Debug/Test Automatic Expansion")]
    private void TestAutomaticExpansion()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "ExperienceOrbPool: Please enter Play Mode "
                + "before testing automatic expansion.",
                this
            );

            return;
        }

        RefreshDebugCounts();

        int requestCount =
            availableCount + 1;

        List<ExperienceOrb> testOrbs =
            new List<ExperienceOrb>();

        for (int i = 0; i < requestCount; i++)
        {
            ExperienceOrb experienceOrb =
                GetExperienceOrb(
                    transform.position,
                    Quaternion.identity,
                    1
                );

            if (experienceOrb != null)
            {
                testOrbs.Add(experienceOrb);
            }
        }

        RefreshDebugCounts();

        Debug.Log(
            "ExperienceOrbPool automatic expansion test "
            + "after checkout:\n"
            + "Requested: "
            + requestCount
            + "\nTotal Count: "
            + totalCount
            + "\nActive Count: "
            + activeCount
            + "\nAvailable Count: "
            + availableCount,
            this
        );

        for (int i = 0; i < testOrbs.Count; i++)
        {
            testOrbs[i].ReturnToPool();
        }

        RefreshDebugCounts();

        Debug.Log(
            "ExperienceOrbPool automatic expansion test "
            + "after return:\n"
            + "Total Count: "
            + totalCount
            + "\nActive Count: "
            + activeCount
            + "\nAvailable Count: "
            + availableCount,
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
        if (Instance == this)
        {
            Instance = null;
        }
    }
}