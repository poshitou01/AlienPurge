using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一管理 Normal、Fast、Heavy 三种敌人的对象池。
///
/// 每种敌人拥有独立的可用队列、激活集合和对象总集合。
/// 本类只管理生命周期，不计算敌人属性和刷怪权重。
/// </summary>
[DisallowMultipleComponent]
public class EnemyPool :
    MonoBehaviour,
    IEnemyPoolReturnHandler
{
    public static EnemyPool Instance
    {
        get;
        private set;
    }

    [Header("Pool Configuration")]

    [Tooltip("不同敌人类型的对象池配置")]
    [SerializeField]
    private List<EnemyPoolEntry> poolEntries =
        new List<EnemyPoolEntry>();

    [Header("Debug Settings")]

    [Tooltip("对象池自动扩容时是否输出调试信息")]
    [SerializeField]
    private bool logPoolExpansion = true;

    [Header("Runtime Pool Debug")]

    [Tooltip("对象池是否已完成初始化")]
    [SerializeField]
    private bool isInitialized;

    [Tooltip("全部敌人对象数量")]
    [SerializeField]
    private int totalEnemyCount;

    [Tooltip("当前激活敌人数量")]
    [SerializeField]
    private int activeEnemyCount;

    [Tooltip("当前可用敌人数量")]
    [SerializeField]
    private int availableEnemyCount;

    private Transform availableEnemiesRoot;
    private Transform activeEnemiesRoot;

    private readonly Dictionary<
        EnemyType,
        RuntimeEnemyPool
    > runtimePools =
        new Dictionary<
            EnemyType,
            RuntimeEnemyPool
        >();

    /// <summary>
    /// 单个敌人类型在运行时的数据。
    /// </summary>
    private sealed class RuntimeEnemyPool
    {
        public EnemyPoolEntry Entry;

        public Transform AvailableParent;
        public Transform ActiveParent;

        public readonly Queue<PooledEnemy>
            AvailableQueue =
                new Queue<PooledEnemy>();

        public readonly HashSet<PooledEnemy>
            AvailableSet =
                new HashSet<PooledEnemy>();

        public readonly HashSet<PooledEnemy>
            ActiveSet =
                new HashSet<PooledEnemy>();

        public readonly HashSet<PooledEnemy>
            AllSet =
                new HashSet<PooledEnemy>();
    }

    public bool IsInitialized =>
        isInitialized;

    public int TotalCount =>
        totalEnemyCount;

    public int ActiveCount =>
        activeEnemyCount;

    public int AvailableCount =>
        availableEnemyCount;

    private void Awake()
    {
        if (Instance != null
            && Instance != this)
        {
            Debug.LogError(
                "场景中存在重复的 EnemyPool，"
                + "后创建的对象将被销毁。",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializePool();
    }

    /// <summary>
    /// 读取配置、创建类型容器并完成预热。
    /// </summary>
    private void InitializePool()
    {
        isInitialized = false;

        runtimePools.Clear();

        availableEnemiesRoot =
            GetOrCreateContainer(
                "AvailableEnemies",
                transform
            );

        activeEnemiesRoot =
            GetOrCreateContainer(
                "ActiveEnemies",
                transform
            );

        if (poolEntries == null
            || poolEntries.Count == 0)
        {
            Debug.LogError(
                "EnemyPool 没有配置任何 "
                + "EnemyPoolEntry。",
                this
            );

            RefreshDebugCounts();
            return;
        }

        HashSet<EnemyType> configuredTypes =
            new HashSet<EnemyType>();

        for (int i = 0;
            i < poolEntries.Count;
            i++)
        {
            EnemyPoolEntry entry =
                poolEntries[i];

            if (entry == null)
            {
                Debug.LogError(
                    "EnemyPool 的第 "
                    + i
                    + " 条配置为空。",
                    this
                );

                continue;
            }

            if (!entry.IsValid(
                    out string validationMessage
                ))
            {
                Debug.LogError(
                    "EnemyPool 配置无效："
                    + validationMessage,
                    this
                );

                continue;
            }

            if (!configuredTypes.Add(
                    entry.Type
                ))
            {
                Debug.LogError(
                    "EnemyPool 存在重复类型配置："
                    + entry.Type,
                    this
                );

                continue;
            }

            PooledEnemy prefabPooledEnemy =
                entry.Prefab.GetComponent<
                    PooledEnemy
                >();

            if (prefabPooledEnemy == null)
            {
                Debug.LogError(
                    entry.Prefab.name
                    + " 的根对象没有挂载 "
                    + "PooledEnemy。",
                    entry.Prefab
                );

                continue;
            }

            if (prefabPooledEnemy.Type
                != entry.Type)
            {
                Debug.LogError(
                    entry.Prefab.name
                    + " 的 PooledEnemy Type 为 "
                    + prefabPooledEnemy.Type
                    + "，但对象池配置类型为 "
                    + entry.Type
                    + "。",
                    entry.Prefab
                );

                continue;
            }

            RuntimeEnemyPool runtimePool =
                new RuntimeEnemyPool();

            runtimePool.Entry = entry;

            runtimePool.AvailableParent =
                GetOrCreateContainer(
                    entry.Type.ToString(),
                    availableEnemiesRoot
                );

            runtimePool.ActiveParent =
                GetOrCreateContainer(
                    entry.Type.ToString(),
                    activeEnemiesRoot
                );

            runtimePools.Add(
                entry.Type,
                runtimePool
            );

            CreateEnemyInstances(
                runtimePool,
                entry.InitialPoolSize
            );
        }

        WarnIfTypeIsMissing(EnemyType.Normal);
        WarnIfTypeIsMissing(EnemyType.Fast);
        WarnIfTypeIsMissing(EnemyType.Heavy);
        WarnIfTypeIsMissing(EnemyType.Ranged);

        isInitialized =
            runtimePools.Count > 0;

        RefreshDebugCounts();

        if (isInitialized)
        {
            Debug.Log(
                "EnemyPool 初始化完成。"
                + " Total="
                + totalEnemyCount
                + ", Active="
                + activeEnemyCount
                + ", Available="
                + availableEnemyCount,
                this
            );
        }
        else
        {
            Debug.LogError(
                "EnemyPool 没有任何有效配置，"
                + "初始化失败。",
                this
            );
        }
    }

    private void WarnIfTypeIsMissing(
        EnemyType enemyType
    )
    {
        if (runtimePools.ContainsKey(
                enemyType
            ))
        {
            return;
        }

        Debug.LogWarning(
            "EnemyPool 缺少 "
            + enemyType
            + " 类型配置。",
            this
        );
    }

    private Transform GetOrCreateContainer(
        string containerName,
        Transform parent
    )
    {
        Transform existingContainer =
            parent.Find(containerName);

        if (existingContainer != null)
        {
            return existingContainer;
        }

        GameObject containerObject =
            new GameObject(containerName);

        Transform containerTransform =
            containerObject.transform;

        containerTransform.SetParent(
            parent,
            false
        );

        return containerTransform;
    }

    /// <summary>
    /// 为指定类型创建一定数量的敌人对象，
    /// 并全部放入可用队列。
    /// </summary>
    private int CreateEnemyInstances(
        RuntimeEnemyPool runtimePool,
        int amount
    )
    {
        if (runtimePool == null
            || runtimePool.Entry == null)
        {
            return 0;
        }

        amount = Mathf.Max(0, amount);

        int createdCount = 0;

        for (int i = 0;
            i < amount;
            i++)
        {
            GameObject enemyObject =
                Instantiate(
                    runtimePool.Entry.Prefab,
                    runtimePool.AvailableParent
                );

            // 防止预热对象在当前帧参与游戏逻辑。
            enemyObject.SetActive(false);

            enemyObject.name =
                runtimePool.Entry.Prefab.name
                + "_Pooled_"
                + (
                    runtimePool.AllSet.Count
                    + 1
                );

            PooledEnemy pooledEnemy =
                enemyObject.GetComponent<
                    PooledEnemy
                >();

            if (pooledEnemy == null)
            {
                Debug.LogError(
                    enemyObject.name
                    + " 没有 PooledEnemy，"
                    + "该对象将被销毁。",
                    enemyObject
                );

                Destroy(enemyObject);
                continue;
            }

            pooledEnemy.InitializePoolMembership(
                this,
                runtimePool.Entry.Type
            );

            pooledEnemy.MarkAsReturned();

            ResetRigidbody(pooledEnemy);

            runtimePool.AllSet.Add(
                pooledEnemy
            );

            runtimePool.AvailableSet.Add(
                pooledEnemy
            );

            runtimePool.AvailableQueue.Enqueue(
                pooledEnemy
            );

            createdCount++;
        }

        RefreshDebugCounts();

        return createdCount;
    }

    /// <summary>
    /// 从指定类型的池中取得一个敌人。
    /// 池不足时根据配置自动扩容。
    /// </summary>
    public PooledEnemy GetEnemy(
        EnemyType enemyType,
        Vector3 position,
        Quaternion rotation
    )
    {
        if (!isInitialized)
        {
            Debug.LogError(
                "EnemyPool 尚未完成初始化，"
                + "无法取得敌人。",
                this
            );

            return null;
        }

        if (!runtimePools.TryGetValue(
                enemyType,
                out RuntimeEnemyPool runtimePool
            ))
        {
            Debug.LogError(
                "EnemyPool 没有 "
                + enemyType
                + " 类型配置，"
                + "本次生成已取消。",
                this
            );

            return null;
        }

        PooledEnemy pooledEnemy =
            TakeAvailableEnemy(runtimePool);

        if (pooledEnemy == null)
        {
            if (!runtimePool.Entry.AllowExpansion)
            {
                Debug.LogWarning(
                    enemyType
                    + " 对象池已用尽，"
                    + "并且不允许扩容。",
                    this
                );

                RefreshDebugCounts();
                return null;
            }

            int createdCount =
                CreateEnemyInstances(
                    runtimePool,
                    runtimePool.Entry
                        .ExpansionAmount
                );

            if (createdCount <= 0)
            {
                Debug.LogError(
                    enemyType
                    + " 对象池扩容失败。",
                    this
                );

                RefreshDebugCounts();
                return null;
            }

            if (logPoolExpansion)
            {
                Debug.Log(
                    enemyType
                    + " 对象池自动扩容：+"
                    + createdCount,
                    this
                );
            }

            pooledEnemy =
                TakeAvailableEnemy(
                    runtimePool
                );
        }

        if (pooledEnemy == null)
        {
            Debug.LogError(
                enemyType
                + " 对象池没有可用对象，"
                + "本次生成已取消。",
                this
            );

            RefreshDebugCounts();
            return null;
        }

        if (!runtimePool.ActiveSet.Add(
                pooledEnemy
            ))
        {
            Debug.LogError(
                pooledEnemy.name
                + " 已存在于 Active 集合中，"
                + "本次取出已取消。",
                pooledEnemy
            );

            ReturnToAvailableQueue(
                runtimePool,
                pooledEnemy
            );

            RefreshDebugCounts();
            return null;
        }

        pooledEnemy.transform.SetParent(
            runtimePool.ActiveParent,
            false
        );

        pooledEnemy.transform.SetPositionAndRotation(
            position,
            rotation
        );

        ResetRigidbody(pooledEnemy);

        pooledEnemy.PrepareForSpawn();

        pooledEnemy.gameObject.SetActive(true);

        RefreshDebugCounts();

        return pooledEnemy;
    }

    /// <summary>
    /// 从可用队列安全取得一个对象。
    /// 会跳过意外被销毁的无效引用。
    /// </summary>
    private PooledEnemy TakeAvailableEnemy(
        RuntimeEnemyPool runtimePool
    )
    {
        while (runtimePool.AvailableQueue.Count
            > 0)
        {
            PooledEnemy pooledEnemy =
                runtimePool.AvailableQueue
                    .Dequeue();

            runtimePool.AvailableSet.Remove(
                pooledEnemy
            );

            if (pooledEnemy == null)
            {
                runtimePool.AllSet.Remove(
                    pooledEnemy
                );

                continue;
            }

            return pooledEnemy;
        }

        return null;
    }

    /// <summary>
    /// 回收敌人。
    /// 同一个对象不能重复加入可用队列。
    /// </summary>
    public void ReturnEnemy(
        PooledEnemy enemy
    )
    {
        if (enemy == null)
        {
            Debug.LogWarning(
                "EnemyPool 收到了空的回收请求。",
                this
            );

            return;
        }

        if (!runtimePools.TryGetValue(
                enemy.Type,
                out RuntimeEnemyPool runtimePool
            ))
        {
            Debug.LogError(
                enemy.name
                + " 的类型 "
                + enemy.Type
                + " 没有对应对象池。",
                enemy
            );

            return;
        }

        if (!runtimePool.AllSet.Contains(
                enemy
            ))
        {
            Debug.LogError(
                enemy.name
                + " 不属于当前 "
                + enemy.Type
                + " 对象池，"
                + "回收请求已拒绝。",
                enemy
            );

            return;
        }

        if (runtimePool.AvailableSet.Contains(
                enemy
            ))
        {
            Debug.LogWarning(
                enemy.name
                + " 已在 Available 集合中，"
                + "重复回收请求已忽略。",
                enemy
            );

            return;
        }

        if (!runtimePool.ActiveSet.Remove(
                enemy
            ))
        {
            Debug.LogWarning(
                enemy.name
                + " 不在 Active 集合中，"
                + "回收请求已忽略。",
                enemy
            );

            return;
        }

        enemy.MarkAsReturned();

        ResetRigidbody(enemy);

        enemy.gameObject.SetActive(false);

        enemy.transform.SetParent(
            runtimePool.AvailableParent,
            false
        );

        ReturnToAvailableQueue(
            runtimePool,
            enemy
        );

        RefreshDebugCounts();
    }

    private void ReturnToAvailableQueue(
        RuntimeEnemyPool runtimePool,
        PooledEnemy enemy
    )
    {
        if (runtimePool == null
            || enemy == null)
        {
            return;
        }

        if (!runtimePool.AvailableSet.Add(
                enemy
            ))
        {
            return;
        }

        runtimePool.AvailableQueue.Enqueue(
            enemy
        );
    }

    private void ResetRigidbody(
        PooledEnemy enemy
    )
    {
        if (enemy == null)
        {
            return;
        }

        Rigidbody2D rb =
            enemy.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            return;
        }

        // 当前项目使用 Unity 2022.3，
        // 因此使用 velocity。
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void RefreshDebugCounts()
    {
        totalEnemyCount = 0;
        activeEnemyCount = 0;
        availableEnemyCount = 0;

        foreach (
            KeyValuePair<
                EnemyType,
                RuntimeEnemyPool
            > pair in runtimePools
        )
        {
            RuntimeEnemyPool runtimePool =
                pair.Value;

            totalEnemyCount +=
                runtimePool.AllSet.Count;

            activeEnemyCount +=
                runtimePool.ActiveSet.Count;

            availableEnemyCount +=
                runtimePool.AvailableSet.Count;
        }
    }

    public int GetTotalActiveCount()
    {
        RefreshDebugCounts();
        return activeEnemyCount;
    }

    public int GetActiveCount(
        EnemyType enemyType
    )
    {
        if (!runtimePools.TryGetValue(
                enemyType,
                out RuntimeEnemyPool runtimePool
            ))
        {
            return 0;
        }

        return runtimePool.ActiveSet.Count;
    }

    public int GetAvailableCount(
        EnemyType enemyType
    )
    {
        if (!runtimePools.TryGetValue(
                enemyType,
                out RuntimeEnemyPool runtimePool
            ))
        {
            return 0;
        }

        return runtimePool.AvailableSet.Count;
    }

    public int GetTotalCount(
        EnemyType enemyType
    )
    {
        if (!runtimePools.TryGetValue(
                enemyType,
                out RuntimeEnemyPool runtimePool
            ))
        {
            return 0;
        }

        return runtimePool.AllSet.Count;
    }

    [ContextMenu("Debug/Print Pool Status")]
    private void PrintPoolStatus()
    {
        RefreshDebugCounts();

        Debug.Log(
            "===== EnemyPool Status ====="
            + "\nTotal Enemies: "
            + totalEnemyCount
            + "\nActive Enemies: "
            + activeEnemyCount
            + "\nAvailable Enemies: "
            + availableEnemyCount,
            this
        );

        PrintTypeStatus(EnemyType.Normal);
        PrintTypeStatus(EnemyType.Fast);
        PrintTypeStatus(EnemyType.Heavy);
    }

    private void PrintTypeStatus(
        EnemyType enemyType
    )
    {
        if (!runtimePools.TryGetValue(
                enemyType,
                out RuntimeEnemyPool runtimePool
            ))
        {
            Debug.LogWarning(
                enemyType
                + ": Pool Not Configured",
                this
            );

            return;
        }

        Debug.Log(
            enemyType
            + ": Total="
            + runtimePool.AllSet.Count
            + ", Active="
            + runtimePool.ActiveSet.Count
            + ", Available="
            + runtimePool.AvailableSet.Count,
            this
        );
    }

    [ContextMenu("Debug/Validate Pool State")]
    private void ValidatePoolState()
    {
        bool passed = true;

        foreach (
            KeyValuePair<
                EnemyType,
                RuntimeEnemyPool
            > pair in runtimePools
        )
        {
            EnemyType enemyType =
                pair.Key;

            RuntimeEnemyPool runtimePool =
                pair.Value;

            int total =
                runtimePool.AllSet.Count;

            int active =
                runtimePool.ActiveSet.Count;

            int available =
                runtimePool.AvailableSet.Count;

            if (total != active + available)
            {
                passed = false;

                Debug.LogError(
                    enemyType
                    + ": Total != Active + Available"
                    + " ("
                    + total
                    + " != "
                    + active
                    + " + "
                    + available
                    + ")",
                    this
                );
            }

            if (runtimePool.AvailableQueue.Count
                != available)
            {
                passed = false;

                Debug.LogError(
                    enemyType
                    + ": Queue Count 与 "
                    + "Available Set Count 不一致。",
                    this
                );
            }

            HashSet<PooledEnemy> queueSet =
                new HashSet<PooledEnemy>();

            foreach (
                PooledEnemy enemy
                in runtimePool.AvailableQueue
            )
            {
                if (enemy == null)
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": Available Queue "
                        + "包含空引用。",
                        this
                    );

                    continue;
                }

                if (!queueSet.Add(enemy))
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": Available Queue "
                        + "包含重复对象 "
                        + enemy.name
                        + "。",
                        enemy
                    );
                }

                if (!runtimePool
                        .AvailableSet
                        .Contains(enemy))
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": Queue 中的 "
                        + enemy.name
                        + " 不在 Available Set。",
                        enemy
                    );
                }
            }

            foreach (
                PooledEnemy enemy
                in runtimePool.ActiveSet
            )
            {
                if (enemy == null)
                {
                    passed = false;
                    continue;
                }

                if (runtimePool
                    .AvailableSet
                    .Contains(enemy))
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": "
                        + enemy.name
                        + " 同时存在于 Active "
                        + "和 Available 集合。",
                        enemy
                    );
                }

                if (!runtimePool
                    .AllSet
                    .Contains(enemy))
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": Active 对象 "
                        + enemy.name
                        + " 不在 All Set。",
                        enemy
                    );
                }
            }

            foreach (
                PooledEnemy enemy
                in runtimePool.AllSet
            )
            {
                if (enemy == null)
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": All Set 包含空引用。",
                        this
                    );

                    continue;
                }

                bool isActive =
                    runtimePool.ActiveSet
                        .Contains(enemy);

                bool isAvailable =
                    runtimePool.AvailableSet
                        .Contains(enemy);

                if (isActive == isAvailable)
                {
                    passed = false;

                    Debug.LogError(
                        enemyType
                        + ": "
                        + enemy.name
                        + " 必须且只能属于 "
                        + "Active 或 Available "
                        + "其中一个集合。",
                        enemy
                    );
                }
            }
        }

        RefreshDebugCounts();

        if (passed)
        {
            Debug.Log(
                "EnemyPool Validate Pool State: PASS"
                + "\nTotal="
                + totalEnemyCount
                + ", Active="
                + activeEnemyCount
                + ", Available="
                + availableEnemyCount,
                this
            );
        }
        else
        {
            Debug.LogError(
                "EnemyPool Validate Pool State: FAIL",
                this
            );
        }
    }

    [ContextMenu("Debug/Spawn One Normal")]
    private void DebugSpawnOneNormal()
    {
        DebugSpawnOne(EnemyType.Normal);
    }

    [ContextMenu("Debug/Spawn One Fast")]
    private void DebugSpawnOneFast()
    {
        DebugSpawnOne(EnemyType.Fast);
    }

    [ContextMenu("Debug/Spawn One Heavy")]
    private void DebugSpawnOneHeavy()
    {
        DebugSpawnOne(EnemyType.Heavy);
    }

    [ContextMenu("Debug/Spawn One Ranged")]
    private void DebugSpawnOneRanged()
    {
        DebugSpawnOne(EnemyType.Ranged);
    }


    private void DebugSpawnOne(
        EnemyType enemyType
    )
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后再测试对象池生成。",
                this
            );

            return;
        }

        PooledEnemy enemy =
            GetEnemy(
                enemyType,
                transform.position,
                Quaternion.identity
            );

        if (enemy == null)
        {
            Debug.LogWarning(
                "未能从对象池取得 "
                + enemyType
                + "。",
                this
            );
        }
    }

    [ContextMenu(
        "Debug/Test Duplicate Return Protection"
    )]
    private void TestDuplicateReturnProtection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后再测试重复回收。",
                this
            );

            return;
        }

        PooledEnemy enemy =
            GetEnemy(
                EnemyType.Normal,
                transform.position,
                Quaternion.identity
            );

        if (enemy == null)
        {
            Debug.LogWarning(
                "无法取得 Normal，"
                + "重复回收测试已取消。",
                this
            );

            return;
        }

        enemy.ReturnToPool();

        int availableBeforeDuplicate =
            GetAvailableCount(
                EnemyType.Normal
            );

        // 故意再次直接请求池回收。
        ReturnEnemy(enemy);

        int availableAfterDuplicate =
            GetAvailableCount(
                EnemyType.Normal
            );

        if (availableBeforeDuplicate
            == availableAfterDuplicate)
        {
            Debug.Log(
                "Duplicate Return Protection: PASS",
                this
            );
        }
        else
        {
            Debug.LogError(
                "Duplicate Return Protection: FAIL",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        runtimePools.Clear();
    }
}