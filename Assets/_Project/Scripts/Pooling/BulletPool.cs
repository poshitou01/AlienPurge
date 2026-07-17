using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("对象池使用的 Bullet Prefab")]
    [SerializeField] private Bullet bulletPrefab;

    [Tooltip("游戏开始时预生成的 Bullet 数量")]
    [SerializeField] private int initialPoolSize = 40;

    [Tooltip("池中没有可用 Bullet 时是否允许自动扩容")]
    [SerializeField] private bool allowExpansion = true;

    [Tooltip("每次扩容时增加的 Bullet 数量")]
    [SerializeField] private int expansionAmount = 10;

    [Header("Runtime Debug")]
    [Tooltip("对象池目前创建的 Bullet 总数量")]
    [SerializeField] private int totalCount;

    [Tooltip("当前正在使用的 Bullet 数量")]
    [SerializeField] private int activeCount;

    [Tooltip("当前可供使用的 Bullet 数量")]
    [SerializeField] private int availableCount;

    // 真正存放可用 Bullet 的先进先出队列。
    private readonly Queue<Bullet> availableBullets =
        new Queue<Bullet>();

    // 记录所有已经位于可用队列中的 Bullet。
    // 用于防止同一颗 Bullet 被重复回收。
    private readonly HashSet<Bullet> availableBulletLookup =
        new HashSet<Bullet>();

    // 记录当前已经取出并正在使用的 Bullet。
    private readonly HashSet<Bullet> activeBulletLookup =
        new HashSet<Bullet>();

    // 记录所有由当前对象池创建的 Bullet。
    private readonly HashSet<Bullet> allBulletLookup =
        new HashSet<Bullet>();

    private Transform availableContainer;
    private Transform activeContainer;

    public int TotalCount => totalCount;
    public int ActiveCount => activeCount;
    public int AvailableCount => availableCount;

    private void Awake()
    {
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
            new GameObject("AvailableBullets");

        availableContainerObject.transform.SetParent(
            transform,
            false
        );

        availableContainer =
            availableContainerObject.transform;

        // 回收池整体保持关闭状态，
        // 保证池中 Bullet 不会执行 Update 和碰撞检测。
        availableContainerObject.SetActive(false);

        GameObject activeContainerObject =
            new GameObject("ActiveBullets");

        activeContainerObject.transform.SetParent(
            transform,
            false
        );

        activeContainer =
            activeContainerObject.transform;
    }

    /// <summary>
    /// 游戏开始时预生成 Bullet。
    /// </summary>
    private void PrewarmPool()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError(
                "BulletPool: Bullet Prefab has not been assigned.",
                this
            );

            return;
        }

        CreateBullets(initialPoolSize);
    }

    /// <summary>
    /// 创建指定数量的 Bullet，并放入可用队列。
    /// </summary>
    private void CreateBullets(int amount)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError(
                "BulletPool: Cannot create bullets because "
                + "Bullet Prefab has not been assigned.",
                this
            );

            return;
        }

        amount = Mathf.Max(1, amount);

        for (int i = 0; i < amount; i++)
        {
            Bullet bullet = Instantiate(
                bulletPrefab,
                availableContainer
            );

            bullet.SetPool(this);

            bullet.name =
                bulletPrefab.name
                + "_Pooled_"
                + allBulletLookup.Count;

            bullet.gameObject.SetActive(false);

            availableBullets.Enqueue(bullet);
            availableBulletLookup.Add(bullet);
            allBulletLookup.Add(bullet);
        }

        RefreshDebugCounts();
    }

    /// <summary>
    /// 从对象池中取得一颗 Bullet。
    /// </summary>
    public Bullet GetBullet(
        Vector3 spawnPosition,
        Quaternion spawnRotation)
    {
        Bullet bullet = GetNextAvailableBullet();

        if (bullet == null)
        {
            Debug.LogWarning(
                "BulletPool: No Bullet is currently available.",
                this
            );

            return null;
        }

        if (!allBulletLookup.Contains(bullet))
        {
            Debug.LogError(
                "BulletPool: An unregistered Bullet was found "
                + "in the available queue.",
                this
            );

            return null;
        }

        availableBulletLookup.Remove(bullet);

        // 正常情况下，同一颗 Bullet 不可能同时处于使用状态。
        if (!activeBulletLookup.Add(bullet))
        {
            Debug.LogError(
                "BulletPool: Attempted to get an already active Bullet: "
                + bullet.name,
                bullet
            );

            // 尽量恢复队列状态。
            availableBullets.Enqueue(bullet);
            availableBulletLookup.Add(bullet);

            RefreshDebugCounts();
            return null;
        }

        bullet.gameObject.SetActive(false);

        bullet.transform.SetParent(
            activeContainer,
            false
        );

        bullet.transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        bullet.gameObject.SetActive(true);

        RefreshDebugCounts();

        return bullet;
    }

    /// <summary>
    /// 获取下一颗可用 Bullet。
    /// 池为空时根据设置决定是否自动扩容。
    /// </summary>
    private Bullet GetNextAvailableBullet()
    {
        while (availableBullets.Count > 0)
        {
            Bullet bullet = availableBullets.Dequeue();

            // 防止队列中存在已经被意外销毁的空引用。
            if (bullet != null)
            {
                return bullet;
            }
        }

        if (!allowExpansion)
        {
            RefreshDebugCounts();
            return null;
        }

        CreateBullets(
            Mathf.Max(1, expansionAmount)
        );

        if (availableBullets.Count <= 0)
        {
            return null;
        }

        return availableBullets.Dequeue();
    }

    /// <summary>
    /// 将 Bullet 放回对象池。
    /// </summary>
    public void ReturnBullet(Bullet bullet)
    {
        if (bullet == null)
        {
            return;
        }

        // 只接受由当前对象池创建的 Bullet。
        if (!allBulletLookup.Contains(bullet))
        {
            Debug.LogError(
                "BulletPool: Attempted to return a Bullet "
                + "that does not belong to this pool: "
                + bullet.name,
                bullet
            );

            return;
        }

        // 同一颗 Bullet 已经在可用集合中，
        // 说明发生了重复回收。
        if (availableBulletLookup.Contains(bullet))
        {
            Debug.LogWarning(
                "BulletPool: Attempted to return the same "
                + "Bullet twice: "
                + bullet.name,
                bullet
            );

            return;
        }

        // 从使用集合中移除。
        // 如果不存在，说明状态记录发生过异常，
        // 但仍继续执行回收以恢复池状态。
        if (!activeBulletLookup.Remove(bullet))
        {
            Debug.LogWarning(
                "BulletPool: Returned Bullet was not recorded "
                + "as active: "
                + bullet.name,
                bullet
            );
        }

        bullet.gameObject.SetActive(false);

        bullet.transform.SetParent(
            availableContainer,
            false
        );

        availableBullets.Enqueue(bullet);
        availableBulletLookup.Add(bullet);

        RefreshDebugCounts();
    }

    /// <summary>
    /// 更新 Inspector 中显示的调试数量。
    /// </summary>
    private void RefreshDebugCounts()
    {
        totalCount = allBulletLookup.Count;
        activeCount = activeBulletLookup.Count;
        availableCount = availableBullets.Count;
    }

    /// <summary>
    /// 输出当前对象池数量。
    /// </summary>
    [ContextMenu("Debug/Print Pool Status")]
    private void PrintPoolStatus()
    {
        RefreshDebugCounts();

        Debug.Log(
            "===== Bullet Pool Status =====\n"
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
            availableBulletLookup.Count;

        int activeLookupCount =
            activeBulletLookup.Count;

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
            !activeBulletLookup.Overlaps(
                availableBulletLookup
            );

        bool isValid =
            totalCountIsValid
            && availableRecordsAreValid
            && activeRecordsAreValid
            && hierarchyIsValid
            && collectionsDoNotOverlap;

        Debug.Log(
            "===== Bullet Pool Validation =====\n"
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
    /// 手动生成一次扩容批次。
    /// </summary>
    [ContextMenu("Debug/Create Expansion Batch")]
    private void CreateExpansionBatch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "BulletPool: Please enter Play Mode before "
                + "using Create Expansion Batch.",
                this
            );

            return;
        }

        CreateBullets(expansionAmount);

        Debug.Log(
            "BulletPool: Expansion batch created. "
            + "Current total count: "
            + totalCount,
            this
        );
    }

    /// <summary>
    /// 人为连续回收同一颗 Bullet 两次，
    /// 验证对象池的重复回收保护。
    /// </summary>
    [ContextMenu("Debug/Test Duplicate Return Protection")]
    private void TestDuplicateReturnProtection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "BulletPool: Please enter Play Mode before "
                + "testing duplicate return protection.",
                this
            );

            return;
        }

        Bullet testBullet = GetBullet(
            transform.position,
            Quaternion.identity
        );

        if (testBullet == null)
        {
            Debug.LogWarning(
                "BulletPool: Could not obtain a Bullet "
                + "for duplicate return testing.",
                this
            );

            return;
        }

        ReturnBullet(testBullet);

        // 第二次回收应该被拦截，并输出警告。
        ReturnBullet(testBullet);

        Debug.Log(
            "BulletPool: Duplicate return protection "
            + "test completed.",
            this
        );

        ValidatePoolState();
    }

    private void OnValidate()
    {
        initialPoolSize =
            Mathf.Max(1, initialPoolSize);

        expansionAmount =
            Mathf.Max(1, expansionAmount);
    }
}