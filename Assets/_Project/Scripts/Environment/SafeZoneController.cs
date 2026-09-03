using UnityEngine;

[DisallowMultipleComponent]
public class SafeZoneController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip("玩家逻辑根对象。Safe Zone 判断使用 Player Root，而不是 VisualRoot。")]
    [SerializeField]
    private Transform player;

    [Tooltip("提供正式地图世界范围。")]
    [SerializeField]
    private CameraFollow cameraFollow;


    // =========================================================
    // Safe Zone Settings
    // =========================================================

    [Header("Safe Zone Settings")]

    [Tooltip("安全区域半径。")]
    [Min(0.1f)]
    [SerializeField]
    private float safeZoneRadius = 8f;


    // =========================================================
    // Relocation Rules
    // =========================================================

    [Header("Relocation Rules")]

    [Tooltip("新安全区中心与玩家之间允许的最小距离。")]
    [Min(0f)]
    [SerializeField]
    private float minRelocationDistance = 10f;

    [Tooltip("新安全区中心与玩家之间允许的最大距离。")]
    [Min(0f)]
    [SerializeField]
    private float maxRelocationDistance = 20f;

    [Tooltip("安全区最外缘与地图边界之间额外保留的距离。")]
    [Min(0f)]
    [SerializeField]
    private float mapPadding = 1f;

    [Tooltip("随机寻找合法安全区位置时允许的最大尝试次数。")]
    [Min(1)]
    [SerializeField]
    private int maxGenerationAttempts = 24;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug")]

    [SerializeField]
    private Vector2 currentCenter;

    [SerializeField]
    private bool hasActiveZone;

    [SerializeField]
    private int lastGenerationAttemptCount;

    [SerializeField]
    private bool usedFallbackLastGeneration;

    [SerializeField]
    private float lastPlayerToZoneDistance;


    // =========================================================
    // Public Read Only State
    // =========================================================

    public Vector2 CurrentCenter =>
        currentCenter;

    public float Radius =>
        safeZoneRadius;

    public bool HasActiveZone =>
        hasActiveZone;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();
    }


    private void OnValidate()
    {
        safeZoneRadius =
            Mathf.Max(
                0.1f,
                safeZoneRadius
            );

        minRelocationDistance =
            Mathf.Max(
                0f,
                minRelocationDistance
            );

        maxRelocationDistance =
            Mathf.Max(
                minRelocationDistance,
                maxRelocationDistance
            );

        mapPadding =
            Mathf.Max(
                0f,
                mapPadding
            );

        maxGenerationAttempts =
            Mathf.Max(
                1,
                maxGenerationAttempts
            );
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );

            if (playerObject != null)
            {
                player =
                    playerObject.transform;
            }
        }

        if (cameraFollow == null)
        {
            cameraFollow =
                CameraFollow.Instance;
        }
    }


    // =========================================================
    // Safe Zone Generation
    // =========================================================

    public bool GenerateSafeZone()
    {
        ResolveReferences();

        if (player == null)
        {
            Debug.LogWarning(
                "SafeZoneController: "
                + "找不到 Player，"
                + "无法生成安全区域。",
                this
            );

            return false;
        }

        if (cameraFollow == null)
        {
            Debug.LogWarning(
                "SafeZoneController: "
                + "找不到 CameraFollow，"
                + "无法读取地图边界。",
                this
            );

            return false;
        }


        Vector2 mapMin =
            cameraFollow.MapMin;

        Vector2 mapMax =
            cameraFollow.MapMax;


        // -----------------------------------------------------
        // Safe Zone 的“中心”不能直接跑到 Map Bounds。
        //
        // 必须额外扣除：
        // Safe Zone Radius + Padding
        //
        // 这样才能保证整个圆都处于地图内部。
        // -----------------------------------------------------

        float requiredEdgeDistance =
            safeZoneRadius
            + mapPadding;


        Vector2 allowedCenterMin =
            mapMin
            + Vector2.one
            * requiredEdgeDistance;

        Vector2 allowedCenterMax =
            mapMax
            - Vector2.one
            * requiredEdgeDistance;


        if (allowedCenterMin.x
                > allowedCenterMax.x
            || allowedCenterMin.y
                > allowedCenterMax.y)
        {
            Debug.LogWarning(
                "SafeZoneController: "
                + "地图范围不足以容纳当前安全区半径。"
                + "\nMap Min: "
                + mapMin
                + "\nMap Max: "
                + mapMax
                + "\nSafe Zone Radius: "
                + safeZoneRadius
                + "\nPadding: "
                + mapPadding,
                this
            );

            return false;
        }


        Vector2 playerPosition =
            player.position;


        float safeMinDistance =
            Mathf.Max(
                0f,
                minRelocationDistance
            );

        float safeMaxDistance =
            Mathf.Max(
                safeMinDistance,
                maxRelocationDistance
            );


        lastGenerationAttemptCount = 0;
        usedFallbackLastGeneration = false;


        // -----------------------------------------------------
        // 有限随机尝试。
        //
        // 不允许 while(true)，
        // 防止特殊地图位置导致无限循环。
        // -----------------------------------------------------

        for (int i = 0;
             i < maxGenerationAttempts;
             i++)
        {
            lastGenerationAttemptCount =
                i + 1;


            float angle =
                Random.Range(
                    0f,
                    Mathf.PI * 2f
                );


            // 使用平方距离随机，
            // 让 10~20 范围内的二维面积分布更自然。
            float minDistanceSquared =
                safeMinDistance
                * safeMinDistance;

            float maxDistanceSquared =
                safeMaxDistance
                * safeMaxDistance;


            float distance =
                Mathf.Sqrt(
                    Random.Range(
                        minDistanceSquared,
                        maxDistanceSquared
                    )
                );


            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                );


            Vector2 candidateCenter =
                playerPosition
                + direction * distance;


            if (!IsCenterInsideAllowedBounds(
                    candidateCenter,
                    allowedCenterMin,
                    allowedCenterMax
                ))
            {
                continue;
            }


            ApplySafeZoneCenter(
                candidateCenter,
                playerPosition
            );


            Debug.Log(
                "===== Safe Zone Generated ====="
                + "\nCenter: "
                + currentCenter
                + "\nRadius: "
                + safeZoneRadius.ToString("F2")
                + "\nPlayer Distance: "
                + lastPlayerToZoneDistance.ToString("F2")
                + "\nAttempts: "
                + lastGenerationAttemptCount
                + "\nFallback: False",
                this
            );


            return true;
        }


        // -----------------------------------------------------
        // Fallback
        //
        // 如果随机 24 次仍然没有成功，
        // 朝地图中心方向寻找一个确定位置。
        //
        // Fallback 首先保证：
        // “安全区一定完整位于地图内部”。
        //
        // 极端边缘情况下，
        // 玩家距离规则允许退居第二优先级。
        // -----------------------------------------------------

        usedFallbackLastGeneration = true;


        Vector2 allowedMapCenter =
            (
                allowedCenterMin
                + allowedCenterMax
            ) * 0.5f;


        Vector2 fallbackDirection =
            allowedMapCenter
            - playerPosition;


        if (fallbackDirection.sqrMagnitude
            < 0.0001f)
        {
            fallbackDirection =
                Vector2.right;
        }
        else
        {
            fallbackDirection.Normalize();
        }


        float fallbackDistance =
            (
                safeMinDistance
                + safeMaxDistance
            ) * 0.5f;


        Vector2 fallbackCenter =
            playerPosition
            + fallbackDirection
            * fallbackDistance;


        fallbackCenter.x =
            Mathf.Clamp(
                fallbackCenter.x,
                allowedCenterMin.x,
                allowedCenterMax.x
            );

        fallbackCenter.y =
            Mathf.Clamp(
                fallbackCenter.y,
                allowedCenterMin.y,
                allowedCenterMax.y
            );


        ApplySafeZoneCenter(
            fallbackCenter,
            playerPosition
        );


        Debug.LogWarning(
            "===== Safe Zone Fallback Used ====="
            + "\nCenter: "
            + currentCenter
            + "\nRadius: "
            + safeZoneRadius.ToString("F2")
            + "\nPlayer Distance: "
            + lastPlayerToZoneDistance.ToString("F2")
            + "\nRandom Attempts: "
            + lastGenerationAttemptCount,
            this
        );


        return true;
    }


    private void ApplySafeZoneCenter(
        Vector2 center,
        Vector2 playerPosition
    )
    {
        currentCenter = center;

        hasActiveZone = true;

        lastPlayerToZoneDistance =
            Vector2.Distance(
                playerPosition,
                currentCenter
            );
    }


    private bool IsCenterInsideAllowedBounds(
        Vector2 center,
        Vector2 allowedMin,
        Vector2 allowedMax
    )
    {
        return center.x >= allowedMin.x
            && center.x <= allowedMax.x
            && center.y >= allowedMin.y
            && center.y <= allowedMax.y;
    }


    // =========================================================
    // Safe Check
    // =========================================================

    public bool IsPositionSafe(
        Vector2 worldPosition
    )
    {
        // 没有启用安全区域时，
        // 世界环境处于普通状态，因此视为安全。
        if (!hasActiveZone)
        {
            return true;
        }


        float distanceSquared =
            (
                worldPosition
                - currentCenter
            ).sqrMagnitude;


        float radiusSquared =
            safeZoneRadius
            * safeZoneRadius;


        return distanceSquared
            <= radiusSquared;
    }


    public void ClearSafeZone()
    {
        hasActiveZone = false;
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu("Test Generate Safe Zone")]
    private void TestGenerateSafeZone()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SafeZoneController: "
                + "请进入 Play Mode 后测试 Safe Zone。",
                this
            );

            return;
        }


        GenerateSafeZone();
    }


    [ContextMenu("Test Clear Safe Zone")]
    private void TestClearSafeZone()
    {
        ClearSafeZone();

        Debug.Log(
            "Safe Zone 已清除。",
            this
        );
    }


    private void OnDrawGizmosSelected()
    {
        if (!hasActiveZone)
        {
            return;
        }


        Gizmos.DrawWireSphere(
            new Vector3(
                currentCenter.x,
                currentCenter.y,
                0f
            ),
            safeZoneRadius
        );
    }
}