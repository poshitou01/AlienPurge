using System;
using UnityEngine;

namespace AlienPurge.ArenaArtV031
{
    [Serializable]
    public class ArenaBodyFilterSnapshot
    {
        public Collider2D collider;
        public LayerMask includeLayers, excludeLayers;
        public int priority;
    }

    // Scene-owned bridge. Body/hurt colliders retain contacts with enemies and
    // projectiles; only their Environment contacts move to the small foot shape.
    [ExecuteAlways, DisallowMultipleComponent]
    public class ArenaV031PlayerFeet : MonoBehaviour
    {
        public Rigidbody2D playerBody;
        public Transform groundAnchor;
        public CapsuleCollider2D feet;
        public LayerMask environmentMask;
        public ArenaBodyFilterSnapshot[] originalBodyFilters;
        public int overridePriority = 40;

        void OnEnable() { Apply(); }
        void OnDisable() { Restore(); }

        public void Apply()
        {
            if (feet == null || playerBody == null || environmentMask.value == 0) return;
            int mask = environmentMask.value;
            if (originalBodyFilters != null)
                foreach (ArenaBodyFilterSnapshot state in originalBodyFilters)
                {
                    if (state == null || state.collider == null) continue;
                    state.collider.includeLayers = state.includeLayers.value & ~mask;
                    state.collider.excludeLayers = state.excludeLayers.value | mask;
                    state.collider.layerOverridePriority = Mathf.Max(overridePriority,state.priority);
                }
            feet.includeLayers = mask;
            feet.excludeLayers = ~mask;
            feet.layerOverridePriority = overridePriority;
            feet.enabled = true;
        }

        public void Restore()
        {
            if (originalBodyFilters != null)
                foreach (ArenaBodyFilterSnapshot state in originalBodyFilters)
                {
                    if (state == null || state.collider == null) continue;
                    state.collider.includeLayers = state.includeLayers;
                    state.collider.excludeLayers = state.excludeLayers;
                    state.collider.layerOverridePriority = state.priority;
                }
            if (feet != null) feet.enabled = false;
        }

        void OnDrawGizmosSelected()
        {
            if (groundAnchor == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(groundAnchor.position+Vector3.left*.18f,groundAnchor.position+Vector3.right*.18f);
        }
    }
}
