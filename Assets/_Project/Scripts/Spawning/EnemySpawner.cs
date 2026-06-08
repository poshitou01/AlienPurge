using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int maxEnemies = 5;

    [Header("Spawn Distance")]
    [SerializeField] private float minSpawnDistance = 5f;
    [SerializeField] private float maxSpawnDistance = 8f;

    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Debug")]
    [SerializeField] private bool spawnOnStart = true;

    private Transform player;
    private float spawnTimer;

    private void Start()
    {
        FindPlayer();

        if (spawnOnStart)
        {
            TrySpawnEnemy();
        }
    }

    private void Update()
    {
        if (player == null)
        {
            FindPlayer();

            if (player == null)
            {
                return;
            }
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnEnemy();
        }
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
            Debug.LogWarning("EnemySpawner: 没有找到 Tag 为 Player 的对象，请检查 Player 的 Tag 设置。");
        }
    }

    private void TrySpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("EnemySpawner: enemyPrefab 没有绑定，请在 Inspector 中拖入 Enemy.prefab。");
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("EnemySpawner: player 为空，无法生成 Enemy。");
            return;
        }

        int currentEnemyCount = GameObject.FindGameObjectsWithTag(enemyTag).Length;

        if (currentEnemyCount >= maxEnemies)
        {
            return;
        }

        Vector3 spawnPosition = GetRandomSpawnPositionAroundPlayer();

        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
    }

    private Vector3 GetRandomSpawnPositionAroundPlayer()
    {
        float randomAngle = Random.Range(0f, 360f);
        float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);

        Vector2 direction = new Vector2(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad),
            Mathf.Sin(randomAngle * Mathf.Deg2Rad)
        );

        Vector3 spawnPosition = player.position + (Vector3)(direction * randomDistance);
        spawnPosition.z = 0f;

        return spawnPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Transform center = player;

        if (center == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

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
        Gizmos.DrawWireSphere(center.position, minSpawnDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center.position, maxSpawnDistance);
    }
}