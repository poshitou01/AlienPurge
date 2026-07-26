using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [Tooltip("��ͨ���� Prefab����ǰ�Զ�ˢ����ʱֻʹ����ͨ����")]
    [SerializeField] private GameObject normalEnemyPrefab;

    [Tooltip("���ٵ��� Prefab��Ŀǰ�����ڶ�����������")]
    [SerializeField] private GameObject fastEnemyPrefab;

    [Tooltip("�ؼ׵��� Prefab��Ŀǰ�����ڶ�����������")]
    [SerializeField] private GameObject heavyEnemyPrefab;

    [Header("Spawner Settings")]
    [Tooltip("��Ϸ�տ�ʼʱ��ˢ�ּ��")]
    [SerializeField] private float spawnInterval = 2f;

    [Tooltip("��Ϸ�տ�ʼʱ������ڵ���������")]
    [SerializeField] private int maxEnemies = 5;

    [Header("Spawn Interval Difficulty")]
    [Tooltip("ˢ�ּ��������͵�����Сֵ")]
    [SerializeField] private float minSpawnInterval = 0.7f;

    [Tooltip("ÿ���� 1 �룬ˢ�ּ����ٶ�����")]
    [SerializeField]
    private float spawnIntervalDecreasePerSecond = 0.02f;

    [Header("Enemy Count Difficulty")]
    [Tooltip("���ϵ�������������ߵ�����������")]
    [SerializeField] private int maxEnemiesLimit = 12;

    [Tooltip("ÿ����������һ�ε�����������")]
    [SerializeField]
    private float maxEnemiesIncreaseInterval = 10f;

    [Tooltip("ÿ����߶��ٸ�������������")]
    [SerializeField]
    private int maxEnemiesIncreaseAmount = 1;

    [Header("Enemy Health Difficulty")]
    [Tooltip("��Ϸ��ʼʱ��ͨ���˵�ȫ�ֻ�������ֵ")]
    [SerializeField] private int enemyInitialMaxHealth = 3;

    [Tooltip("ÿ����������һ��ȫ�ֻ�������ֵ")]
    [SerializeField]
    private float enemyHealthIncreaseInterval = 20f;

    [Tooltip("ÿ����߶��ٵ�ȫ�ֻ�������ֵ")]
    [SerializeField]
    private int enemyHealthIncreaseAmount = 1;

    [Tooltip("ȫ�ֻ�������ֵ����ɳ�������������")]
    [SerializeField] private int enemyMaxHealthLimit = 6;

    [Header("Enemy Move Speed Difficulty")]
    [Tooltip("��Ϸ��ʼʱ��ͨ���˵�ȫ�ֻ����ƶ��ٶ�")]
    [SerializeField]
    private float enemyInitialMoveSpeed = 1.5f;

    [Tooltip("ÿ����������һ��ȫ�ֻ����ƶ��ٶ�")]
    [SerializeField]
    private float enemyMoveSpeedIncreaseInterval = 20f;

    [Tooltip("ÿ����߶���ȫ�ֻ����ƶ��ٶ�")]
    [SerializeField]
    private float enemyMoveSpeedIncreaseAmount = 0.25f;

    [Tooltip("ȫ�ֻ����ƶ��ٶ�����ɳ�������������")]
    [SerializeField]
    private float enemyMoveSpeedLimit = 2.25f;

    [Header("Enemy Contact Damage Difficulty")]
    [Tooltip("��Ϸ��ʼʱ��ͨ���˵�ȫ�ֻ����Ӵ��˺�")]
    [SerializeField]
    private int enemyInitialContactDamage = 1;

    [Tooltip("ÿ����������һ��ȫ�ֻ����Ӵ��˺�")]
    [SerializeField]
    private float enemyContactDamageIncreaseInterval = 30f;

    [Tooltip("ÿ����߶��ٵ�ȫ�ֻ����Ӵ��˺�")]
    [SerializeField]
    private int enemyContactDamageIncreaseAmount = 1;

    [Tooltip("ȫ�ֻ����Ӵ��˺�����ɳ�������������")]
    [SerializeField]
    private int enemyContactDamageLimit = 3;

    [Header("Spawn Distance")]
    [SerializeField] private float minSpawnDistance = 5f;
    [SerializeField] private float maxSpawnDistance = 8f;

    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Debug Settings")]
    [Tooltip("�Ƿ����������ļ�ʱ�Զ�ˢ��")]
    [SerializeField] private bool enableAutomaticSpawning = true;

    [Tooltip("������Ϸ���Ƿ���������һ����ͨ����")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Runtime Spawn Debug")]
    [Tooltip("��ǰʵ��ʹ�õ�ˢ�ּ��")]
    [SerializeField] private float currentSpawnInterval;

    [Tooltip("��ǰʵ��������ڵ���������")]
    [SerializeField] private int currentMaxEnemies;

    [Tooltip("���һ�μ�⵽�ĳ��ϵ�����")]
    [SerializeField] private int currentEnemyCount;

    [Header("Runtime Enemy Attribute Debug")]
    [Tooltip("��ǰʱ����ȫ�ֻ����������ֵ")]
    [SerializeField] private int currentEnemyMaxHealth;

    [Tooltip("��ǰʱ����ȫ�ֻ����ƶ��ٶ�")]
    [SerializeField] private float currentEnemyMoveSpeed;

    [Tooltip("��ǰʱ����ȫ�ֻ����Ӵ��˺�")]
    [SerializeField] private int currentEnemyContactDamage;

    private Transform player;
    private float spawnTimer;

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
                "EnemySpawner: û���ҵ� Tag Ϊ "
                + playerTag
                + " �Ķ���",
                this
            );
        }
    }

    /// <summary>
    /// ��ǰ��ʽ�Զ�ˢ����ʱֻ������ͨ���ˡ�
    /// ���׶β�ʵ���������Ȩ�ء�
    /// </summary>
    private void TrySpawnEnemy()
    {
        SpawnEnemyFromPrefab(
            normalEnemyPrefab,
            true,
            "Normal"
        );
    }

    /// <summary>
    /// ����ָ�����ˣ���ͨ�� EnemyDefinition
    /// Ӧ�õ�ǰȫ���Ѷ����������ͱ��ʡ�
    /// </summary>
    private bool SpawnEnemyFromPrefab(
        GameObject enemyPrefab,
        bool respectEnemyLimit,
        string enemyLabel
    )
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning(
                "EnemySpawner: "
                + enemyLabel
                + " Enemy Prefab û�а󶨡�",
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
                    "EnemySpawner: Player Ϊ�գ�"
                    + "�޷����� "
                    + enemyLabel
                    + " Enemy��",
                    this
                );

                return false;
            }
        }

        RefreshCurrentEnemyCount();

        if (respectEnemyLimit
            && currentEnemyCount >= currentMaxEnemies)
        {
            return false;
        }

        Vector3 spawnPosition =
            GetRandomSpawnPositionAroundPlayer();

        GameObject spawnedEnemy =
            Instantiate(
                enemyPrefab,
                spawnPosition,
                Quaternion.identity
            );

        if (!InitializeEnemyAttributes(
                spawnedEnemy
            ))
        {
            Destroy(spawnedEnemy);
            return false;
        }

        RefreshCurrentEnemyCount();

        return true;
    }

    /// <summary>
    /// ����ǰȫ���ѶȻ������Դ��ݸ� EnemyDefinition��
    ///
    /// EnemyDefinition �ٸ��ݰ󶨵� EnemyData
    /// �����Ӧ�������͵��������ԡ�
    /// </summary>
    private bool InitializeEnemyAttributes(
        GameObject spawnedEnemy
    )
    {
        if (spawnedEnemy == null)
        {
            Debug.LogError(
                "EnemySpawner: ���ɵĵ���Ϊ�գ�"
                + "�޷���ʼ�����ԡ�",
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
                + " û�� EnemyDefinition��"
                + "�޷�Ӧ�õ����������ݡ�",
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
                + " �� EnemyDefinition "
                + "��ʼ��ʧ�ܡ�",
                spawnedEnemy
            );

            return false;
        }

        return true;
    }

    private void RefreshCurrentEnemyCount()
    {
        currentEnemyCount =
            GameObject.FindGameObjectsWithTag(
                enemyTag
            ).Length;
    }

    private Vector3
        GetRandomSpawnPositionAroundPlayer()
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

    /// <summary>
    /// ��������������ڡ�
    /// �������ɺ��������������ƣ�
    /// ����Ҫ���� Playing ״̬��
    /// </summary>
    private void SpawnEnemyForTesting(
        GameObject enemyPrefab,
        string enemyLabel
    )
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "����� Play ģʽ���ٲ������� "
                + enemyLabel
                + " Enemy��",
                this
            );

            return;
        }

        if (!CanSpawnEnemies())
        {
            Debug.LogWarning(
                "��ǰ��Ϸ״̬���������ɵ��ˡ�",
                this
            );

            return;
        }

        UpdateDifficulty();

        SpawnEnemyFromPrefab(
            enemyPrefab,
            false,
            enemyLabel
        );
    }

    [ContextMenu("Test Spawn Normal Enemy")]
    private void TestSpawnNormalEnemy()
    {
        SpawnEnemyForTesting(
            normalEnemyPrefab,
            "Normal"
        );
    }

    [ContextMenu("Test Spawn Fast Enemy")]
    private void TestSpawnFastEnemy()
    {
        SpawnEnemyForTesting(
            fastEnemyPrefab,
            "Fast"
        );
    }

    [ContextMenu("Test Spawn Heavy Enemy")]
    private void TestSpawnHeavyEnemy()
    {
        SpawnEnemyForTesting(
            heavyEnemyPrefab,
            "Heavy"
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
    /// ���ָ������ʱ�������ֵ��˵��������ԡ�
    ///
    /// �˷���ֻ������ֵԤ�����������ɵ��ˣ�
    /// Ҳ�����޸ĵ�ǰ��Ϸ״̬��
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
            normalEnemyPrefab,
            "Normal",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );

        LogSingleEnemyTypeStats(
            fastEnemyPrefab,
            "Fast",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );

        LogSingleEnemyTypeStats(
            heavyEnemyPrefab,
            "Heavy",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );
    }

    /// <summary>
    /// ��ȡָ������ Prefab �󶨵� EnemyData��
    /// �����Ӧ�����ͱ��ʺ���������ԡ�
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
                + " Enemy Prefab û�а󶨣�"
                + "�޷�Ԥ�����ԡ�",
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
                + " û�� EnemyDefinition��"
                + "�޷�Ԥ�����ԡ�",
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
                + " û�а� EnemyData��"
                + "�޷�Ԥ�����ԡ�",
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
    /// �� EnemyDefinition ʹ����ͬ�������������������
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
                // �༭���� Tag ��δ����ʱ��ִ�в��ҡ�
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