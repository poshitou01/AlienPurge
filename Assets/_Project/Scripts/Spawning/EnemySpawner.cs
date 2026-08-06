using System.Collections.Generic;
using UnityEngine;
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Spawn Configuration")]
    [Tooltip(
    "所有可参与自动刷怪的敌人配置。"
    + "敌人是否可用将由解锁时间、权重和 Prefab 共同决定"
)]
    [SerializeField]
    private List<EnemySpawnEntry> enemySpawnEntries =
    new List<EnemySpawnEntry>();




    [Header("Spawner Settings")]
    [Tooltip("游戏刚开始时的刷怪间隔")]
    [SerializeField] private float spawnInterval = 2f;

    [Tooltip("游戏刚开始时允许存在的最大敌人数")]
    [SerializeField] private int maxEnemies = 5;

    [Header("Spawn Interval Difficulty")]
    [Tooltip("刷怪间隔允许降低到的最小值")]
    [SerializeField] private float minSpawnInterval = 0.7f;

    [Tooltip("每生存 1 秒，刷怪间隔减少多少秒")]
    [SerializeField]
    private float spawnIntervalDecreasePerSecond = 0.02f;

    [Header("Enemy Count Difficulty")]
    [Tooltip("场上敌人数量允许提高到的最终上限")]
    [SerializeField] private int maxEnemiesLimit = 12;

    [Tooltip("每隔多少秒提高一次敌人数量上限")]
    [SerializeField]
    private float maxEnemiesIncreaseInterval = 10f;

    [Tooltip("每次提高多少个敌人数量上限")]
    [SerializeField]
    private int maxEnemiesIncreaseAmount = 1;

    [Header("Enemy Health Difficulty")]
    [Tooltip("游戏开始时普通敌人的全局基础生命值")]
    [SerializeField] private int enemyInitialMaxHealth = 3;

    [Tooltip("每隔多少秒提高一次全局基础生命值")]
    [SerializeField]
    private float enemyHealthIncreaseInterval = 20f;

    [Tooltip("每次提高多少点全局基础生命值")]
    [SerializeField]
    private int enemyHealthIncreaseAmount = 1;

    [Tooltip("全局基础生命值允许成长到的最终上限")]
    [SerializeField] private int enemyMaxHealthLimit = 6;

    [Header("Enemy Move Speed Difficulty")]
    [Tooltip("游戏开始时普通敌人的全局基础移动速度")]
    [SerializeField]
    private float enemyInitialMoveSpeed = 1.5f;

    [Tooltip("每隔多少秒提高一次全局基础移动速度")]
    [SerializeField]
    private float enemyMoveSpeedIncreaseInterval = 20f;

    [Tooltip("每次提高多少全局基础移动速度")]
    [SerializeField]
    private float enemyMoveSpeedIncreaseAmount = 0.25f;

    [Tooltip("全局基础移动速度允许成长到的最终上限")]
    [SerializeField]
    private float enemyMoveSpeedLimit = 2.25f;

    [Header("Enemy Contact Damage Difficulty")]
    [Tooltip("游戏开始时普通敌人的全局基础接触伤害")]
    [SerializeField]
    private int enemyInitialContactDamage = 1;

    [Tooltip("每隔多少秒提高一次全局基础接触伤害")]
    [SerializeField]
    private float enemyContactDamageIncreaseInterval = 30f;

    [Tooltip("每次提高多少点全局基础接触伤害")]
    [SerializeField]
    private int enemyContactDamageIncreaseAmount = 1;

    [Tooltip("全局基础接触伤害允许成长到的最终上限")]
    [SerializeField]
    private int enemyContactDamageLimit = 3;

    [Header("Spawn Distance")]
    [SerializeField] private float minSpawnDistance = 5f;
    [SerializeField] private float maxSpawnDistance = 8f;

    [Header("Map Spawn Bounds")]
    [Tooltip("是否把敌人的生成位置限制在正式地图范围内")]
    [SerializeField] private bool limitSpawnToMapBounds = true;

    [Tooltip("正式地图的左下角世界坐标")]
    [SerializeField]
    private Vector2 spawnMapMin =
        new Vector2(-25f, -25f);

    [Tooltip("正式地图的右上角世界坐标")]
    [SerializeField]
    private Vector2 spawnMapMax =
        new Vector2(25f, 25f);

    [Tooltip("生成点与地图边界之间保留的安全距离")]
    [Min(0f)]
    [SerializeField]
    private float spawnBoundsPadding = 1f;

    [Tooltip("寻找地图内部有效生成位置的最大尝试次数")]
    [Min(1)]
    [SerializeField]
    private int maxSpawnPositionAttempts = 24;

    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Debug Settings")]
    [Tooltip("是否允许正常的计时自动刷怪")]
    [SerializeField] private bool enableAutomaticSpawning = true;

    [Tooltip("进入游戏后是否立即生成一个普通敌人")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Runtime Spawn Debug")]
    [Tooltip("当前实际使用的刷怪间隔")]
    [SerializeField] private float currentSpawnInterval;

    [Tooltip("当前实际允许存在的最大敌人数")]
    [SerializeField] private int currentMaxEnemies;

    [Tooltip("最近一次检测到的场上敌人数")]
    [SerializeField] private int currentEnemyCount;

    [Tooltip("当前通过全部检查的有效刷怪候选数量")]
    [SerializeField]
    private int currentSpawnCandidateCount;

    [Tooltip(
    "当前同时满足解锁时间、"
    + "Prefab 和权重检查的敌人类型"
)]
    [SerializeField]
    private string currentUnlockedEnemyTypes =
    "None";

    [Tooltip("当前所有有效刷怪候选的权重总和")]
    [SerializeField]
    private float currentSpawnWeightTotal;

    [Tooltip("最近一次加权随机选择是否成功")]
    [SerializeField]
    private bool lastSpawnSelectionSucceeded;

    [Tooltip("最近一次加权随机选中的敌人类型")]
    [SerializeField]
    private EnemyType lastSelectedEnemyType =
        EnemyType.Normal;

    [Header("Runtime Enemy Attribute Debug")]
    [Tooltip("当前时间点的全局基础最大生命值")]
    [SerializeField] private int currentEnemyMaxHealth;

    [Tooltip("当前时间点的全局基础移动速度")]
    [SerializeField] private float currentEnemyMoveSpeed;

    [Tooltip("当前时间点的全局基础接触伤害")]
    [SerializeField] private int currentEnemyContactDamage;

    private readonly List<EnemySpawnEntry>
    currentSpawnCandidates =
        new List<EnemySpawnEntry>();

    private Transform player;
    private float spawnTimer;

    // 避免配置全部无效时，
    // 每个刷怪间隔都重复输出相同警告。
    private bool hasWarnedAboutMissingSpawnCandidate;

    public int CurrentEnemyMaxHealth =>
        currentEnemyMaxHealth;

    public float CurrentEnemyMoveSpeed =>
        currentEnemyMoveSpeed;

    public int CurrentEnemyContactDamage =>
        currentEnemyContactDamage;

    private void Start()
    {
        FindPlayer();
        UpdateDifficulty();

        spawnTimer = 0f;

        if (enableAutomaticSpawning
            && spawnOnStart
            && CanSpawnEnemies())
        {
            TrySpawnEnemy();
        }
    }

    private void Update()
    {
        if (!enableAutomaticSpawning)
        {
            return;
        }

        if (!CanSpawnEnemies())
        {
            return;
        }

        if (player == null)
        {
            FindPlayer();

            if (player == null)
            {
                return;
            }
        }

        UpdateDifficulty();

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnEnemy();
        }
    }

    private bool CanSpawnEnemies()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }

        if (GameManager.Instance.CurrentState
            != GameState.Playing)
        {
            return false;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 根据指定生存时间刷新当前有效的刷怪候选，
    /// 同时计算全部有效候选的权重总和。
    /// </summary>
    private void RefreshSpawnCandidates(
        float survivalTime
    )
    {
        currentSpawnCandidates.Clear();

        currentSpawnCandidateCount = 0;
        currentUnlockedEnemyTypes =
            "None";
        currentSpawnWeightTotal = 0f;

        survivalTime = Mathf.Max(
            0f,
            survivalTime
        );

        if (enemySpawnEntries == null)
        {
            return;
        }

        for (int i = 0;
            i < enemySpawnEntries.Count;
            i++)
        {
            EnemySpawnEntry entry =
                enemySpawnEntries[i];

            if (!IsSpawnEntryValid(
                    entry,
                    survivalTime
                ))
            {
                continue;
            }

            currentSpawnCandidates.Add(entry);

            currentSpawnWeightTotal +=
                entry.SpawnWeight;
        }

        currentSpawnCandidateCount =
            currentSpawnCandidates.Count;

        if (currentSpawnCandidates.Count > 0)
        {
            currentUnlockedEnemyTypes =
                string.Empty;

            for (int i = 0;
                i < currentSpawnCandidates.Count;
                i++)
            {
                if (i > 0)
                {
                    currentUnlockedEnemyTypes +=
                        ", ";
                }

                currentUnlockedEnemyTypes +=
                    currentSpawnCandidates[i]
                        .Type
                        .ToString();
            }
        }

        if (float.IsNaN(currentSpawnWeightTotal)
     || float.IsInfinity(
         currentSpawnWeightTotal
     )
     || currentSpawnWeightTotal < 0f)
        {
            currentSpawnCandidates.Clear();

            currentSpawnCandidateCount = 0;

            currentUnlockedEnemyTypes =
                "None";

            currentSpawnWeightTotal = 0f;
        }
    }

    /// <summary>
    /// 判断单个生成配置在指定时间是否可以进入候选。
    /// </summary>
    /// <summary>
    /// 判断单个生成配置在指定时间是否可以进入候选。
    /// </summary>
    private bool IsSpawnEntryValid(
        EnemySpawnEntry entry,
        float survivalTime
    )
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.Prefab == null)
        {
            return false;
        }

        float spawnWeight =
            entry.SpawnWeight;

        if (float.IsNaN(spawnWeight)
            || float.IsInfinity(spawnWeight)
            || spawnWeight <= 0f)
        {
            return false;
        }

        float unlockTime =
            entry.UnlockTime;

        if (float.IsNaN(unlockTime)
            || float.IsInfinity(unlockTime))
        {
            return false;
        }

        float safeUnlockTime =
            Mathf.Max(0f, unlockTime);

        if (survivalTime < safeUnlockTime)
        {
            return false;
        }

        return true;
    }
    /// <summary>
    /// 根据指定生存时间筛选候选，
    /// 然后按相对权重随机选择一个敌人配置。
    ///
    /// 没有有效候选时返回 null，
    /// 不执行随机范围计算。
    /// </summary>
    private EnemySpawnEntry
        SelectWeightedSpawnEntry(
            float survivalTime
        )
    {
        lastSpawnSelectionSucceeded = false;

        RefreshSpawnCandidates(
            survivalTime
        );

        if (currentSpawnCandidates.Count == 0)
        {
            return null;
        }

        if (currentSpawnWeightTotal <= 0f
            || float.IsNaN(
                currentSpawnWeightTotal
            )
            || float.IsInfinity(
                currentSpawnWeightTotal
            ))
        {
            return null;
        }

        float randomWeight =
            Random.Range(
                0f,
                currentSpawnWeightTotal
            );

        float accumulatedWeight = 0f;

        for (int i = 0;
            i < currentSpawnCandidates.Count;
            i++)
        {
            EnemySpawnEntry entry =
                currentSpawnCandidates[i];

            accumulatedWeight +=
                entry.SpawnWeight;

            if (randomWeight
                < accumulatedWeight)
            {
                RecordSelectedSpawnEntry(
                    entry
                );

                return entry;
            }
        }

        // 浮点数计算可能出现极小的边界误差。
        // 此时安全返回最后一个有效候选。
        EnemySpawnEntry fallbackCandidate =
            currentSpawnCandidates[
                currentSpawnCandidates.Count - 1
            ];

        RecordSelectedSpawnEntry(
            fallbackCandidate
        );

        return fallbackCandidate;
    }

    /// <summary>
    /// 保存最近一次成功的随机选择结果。
    /// </summary>
    private void RecordSelectedSpawnEntry(
        EnemySpawnEntry selectedEntry
    )
    {
        if (selectedEntry == null)
        {
            lastSpawnSelectionSucceeded =
                false;

            return;
        }

        lastSelectedEnemyType =
            selectedEntry.Type;

        lastSpawnSelectionSucceeded =
            true;
    }

    private void UpdateDifficulty()
    {
        float survivalTime = 0f;

        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance.SurvivalTime;
        }

        survivalTime = Mathf.Max(
            0f,
            survivalTime
        );

        UpdateSpawnDifficulty(survivalTime);
        UpdateEnemyAttributeDifficulty(survivalTime);
        RefreshSpawnCandidates(survivalTime);
    }

    private void UpdateSpawnDifficulty(
        float survivalTime
    )
    {
        currentSpawnInterval =
            spawnInterval
            - survivalTime
            * spawnIntervalDecreasePerSecond;

        currentSpawnInterval = Mathf.Max(
            minSpawnInterval,
            currentSpawnInterval
        );

        int enemyCountIncreaseCount =
            Mathf.FloorToInt(
                survivalTime
                / maxEnemiesIncreaseInterval
            );

        currentMaxEnemies =
            maxEnemies
            + enemyCountIncreaseCount
            * maxEnemiesIncreaseAmount;

        currentMaxEnemies = Mathf.Min(
            currentMaxEnemies,
            maxEnemiesLimit
        );
    }

    private void UpdateEnemyAttributeDifficulty(
        float survivalTime
    )
    {
        currentEnemyMaxHealth =
            CalculateEnemyMaxHealth(
                survivalTime
            );

        currentEnemyMoveSpeed =
            CalculateEnemyMoveSpeed(
                survivalTime
            );

        currentEnemyContactDamage =
            CalculateEnemyContactDamage(
                survivalTime
            );
    }

    private int CalculateEnemyMaxHealth(
        float survivalTime
    )
    {
        survivalTime = Mathf.Max(
            0f,
            survivalTime
        );

        int increaseCount =
            Mathf.FloorToInt(
                survivalTime
                / enemyHealthIncreaseInterval
            );

        int calculatedMaxHealth =
            enemyInitialMaxHealth
            + increaseCount
            * enemyHealthIncreaseAmount;

        return Mathf.Min(
            calculatedMaxHealth,
            enemyMaxHealthLimit
        );
    }

    private float CalculateEnemyMoveSpeed(
        float survivalTime
    )
    {
        survivalTime = Mathf.Max(
            0f,
            survivalTime
        );

        int increaseCount =
            Mathf.FloorToInt(
                survivalTime
                / enemyMoveSpeedIncreaseInterval
            );

        float calculatedMoveSpeed =
            enemyInitialMoveSpeed
            + increaseCount
            * enemyMoveSpeedIncreaseAmount;

        return Mathf.Min(
            calculatedMoveSpeed,
            enemyMoveSpeedLimit
        );
    }

    private int CalculateEnemyContactDamage(
        float survivalTime
    )
    {
        survivalTime = Mathf.Max(
            0f,
            survivalTime
        );

        int increaseCount =
            Mathf.FloorToInt(
                survivalTime
                / enemyContactDamageIncreaseInterval
            );

        int calculatedDamage =
            enemyInitialContactDamage
            + increaseCount
            * enemyContactDamageIncreaseAmount;

        return Mathf.Min(
            calculatedDamage,
            enemyContactDamageLimit
        );
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                playerTag
            );

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning(
                "EnemySpawner: 没有找到 Tag 为 "
                + playerTag
                + " 的对象。",
                this
            );
        }
    }
    /// <summary>
    /// 根据当前生存时间完成加权选择，
    /// 然后从 EnemyPool 取得对应类型的敌人。
    /// </summary>
    private void TrySpawnEnemy()
    {
        float survivalTime = 0f;

        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance
                    .SurvivalTime;
        }

        survivalTime =
            Mathf.Max(0f, survivalTime);

        EnemySpawnEntry selectedEntry =
            SelectWeightedSpawnEntry(
                survivalTime
            );

        if (selectedEntry == null)
        {
            if (!hasWarnedAboutMissingSpawnCandidate)
            {
                Debug.LogError(
                    "EnemySpawner: 当前没有有效的"
                    + "敌人生成候选，"
                    + "本次对象池生成已安全取消。",
                    this
                );

                hasWarnedAboutMissingSpawnCandidate =
                    true;
            }

            return;
        }

        hasWarnedAboutMissingSpawnCandidate =
            false;

        SpawnEnemyFromPool(
            selectedEntry.Type,
            true,
            selectedEntry.Type.ToString()
        );
    }


    /// <summary>
    /// 从 EnemyPool 取得指定类型的敌人，
    /// 并应用当前时间点的最新难度属性。
    /// </summary>
    private bool SpawnEnemyFromPool(
        EnemyType enemyType,
        bool respectEnemyLimit,
        string enemyLabel
    )
    {
        if (EnemyPool.Instance == null)
        {
            Debug.LogError(
                "EnemySpawner: 场景中没有 EnemyPool，"
                + "无法生成 "
                + enemyLabel
                + " Enemy。",
                this
            );

            return false;
        }

        if (!EnemyPool.Instance.IsInitialized)
        {
            Debug.LogError(
                "EnemySpawner: EnemyPool "
                + "尚未完成有效初始化，"
                + "无法生成 "
                + enemyLabel
                + " Enemy。",
                this
            );

            return false;
        }

        if (player == null)
        {
            FindPlayer();

            if (player == null)
            {
                Debug.LogWarning(
                    "EnemySpawner: Player 为空，"
                    + "无法生成 "
                    + enemyLabel
                    + " Enemy。",
                    this
                );

                return false;
            }
        }

        RefreshCurrentEnemyCount();

        if (respectEnemyLimit
            && currentEnemyCount
            >= currentMaxEnemies)
        {
            return false;
        }

        Vector3 spawnPosition =
            GetRandomSpawnPositionAroundPlayer();

        PooledEnemy pooledEnemy =
            EnemyPool.Instance.GetEnemy(
                enemyType,
                spawnPosition,
                Quaternion.identity
            );

        if (pooledEnemy == null)
        {
            Debug.LogWarning(
                "EnemySpawner: 未能从 EnemyPool "
                + "取得 "
                + enemyLabel
                + " Enemy。",
                this
            );

            RefreshCurrentEnemyCount();
            return false;
        }

        GameObject spawnedEnemy =
            pooledEnemy.gameObject;

        if (!InitializeEnemyAttributes(
                spawnedEnemy
            ))
        {
            Debug.LogError(
                spawnedEnemy.name
                + " 属性初始化失败，"
                + "将立即返回对象池。",
                spawnedEnemy
            );

            pooledEnemy.ReturnToPool();

            RefreshCurrentEnemyCount();
            return false;
        }

        RefreshCurrentEnemyCount();

        return true;
    }

    /// <summary>
    /// 将当前全局难度基础属性传递给 EnemyDefinition。
    ///
    /// EnemyDefinition 再根据绑定的 EnemyData
    /// 计算对应敌人类型的最终属性。
    /// </summary>
    private bool InitializeEnemyAttributes(
        GameObject spawnedEnemy
    )
    {
        if (spawnedEnemy == null)
        {
            Debug.LogError(
                "EnemySpawner: 生成的敌人为空，"
                + "无法初始化属性。",
                this
            );

            return false;
        }

        EnemyDefinition enemyDefinition =
            spawnedEnemy.GetComponent<
                EnemyDefinition
            >();

        if (enemyDefinition == null)
        {
            Debug.LogError(
                spawnedEnemy.name
                + " 没有 EnemyDefinition，"
                + "无法应用敌人类型数据。",
                spawnedEnemy
            );

            return false;
        }

        enemyDefinition
            .InitializeFromGlobalDifficulty(
                currentEnemyMaxHealth,
                currentEnemyMoveSpeed,
                currentEnemyContactDamage
            );

        if (!enemyDefinition.HasBeenInitialized)
        {
            Debug.LogError(
                spawnedEnemy.name
                + " 的 EnemyDefinition "
                + "初始化失败。",
                spawnedEnemy
            );

            return false;
        }

        return true;
    }

    private void RefreshCurrentEnemyCount()
    {
        if (EnemyPool.Instance != null
            && EnemyPool.Instance.IsInitialized)
        {
            currentEnemyCount =
                EnemyPool.Instance
                    .GetTotalActiveCount();

            return;
        }

        // EnemyPool 缺失时保留旧 Tag 统计，
        // 仅作为安全调试回退。
        currentEnemyCount =
            GameObject.FindGameObjectsWithTag(
                enemyTag
            ).Length;
    }

    private Vector3
    GetRandomSpawnPositionAroundPlayer()
    {
        if (!limitSpawnToMapBounds)
        {
            return CreateRandomSpawnPositionAroundPlayer();
        }

        float safePadding =
            Mathf.Max(0f, spawnBoundsPadding);

        Vector2 safeMin =
            spawnMapMin
            + Vector2.one * safePadding;

        Vector2 safeMax =
            spawnMapMax
            - Vector2.one * safePadding;

        if (safeMin.x > safeMax.x
            || safeMin.y > safeMax.y)
        {
            Debug.LogWarning(
                "EnemySpawner: 地图生成范围无效，"
                + "将暂时使用未限制的随机生成位置。",
                this
            );

            return CreateRandomSpawnPositionAroundPlayer();
        }

        int attemptCount =
            Mathf.Max(1, maxSpawnPositionAttempts);

        for (int i = 0; i < attemptCount; i++)
        {
            Vector3 candidatePosition =
                CreateRandomSpawnPositionAroundPlayer();

            if (IsSpawnPositionInsideBounds(
                    candidatePosition,
                    safeMin,
                    safeMax
                ))
            {
                return candidatePosition;
            }
        }

        // 极端情况下仍未随机到有效位置时，
        // 改为朝地图中心生成，避免敌人出现在墙外。
        Vector2 mapCenter =
            (safeMin + safeMax) * 0.5f;

        Vector2 directionToCenter =
            mapCenter
            - (Vector2)player.position;

        if (directionToCenter.sqrMagnitude
            <= Mathf.Epsilon)
        {
            directionToCenter = Vector2.right;
        }

        float fallbackDistance =
            Random.Range(
                minSpawnDistance,
                maxSpawnDistance
            );

        Vector2 fallbackPosition =
            (Vector2)player.position
            + directionToCenter.normalized
            * fallbackDistance;

        fallbackPosition.x =
            Mathf.Clamp(
                fallbackPosition.x,
                safeMin.x,
                safeMax.x
            );

        fallbackPosition.y =
            Mathf.Clamp(
                fallbackPosition.y,
                safeMin.y,
                safeMax.y
            );

        return new Vector3(
            fallbackPosition.x,
            fallbackPosition.y,
            0f
        );
    }

    private Vector3
        CreateRandomSpawnPositionAroundPlayer()
    {
        float randomAngle =
            Random.Range(0f, 360f);

        float randomDistance =
            Random.Range(
                minSpawnDistance,
                maxSpawnDistance
            );

        Vector2 direction =
            new Vector2(
                Mathf.Cos(
                    randomAngle
                    * Mathf.Deg2Rad
                ),
                Mathf.Sin(
                    randomAngle
                    * Mathf.Deg2Rad
                )
            );

        Vector3 spawnPosition =
            player.position
            + (Vector3)(
                direction
                * randomDistance
            );

        spawnPosition.z = 0f;

        return spawnPosition;
    }

    private bool IsSpawnPositionInsideBounds(
        Vector3 spawnPosition,
        Vector2 safeMin,
        Vector2 safeMax
    )
    {
        return spawnPosition.x >= safeMin.x
            && spawnPosition.x <= safeMax.x
            && spawnPosition.y >= safeMin.y
            && spawnPosition.y <= safeMax.y;
    }
    /// <summary>
    /// 独立测试生成入口。
    /// 测试生成忽略最大敌人数限制，
    /// 但仍要求处于 Playing 状态。
    /// </summary>
    private void SpawnEnemyForTesting(
        EnemyType enemyType,
        string enemyLabel
    )
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试生成 "
                + enemyLabel
                + " Enemy。",
                this
            );

            return;
        }

        if (!CanSpawnEnemies())
        {
            Debug.LogWarning(
                "当前游戏状态不允许生成敌人。",
                this
            );

            return;
        }

        UpdateDifficulty();

        SpawnEnemyFromPool(
            enemyType,
            false,
            enemyLabel
        );
    }

    [ContextMenu("Test Spawn Normal Enemy")]
    private void TestSpawnNormalEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Normal,
            "Normal"
        );
    }

    [ContextMenu("Test Spawn Fast Enemy")]
    private void TestSpawnFastEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Fast,
            "Fast"
        );
    }

    [ContextMenu("Test Spawn Heavy Enemy")]
    private void TestSpawnHeavyEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Heavy,
            "Heavy"
        );
    }

    [ContextMenu("Test Spawn Ranged Enemy")]
    private void TestSpawnRangedEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Ranged,
            "Ranged"
        );
    }
    private void LogDifficultyAtTime(
        float testSurvivalTime
    )
    {
        testSurvivalTime = Mathf.Max(
            0f,
            testSurvivalTime
        );

        float testSpawnInterval =
            spawnInterval
            - testSurvivalTime
            * spawnIntervalDecreasePerSecond;

        testSpawnInterval = Mathf.Max(
            minSpawnInterval,
            testSpawnInterval
        );

        int enemyCountIncreaseCount =
            Mathf.FloorToInt(
                testSurvivalTime
                / maxEnemiesIncreaseInterval
            );

        int testMaxEnemies =
            maxEnemies
            + enemyCountIncreaseCount
            * maxEnemiesIncreaseAmount;

        testMaxEnemies = Mathf.Min(
            testMaxEnemies,
            maxEnemiesLimit
        );

        int testEnemyMaxHealth =
            CalculateEnemyMaxHealth(
                testSurvivalTime
            );

        float testEnemyMoveSpeed =
            CalculateEnemyMoveSpeed(
                testSurvivalTime
            );

        int testEnemyContactDamage =
            CalculateEnemyContactDamage(
                testSurvivalTime
            );

        Debug.Log(
            "===== Difficulty At "
            + testSurvivalTime.ToString("F0")
            + " Seconds =====\n"
            + "Spawn Interval: "
            + testSpawnInterval.ToString("F2")
            + "\nMax Enemies: "
            + testMaxEnemies
            + "\nGlobal Enemy Max Health: "
            + testEnemyMaxHealth
            + "\nGlobal Enemy Move Speed: "
            + testEnemyMoveSpeed.ToString("F2")
            + "\nGlobal Enemy Contact Damage: "
            + testEnemyContactDamage,
            this
        );
    }

    [ContextMenu("Debug Difficulty At 0 Seconds")]
    private void DebugDifficultyAt0Seconds()
    {
        LogDifficultyAtTime(0f);
    }

    [ContextMenu("Debug Difficulty At 30 Seconds")]
    private void DebugDifficultyAt30Seconds()
    {
        LogDifficultyAtTime(30f);
    }

    [ContextMenu("Debug Difficulty At 60 Seconds")]
    private void DebugDifficultyAt60Seconds()
    {
        LogDifficultyAtTime(60f);
    }
    /// <summary>
    /// 从生成配置列表中读取指定类型的 Prefab。
    ///
    /// 仅用于属性预览和调试，不负责生成敌人。
    /// 正式生成仍然只能通过 EnemyPool。
    /// </summary>
    private GameObject FindConfiguredEnemyPrefab(
        EnemyType enemyType
    )
    {
        if (enemySpawnEntries == null)
        {
            return null;
        }

        for (int i = 0;
            i < enemySpawnEntries.Count;
            i++)
        {
            EnemySpawnEntry entry =
                enemySpawnEntries[i];

            if (entry == null)
            {
                continue;
            }

            if (entry.Type != enemyType)
            {
                continue;
            }

            if (entry.Prefab == null)
            {
                continue;
            }

            return entry.Prefab;
        }

        return null;
    }

    /// <summary>
    /// 输出指定生存时间下三种敌人的最终属性。
    ///
    /// 此方法只进行数值预览，不会生成敌人，
    /// 也不会修改当前游戏状态。
    /// </summary>
    private void LogEnemyTypeStatsAtTime(
        float testSurvivalTime
    )
    {
        testSurvivalTime =
            Mathf.Max(0f, testSurvivalTime);

        int globalMaxHealth =
            CalculateEnemyMaxHealth(
                testSurvivalTime
            );

        float globalMoveSpeed =
            CalculateEnemyMoveSpeed(
                testSurvivalTime
            );

        int globalContactDamage =
            CalculateEnemyContactDamage(
                testSurvivalTime
            );

        Debug.Log(
            "===== Enemy Type Stats At "
            + testSurvivalTime.ToString("F0")
            + " Seconds =====\n"
            + "Global HP="
            + globalMaxHealth
            + ", Global Speed="
            + globalMoveSpeed.ToString("F2")
            + ", Global Damage="
            + globalContactDamage,
            this
        );

        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Normal
            ),
            "Normal",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );

        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Fast
            ),
            "Fast",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );

        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Heavy
            ),
            "Heavy",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );

        LogSingleEnemyTypeStats(
    FindConfiguredEnemyPrefab(
        EnemyType.Ranged
    ),
    "Ranged",
    globalMaxHealth,
    globalMoveSpeed,
    globalContactDamage
);
    }

    /// <summary>
    /// 读取指定敌人 Prefab 绑定的 EnemyData，
    /// 并输出应用类型倍率后的最终属性。
    /// </summary>
    private void LogSingleEnemyTypeStats(
        GameObject enemyPrefab,
        string enemyLabel,
        int globalMaxHealth,
        float globalMoveSpeed,
        int globalContactDamage
    )
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning(
                enemyLabel
                + " Enemy Prefab 没有绑定，"
                + "无法预览属性。",
                this
            );

            return;
        }

        EnemyDefinition enemyDefinition =
            enemyPrefab.GetComponent<
                EnemyDefinition
            >();

        if (enemyDefinition == null)
        {
            Debug.LogWarning(
                enemyPrefab.name
                + " 没有 EnemyDefinition，"
                + "无法预览属性。",
                enemyPrefab
            );

            return;
        }

        EnemyData enemyData =
            enemyDefinition.Data;

        if (enemyData == null)
        {
            Debug.LogWarning(
                enemyPrefab.name
                + " 没有绑定 EnemyData，"
                + "无法预览属性。",
                enemyPrefab
            );

            return;
        }

        int finalMaxHealth =
            RoundEnemyAttributeToPositiveInt(
                globalMaxHealth
                * enemyData.HealthMultiplier
            );

        float finalMoveSpeed =
            Mathf.Max(
                0.01f,
                globalMoveSpeed
                * enemyData.MoveSpeedMultiplier
            );

        int finalContactDamage =
            RoundEnemyAttributeToPositiveInt(
                globalContactDamage
                * enemyData.DamageMultiplier
            );

        int finalExperienceAmount =
            Mathf.Max(
                1,
                enemyData.ExperienceAmount
            );

        float finalVisualScale =
            Mathf.Max(
                0.1f,
                enemyData.VisualScale
            );

        Debug.Log(
            enemyLabel
            + " Enemy"
            + ": HP="
            + finalMaxHealth
            + ", Speed="
            + finalMoveSpeed.ToString("F3")
            + ", Damage="
            + finalContactDamage
            + ", EXP="
            + finalExperienceAmount
            + ", Scale="
            + finalVisualScale.ToString("F2"),
            enemyPrefab
        );
    }

    /// <summary>
    /// 与 EnemyDefinition 使用相同的正整数四舍五入规则。
    /// </summary>
    private int RoundEnemyAttributeToPositiveInt(
        float value
    )
    {
        int roundedValue =
            Mathf.FloorToInt(value + 0.5f);

        return Mathf.Max(
            1,
            roundedValue
        );
    }

    [ContextMenu("Debug Enemy Types At 0 Seconds")]
    private void DebugEnemyTypesAt0Seconds()
    {
        LogEnemyTypeStatsAtTime(0f);
    }

    [ContextMenu("Debug Enemy Types At 30 Seconds")]
    private void DebugEnemyTypesAt30Seconds()
    {
        LogEnemyTypeStatsAtTime(30f);
    }

    [ContextMenu("Debug Enemy Types At 60 Seconds")]
    private void DebugEnemyTypesAt60Seconds()
    {
        LogEnemyTypeStatsAtTime(60f);
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(
            0.01f,
            spawnInterval
        );

        minSpawnInterval = Mathf.Clamp(
            minSpawnInterval,
            0.01f,
            spawnInterval
        );

        spawnIntervalDecreasePerSecond =
            Mathf.Max(
                0f,
                spawnIntervalDecreasePerSecond
            );

        maxEnemies = Mathf.Max(
            1,
            maxEnemies
        );

        maxEnemiesLimit = Mathf.Max(
            maxEnemies,
            maxEnemiesLimit
        );

        maxEnemiesIncreaseInterval =
            Mathf.Max(
                0.1f,
                maxEnemiesIncreaseInterval
            );

        maxEnemiesIncreaseAmount =
            Mathf.Max(
                1,
                maxEnemiesIncreaseAmount
            );

        enemyInitialMaxHealth =
            Mathf.Max(
                1,
                enemyInitialMaxHealth
            );

        enemyHealthIncreaseInterval =
            Mathf.Max(
                0.1f,
                enemyHealthIncreaseInterval
            );

        enemyHealthIncreaseAmount =
            Mathf.Max(
                1,
                enemyHealthIncreaseAmount
            );

        enemyMaxHealthLimit =
            Mathf.Max(
                enemyInitialMaxHealth,
                enemyMaxHealthLimit
            );

        enemyInitialMoveSpeed =
            Mathf.Max(
                0.01f,
                enemyInitialMoveSpeed
            );

        enemyMoveSpeedIncreaseInterval =
            Mathf.Max(
                0.1f,
                enemyMoveSpeedIncreaseInterval
            );

        enemyMoveSpeedIncreaseAmount =
            Mathf.Max(
                0f,
                enemyMoveSpeedIncreaseAmount
            );

        enemyMoveSpeedLimit =
            Mathf.Max(
                enemyInitialMoveSpeed,
                enemyMoveSpeedLimit
            );

        enemyInitialContactDamage =
            Mathf.Max(
                1,
                enemyInitialContactDamage
            );

        enemyContactDamageIncreaseInterval =
            Mathf.Max(
                0.1f,
                enemyContactDamageIncreaseInterval
            );

        enemyContactDamageIncreaseAmount =
            Mathf.Max(
                1,
                enemyContactDamageIncreaseAmount
            );

        enemyContactDamageLimit =
            Mathf.Max(
                enemyInitialContactDamage,
                enemyContactDamageLimit
            );

        minSpawnDistance =
            Mathf.Max(
                0f,
                minSpawnDistance
            );

        maxSpawnDistance =
            Mathf.Max(
                minSpawnDistance,
                maxSpawnDistance
            );
    }

    /// <summary>
    /// 输出指定生存时间下的有效刷怪候选。
    /// 只进行数据预览，不会实际生成敌人。
    /// </summary>
    private void LogSpawnCandidatesAtTime(
        float testSurvivalTime
    )
    {
        testSurvivalTime =
            Mathf.Max(0f, testSurvivalTime);

        RefreshSpawnCandidates(
            testSurvivalTime
        );

        Debug.Log(
     "===== Spawn Candidates At "
     + testSurvivalTime.ToString("F0")
     + " Seconds =====\n"
            + "Valid Candidate Count: "
            + currentSpawnCandidates.Count
            + "\nUnlocked Enemy Types: "
            + currentUnlockedEnemyTypes
            + "\nTotal Spawn Weight: "
     + currentSpawnWeightTotal
         .ToString("F1"),
     this
 );

        if (currentSpawnCandidates.Count == 0)
        {
            Debug.LogWarning(
                "当前没有有效的敌人生成候选。",
                this
            );

            return;
        }

        for (int i = 0;
            i < currentSpawnCandidates.Count;
            i++)
        {
            EnemySpawnEntry entry =
                currentSpawnCandidates[i];

            Debug.Log(
                "Candidate "
                + i
                + ": Type="
                + entry.Type
                + ", Prefab="
                + entry.Prefab.name
                + ", Unlock Time="
                + entry.UnlockTime.ToString("F1")
                + ", Spawn Weight="
                + entry.SpawnWeight.ToString("F1"),
                entry.Prefab
            );
        }
    }
    /// <summary>
    /// 测试指定时间下的一次加权随机选择。
    /// 不会实际生成敌人。
    /// </summary>
    private void TestWeightedSelectionAtTime(
        float testSurvivalTime
    )
    {
        EnemySpawnEntry selectedEntry =
            SelectWeightedSpawnEntry(
                testSurvivalTime
            );

        if (selectedEntry == null)
        {
            Debug.LogWarning(
                "===== Weighted Selection At "
                + testSurvivalTime
                    .ToString("F0")
                + " Seconds =====\n"
                + "没有有效候选，"
                + "本次随机选择已安全取消。",
                this
            );

            return;
        }

        Debug.Log(
            "===== Weighted Selection At "
            + testSurvivalTime
                .ToString("F0")
            + " Seconds =====\n"
                       + "\nTotal Spawn Weight: "
            + currentSpawnWeightTotal
                .ToString("F1")
            + "\nSelection Succeeded: "
            + lastSpawnSelectionSucceeded
            + "\nSelected Type: "
            + selectedEntry.Type
            + "\nSelected Prefab: "
            + selectedEntry.Prefab.name
            + "\nSelected Weight: "
            + selectedEntry.SpawnWeight
                .ToString("F1"),
            selectedEntry.Prefab
        );
    }

    [ContextMenu(
        "Test Weighted Selection At 0 Seconds"
    )]
    private void TestWeightedSelectionAt0Seconds()
    {
        TestWeightedSelectionAtTime(0f);
    }

    [ContextMenu(
        "Test Weighted Selection At 15 Seconds"
    )]
    private void TestWeightedSelectionAt15Seconds()
    {
        TestWeightedSelectionAtTime(15f);
    }

    [ContextMenu(
        "Test Weighted Selection At 30 Seconds"
    )]
    private void TestWeightedSelectionAt30Seconds()
    {
        TestWeightedSelectionAtTime(30f);
    }

    [ContextMenu(
    "Test Weighted Selection At 45 Seconds"
)]
    private void TestWeightedSelectionAt45Seconds()
    {
        TestWeightedSelectionAtTime(45f);
    }

    [ContextMenu(
        "Test Weighted Selection At 60 Seconds"
    )]
    private void TestWeightedSelectionAt60Seconds()
    {
        TestWeightedSelectionAtTime(60f);
    }
    [ContextMenu(
        "Debug Spawn Candidates At 0 Seconds"
    )]
    private void DebugSpawnCandidatesAt0Seconds()
    {
        LogSpawnCandidatesAtTime(0f);
    }

    [ContextMenu(
        "Debug Spawn Candidates At 15 Seconds"
    )]
    private void DebugSpawnCandidatesAt15Seconds()
    {
        LogSpawnCandidatesAtTime(15f);
    }

    [ContextMenu(
        "Debug Spawn Candidates At 30 Seconds"
    )]
    private void DebugSpawnCandidatesAt30Seconds()
    {
        LogSpawnCandidatesAtTime(30f);
    }


    [ContextMenu(
    "Debug Spawn Candidates At 45 Seconds"
)]
    private void DebugSpawnCandidatesAt45Seconds()
    {
        LogSpawnCandidatesAtTime(45f);
    }
    [ContextMenu(
        "Debug Spawn Candidates At 60 Seconds"
    )]
    private void DebugSpawnCandidatesAt60Seconds()
    {
        LogSpawnCandidatesAtTime(60f);
    }

    /// <summary>
    /// 在指定生存时间下进行多次加权随机抽取，
    /// 并统计每种敌人类型的抽取次数和比例。
    ///
    /// 该测试不会实际生成敌人。
    /// </summary>
    private void RunWeightedSelectionBatchTest(
        float testSurvivalTime,
        int sampleCount
    )
    {
        testSurvivalTime =
            Mathf.Max(0f, testSurvivalTime);

        sampleCount =
            Mathf.Max(1, sampleCount);

        RefreshSpawnCandidates(
            testSurvivalTime
        );

        if (currentSpawnCandidates.Count == 0
            || currentSpawnWeightTotal <= 0f)
        {
            Debug.LogWarning(
                "===== Weighted Batch Test At "
                + testSurvivalTime
                    .ToString("F0")
                + " Seconds =====\n"
                + "没有有效候选，"
                + "批量随机测试已安全取消。",
                this
            );

            return;
        }

        Dictionary<EnemyType, int>
            selectionCounts =
                new Dictionary<
                    EnemyType,
                    int
                >();

        // 先登记所有有效候选类型。
        // 即使某种类型本次抽取为 0，
        // 最终报告中也会显示出来。
        for (int i = 0;
            i < currentSpawnCandidates.Count;
            i++)
        {
            EnemyType candidateType =
                currentSpawnCandidates[i].Type;

            if (!selectionCounts.ContainsKey(
                    candidateType
                ))
            {
                selectionCounts.Add(
                    candidateType,
                    0
                );
            }
        }

        int successfulSelectionCount = 0;
        int failedSelectionCount = 0;

        for (int i = 0;
            i < sampleCount;
            i++)
        {
            EnemySpawnEntry selectedEntry =
                SelectWeightedSpawnEntry(
                    testSurvivalTime
                );

            if (selectedEntry == null)
            {
                failedSelectionCount++;
                continue;
            }

            EnemyType selectedType =
                selectedEntry.Type;

            if (!selectionCounts.ContainsKey(
                    selectedType
                ))
            {
                selectionCounts.Add(
                    selectedType,
                    0
                );
            }

            selectionCounts[selectedType]++;

            successfulSelectionCount++;
        }

        string resultMessage =
            "===== Weighted Batch Test At "
            + testSurvivalTime.ToString("F0")
            + " Seconds =====\n"
            + "Requested Samples: "
            + sampleCount
            + "\nSuccessful Selections: "
            + successfulSelectionCount
            + "\nFailed Selections: "
            + failedSelectionCount
            + "\nTotal Spawn Weight: "
            + currentSpawnWeightTotal
                .ToString("F1");

        foreach (
            KeyValuePair<EnemyType, int>
                result in selectionCounts
        )
        {
            float percentage = 0f;

            if (successfulSelectionCount > 0)
            {
                percentage =
                    result.Value
                    * 100f
                    / successfulSelectionCount;
            }

            resultMessage +=
                "\n"
                + result.Key
                + ": "
                + result.Value
                + " ("
                + percentage.ToString("F2")
                + "%)";
        }

        Debug.Log(
            resultMessage,
            this
        );
    }
    [ContextMenu(
    "Batch Test 1000 Selections At 0 Seconds"
)]
    private void BatchTestAt0Seconds()
    {
        RunWeightedSelectionBatchTest(
            0f,
            1000
        );
    }

    [ContextMenu(
        "Batch Test 1000 Selections At 15 Seconds"
    )]
    private void BatchTestAt15Seconds()
    {
        RunWeightedSelectionBatchTest(
            15f,
            1000
        );
    }

    [ContextMenu(
        "Batch Test 1000 Selections At 30 Seconds"
    )]
    private void BatchTestAt30Seconds()
    {
        RunWeightedSelectionBatchTest(
            30f,
            1000
        );
    }

    [ContextMenu(
    "Batch Test 1000 Selections At 45 Seconds"
)]
    private void BatchTestAt45Seconds()
    {
        RunWeightedSelectionBatchTest(
            45f,
            1000
        );
    }

    [ContextMenu(
        "Batch Test 1000 Selections At 60 Seconds"
    )]
    private void BatchTestAt60Seconds()
    {
        RunWeightedSelectionBatchTest(
            60f,
            1000
        );
    }
    private void OnDrawGizmosSelected()
    {
        Transform center = player;

        if (center == null)
        {
            GameObject playerObject = null;

            try
            {
                playerObject =
                    GameObject
                        .FindGameObjectWithTag(
                            playerTag
                        );
            }
            catch (UnityException)
            {
                // 编辑器中 Tag 尚未创建时不执行查找。
            }

            if (playerObject != null)
            {
                center =
                    playerObject.transform;
            }
            else
            {
                center = transform;
            }
        }

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            center.position,
            minSpawnDistance
        );

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            center.position,
            maxSpawnDistance
        );
    }
}