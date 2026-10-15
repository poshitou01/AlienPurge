using UnityEngine;

namespace AlienPurge.ArenaArtV03
{
    [DisallowMultipleComponent, RequireComponent(typeof(PolygonCollider2D))]
    public class ArenaV03Boundary : MonoBehaviour
    {
        public Vector2[] footprint;
        [Min(.1f)] public float thickness = 1.0f;
        public bool showAuthoringOutline;

        public void RebuildCollider()
        {
            if (footprint == null || footprint.Length < 2) return;
            PolygonCollider2D collider = GetComponent<PolygonCollider2D>();
            collider.isTrigger = false;
            collider.pathCount = 1;
            collider.SetPath(0, ArenaV03Geometry.BoundaryRibbon(footprint, thickness));
        }

        void OnDrawGizmosSelected()
        {
            if (!showAuthoringOutline || footprint == null) return;
            Gizmos.color = new Color(1f, .25f, .45f, .9f);
            for (int i = 1; i < footprint.Length; i++)
                Gizmos.DrawLine(transform.TransformPoint(footprint[i-1]), transform.TransformPoint(footprint[i]));
        }
    }
}
