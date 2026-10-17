using AlienPurge.Level01ConnectedV01;
using Pathfinding;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    public static class RegionSpawnSpace
    {
        static readonly Collider2D[] hits = new Collider2D[64];
        public static bool Valid(L01World world, string region, Vector3 position, Vector3 destination,
            float clearance, bool navigation, out string reason)
        {
            reason = null;
            if (world == null || world.ResolveRegion(world.Local(position)) != region)
            { reason = "outside region walk polygon"; return false; }
            int count = Physics2D.OverlapCircleNonAlloc(position, clearance, hits);
            if (count == hits.Length) { reason = "collision query saturated"; return false; }
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit == null || !hit.enabled || hit.isTrigger) continue;
                if (hit.GetComponentInParent<EnemyHealth>() != null || hit.GetComponentInParent<PlayerHealth>() != null) continue;
                reason = "blocked by " + hit.name; return false;
            }
            if (!navigation) return true;
            var astar = world.navigation != null ? world.navigation.astar : null;
            if (astar == null || astar.isScanning) { reason = "navigation not ready"; return false; }
            var from = astar.GetNearest(position, NNConstraint.Default);
            var to = astar.GetNearest(destination, NNConstraint.Default);
            if (from.node == null || to.node == null || !from.node.Walkable || !to.node.Walkable ||
                Vector2.Distance(from.position, position) > 1.2f || Vector2.Distance(to.position, destination) > 1.2f ||
                !PathUtilities.IsPathPossible(from.node, to.node))
            { reason = "no walkable path to combat space (check buildings / closed gates)"; return false; }
            return true;
        }
    }
}
