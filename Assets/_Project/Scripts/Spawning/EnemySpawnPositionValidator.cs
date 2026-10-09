using UnityEngine;
using Pathfinding;

/// <summary>
/// Phase40: validates that enemies spawn in the intended Cargo Yard
/// play area, on walkable A* nodes, away from solid obstacles.
/// This component never spawns enemies by itself.
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawnPositionValidator : MonoBehaviour
{
    [Header("Playable Spawn Area")]
    [Tooltip("A trigger PolygonCollider2D outlining the INSIDE of the Cargo Yard walls. Do not put it on the Environment layer.")]
    [SerializeField]
    private PolygonCollider2D spawnPlayableArea;

    [Header("Environment Collision")]
    [Tooltip("Physical layers for buildings, shipping containers and solid walls. Select Environment.")]
    [SerializeField]
    private LayerMask environmentBlockingLayers = 1 << 7;

    [Tooltip("Free circular space required for every enemy. 0.6 covers the current tested enemy sizes; increase if your prefab colliders become larger.")]
    [Min(0.01f)]
    [SerializeField]
    private float spawnClearanceRadius = 0.6f;

    [Header("A* Graph Validation")]
    [Tooltip("Candidate must be very close to its nearest real grid node. With Node Size 0.4, 0.35 is a reasonable test value.")]
    [Min(0.01f)]
    [SerializeField]
    private float maxDistanceToNode = 0.35f;

    [Tooltip("Player must be close enough to the scanned navigation grid.")]
    [Min(0.01f)]
    [SerializeField]
    private float maxPlayerNodeDistance = 1.2f;

    [Header("Debug")]
    [SerializeField]
    private bool logRejectedSpawnAttempts = false;

    [SerializeField]
    private bool logFailedSpawnSearches = true;

    [SerializeField]
    private int lastAttemptCount;

    [SerializeField]
    private int lastRejectedOutsideArea;

    [SerializeField]
    private int lastRejectedByObstacle;

    [SerializeField]
    private int lastRejectedByGraph;

    [SerializeField]
    private int lastRejectedByBounds;

    [SerializeField]
    private Vector3 lastValidSpawnPosition;

    private float nextFailureLogTime;

    // Reuse one array to avoid allocating new arrays each spawn.
    private readonly Collider2D[] obstacleResults =
        new Collider2D[16];

    /// <summary>
    /// True only if the candidate lies in the playable polygon,
    /// fits without hitting Environment colliders, and shares the
    /// player's connected A* GridGraph area.
    /// </summary>
    public bool TryFindValidSpawnPosition(
        Transform player,
        float minSpawnDistance,
        float maxSpawnDistance,
        bool limitToMapBounds,
        Vector2 mapMin,
        Vector2 mapMax,
        float boundsPadding,
        int maxAttempts,
        out Vector3 validPosition)
    {
        validPosition = Vector3.zero;
        ResetDebugCounters();

        if (player == null)
        {
            ReportFailure("Player is missing.");
            return false;
        }

        if (spawnPlayableArea == null
            || !spawnPlayableArea.enabled
            || !spawnPlayableArea.gameObject.activeInHierarchy
            || !spawnPlayableArea.isTrigger)
        {
            ReportFailure(
                "SpawnPlayableArea must be an enabled trigger PolygonCollider2D."
            );
            return false;
        }

        if (environmentBlockingLayers.value == 0)
        {
            ReportFailure("Environment Blocking Layers is empty.");
            return false;
        }

        if (AstarPath.active == null
            || AstarPath.active.data == null
            || AstarPath.active.data.gridGraph == null
            || AstarPath.active.data.gridGraph.nodes == null)
        {
            ReportFailure("No scanned A* GridGraph found.");
            return false;
        }

        // The player uses a walkable node; candidates will use their
        // actual nearest node (including unwalkable) to reject walls.
        NNInfo playerInfo = AstarPath.active.GetNearest(
            player.position,
            NNConstraint.Default
        );

        GraphNode playerNode = playerInfo.node;

        if (playerNode == null
            || !playerNode.Walkable
            || playerNode.Area == 0
            || playerNode.GraphIndex
                != AstarPath.active.data.gridGraph.graphIndex
            || Vector2.Distance(
                player.position,
                playerInfo.position
            ) > maxPlayerNodeDistance)
        {
            ReportFailure("Player is not near a connected, walkable GridGraph node.");
            return false;
        }

        float minDistance = Mathf.Max(0f, minSpawnDistance);
        float maxDistance = Mathf.Max(minDistance, maxSpawnDistance);
        float padding = Mathf.Max(0f, boundsPadding);
        Vector2 safeMin = mapMin + Vector2.one * padding;
        Vector2 safeMax = mapMax - Vector2.one * padding;

        if (limitToMapBounds
            && (safeMin.x > safeMax.x || safeMin.y > safeMax.y))
        {
            ReportFailure("Rectangular Map Spawn Bounds are invalid.");
            return false;
        }

        int attempts = Mathf.Max(1, maxAttempts);

        for (int i = 0; i < attempts; i++)
        {
            lastAttemptCount++;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minDistance, maxDistance);

            Vector2 direction = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            );

            Vector2 candidate = (Vector2)player.position
                + direction * distance;

            if (limitToMapBounds
                && !IsInsideRect(candidate, safeMin, safeMax))
            {
                lastRejectedByBounds++;
                LogRejection("Outside legacy map rectangle", candidate);
                continue;
            }

            if (!IsInsidePlayableArea(candidate))
            {
                lastRejectedOutsideArea++;
                LogRejection("Outside playable polygon", candidate);
                continue;
            }

            if (OverlapsEnvironment(candidate))
            {
                lastRejectedByObstacle++;
                LogRejection("Overlaps Environment obstacle", candidate);
                continue;
            }

            if (!HasValidGraphNode(candidate, playerNode))
            {
                lastRejectedByGraph++;
                LogRejection("Not a connected walkable A* node", candidate);
                continue;
            }

            validPosition = new Vector3(candidate.x, candidate.y, 0f);
            lastValidSpawnPosition = validPosition;
            return true;
        }

        ReportFailure(
            "No valid position within " + attempts + " attempts."
        );
        return false;
    }

    private static bool IsInsideRect(
        Vector2 point,
        Vector2 minimum,
        Vector2 maximum)
    {
        return point.x >= minimum.x
            && point.x <= maximum.x
            && point.y >= minimum.y
            && point.y <= maximum.y;
    }

    private bool IsInsidePlayableArea(Vector2 point)
    {
        if (!spawnPlayableArea.OverlapPoint(point))
        {
            return false;
        }

        // A centre point alone is not enough: keep a clearance
        // circle inside the polygon so large enemies don't spawn
        // directly over a perimeter wall.
        const int sampleCount = 12;
        for (int i = 0; i < sampleCount; i++)
        {
            float angle = Mathf.PI * 2f * i / sampleCount;
            Vector2 offset = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * spawnClearanceRadius;

            if (!spawnPlayableArea.OverlapPoint(point + offset))
            {
                return false;
            }
        }
        return true;
    }

    private bool OverlapsEnvironment(Vector2 point)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(environmentBlockingLayers);
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapCircle(
            point,
            spawnClearanceRadius,
            filter,
            obstacleResults
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = obstacleResults[i];
            if (hit != null && !hit.isTrigger)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasValidGraphNode(
        Vector2 candidate,
        GraphNode playerNode)
    {
        // Unconstrained nearest prevents a distant valid node on the
        // other side of a wall from legitimizing a blocked position.
        NNInfo nodeInfo = AstarPath.active.GetNearest(
            new Vector3(candidate.x, candidate.y, 0f),
            NNConstraint.None
        );

        GraphNode candidateNode = nodeInfo.node;
        if (candidateNode == null
            || !candidateNode.Walkable
            || candidateNode.Area == 0
            || candidateNode.GraphIndex != playerNode.GraphIndex
            || candidateNode.Area != playerNode.Area)
        {
            return false;
        }

        return Vector2.Distance(
            candidate,
            nodeInfo.position
        ) <= maxDistanceToNode;
    }

    private void ResetDebugCounters()
    {
        lastAttemptCount = 0;
        lastRejectedOutsideArea = 0;
        lastRejectedByObstacle = 0;
        lastRejectedByGraph = 0;
        lastRejectedByBounds = 0;
    }

    private void LogRejection(string reason, Vector2 position)
    {
        if (!logRejectedSpawnAttempts)
        {
            return;
        }
        Debug.Log(
            "[Phase40 Spawn Validator] Rejected " + position
            + " : " + reason,
            this
        );
    }

    private void ReportFailure(string reason)
    {
        if (!logFailedSpawnSearches
            || Time.unscaledTime < nextFailureLogTime)
        {
            return;
        }

        nextFailureLogTime = Time.unscaledTime + 5f;

        Debug.LogWarning(
            "[Phase40 Spawn Validator] " + reason
            + "\nAttempts: " + lastAttemptCount
            + "\nOutside playable area: " + lastRejectedOutsideArea
            + "\nBlocked by Environment: " + lastRejectedByObstacle
            + "\nInvalid A* node: " + lastRejectedByGraph
            + "\nOutside map rectangle: " + lastRejectedByBounds,
            this
        );
    }

    private void OnValidate()
    {
        spawnClearanceRadius = Mathf.Max(0.01f, spawnClearanceRadius);
        maxDistanceToNode = Mathf.Max(0.01f, maxDistanceToNode);
        maxPlayerNodeDistance = Mathf.Max(0.01f, maxPlayerNodeDistance);
    }
}
