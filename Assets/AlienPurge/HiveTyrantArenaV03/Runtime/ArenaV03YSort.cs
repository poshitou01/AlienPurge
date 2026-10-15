using UnityEngine;
using UnityEngine.Rendering;

namespace AlienPurge.ArenaArtV03
{
    // Matches AlienPurge's existing YSortRenderer formula. Sorting uses the
    // ground anchor, never the body's Jet Jump visual offset or sprite bounds.
    [ExecuteAlways, DefaultExecutionOrder(15000), DisallowMultipleComponent]
    public class ArenaV03YSort : MonoBehaviour
    {
        public Transform sortAnchor;
        public Renderer targetRenderer;
        public SortingGroup targetGroup;
        public int sortingLayerId;
        public int orderPerUnit = 100, baseOrder, orderOffset;
        public bool restoreGroupOnDisable;
        public int originalLayerId, originalOrder;
        public bool originalEnabled;

        void OnEnable() { Refresh(); }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            Transform anchor = sortAnchor != null ? sortAnchor : transform;
            int order = Mathf.Clamp(baseOrder - Mathf.RoundToInt(anchor.position.y * orderPerUnit) + orderOffset, -28000, 28000);
            if (targetRenderer != null)
            {
                targetRenderer.sortingLayerID = sortingLayerId;
                targetRenderer.sortingOrder = order;
            }
            if (targetGroup != null)
            {
                targetGroup.enabled = true;
                targetGroup.sortingLayerID = sortingLayerId;
                targetGroup.sortingOrder = order;
            }
        }
        void OnDisable()
        {
            if (!restoreGroupOnDisable || targetGroup == null) return;
            targetGroup.sortingLayerID = originalLayerId;
            targetGroup.sortingOrder = originalOrder;
            targetGroup.enabled = originalEnabled;
        }
    }
}
