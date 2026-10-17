using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    [DisallowMultipleComponent]
    public sealed class RegionAnchor : MonoBehaviour
    {
        public string stableId, regionId;
        public float radius = 1.5f;
        public bool residentSlot;
        public LineRenderer warning;
        public void ShowWarning(bool visible) { if (warning != null) warning.enabled = visible; }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
