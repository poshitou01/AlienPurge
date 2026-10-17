using Pathfinding;
using UnityEngine;
using AlienPurge.Level01ConnectedV01;

// Only installed in the dedicated scene. Real native A* graph, no replacement chase logic.
[DefaultExecutionOrder(-8000), DisallowMultipleComponent]
public sealed class HiveTyrantTestNavigation : MonoBehaviour
{
    [SerializeField] private AstarPath astar;
    private void Start()
    {
        if (astar == null) astar = GetComponent<AstarPath>();
        GridGraph graph = L01Navigation.EnsureGrid(astar);
        graph.center = Vector3.zero; graph.rotation = new Vector3(90, 0, 0);
        graph.SetDimensions(120, 100, .5f);
        graph.collision.use2D = true; graph.collision.type = Pathfinding.ColliderType.Sphere;
        graph.collision.heightCheck = false; graph.collision.unwalkableWhenNoGround = false;
        graph.collision.collisionCheck = true; graph.collision.mask = 1 << LayerMask.NameToLayer("Environment");
        graph.collision.diameter = 1.5f; graph.erodeIterations = 1;
        Physics2D.SyncTransforms(); astar.Scan();
    }
}
