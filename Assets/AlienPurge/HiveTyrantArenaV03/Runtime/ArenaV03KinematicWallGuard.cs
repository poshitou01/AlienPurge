using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.ArenaArtV03
{
    // The repository's HiveTyrantBossMotor uses MovePosition on a Kinematic body.
    // Such a body is not pushed back by static walls. Constrain its completed
    // movement with shape casts, without changing the existing motor/body type.
    // This scene-owned link is removed/disabled together with the arena.
    [DefaultExecutionOrder(14000), DisallowMultipleComponent]
    public class ArenaV03KinematicWallGuard : MonoBehaviour
    {
        public Rigidbody2D body;
        public Collider2D solidBody;
        public Transform collisionRoot;
        [Min(.001f)] public float skin = .025f;
        readonly RaycastHit2D[] hits = new RaycastHit2D[128];
        readonly HashSet<Collider2D> arenaColliders = new HashSet<Collider2D>();
        Vector2 lastPosition;
        bool initialized;
        ContactFilter2D filter;

        void OnEnable() { initialized = false; Cache(); }
        void Start() { Cache(); Initialize(); }

        void Cache()
        {
            arenaColliders.Clear();
            int mask = 0;
            if (collisionRoot != null)
                foreach (Collider2D c in collisionRoot.GetComponentsInChildren<Collider2D>(true))
                { arenaColliders.Add(c); mask |= 1 << c.gameObject.layer; }
            filter = new ContactFilter2D(); filter.SetLayerMask(mask); filter.useTriggers = false;
        }

        void Initialize()
        {
            if (body == null || solidBody == null) return;
            lastPosition = body.position;
            initialized = true;
        }

        void LateUpdate()
        {
            if (body == null || solidBody == null || !solidBody.enabled ||
                body.bodyType != RigidbodyType2D.Kinematic || !body.simulated) return;
            if (!initialized) { Initialize(); return; }
            Vector2 desired = body.position;
            Vector2 remaining = desired - lastPosition;
            if (remaining.sqrMagnitude < .00000001f) return;
            Vector2 current = lastPosition;
            body.position = current;
            Physics2D.SyncTransforms();
            // Resolve a sweep and up to two tangent slides around curved wall faces.
            for (int iteration = 0; iteration < 3 && remaining.sqrMagnitude > .00000001f; iteration++)
            {
                float distance = remaining.magnitude;
                Vector2 direction = remaining / distance;
                int count = solidBody.Cast(direction, filter, hits, distance + skin, true);
                float allowed = distance;
                Vector2 normal = Vector2.zero;
                bool blocked = false;
                for (int i = 0; i < count; i++)
                {
                    Collider2D wall = hits[i].collider;
                    if (wall == null || !arenaColliders.Contains(wall) || !wall.enabled || wall.isTrigger) continue;
                    // A tangent/away movement should not stick to a prior contact.
                    if (Vector2.Dot(direction, hits[i].normal) >= -.0001f) continue;
                    float safe = Mathf.Max(0f, hits[i].distance - skin);
                    if (safe < allowed) { allowed = safe; normal = hits[i].normal; blocked = true; }
                }
                current += direction * allowed;
                body.position = current;
                Physics2D.SyncTransforms();
                if (!blocked) break;
                remaining -= direction * allowed;
                float into = Vector2.Dot(remaining, normal);
                if (into < 0f) remaining -= normal * into;
            }
            lastPosition = body.position;
        }
    }
}
