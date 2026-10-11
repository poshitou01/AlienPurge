
using UnityEngine;

/// <summary>
/// Phase40 - Toxic Fog Area (geometry test only).
/// Converts ordered cross-sections into a growing hazard region.
/// No damage, colliders, particles or Storm changes at this stage.
/// </summary>
[DisallowMultipleComponent]
public class ToxicFogAreaController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ToxicFogRouteController routeController;

    [Tooltip("Ordered Route_00, Route_01, ... Route_05.")]
    [SerializeField]
    private ToxicFogCrossSection[] crossSections =
        new ToxicFogCrossSection[0];

    [Tooltip("Optional Player root, for observing the region test.")]
    [SerializeField] private Transform debugPlayer;

    [Header("Scene Gizmos")]
    [SerializeField] private bool drawAreaGizmos = true;
    [SerializeField] private bool logPlayerFogTransitions = true;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool geometryIsValid;
    [SerializeField] private float totalAreaRouteLength;
    [SerializeField] private float coveredDistance;
    [SerializeField] private Vector2 currentFrontLeft;
    [SerializeField] private Vector2 currentFrontRight;
    [SerializeField] private bool debugPlayerInsideFog;

    private float[] segmentLengths = new float[0];
    private bool hasObservedPlayer;

    public bool GeometryIsValid => geometryIsValid;
    public float CoveredDistance => GetLiveCoveredDistance();
    public bool DebugPlayerInsideFog => debugPlayerInsideFog;

    private void Reset()
    {
        routeController = GetComponent<ToxicFogRouteController>();
        AssignSectionsFromDirectChildren();
    }

    private void Awake()
    {
        RebuildAreaGeometry();
    }

    private void Start()
    {
        // The route controller's Awake has finished by Start.
        if (geometryIsValid && routeController != null &&
            routeController.TotalRouteDistance > 0f &&
            Mathf.Abs(totalAreaRouteLength -
                      routeController.TotalRouteDistance) > 0.05f)
        {
            Debug.LogWarning(
                "[ToxicFogArea] Section route length differs from the " +
                "route controller. Check that both arrays have the same order.",
                this);
        }
    }

    private void Update()
    {
        coveredDistance = GetLiveCoveredDistance();
        UpdateFrontBoundary(coveredDistance);

        if (debugPlayer == null || !geometryIsValid)
        {
            debugPlayerInsideFog = false;
            hasObservedPlayer = false;
            return;
        }

        bool inside = IsPositionInsideFog(debugPlayer.position);
        if (logPlayerFogTransitions && hasObservedPlayer &&
            inside != debugPlayerInsideFog)
        {
            Debug.Log(inside
                ? "[ToxicFogArea] Player ENTERED the covered area."
                : "[ToxicFogArea] Player LEFT the covered area.", this);
        }

        debugPlayerInsideFog = inside;
        hasObservedPlayer = true;
    }

    [ContextMenu("Debug/Assign Sections From Direct Children")]
    private void AssignSectionsFromDirectChildren()
    {
        crossSections = new ToxicFogCrossSection[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            crossSections[i] = transform.GetChild(i)
                .GetComponent<ToxicFogCrossSection>();
        }
    }

    [ContextMenu("Debug/Rebuild Area Geometry")]
    private void RebuildAreaGeometry()
    {
        geometryIsValid = false;
        totalAreaRouteLength = 0f;
        segmentLengths = new float[0];

        if (routeController == null)
            routeController = GetComponent<ToxicFogRouteController>();

        if (routeController == null || crossSections == null ||
            crossSections.Length < 2)
        {
            Debug.LogWarning("[ToxicFogArea] Assign route and at least " +
                "two ordered cross-sections.", this);
            return;
        }

        for (int i = 0; i < crossSections.Length; i++)
        {
            if (crossSections[i] == null || !crossSections[i].IsValid)
            {
                Debug.LogWarning("[ToxicFogArea] Section or its " +
                    "FogLeft/FogRight is missing at index " + i, this);
                return;
            }
        }

        segmentLengths = new float[crossSections.Length - 1];
        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = Vector2.Distance(
                crossSections[i].Center, crossSections[i + 1].Center);
            if (length <= 0.0001f)
            {
                Debug.LogWarning("[ToxicFogArea] Repeated section centers " +
                    "at segment " + i, this);
                return;
            }
            segmentLengths[i] = length;
            totalAreaRouteLength += length;
        }

        geometryIsValid = true;
        coveredDistance = 0f;
        currentFrontLeft = crossSections[0].LeftPosition;
        currentFrontRight = crossSections[0].RightPosition;
    }

    // Public query for a later damage controller. Does not apply damage.
    public bool IsPositionInsideFog(Vector2 worldPosition)
    {
        if (!geometryIsValid) return false;
        float remaining = GetLiveCoveredDistance();
        if (remaining <= 0.0001f) return false;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = segmentLengths[i];
            float t = Mathf.Clamp01(remaining / length);
            ToxicFogCrossSection a = crossSections[i];
            ToxicFogCrossSection b = crossSections[i + 1];
            Vector2 left = Vector2.Lerp(a.LeftPosition, b.LeftPosition, t);
            Vector2 right = Vector2.Lerp(a.RightPosition, b.RightPosition, t);

            if (IsPointInQuad(worldPosition, a.LeftPosition,
                a.RightPosition, right, left))
                return true;

            if (remaining <= length) break;
            remaining -= length;
        }
        return false;
    }

    private float GetLiveCoveredDistance()
    {
        if (!geometryIsValid || routeController == null ||
            !routeController.HasStarted) return 0f;
        return Mathf.Clamp(routeController.DistanceTravelled,
            0f, totalAreaRouteLength);
    }

    // Odd-even polygon test, also valid for non-convex quadrilaterals.
    private static bool IsPointInQuad(Vector2 p,
        Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        bool inside = false;
        Vector2 previous = d;
        for (int i = 0; i < 4; i++)
        {
            Vector2 current = i == 0 ? a : i == 1 ? b : i == 2 ? c : d;
            if (IsPointOnSegment(p, previous, current)) return true;

            if ((current.y > p.y) != (previous.y > p.y))
            {
                float x = current.x + (p.y - current.y) *
                    (previous.x - current.x) / (previous.y - current.y);
                if (p.x < x) inside = !inside;
            }
            previous = current;
        }
        return inside;
    }

    private static bool IsPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq <= 0.0000001f)
            return (p - a).sqrMagnitude <= 0.000001f;
        float t = Vector2.Dot(p - a, ab) / lenSq;
        if (t < 0f || t > 1f) return false;
        return (p - (a + t * ab)).sqrMagnitude <= 0.000001f;
    }

    private void UpdateFrontBoundary(float distance)
    {
        if (!geometryIsValid) return;
        float remaining = distance;
        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = segmentLengths[i];
            float t = Mathf.Clamp01(remaining / length);
            currentFrontLeft = Vector2.Lerp(crossSections[i].LeftPosition,
                crossSections[i + 1].LeftPosition, t);
            currentFrontRight = Vector2.Lerp(crossSections[i].RightPosition,
                crossSections[i + 1].RightPosition, t);
            if (remaining <= length) return;
            remaining -= length;
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawAreaGizmos || crossSections == null ||
            crossSections.Length < 2) return;

        // Muted cyan: the entire ribbon outline.
        Gizmos.color = new Color(0.2f, 0.7f, 0.8f, 0.5f);
        for (int i = 0; i < crossSections.Length - 1; i++)
        {
            var a = crossSections[i];
            var b = crossSections[i + 1];
            if (a == null || b == null || !a.IsValid || !b.IsValid)
                continue;
            Gizmos.DrawLine(a.LeftPosition, b.LeftPosition);
            Gizmos.DrawLine(a.RightPosition, b.RightPosition);
        }

        if (!Application.isPlaying || !geometryIsValid ||
            routeController == null || !routeController.HasStarted) return;

        float remaining = GetLiveCoveredDistance();
        for (int i = 0; i < segmentLengths.Length &&
             remaining > 0.0001f; i++)
        {
            var a = crossSections[i];
            var b = crossSections[i + 1];
            float length = segmentLengths[i];
            float t = Mathf.Clamp01(remaining / length);
            Vector2 left = Vector2.Lerp(a.LeftPosition, b.LeftPosition, t);
            Vector2 right = Vector2.Lerp(a.RightPosition, b.RightPosition, t);

            // Green: boundaries already covered by fog.
            Gizmos.color = new Color(0.5f, 1f, 0.25f, 1f);
            Gizmos.DrawLine(a.LeftPosition, a.RightPosition);
            Gizmos.DrawLine(a.LeftPosition, left);
            Gizmos.DrawLine(a.RightPosition, right);

            // Yellow: the current advancing front only.
            if (remaining <= length)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(left, right);
                Gizmos.DrawWireSphere(left, 0.22f);
                Gizmos.DrawWireSphere(right, 0.22f);
            }
            remaining -= length;
        }
    }
}
