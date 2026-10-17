using Pathfinding;
using UnityEngine;

[DefaultExecutionOrder(-8000)]
public sealed class EnemyAbilityTestNavigation : MonoBehaviour
{
    private void Start()
    {
        var astar = GetComponent<AstarPath>();
        var graph = AlienPurge.Level01ConnectedV01.L01Navigation.EnsureGrid(astar);
        graph.center = Vector3.zero; graph.rotation = new Vector3(90, 0, 0); graph.SetDimensions(72, 48, .5f);
        graph.collision.use2D = true; graph.collision.type = Pathfinding.ColliderType.Sphere;
        graph.collision.heightCheck = false; graph.collision.unwalkableWhenNoGround = false;
        graph.collision.collisionCheck = true; graph.collision.mask = 1 << 7; graph.collision.diameter = 1.5f; graph.erodeIterations = 1;
        Physics2D.SyncTransforms(); astar.Scan();
    }
}
