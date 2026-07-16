using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [Tooltip("需要生成的敌人 Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("游戏刚开始时的刷怪间隔")]
    [SerializeField] private float spawnInterval = 2f;

    [Tooltip("游戏刚开始时允许存在的最大敌人数")]
    [SerializeField] private int maxEnemies = 5;

    [Header("Spawn Interval Difficulty")]
    [Tooltip("刷怪间隔允许降低到的最小值")]
    [SerializeField] private float minSpawnInterval = 0.7f;

    [Tooltip("每生存 1 秒，刷怪间隔减少多少秒")]
    [SerializeField] private float spawnIntervalDecreasePerSecond = 0.02f;

    [Header("Enemy Count Difficulty")]
    [Tooltip("场上敌人数量允许提高到的最终上限")]
    [SerializeField] private int maxEnemiesLimit = 12;

    [Tooltip("每隔多少秒提高一次敌人数量上限")]
    [SerializeField] private float maxEnemiesIncreaseInterval = 10f;

    [Tooltip("每次提高多少个敌人数量上限")]
    [SerializeField] private int maxEnemiesIncreaseAmount = 1;

    [Header("Enemy Health Difficulty")]
    [Tooltip("游戏开始时新生成敌人的最大生命值")]
    [SerializeField] private int enemyInitialMaxHealth = 3;

    [Tooltip("每隔多少秒提高一次敌人的最大生命值")]
    [SerializeField] private float enemyHealthIncreaseInterval = 20f;

    [Tooltip("每次提高多少点敌人最大生命值")]
    [SerializeField] private int enemyHealthIncreaseAmount = 1;

    [Tooltip("敌人最大生命值允许成长到的最终上限")]
    [SerializeField] private int enemyMaxHealthLimit = 6;

    [Header("Enemy Move Speed Difficulty")]
    [Tooltip("游戏开始时新生成敌人的移动速度")]
    [SerializeField] private float enemyInitialMoveSpeed = 1.5f;

    [Tooltip("每隔多少秒提高一次敌人的移动速度")]
    [SerializeField] private float enemyMoveSpeedIncreaseInterval = 20f;

    [Tooltip("每次提高多少敌人移动速度")]
    [SerializeField] private float enemyMoveSpeedIncreaseAmount = 0.25f;

    [Tooltip("敌人移动速度允许成长到的最终上限")]
    [SerializeField] private float enemyMoveSpeedLimit = 2.25f;

    [Header("Enemy Contact Damage Difficulty")]
    [Tooltip("游戏开始时新生成敌人的接触伤害")]
    [SerializeField] private int enemyInitialContactDamage = 1;

    [Tooltip("每隔多少秒提高一次敌人的接触伤害")]
    [SerializeField] private float enemyContactDamageIncreaseInterval = 30f;

    [Tooltip("每次提高多少点敌人接触伤害")]
    [SerializeField] private int enemyContactDamageIncreaseAmount = 1;

    [Tooltip("敌人接触伤害允许成长到的最终上限")]
    [SerializeField] private int enemyContactDamageLimit = 3;

    [Header("Spawn Distance")]
    [SerializeField] private float minSpawnDistance = 5f;
    [SerializeField] private float maxSpawnDistance = 8f;

    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Debug Settings")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Runtime Spawn Debug")]
    [Tooltip("当前实际使用的刷怪间隔")]
    [SerializeField] private float currentSpawnInterval;

    [Tooltip("当前实际允许存在的最大敌人数")]
    [SerializeField] private int currentMaxEnemies;

    [Tooltip("最近一次检测到的场上敌人数")]
    [SerializeField] private int currentEnemyCount;

    [Header("Runtime Enemy Attribute Debug")]
    [Tooltip("当前时间点新生成敌人应具有的最大生命值")]
    [SerializeField] private int currentEnemyMaxHealth;

    [Tooltip("当前时间点新生成敌人应具有的移动速度")]
    [SerializeField] private float currentEnemyMoveSpeed;

    [Tooltip("当前时间点新生成敌人应具有的接触伤害")]
    [SerializeField] private int currentEnemyContactDamage;

    private Transform player;
    private float spawnTimer;

    public int CurrentEnemyMaxHealth => currentEnemyMaxHealth;
    public float CurrentEnemyMoveSpeed => currentEnemyMoveSpeed;
    public int CurrentEnemyContactDamage => currentEnemyContactDamage;

    private void Start()
    {
        FindPlayer();

        UpdateDifficulty();

        spawnTimer = 0f;

        if (spawnOnStart && CanSpawnEnemies())
        {
            TrySpawnEnemy();
        }
    }

    private void Update()
    {
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

        if (GameManager.Instance.CurrentState != GameState.Playing)
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
            survivalTime = GameManager.Instance.SurvivalTime;
        }

        survivalTime = Mathf.Max(0f, survivalTime);

        UpdateSpawnDifficulty(survivalTime);
        UpdateEnemyAttributeDifficulty(survivalTime);
    }

    private void UpdateSpawnDifficulty(float survivalTime)
    {
        currentSpawnInterval =
            spawnInterval -
            survivalTime * spawnIntervalDecreasePerSecond;

        currentSpawnInterval = Mathf.Max(
            minSpawnInterval,
            currentSpawnInterval
        );

        int enemyCountIncreaseCount = Mathf.FloorToInt(
            survivalTime / maxEnemiesIncreaseInterval
        );

        currentMaxEnemies =
            maxEnemies +
            enemyCountIncreaseCount * maxEnemiesIncreaseAmount;

        currentMaxEnemies = Mathf.Min(
            currentMaxEnemies,
            maxEnemiesLimit
        );
    }

    private void UpdateEnemyAttributeDifficulty(float survivalTime)
    {
        currentEnemyMaxHealth =
            CalculateEnemyMaxHealth(survivalTime);

        currentEnemyMoveSpeed =
            CalculateEnemyMoveSpeed(survivalTime);

        currentEnemyContactDamage =
            CalculateEnemyContactDamage(survivalTime);
    }

    private int CalculateEnemyMaxHealth(float survivalTime)
    {
        survivalTime = Mathf.Max(0f, survivalTime);

        int increaseCount = Mathf.FloorToInt(
            survivalTime / enemyHealthIncreaseInterval
        );

        int calculatedMaxHealth =
            enemyInitialMaxHealth +
            increaseCount * enemyHealthIncreaseAmount;

        return Mathf.Min(
            calculatedMaxHealth,
            enemyMaxHealthLimit
        );
    }

    private float CalculateEnemyMoveSpeed(float survivalTime)
    {
        survivalTime = Mathf.Max(0f, survivalTime);

        int increaseCount = Mathf.FloorToInt(
            survivalTime / enemyMoveSpeedIncreaseInterval
        );

        float calculatedMoveSpeed =
            enemyInitialMoveSpeed +
            increaseCount * enemyMoveSpeedIncreaseAmount;

        return Mathf.Min(
            calculatedMoveSpeed,
            enemyMoveSpeedLimit
        );
    }

    private int CalculateEnemyContactDamage(float survivalTime)
    {
        survivalTime = Mathf.Max(0f, survivalTime);

        int increaseCount = Mathf.FloorToInt(
            survivalTime / enemyContactDamageIncreaseInterval
        );

        int calculatedDamage =
            enemyInitialContactDamage +
            increaseCount * enemyContactDamageIncreaseAmount;

        return Mathf.Min(
            calculatedDamage,
            enemyContactDamageLimit
        );
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning(
                "EnemySpawner: 没有找到 Tag 为 Player 的对象，请检查 Player 的 Tag 设置。"
            );
        }
    }

    private void TrySpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning(
                "EnemySpawner: enemyPrefab 没有绑定，请在 Inspector 中拖入 Enemy.prefab。"
            );

            return;
        }

        if (player == null)
        {
            Debug.LogWarning(
                "EnemySpawner: player 为空，无法生成 Enemy。"
            );

            return;
        }

        currentEnemyCount =
            GameObject.FindGameObjectsWithTag(enemyTag).Length;

        if (currentEnemyCount >= currentMaxEnemies)
        {
            return;
        }

        Vector3 spawnPosition =
            GetRandomSpawnPositionAroundPlayer();

        GameObject spawnedEnemy = Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity
        );

        InitializeEnemyAttributes(spawnedEnemy);

        currentEnemyCount++;
    }

    /// <summary>
    /// 将当前难度属性应用到本次新生成的敌人。
    /// 已经存在的敌人不会受到影响。
    /// </summary>
    private void InitializeEnemyAttributes(GameObject spawnedEnemy)
    {
        if (spawnedEnemy == null)
        {
            Debug.LogWarning(
                "EnemySpawner: 生成的 Enemy 对象为空，无法初始化属性。"
            );

            return;
        }

        EnemyHealth enemyHealth =
            spawnedEnemy.GetComponent<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.InitializeHealth(
                currentEnemyMaxHealth
            );
        }
        else
        {
            Debug.LogWarning(
                $"{spawnedEnemy.name} 没有找到 EnemyHealth，无法初始化生命值。"
            );
        }

        EnemyMovement enemyMovement =
            spawnedEnemy.GetComponent<EnemyMovement>();

        if (enemyMovement != null)
        {
            enemyMovement.InitializeMoveSpeed(
                currentEnemyMoveSpeed
            );
        }
        else
        {
            Debug.LogWarning(
                $"{spawnedEnemy.name} 没有找到 EnemyMovement，无法初始化移动速度。"
            );
        }

        EnemyContactDamage enemyContactDamage =
            spawnedEnemy.GetComponent<EnemyContactDamage>();

        if (enemyContactDamage != null)
        {
            enemyContactDamage.InitializeDamage(
                currentEnemyContactDamage
            );
        }
        else
        {
            Debug.LogWarning(
                $"{spawnedEnemy.name} 没有找到 EnemyContactDamage，无法初始化接触伤害。"
            );
        }

        Debug.Log(
            $"{spawnedEnemy.name} 属性初始化完成：" +
            $" HP={currentEnemyMaxHealth}," +
            $" Speed={currentEnemyMoveSpeed:F2}," +
            $" Damage={currentEnemyContactDamage}"
        );
    }

    private Vector3 GetRandomSpawnPositionAroundPlayer()
    {
        float randomAngle = Random.Range(0f, 360f);

        float randomDistance = Random.Range(
            minSpawnDistance,
            maxSpawnDistance
        );

        Vector2 direction = new Vector2(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad),
            Mathf.Sin(randomAngle * Mathf.Deg2Rad)
        );

        Vector3 spawnPosition =
            player.position +
            (Vector3)(direction * randomDistance);

        spawnPosition.z = 0f;

        return spawnPosition;
    }

    private void LogDifficultyAtTime(float testSurvivalTime)
    {
        testSurvivalTime = Mathf.Max(0f, testSurvivalTime);

        float testSpawnInterval =
            spawnInterval -
            testSurvivalTime * spawnIntervalDecreasePerSecond;

        testSpawnInterval = Mathf.Max(
            minSpawnInterval,
            testSpawnInterval
        );

        int enemyCountIncreaseCount = Mathf.FloorToInt(
            testSurvivalTime / maxEnemiesIncreaseInterval
        );

        int testMaxEnemies =
            maxEnemies +
            enemyCountIncreaseCount * maxEnemiesIncreaseAmount;

        testMaxEnemies = Mathf.Min(
            testMaxEnemies,
            maxEnemiesLimit
        );

        int testEnemyMaxHealth =
            CalculateEnemyMaxHealth(testSurvivalTime);

        float testEnemyMoveSpeed =
            CalculateEnemyMoveSpeed(testSurvivalTime);

        int testEnemyContactDamage =
            CalculateEnemyContactDamage(testSurvivalTime);

        Debug.Log(
            $"===== Difficulty At {testSurvivalTime:F0} Seconds =====\n" +
            $"Spawn Interval: {testSpawnInterval:F2}\n" +
            $"Max Enemies: {testMaxEnemies}\n" +
            $"Enemy Max Health: {testEnemyMaxHealth}\n" +
            $"Enemy Move Speed: {testEnemyMoveSpeed:F2}\n" +
            $"Enemy Contact Damage: {testEnemyContactDamage}"
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

        spawnIntervalDecreasePerSecond = Mathf.Max(
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

        maxEnemiesIncreaseInterval = Mathf.Max(
            0.1f,
            maxEnemiesIncreaseInterval
        );

        maxEnemiesIncreaseAmount = Mathf.Max(
            1,
            maxEnemiesIncreaseAmount
        );

        enemyInitialMaxHealth = Mathf.Max(
            1,
            enemyInitialMaxHealth
        );

        enemyHealthIncreaseInterval = Mathf.Max(
            0.1f,
            enemyHealthIncreaseInterval
        );

        enemyHealthIncreaseAmount = Mathf.Max(
            1,
            enemyHealthIncreaseAmount
        );

        enemyMaxHealthLimit = Mathf.Max(
            enemyInitialMaxHealth,
            enemyMaxHealthLimit
        );

        enemyInitialMoveSpeed = Mathf.Max(
            0f,
            enemyInitialMoveSpeed
        );

        enemyMoveSpeedIncreaseInterval = Mathf.Max(
            0.1f,
            enemyMoveSpeedIncreaseInterval
        );

        enemyMoveSpeedIncreaseAmount = Mathf.Max(
            0f,
            enemyMoveSpeedIncreaseAmount
        );

        enemyMoveSpeedLimit = Mathf.Max(
            enemyInitialMoveSpeed,
            enemyMoveSpeedLimit
        );

        enemyInitialContactDamage = Mathf.Max(
            1,
            enemyInitialContactDamage
        );

        enemyContactDamageIncreaseInterval = Mathf.Max(
            0.1f,
            enemyContactDamageIncreaseInterval
        );

        enemyContactDamageIncreaseAmount = Mathf.Max(
            1,
            enemyContactDamageIncreaseAmount
        );

        enemyContactDamageLimit = Mathf.Max(
            enemyInitialContactDamage,
            enemyContactDamageLimit
        );

        minSpawnDistance = Mathf.Max(
            0f,
            minSpawnDistance
        );

        maxSpawnDistance = Mathf.Max(
            minSpawnDistance,
            maxSpawnDistance
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
                    GameObject.FindGameObjectWithTag(playerTag);
            }
            catch (UnityException)
            {
                // 编辑器中 Tag 尚未创建时，不执行查找。
            }

            if (playerObject != null)
            {
                center = playerObject.transform;
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