using UnityEngine;

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

    [Header("Spawn Distance")]
    [SerializeField] private float minSpawnDistance = 5f;
    [SerializeField] private float maxSpawnDistance = 8f;

    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Debug Settings")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Runtime Debug")]
    [Tooltip("当前实际使用的刷怪间隔")]
    [SerializeField] private float currentSpawnInterval;

    [Tooltip("当前实际允许存在的最大敌人数")]
    [SerializeField] private int currentMaxEnemies;

    [Tooltip("最近一次检测到的场上敌人数")]
    [SerializeField] private int currentEnemyCount;

    private Transform player;
    private float spawnTimer;

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

    /// <summary>
    /// 判断当前是否允许继续刷怪。
    /// </summary>
    private bool CanSpawnEnemies()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }

        // GameOver 或 Victory 后不再继续刷怪。
        if (GameManager.Instance.CurrentState != GameState.Playing)
        {
            return false;
        }

        // 升级三选一期间停止刷怪计时和刷怪。
        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 根据当前生存时间计算刷怪间隔和敌人数量上限。
    /// </summary>
    private void UpdateDifficulty()
    {
        float survivalTime = 0f;

        if (GameManager.Instance != null)
        {
            survivalTime = GameManager.Instance.SurvivalTime;
        }

        // 生存时间越长，刷怪间隔越短。
        currentSpawnInterval =
            spawnInterval -
            survivalTime * spawnIntervalDecreasePerSecond;

        // 不允许低于最小刷怪间隔。
        currentSpawnInterval = Mathf.Max(
            minSpawnInterval,
            currentSpawnInterval
        );

        // 计算敌人数量上限已经成长了多少次。
        int increaseCount = Mathf.FloorToInt(
            survivalTime / maxEnemiesIncreaseInterval
        );

        currentMaxEnemies =
            maxEnemies +
            increaseCount * maxEnemiesIncreaseAmount;

        // 不允许超过敌人数量的最终上限。
        currentMaxEnemies = Mathf.Min(
            currentMaxEnemies,
            maxEnemiesLimit
        );
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

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

        Vector3 spawnPosition = GetRandomSpawnPositionAroundPlayer();

        Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity
        );

        currentEnemyCount++;
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

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.01f, spawnInterval);

        minSpawnInterval = Mathf.Clamp(
            minSpawnInterval,
            0.01f,
            spawnInterval
        );

        spawnIntervalDecreasePerSecond = Mathf.Max(
            0f,
            spawnIntervalDecreasePerSecond
        );

        maxEnemies = Mathf.Max(1, maxEnemies);

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