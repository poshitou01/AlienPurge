using System;
using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [Serializable] public class L01ColliderFilter
    {
        public Collider2D collider;
        public LayerMask includeLayers,excludeLayers;
        public int priority;
    }
    // Only installed when the preserved Arena did not already provide its
    // working feet binding. One feet owner is shared by the complete level.
    [DefaultExecutionOrder(-18500),DisallowMultipleComponent]
    public sealed class L01FeetContact : MonoBehaviour
    {
        public CapsuleCollider2D feet;
        public LayerMask environmentMask;
        public L01ColliderFilter[] original;
        public void Apply()
        {
            if(feet==null||original==null)return;
            int mask=environmentMask.value;
            foreach(L01ColliderFilter s in original)
            {
                if(s.collider==null)continue;
                s.collider.includeLayers=s.includeLayers.value&~mask;
                s.collider.excludeLayers=s.excludeLayers.value|mask;
                s.collider.layerOverridePriority=45;
            }
            feet.includeLayers=mask;feet.excludeLayers=~mask;feet.layerOverridePriority=45;feet.enabled=true;
        }
        void OnEnable(){Apply();}
        void OnDisable()
        {
            if(original!=null)foreach(L01ColliderFilter s in original)
                if(s.collider!=null){s.collider.includeLayers=s.includeLayers;s.collider.excludeLayers=s.excludeLayers;s.collider.layerOverridePriority=s.priority;}
            if(feet!=null)feet.enabled=false;
        }
    }
}
