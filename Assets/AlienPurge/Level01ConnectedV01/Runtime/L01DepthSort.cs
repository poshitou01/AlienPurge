using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class L01DepthSort : MonoBehaviour
    {
        public Renderer target;
        public Transform anchor;
        public Vector3 localAnchor;
        public int orderPerUnit=100, baseOrder, offset, sortingLayer;
        public bool alwaysForeground;
        void OnEnable(){Apply();}
        void LateUpdate(){Apply();}
        public void Apply()
        {
            if(target==null)return;
            Vector3 position=anchor!=null?anchor.position:transform.TransformPoint(localAnchor);
            int order=alwaysForeground?28000:
                Mathf.Clamp(baseOrder+offset-Mathf.RoundToInt(position.y*orderPerUnit),-32760,32760);
            // Static scenery keeps the same order for most frames. Avoid
            // invalidating renderer sorting until its actual value changes.
            if(target.sortingLayerID!=sortingLayer)target.sortingLayerID=sortingLayer;
            if(target.sortingOrder!=order)target.sortingOrder=order;
        }
    }
}
