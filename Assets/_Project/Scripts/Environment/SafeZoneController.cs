using UnityEngine;

/// <summary>
/// AlienPurge - Safe Zone Controller (Phase40 Level01 opt-in).
/// Legacy random mode is preserved for older scenes.
/// Level01 mode uses designer-placed, reachable anchors and rejects
/// any complete Safe Zone circle intersecting the predicted fog ribbon.
/// </summary>
[DisallowMultipleComponent]
public partial class SafeZoneController : MonoBehaviour
{
    // =========================================================
    // Original References and Settings (serialized names preserved)
    // =========================================================

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Safe Zone Settings")]
    [Min(0.1f)]
    [SerializeField] private float safeZoneRadius = 8f;

    [Header("Relocation Rules - Legacy Mode")]
    [Min(0f)]
    [SerializeField] private float minRelocationDistance = 10f;
    [Min(0f)]
    [SerializeField] private float maxRelocationDistance = 20f;
    [Min(0f)]
    [SerializeField] private float mapPadding = 1f;
    [Min(1)]
    [SerializeField] private int maxGenerationAttempts = 24;

    // =========================================================
    // Level01 - NEW, OPT-IN ONLY
    // =========================================================

    [Header("Phase40 Level01 - Fog Aware Generation")]
    [Tooltip("OFF = original random + original fallback. " +
             "ON = only place Safe Zones at valid Level01 anchors.")]
    [SerializeField] private bool useLevel01FogAwareAnchors = false;

    [Tooltip("The existing ToxicFogAreaController on CargoYard_FogRoute.")]
    [SerializeField] private ToxicFogAreaController toxicFogArea;

    [Tooltip("The existing ToxicFogRouteController on CargoYard_FogRoute.")]
    [SerializeField] private ToxicFogRouteController toxicFogRoute;

    [Tooltip("Ordered Route_00 ... Route_05 ToxicFogCrossSection components.")]
    [SerializeField]
    private ToxicFogCrossSection[] fogSections =
        new ToxicFogCrossSection[0];

    [Tooltip("Designer-placed, accessible Safe Zone CENTER points. " +
             "At least 3 are recommended. NO random fallback in Level01 mode.")]
    [SerializeField]
    private Transform[] level01SafeZoneAnchors =
        new Transform[0];

    [Tooltip("How far the fog can travel during Warning + Active + margin, " +
             "in world units. At 0.35 units/s for 26s use at least 9.1; " +
             "11 is a reasonable starting value. Update if fog speed changes.")]
    [Min(0f)]
    [SerializeField] private float fogFutureTravelBuffer = 11f;

    [Tooltip("Extra separation between the entire Safe Zone circle and " +
             "the predicted Toxic Fog coverage, in world units.")]
    [Min(0f)]
    [SerializeField] private float fogSeparationPadding = 0.5f;

    [Tooltip("Avoid centers behind the Player relative to the route exit.")]
    [SerializeField] private bool requireForwardAnchor = true;

    [Tooltip("Allowed slight backtracking in the direction-to-exit test.")]
    [Min(0f)]
    [SerializeField] private float allowedBacktrackDistance = 1f;

    [Tooltip("Ignore Safe Zone anchors farther away than this from Player.")]
    [Min(1f)]
    [SerializeField] private float anchorMaxPlayerDistance = 28f;

    [Tooltip("Check the CENTER of each anchor is clear of Environment walls. " +
             "This is not a navigation path test; anchors must be placed on " +
             "confirmed reachable ground manually.")]
    [SerializeField] private bool checkAnchorCenterForWalls = true;

    [SerializeField] private LayerMask environmentBlockingLayers = 1 << 7;
    [Min(0.01f)]
    [SerializeField] private float anchorCenterClearanceRadius = 0.4f;

    // =========================================================
    // Runtime Debug - Original Fields Preserved
    // =========================================================

    [Header("Runtime Debug")]
    [SerializeField] private Vector2 currentCenter;
    [SerializeField] private bool hasActiveZone;
    [SerializeField] private int lastGenerationAttemptCount;
    [SerializeField] private bool usedFallbackLastGeneration;
    [SerializeField] private float lastPlayerToZoneDistance;

    [Header("Phase40 Runtime Debug")]
    [SerializeField] private int rejectedByFog;
    [SerializeField] private int rejectedByBounds;
    [SerializeField] private int rejectedByWalls;
    [SerializeField] private int rejectedByDistanceOrDirection;
    [SerializeField] private float lastPredictedFogDistance;
    [SerializeField] private string lastGenerationResult = "Not generated yet";

    // IMPORTANT: these public APIs are used by Storm Damage / HUD.
    public Vector2 CurrentCenter => currentCenter;
    public float Radius => safeZoneRadius;
    public bool HasActiveZone => hasActiveZone;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        safeZoneRadius = Mathf.Max(0.1f, safeZoneRadius);
        minRelocationDistance = Mathf.Max(0f, minRelocationDistance);
        maxRelocationDistance = Mathf.Max(
            minRelocationDistance, maxRelocationDistance);
        mapPadding = Mathf.Max(0f, mapPadding);
        maxGenerationAttempts = Mathf.Max(1, maxGenerationAttempts);
        fogFutureTravelBuffer = Mathf.Max(0f, fogFutureTravelBuffer);
        fogSeparationPadding = Mathf.Max(0f, fogSeparationPadding);
        allowedBacktrackDistance = Mathf.Max(0f, allowedBacktrackDistance);
        anchorMaxPlayerDistance = Mathf.Max(1f, anchorMaxPlayerDistance);
        anchorCenterClearanceRadius = Mathf.Max(0.01f,
            anchorCenterClearanceRadius);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) player = obj.transform;
        }

        if (cameraFollow == null) cameraFollow = CameraFollow.Instance;
    }

    // =========================================================
    // Public Entry Point - Preserved
    // =========================================================

    public bool GenerateSafeZone()
    {
        ResolveReferences();
        ResetGenerationDebug();

        if (player == null || cameraFollow == null)
        {
            lastGenerationResult = "Missing Player or CameraFollow";
            Debug.LogWarning("SafeZoneController: " + lastGenerationResult, this);
            return false;
        }

        Vector2 allowedMin;
        Vector2 allowedMax;
        if (!TryGetAllowedBounds(out allowedMin, out allowedMax))
        {
            lastGenerationResult = "Map too small for Safe Zone radius";
            Debug.LogWarning("SafeZoneController: " + lastGenerationResult, this);
            return false;
        }

        if (useLevel01FogAwareAnchors)
            return GenerateLevel01SafeZone(allowedMin, allowedMax);

        // IMPORTANT: Legacy mode follows the original random algorithm
        // and retains its map-center fallback for older Day01 scenes.
        return GenerateLegacySafeZone(allowedMin, allowedMax);
    }

    private bool TryGetAllowedBounds(out Vector2 allowedMin,
        out Vector2 allowedMax)
    {
        float edge = safeZoneRadius + mapPadding;
        allowedMin = cameraFollow.MapMin + Vector2.one * edge;
        allowedMax = cameraFollow.MapMax - Vector2.one * edge;
        return allowedMin.x <= allowedMax.x &&
               allowedMin.y <= allowedMax.y;
    }

    // =========================================================
    // Original Day01 Behavior (opt-in OFF)
    // =========================================================

    private bool GenerateLegacySafeZone(Vector2 allowedMin,
        Vector2 allowedMax)
    {
        Vector2 playerPosition = player.position;
        float minDist = Mathf.Max(0f, minRelocationDistance);
        float maxDist = Mathf.Max(minDist, maxRelocationDistance);

        for (int i = 0; i < maxGenerationAttempts; i++)
        {
            lastGenerationAttemptCount = i + 1;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float minSq = minDist * minDist;
            float maxSq = maxDist * maxDist;
            float distance = Mathf.Sqrt(Random.Range(minSq, maxSq));
            Vector2 direction = new Vector2(
                Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 center = playerPosition + direction * distance;
            if (!IsCenterInsideAllowedBounds(center, allowedMin, allowedMax))
                continue;

            ApplySafeZoneCenter(center, playerPosition);
            lastGenerationResult = "Legacy random";
            Debug.Log("===== Safe Zone Generated =====\nCenter: " +
                currentCenter + "\nRadius: " + safeZoneRadius.ToString("F2") +
                "\nAttempts: " + lastGenerationAttemptCount +
                "\nFallback: False", this);
            return true;
        }

        usedFallbackLastGeneration = true;
        Vector2 mapCenter = (allowedMin + allowedMax) * 0.5f;
        Vector2 directionFallback = mapCenter - playerPosition;
        if (directionFallback.sqrMagnitude < 0.0001f)
            directionFallback = Vector2.right;
        else
            directionFallback.Normalize();

        float fallbackDistance = (minDist + maxDist) * 0.5f;
        Vector2 fallback = playerPosition +
                           directionFallback * fallbackDistance;
        fallback.x = Mathf.Clamp(fallback.x, allowedMin.x, allowedMax.x);
        fallback.y = Mathf.Clamp(fallback.y, allowedMin.y, allowedMax.y);

        ApplySafeZoneCenter(fallback, playerPosition);
        lastGenerationResult = "Legacy map-center fallback";
        Debug.LogWarning("===== Safe Zone Fallback Used =====\nCenter: " +
            currentCenter + "\nRadius: " + safeZoneRadius.ToString("F2") +
            "\nRandom Attempts: " + lastGenerationAttemptCount,
            this);
        return true;
    }

    // =========================================================
    // Level01 Generation - NO HAZARDOUS FALLBACK
    // =========================================================

    private bool GenerateLevel01SafeZone(Vector2 allowedMin,
        Vector2 allowedMax)
    {
        // Misconfigured dependencies must NOT silently revert to unsafe random.
        if (toxicFogArea == null || !toxicFogArea.GeometryIsValid ||
            toxicFogRoute == null || !ValidateFogSections() ||
            level01SafeZoneAnchors == null ||
            level01SafeZoneAnchors.Length == 0)
        {
            ClearSafeZone();
            lastGenerationResult = "Level01 dependencies/anchors missing";
            Debug.LogWarning("[SafeZone Level01] " + lastGenerationResult +
                ". Storm will skip this wave instead of spawning " +
                "an unsafe Safe Zone.", this);
            return false;
        }

        float predicted = Mathf.Clamp(
            toxicFogRoute.DistanceTravelled + fogFutureTravelBuffer,
            0f, toxicFogRoute.TotalRouteDistance);
        lastPredictedFogDistance = predicted;

        // Rotate the search order for variety, but use ONLY designer anchors.
        int count = level01SafeZoneAnchors.Length;
        int startIndex = Random.Range(0, count);
        for (int attempt = 0; attempt < count; attempt++)
        {
            lastGenerationAttemptCount = attempt + 1;
            Transform anchor = level01SafeZoneAnchors[
                (startIndex + attempt) % count];
            if (anchor == null) continue;
            Vector2 candidate = anchor.position;
            if (!IsCenterInsideAllowedBounds(candidate, allowedMin, allowedMax))
            {
                rejectedByBounds++;
                continue;
            }
            if (!IsWithinForwardReach(candidate))
            {
                rejectedByDistanceOrDirection++;
                continue;
            }
            if (checkAnchorCenterForWalls &&
                Physics2D.OverlapCircle(candidate,
                    anchorCenterClearanceRadius,
                    environmentBlockingLayers) != null)
            {
                rejectedByWalls++;
                continue;
            }

            // Circle-vs-polygon intersection: reject even PARTIAL overlap.
            if (CircleIntersectsPredictedFog(candidate,
                    safeZoneRadius + fogSeparationPadding, predicted))
            {
                rejectedByFog++;
                continue;
            }

            ApplySafeZoneCenter(candidate, player.position);
            lastGenerationResult = "Level01 valid anchor: " + anchor.name;
            Debug.Log("===== Level01 Safe Zone Generated =====" +
                "\nAnchor: " + anchor.name +
                "\nCenter: " + currentCenter +
                "\nRadius: " + safeZoneRadius.ToString("F2") +
                "\nPredicted Fog Distance: " +
                predicted.ToString("F2") +
                "\nSafe Margin: " + fogSeparationPadding.ToString("F2"),
                this);
            return true;
        }

        // Critical: NEVER use the old fallback when the fog-aware mode is ON.
        // PlanetStormController already handles GenerateSafeZone()==false.
        ClearSafeZone();
        lastGenerationResult = "No valid Level01 Safe Zone anchor";
        Debug.LogWarning("[SafeZone Level01] No safe anchor. " +
            "Storm will skip this wave. " +
            "Rejected Fog=" + rejectedByFog +
            ", Bounds=" + rejectedByBounds +
            ", Walls=" + rejectedByWalls +
            ", Direction/Distance=" + rejectedByDistanceOrDirection +
            ". Consider smaller radius, more forward anchors, or " +
            "adjusting Storm/Fog timings.", this);
        return false;
    }

    private bool IsWithinForwardReach(Vector2 candidate)
    {
        Vector2 fromPlayer = candidate - (Vector2)player.position;
        if (fromPlayer.magnitude > anchorMaxPlayerDistance)
            return false;
        if (!requireForwardAnchor) return true;

        Vector2 goal = fogSections[fogSections.Length - 1].Center;
        Vector2 towardExit = goal - (Vector2)player.position;
        if (towardExit.sqrMagnitude < 0.0001f) return true;
        return Vector2.Dot(fromPlayer, towardExit.normalized) >=
               -allowedBacktrackDistance;
    }

    private bool ValidateFogSections()
    {
        if (fogSections == null || fogSections.Length < 2) return false;
        for (int i = 0; i < fogSections.Length; i++)
            if (fogSections[i] == null || !fogSections[i].IsValid)
                return false;

        float measured = 0f;
        for (int i = 0; i < fogSections.Length - 1; i++)
            measured += Vector2.Distance(fogSections[i].Center,
                                         fogSections[i + 1].Center);
        return Mathf.Abs(measured - toxicFogRoute.TotalRouteDistance) < 0.1f;
    }

    [ContextMenu("Debug/Assign Fog Sections From Route Children")]
    private void AssignFogSectionsFromRouteChildren()
    {
        if (toxicFogRoute == null)
        {
            Debug.LogWarning("Assign Toxic Fog Route first.", this);
            return;
        }
        Transform root = toxicFogRoute.transform;
        fogSections = new ToxicFogCrossSection[root.childCount];
        for (int i = 0; i < root.childCount; i++)
            fogSections[i] = root.GetChild(i)
                .GetComponent<ToxicFogCrossSection>();
    }

    // =========================================================
    // Exact circle-vs-covered-quadrilateral geometry
    // =========================================================

    private bool CircleIntersectsPredictedFog(Vector2 circleCenter,
        float radius, float distance)
    {
        float remaining = distance;
        for (int i = 0; i < fogSections.Length - 1; i++)
        {
            if (remaining <= 0.00001f) return false;

            ToxicFogCrossSection a = fogSections[i];
            ToxicFogCrossSection b = fogSections[i + 1];
            float length = Vector2.Distance(a.Center, b.Center);
            if (length <= 0.00001f) continue;
            float t = Mathf.Clamp01(remaining / length);

            // Same four corners as ToxicFogAreaController's ribbon.
            Vector2 p0 = a.LeftPosition;
            Vector2 p1 = a.RightPosition;
            Vector2 p2 = Vector2.Lerp(a.RightPosition, b.RightPosition, t);
            Vector2 p3 = Vector2.Lerp(a.LeftPosition, b.LeftPosition, t);

            if (CircleIntersectsQuad(circleCenter, radius,
                                     p0, p1, p2, p3))
                return true;
            remaining -= length;
        }
        return false;
    }

    private static bool CircleIntersectsQuad(Vector2 center, float radius,
        Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (PointInQuad(center, a, b, c, d)) return true;
        float rSq = radius * radius;
        if ((center - a).sqrMagnitude <= rSq ||
            (center - b).sqrMagnitude <= rSq ||
            (center - c).sqrMagnitude <= rSq ||
            (center - d).sqrMagnitude <= rSq)
            return true;

        return DistanceSquaredToSegment(center, a, b) <= rSq ||
               DistanceSquaredToSegment(center, b, c) <= rSq ||
               DistanceSquaredToSegment(center, c, d) <= rSq ||
               DistanceSquaredToSegment(center, d, a) <= rSq;
    }

    private static float DistanceSquaredToSegment(Vector2 p,
        Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 0.0000001f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
        return (p - (a + t * ab)).sqrMagnitude;
    }

    private static bool PointInQuad(Vector2 p,
        Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        bool inside = false;
        Vector2 previous = d;
        for (int i = 0; i < 4; i++)
        {
            Vector2 current = i == 0 ? a : i == 1 ? b : i == 2 ? c : d;
            if ((current.y > p.y) != (previous.y > p.y))
            {
                float x = current.x + (p.y - current.y) *
                          (previous.x - current.x) /
                          (previous.y - current.y);
                if (p.x < x) inside = !inside;
            }
            previous = current;
        }
        return inside;
    }

    // =========================================================
    // Original Public API / Helpers
    // =========================================================

    private void ApplySafeZoneCenter(Vector2 center, Vector2 playerPosition)
    {
        currentCenter = center;
        hasActiveZone = true;
        lastPlayerToZoneDistance = Vector2.Distance(playerPosition, center);
    }

    private static bool IsCenterInsideAllowedBounds(Vector2 center,
        Vector2 allowedMin, Vector2 allowedMax)
    {
        return center.x >= allowedMin.x && center.x <= allowedMax.x &&
               center.y >= allowedMin.y && center.y <= allowedMax.y;
    }

    public bool IsPositionSafe(Vector2 worldPosition)
    {
        if (!hasActiveZone) return true;
        return ((Vector2)worldPosition - currentCenter).sqrMagnitude <=
               safeZoneRadius * safeZoneRadius;
    }

    public void ClearSafeZone()
    {
        hasActiveZone = false;
    }

    private void ResetGenerationDebug()
    {
        lastGenerationAttemptCount = 0;
        usedFallbackLastGeneration = false;
        rejectedByFog = 0;
        rejectedByBounds = 0;
        rejectedByWalls = 0;
        rejectedByDistanceOrDirection = 0;
        lastPredictedFogDistance = 0f;
    }

    // =========================================================
    // Original Context Menus - Preserved
    // =========================================================

    [ContextMenu("Test Generate Safe Zone")]
    private void TestGenerateSafeZone()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("SafeZoneController: 请进入 Play Mode 后测试 Safe Zone。", this);
            return;
        }
        GenerateSafeZone();
    }

    [ContextMenu("Test Clear Safe Zone")]
    private void TestClearSafeZone()
    {
        ClearSafeZone();
        Debug.Log("Safe Zone 已清除。", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (hasActiveZone)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(new Vector3(currentCenter.x,
                currentCenter.y, 0f), safeZoneRadius);
        }

        if (!useLevel01FogAwareAnchors ||
            level01SafeZoneAnchors == null) return;

        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.35f);
        for (int i = 0; i < level01SafeZoneAnchors.Length; i++)
        {
            Transform anchor = level01SafeZoneAnchors[i];
            if (anchor != null)
                Gizmos.DrawWireSphere(anchor.position, safeZoneRadius);
        }
    }
}
