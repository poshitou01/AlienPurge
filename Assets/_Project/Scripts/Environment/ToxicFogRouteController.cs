
using UnityEngine;

/// <summary>
/// Phase40 - Toxic Fog Stage 1.
///
/// 沿路径匀速推进的毒雾前线控制器。
///
/// 当前版本只负责：
/// 1. 路线读取
/// 2. 固定速度推进
/// 3. 等待启动
/// 4. Scene Gizmos 调试
///
/// 暂时不包含毒雾伤害、区域覆盖和视觉特效。
/// </summary>
[DisallowMultipleComponent]
public class ToxicFogRouteController : MonoBehaviour
{
    [Tooltip("Scene-owned region scheduling; default false preserves legacy scenes.")] public bool regionDriven;

    public enum FogRouteState
    {
        Waiting,
        Advancing,
        ReachedRouteEnd
    }

    // =========================================================
    // Route
    // =========================================================

    [Header("Route - In Travel Order")]

    [Tooltip(
        "按照 Route_00 到 Route_05 的顺序设置。"
    )]
    [SerializeField]
    private Transform[] routePoints =
        new Transform[0];


    // =========================================================
    // Timing
    // =========================================================

    [Header("Fixed-Speed Progression")]

    [Tooltip(
        "游戏正式生存时间达到多少秒后，" +
        "毒雾开始推进。"
    )]
    [Min(0f)]
    [SerializeField]
    private float startDelay = 45f;

    [Tooltip(
        "毒雾沿路线移动的固定速度，" +
        "单位：世界单位/游戏秒。"
    )]
    [Min(0.01f)]
    [SerializeField]
    private float speedWorldUnitsPerSecond = 0.35f;


    // =========================================================
    // Scene Gizmos
    // =========================================================

    [Header("Scene Gizmos")]

    [SerializeField]
    private bool drawRouteGizmos = true;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug - Read Only")]

    [SerializeField]
    private FogRouteState currentState =
        FogRouteState.Waiting;

    [SerializeField]
    private float totalRouteDistance;

    [SerializeField]
    private float distanceTravelled;

    [SerializeField]
    private Vector2 frontWorldPosition;

    private bool routeIsValid;


    public FogRouteState CurrentState =>
        currentState;

    public bool HasStarted =>
        currentState != FogRouteState.Waiting;

    public bool ReachedEnd =>
        currentState == FogRouteState.ReachedRouteEnd;

    public float TotalRouteDistance =>
        totalRouteDistance;

    public float DistanceTravelled =>
        distanceTravelled;

    public Vector2 FrontWorldPosition =>
        frontWorldPosition;

    public float Progress01 =>
        totalRouteDistance > 0f
            ? Mathf.Clamp01(
                distanceTravelled /
                totalRouteDistance
            )
            : 0f;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Reset()
    {
        AutoAssignDirectChildren();
    }


    private void Awake()
    {
        RebuildRoute();

        currentState =
            FogRouteState.Waiting;

        distanceTravelled = 0f;

        if (routePoints != null &&
            routePoints.Length > 0 &&
            routePoints[0] != null)
        {
            frontWorldPosition =
                routePoints[0].position;
        }
    }


    private void Update()
    {
        if (regionDriven) return;

        if (!routeIsValid)
        {
            return;
        }

        GameManager manager =
            GameManager.Instance;

        if (manager == null ||
            !manager.IsPlaying)
        {
            return;
        }

        // 使用现有 GameManager 的生存时间。
        //
        // 暂停、升级选卡、模块选择期间，
        // 原有 SurvivalTime 不会推进。
        //
        // 因此毒雾也不会在这些状态下偷跑。
        float survivalTime =
            manager.SurvivalTime;

        if (survivalTime < startDelay)
        {
            return;
        }


        // =====================================================
        // Start
        // =====================================================

        if (currentState ==
            FogRouteState.Waiting)
        {
            currentState =
                FogRouteState.Advancing;

            Debug.Log(
                "[ToxicFog] Front started. " +
                "Fixed speed: " +
                speedWorldUnitsPerSecond.ToString("F2") +
                " world units/s.",
                this
            );
        }


        // =====================================================
        // Fixed Speed
        // =====================================================
        //
        // 使用“沿路线累计世界距离”，
        // 而不是按 Waypoint 数量插值。
        //
        // 所以无论某一段路径长还是短，
        // 前线的实际移动速度都保持一致。
        // =====================================================

        float targetDistance =
            Mathf.Clamp(
                (survivalTime - startDelay) *
                speedWorldUnitsPerSecond,
                0f,
                totalRouteDistance
            );

        // 在 Play Mode 临时改变速度时，
        // 不让前线向后倒退。
        distanceTravelled =
            Mathf.Max(
                distanceTravelled,
                targetDistance
            );

        frontWorldPosition =
            EvaluateRoute(
                distanceTravelled
            );


        // =====================================================
        // Route End
        // =====================================================

        if (distanceTravelled >=
                totalRouteDistance &&
            currentState !=
                FogRouteState.ReachedRouteEnd)
        {
            currentState =
                FogRouteState.ReachedRouteEnd;

            Debug.Log(
                "[ToxicFog] Reached Cargo Yard " +
                "route end. " +
                "Next region is not connected yet.",
                this
            );
        }
    }


    private void OnValidate()
    {
        startDelay =
            Mathf.Max(0f, startDelay);

        speedWorldUnitsPerSecond =
            Mathf.Max(
                0.01f,
                speedWorldUnitsPerSecond
            );
    }


    // =========================================================
    // Route Setup
    // =========================================================

    [ContextMenu(
        "Debug/Assign Route From Direct Children"
    )]
    private void AutoAssignDirectChildren()
    {
        int count =
            transform.childCount;

        routePoints =
            new Transform[count];

        for (int i = 0; i < count; i++)
        {
            routePoints[i] =
                transform.GetChild(i);
        }
    }


    [ContextMenu("Debug/Rebuild Route")]
    private void RebuildRoute()
    {
        routeIsValid = false;

        totalRouteDistance = 0f;

        if (routePoints == null ||
            routePoints.Length < 2)
        {
            Debug.LogWarning(
                "[ToxicFog] Route requires " +
                "at least two assigned points.",
                this
            );

            return;
        }

        for (int i = 0;
             i < routePoints.Length;
             i++)
        {
            if (routePoints[i] == null)
            {
                Debug.LogWarning(
                    "[ToxicFog] Route point " +
                    i +
                    " is missing.",
                    this
                );

                return;
            }
        }

        totalRouteDistance =
            CalculateRouteLength();

        if (totalRouteDistance <= 0.001f)
        {
            Debug.LogWarning(
                "[ToxicFog] Route length is zero. " +
                "Check point positions.",
                this
            );

            return;
        }

        routeIsValid = true;
    }


    private float CalculateRouteLength()
    {
        if (routePoints == null ||
            routePoints.Length < 2)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 0;
             i < routePoints.Length - 1;
             i++)
        {
            if (routePoints[i] == null ||
                routePoints[i + 1] == null)
            {
                continue;
            }

            length +=
                Vector2.Distance(
                    routePoints[i].position,
                    routePoints[i + 1].position
                );
        }

        return length;
    }


    // =========================================================
    // Route Position Evaluation
    // =========================================================

    private Vector2 EvaluateRoute(
        float distance)
    {
        if (routePoints == null ||
            routePoints.Length == 0 ||
            routePoints[0] == null)
        {
            return transform.position;
        }

        float remaining =
            Mathf.Max(
                0f,
                distance
            );

        for (int i = 0;
             i < routePoints.Length - 1;
             i++)
        {
            Vector2 a =
                routePoints[i].position;

            Vector2 b =
                routePoints[i + 1].position;

            float length =
                Vector2.Distance(a, b);

            if (length <= 0.0001f)
            {
                continue;
            }

            if (remaining <= length)
            {
                return Vector2.Lerp(
                    a,
                    b,
                    remaining / length
                );
            }

            remaining -= length;
        }

        return routePoints[
            routePoints.Length - 1
        ].position;
    }


    private bool AllRoutePointsAssigned()
    {
        if (routePoints == null ||
            routePoints.Length < 2)
        {
            return false;
        }

        for (int i = 0;
             i < routePoints.Length;
             i++)
        {
            if (routePoints[i] == null)
            {
                return false;
            }
        }

        return true;
    }


    // =========================================================
    // Scene Gizmos
    // =========================================================
    //
    // 注意：
    // 这里显示的是毒雾推进路径，
    // 并不是毒雾实际覆盖的伤害范围。
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!drawRouteGizmos ||
            routePoints == null ||
            routePoints.Length == 0)
        {
            return;
        }

        // 青色：路线与锚点
        Gizmos.color =
            new Color(
                0.25f,
                0.78f,
                0.95f,
                0.95f
            );

        for (int i = 0;
             i < routePoints.Length;
             i++)
        {
            if (routePoints[i] == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(
                routePoints[i].position,
                0.25f
            );

            if (i + 1 < routePoints.Length &&
                routePoints[i + 1] != null)
            {
                Gizmos.DrawLine(
                    routePoints[i].position,
                    routePoints[i + 1].position
                );
            }
        }

        float debugLength =
            Application.isPlaying &&
            routeIsValid
                ? totalRouteDistance
                : CalculateRouteLength();

        if (debugLength <= 0f ||
            !AllRoutePointsAssigned())
        {
            return;
        }

        Vector2 front =
            Application.isPlaying &&
            routeIsValid
                ? frontWorldPosition
                : EvaluateRoute(0f);

        // 绿色：当前毒雾前线位置
        Gizmos.color =
            new Color(
                0.7f,
                1f,
                0.3f,
                1f
            );

        Gizmos.DrawSphere(
            new Vector3(
                front.x,
                front.y,
                0f
            ),
            0.40f
        );
    }
}
