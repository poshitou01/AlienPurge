using UnityEngine;

/// <summary>
/// EnemyPool 提供给 PooledEnemy 的最小回收接口。
/// </summary>
public interface IEnemyPoolReturnHandler
{
    void ReturnEnemy(PooledEnemy enemy);
}

/// <summary>
/// 保存敌人的对象池归属，并协调各行为组件
/// 执行自己的复用状态重置。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyContactDamage))]
public class PooledEnemy : MonoBehaviour
{
    [Header("Enemy Identity")]

    [Tooltip("该对象对应的敌人类型")]
    [SerializeField]
    private EnemyType enemyType =
        EnemyType.Normal;

    [Header("Runtime Pool State")]

    [Tooltip("当前是否已经返回对象池")]
    [SerializeField]
    private bool isReturned = true;

    [Tooltip("运行时所属的 EnemyPool 组件")]
    [SerializeField]
    private MonoBehaviour owningPool;

    private IEnemyPoolReturnHandler
        poolReturnHandler;

    private EnemyHealth enemyHealth;
    private EnemyMovement enemyMovement;
    private EnemyContactDamage
        enemyContactDamage;

    public EnemyType Type =>
        enemyType;

    public bool IsReturned =>
        isReturned;

    public bool HasOwningPool =>
        owningPool != null
        && poolReturnHandler != null;

    private void Awake()
    {
        CacheComponents();
    }

    public void InitializePoolMembership(
        MonoBehaviour newOwningPool,
        EnemyType newEnemyType
    )
    {
        owningPool = newOwningPool;
        enemyType = newEnemyType;

        poolReturnHandler =
            newOwningPool
            as IEnemyPoolReturnHandler;

        isReturned = true;

        CacheComponents();

        if (newOwningPool == null)
        {
            Debug.LogWarning(
                gameObject.name
                + " 初始化对象池归属时，"
                + "没有收到有效的 EnemyPool。",
                this
            );

            return;
        }

        if (poolReturnHandler == null)
        {
            Debug.LogError(
                newOwningPool.name
                + " 没有实现 "
                + "IEnemyPoolReturnHandler，"
                + gameObject.name
                + " 将无法正常回收。",
                this
            );
        }
    }

    /// <summary>
    /// 每次从对象池取出时调用。
    ///
    /// PooledEnemy 只负责协调，各组件仍然负责
    /// 重置自己管理的运行时状态。
    /// </summary>
    public void PrepareForSpawn()
    {
        isReturned = false;

        CacheComponents();

        if (enemyHealth != null)
        {
            enemyHealth.PrepareForSpawn();
        }
        else
        {
            Debug.LogError(
                gameObject.name
                + " 缺少 EnemyHealth。",
                this
            );
        }

        if (enemyMovement != null)
        {
            enemyMovement.PrepareForSpawn();
        }
        else
        {
            Debug.LogError(
                gameObject.name
                + " 缺少 EnemyMovement。",
                this
            );
        }

        if (enemyContactDamage != null)
        {
            enemyContactDamage
                .PrepareForSpawn();
        }
        else
        {
            Debug.LogError(
                gameObject.name
                + " 缺少 EnemyContactDamage。",
                this
            );
        }
    }

    public void ReturnToPool()
    {
        if (isReturned)
        {
            Debug.LogWarning(
                gameObject.name
                + " 已经返回对象池，"
                + "本次重复回收请求已忽略。",
                this
            );

            return;
        }

        isReturned = true;

        if (owningPool == null
            || poolReturnHandler == null)
        {
            Debug.LogWarning(
                gameObject.name
                + " 没有有效的 EnemyPool，"
                + "将使用 Destroy 兼容处理。",
                this
            );

            Destroy(gameObject);
            return;
        }

        poolReturnHandler.ReturnEnemy(this);
    }

    internal void MarkAsReturned()
    {
        isReturned = true;
    }

    private void CacheComponents()
    {
        if (enemyHealth == null)
        {
            enemyHealth =
                GetComponent<EnemyHealth>();
        }

        if (enemyMovement == null)
        {
            enemyMovement =
                GetComponent<EnemyMovement>();
        }

        if (enemyContactDamage == null)
        {
            enemyContactDamage =
                GetComponent<
                    EnemyContactDamage
                >();
        }
    }

    private void OnDestroy()
    {
        owningPool = null;
        poolReturnHandler = null;
    }
}