using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Phase40 - Enemy A* Navigation
///
/// 职责：
/// 1. 向 A* Seeker 请求路径。
/// 2. 保存路径点。
/// 3. 为 EnemyMovement 提供下一个目标点。
/// 4. 对象池复用时清理旧路径。
///
/// 本组件不直接移动 Rigidbody2D。
///
/// 目标版本：
/// A* Pathfinding Project Free 4.2.17
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(EnemyMovement))]
public sealed class EnemyPathNavigator : MonoBehaviour
{
    // =====================================================
    // Inspector
    // =====================================================

    [Header("Repath Settings")]
    [Min(0.15f)]
    [SerializeField]
    private float repathInterval = 0.9f;

    [Min(0f)]
    [SerializeField]
    private float initialRequestSpread = 0.55f;

    [Header("Waypoint Settings")]
    [Min(0.02f)]
    [SerializeField]
    private float waypointReachDistance = 0.2f;

    [Header("Debug")]
    [SerializeField]
    private bool drawCurrentPath = true;

    // =====================================================
    // References
    // =====================================================

    private Seeker seeker;
    private EnemyMovement enemyMovement;

    private OnPathDelegate cachedPathCallback;

    // =====================================================
    // Runtime
    // =====================================================

    private readonly List<Vector3> waypoints =
        new List<Vector3>(32);

    private Path pendingPath;

    private int waypointIndex;
    private float nextRepathTime;

    private bool hasValidPath;

    public bool HasValidPath => hasValidPath;

    // =====================================================
    // Unity Lifecycle
    // =====================================================

    private void Awake()
    {
        CacheComponents();

        cachedPathCallback = OnPathCompleted;
    }

    private void OnEnable()
    {
        CacheComponents();

        ResetNavigation();
    }

    private void OnDisable()
    {
        ResetNavigation();
    }

    private void Update()
    {
        if (!CanRequestPath())
        {
            return;
        }

        if (Time.time < nextRepathTime)
        {
            return;
        }

        if (pendingPath != null)
        {
            return;
        }

        RequestPath();
    }

    // =====================================================
    // Request Conditions
    // =====================================================

    private bool CanRequestPath()
    {
        if (seeker == null ||
            enemyMovement == null)
        {
            return false;
        }

        if (enemyMovement.NavigationTarget == null)
        {
            return false;
        }

        if (enemyMovement.IsMovementLocked)
        {
            return false;
        }

        if (enemyMovement.IsKnockedBack)
        {
            return false;
        }

        if (Time.timeScale <= 0f)
        {
            return false;
        }

        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
        {
            return false;
        }

        if (AstarPath.active == null)
        {
            return false;
        }

        return true;
    }

    // =====================================================
    // Path Request
    // =====================================================

    private void RequestPath()
    {
        Vector2 startPosition =
            transform.position;

        Vector2 targetPosition =
            enemyMovement.NavigationTarget.position;

        float stoppingDistance =
            enemyMovement.StoppingDistance;

        if ((startPosition - targetPosition)
            .sqrMagnitude <=
            stoppingDistance * stoppingDistance)
        {
            nextRepathTime =
                Time.time + repathInterval;

            return;
        }

        nextRepathTime =
            Time.time + repathInterval;

        pendingPath = seeker.StartPath(
            startPosition,
            targetPosition,
            cachedPathCallback
        );
    }

    // =====================================================
    // Path Callback
    // =====================================================

    private void OnPathCompleted(Path completedPath)
    {
        // 忽略已取消或旧对象池生命周期的回调。
        if (!ReferenceEquals(
                completedPath,
                pendingPath))
        {
            return;
        }

        pendingPath = null;

        if (!isActiveAndEnabled ||
            completedPath.error ||
            completedPath.vectorPath == null ||
            completedPath.vectorPath.Count == 0)
        {
            ClearWaypoints();
            return;
        }

        // 拷贝路径，避免持有可能被插件回收的 Path 数据。
        waypoints.Clear();

        for (int i = 0;
             i < completedPath.vectorPath.Count;
             i++)
        {
            waypoints.Add(
                completedPath.vectorPath[i]
            );
        }

        waypointIndex = 0;
        hasValidPath = true;
    }

    // =====================================================
    // Waypoint Query
    // =====================================================

    /// <summary>
    /// 返回当前应追踪的下一个路径点。
    ///
    /// 没有有效路径时返回 false。
    /// EnemyMovement 不应在这种情况下
    /// 自动穿墙直追 Player。
    /// </summary>
    public bool TryGetNextWaypoint(
        Vector2 currentPosition,
        out Vector2 waypoint)
    {
        waypoint = currentPosition;

        if (!isActiveAndEnabled ||
            !hasValidPath ||
            waypoints.Count == 0)
        {
            return false;
        }

        float reachDistanceSqr =
            waypointReachDistance *
            waypointReachDistance;

        while (waypointIndex < waypoints.Count)
        {
            Vector2 candidate =
                waypoints[waypointIndex];

            float distanceSqr =
                (candidate - currentPosition)
                .sqrMagnitude;

            if (distanceSqr > reachDistanceSqr)
            {
                waypoint = candidate;
                return true;
            }

            waypointIndex++;
        }

        return false;
    }

    // =====================================================
    // Pool Reset
    // =====================================================

    /// <summary>
    /// 敌人从对象池生成、回收、
    /// 或进入需要重新寻路的状态时调用。
    /// </summary>
    public void ResetNavigation()
    {
        CacheComponents();

        // 必须先清空当前请求引用，
        // 后续取消回调才会被正确忽略。
        pendingPath = null;

        if (seeker != null)
        {
            seeker.CancelCurrentPathRequest();
        }

        ClearWaypoints();

        // 错开多个敌人首次寻路的时刻。
        nextRepathTime =
            Time.time +
            Random.Range(
                0f,
                Mathf.Max(
                    0f,
                    initialRequestSpread
                )
            );
    }

    private void ClearWaypoints()
    {
        waypoints.Clear();

        waypointIndex = 0;
        hasValidPath = false;
    }

    // =====================================================
    // References
    // =====================================================

    private void CacheComponents()
    {
        if (seeker == null)
        {
            seeker = GetComponent<Seeker>();
        }

        if (enemyMovement == null)
        {
            enemyMovement =
                GetComponent<EnemyMovement>();
        }
    }

    // =====================================================
    // Validation
    // =====================================================

    private void OnValidate()
    {
        repathInterval =
            Mathf.Max(0.15f, repathInterval);

        initialRequestSpread =
            Mathf.Max(0f, initialRequestSpread);

        waypointReachDistance =
            Mathf.Max(0.02f, waypointReachDistance);
    }

    // =====================================================
    // Gizmos
    // =====================================================

    private void OnDrawGizmosSelected()
    {
        if (!drawCurrentPath ||
            waypoints.Count < 2)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        for (int i = 0;
             i < waypoints.Count - 1;
             i++)
        {
            Gizmos.DrawLine(
                waypoints[i],
                waypoints[i + 1]
            );
        }
    }
}