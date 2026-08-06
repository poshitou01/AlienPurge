using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyProjectilePool :
    MonoBehaviour
{
    public static EnemyProjectilePool Instance
    {
        get;
        private set;
    }

    [Header("Pool Settings")]
    [Tooltip("对象池使用的敌方投射物 Prefab")]
    [SerializeField]
    private EnemyProjectile projectilePrefab;

    [Min(1)]
    [SerializeField]
    private int initialPoolSize = 16;

    [SerializeField]
    private bool allowExpansion = true;

    [Min(1)]
    [SerializeField]
    private int expansionAmount = 8;

    [Header("Runtime Debug")]
    [SerializeField]
    private int totalCount;

    [SerializeField]
    private int activeCount;

    [SerializeField]
    private int availableCount;

    private readonly Queue<EnemyProjectile>
        availableProjectiles =
            new Queue<EnemyProjectile>();

    private readonly HashSet<EnemyProjectile>
        availableProjectileLookup =
            new HashSet<EnemyProjectile>();

    private readonly HashSet<EnemyProjectile>
        activeProjectileLookup =
            new HashSet<EnemyProjectile>();

    private readonly HashSet<EnemyProjectile>
        allProjectileLookup =
            new HashSet<EnemyProjectile>();

    private Transform availableContainer;
    private Transform activeContainer;

    public int TotalCount =>
        totalCount;

    public int ActiveCount =>
        activeCount;

    public int AvailableCount =>
        availableCount;

    private void Awake()
    {
        if (Instance != null
            && Instance != this)
        {
            Debug.LogError(
                "场景中存在重复的 "
                + "EnemyProjectilePool，"
                + "后创建的对象将被销毁。",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        CreateRuntimeContainers();
        PrewarmPool();
        RefreshDebugCounts();
    }

    private void CreateRuntimeContainers()
    {
        GameObject availableObject =
            new GameObject(
                "AvailableEnemyProjectiles"
            );

        availableObject.transform.SetParent(
            transform,
            false
        );

        availableContainer =
            availableObject.transform;

        availableObject.SetActive(false);

        GameObject activeObject =
            new GameObject(
                "ActiveEnemyProjectiles"
            );

        activeObject.transform.SetParent(
            transform,
            false
        );

        activeContainer =
            activeObject.transform;
    }

    private void PrewarmPool()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError(
                "EnemyProjectilePool 没有绑定 "
                + "EnemyProjectile Prefab。",
                this
            );

            return;
        }

        CreateProjectiles(
            initialPoolSize
        );
    }

    private int CreateProjectiles(
        int amount
    )
    {
        if (projectilePrefab == null)
        {
            return 0;
        }

        amount = Mathf.Max(1, amount);

        int createdCount = 0;

        for (int i = 0;
            i < amount;
            i++)
        {
            EnemyProjectile projectile =
                Instantiate(
                    projectilePrefab,
                    availableContainer
                );

            projectile.SetPool(this);

            projectile.name =
                projectilePrefab.name
                + "_Pooled_"
                + allProjectileLookup.Count;

            projectile.gameObject.SetActive(
                false
            );

            availableProjectiles.Enqueue(
                projectile
            );

            availableProjectileLookup.Add(
                projectile
            );

            allProjectileLookup.Add(
                projectile
            );

            createdCount++;
        }

        RefreshDebugCounts();

        return createdCount;
    }

    public EnemyProjectile GetProjectile(
        Vector3 spawnPosition,
        Quaternion spawnRotation
    )
    {
        EnemyProjectile projectile =
            GetNextAvailableProjectile();

        if (projectile == null)
        {
            Debug.LogWarning(
                "EnemyProjectilePool "
                + "当前没有可用投射物。",
                this
            );

            return null;
        }

        if (!allProjectileLookup.Contains(
                projectile
            ))
        {
            Debug.LogError(
                "可用队列中出现了未登记的"
                + "敌方投射物。",
                this
            );

            return null;
        }

        availableProjectileLookup.Remove(
            projectile
        );

        if (!activeProjectileLookup.Add(
                projectile
            ))
        {
            Debug.LogError(
                projectile.name
                + " 已经处于激活状态，"
                + "本次取出已取消。",
                projectile
            );

            availableProjectiles.Enqueue(
                projectile
            );

            availableProjectileLookup.Add(
                projectile
            );

            RefreshDebugCounts();
            return null;
        }

        projectile.gameObject.SetActive(
            false
        );

        projectile.transform.SetParent(
            activeContainer,
            false
        );

        projectile.transform
            .SetPositionAndRotation(
                spawnPosition,
                spawnRotation
            );

        projectile.gameObject.SetActive(
            true
        );

        RefreshDebugCounts();

        return projectile;
    }

    private EnemyProjectile
        GetNextAvailableProjectile()
    {
        while (availableProjectiles.Count
            > 0)
        {
            EnemyProjectile projectile =
                availableProjectiles.Dequeue();

            if (projectile != null)
            {
                return projectile;
            }
        }

        if (!allowExpansion)
        {
            RefreshDebugCounts();
            return null;
        }

        int createdCount =
            CreateProjectiles(
                expansionAmount
            );

        if (createdCount <= 0
            || availableProjectiles.Count
            <= 0)
        {
            return null;
        }

        return availableProjectiles
            .Dequeue();
    }

    public void ReturnProjectile(
        EnemyProjectile projectile
    )
    {
        if (projectile == null)
        {
            return;
        }

        if (!allProjectileLookup.Contains(
                projectile
            ))
        {
            Debug.LogError(
                projectile.name
                + " 不属于当前 "
                + "EnemyProjectilePool。",
                projectile
            );

            return;
        }

        if (availableProjectileLookup
            .Contains(projectile))
        {
            Debug.LogWarning(
                projectile.name
                + " 已经位于可用队列中，"
                + "重复回收已忽略。",
                projectile
            );

            return;
        }

        if (!activeProjectileLookup.Remove(
                projectile
            ))
        {
            Debug.LogWarning(
                projectile.name
                + " 未登记为 Active，"
                + "但仍将尝试恢复池状态。",
                projectile
            );
        }

        projectile.gameObject.SetActive(
            false
        );

        projectile.transform.SetParent(
            availableContainer,
            false
        );

        availableProjectiles.Enqueue(
            projectile
        );

        availableProjectileLookup.Add(
            projectile
        );

        RefreshDebugCounts();
    }

    private void RefreshDebugCounts()
    {
        totalCount =
            allProjectileLookup.Count;

        activeCount =
            activeProjectileLookup.Count;

        availableCount =
            availableProjectiles.Count;
    }

    [ContextMenu("Debug/Print Pool Status")]
    private void PrintPoolStatus()
    {
        RefreshDebugCounts();

        Debug.Log(
            "===== Enemy Projectile Pool ====="
            + "\nTotal: "
            + totalCount
            + "\nActive: "
            + activeCount
            + "\nAvailable: "
            + availableCount
            + "\nAllow Expansion: "
            + allowExpansion
            + "\nExpansion Amount: "
            + expansionAmount,
            this
        );
    }

    [ContextMenu("Debug/Validate Pool State")]
    private void ValidatePoolState()
    {
        RefreshDebugCounts();

        bool totalIsValid =
            totalCount
            == activeCount
            + availableCount;

        bool availableIsValid =
            availableCount
            == availableProjectileLookup.Count;

        bool activeIsValid =
            activeCount
            == activeProjectileLookup.Count;

        bool hierarchyIsValid =
            availableContainer != null
            && activeContainer != null
            && availableContainer.childCount
                == availableCount
            && activeContainer.childCount
                == activeCount;

        bool collectionsDoNotOverlap =
            !activeProjectileLookup.Overlaps(
                availableProjectileLookup
            );

        bool passed =
            totalIsValid
            && availableIsValid
            && activeIsValid
            && hierarchyIsValid
            && collectionsDoNotOverlap;

        Debug.Log(
            "===== Enemy Projectile Pool Validation ====="
            + "\nResult: "
            + (passed ? "PASS" : "FAIL")
            + "\nTotal = Active + Available: "
            + totalIsValid
            + "\nAvailable Records Match: "
            + availableIsValid
            + "\nActive Records Match: "
            + activeIsValid
            + "\nHierarchy Counts Match: "
            + hierarchyIsValid
            + "\nCollections Do Not Overlap: "
            + collectionsDoNotOverlap
            + "\nTotal: "
            + totalCount
            + "\nActive: "
            + activeCount
            + "\nAvailable: "
            + availableCount,
            this
        );
    }

    [ContextMenu(
        "Debug/Fire Test Projectile At Player"
    )]
    private void FireTestProjectileAtPlayer()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后再测试"
                + "敌方投射物。",
                this
            );

            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject == null)
        {
            Debug.LogWarning(
                "没有找到 Player。",
                this
            );

            return;
        }

        Vector3 spawnPosition =
            playerObject.transform.position
            + Vector3.left * 4f;

        Vector2 direction =
            playerObject.transform.position
            - spawnPosition;

        EnemyProjectile projectile =
            GetProjectile(
                spawnPosition,
                Quaternion.identity
            );

        if (projectile == null)
        {
            return;
        }

        projectile.Initialize(
            direction,
            7f,
            1,
            3f,
            1f
        );
    }

    [ContextMenu(
        "Debug/Test Duplicate Return Protection"
    )]
    private void TestDuplicateReturnProtection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play Mode 后再测试"
                + "重复回收保护。",
                this
            );

            return;
        }

        EnemyProjectile projectile =
            GetProjectile(
                transform.position,
                Quaternion.identity
            );

        if (projectile == null)
        {
            return;
        }

        ReturnProjectile(projectile);
        ReturnProjectile(projectile);

        ValidatePoolState();
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

        availableProjectiles.Clear();
        availableProjectileLookup.Clear();
        activeProjectileLookup.Clear();
        allProjectileLookup.Clear();
    }
}