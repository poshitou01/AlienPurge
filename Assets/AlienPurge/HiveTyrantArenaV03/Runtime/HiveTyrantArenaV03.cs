using UnityEngine;

namespace AlienPurge.ArenaArtV03
{
    [DisallowMultipleComponent]
    public class HiveTyrantArenaV03 : MonoBehaviour
    {
        public float worldCanvasWidth = 72f;
        public Transform[] broodPoints;
        public Transform bossStart, playerStart, entryApproach, exitReward;
        public GameObject entryClosedLeaf;
        public Collider2D entryBlocker;
        [SerializeField] bool entranceLocked;

        public bool EntranceLocked { get { return entranceLocked; } }
        void Awake() { SetEntranceLocked(entranceLocked); }
        public void LockEntrance() { SetEntranceLocked(true); }
        public void UnlockEntrance() { SetEntranceLocked(false); }
        public void SetEntranceLocked(bool locked)
        {
            entranceLocked = locked;
            if (entryClosedLeaf != null) entryClosedLeaf.SetActive(locked);
            if (entryBlocker != null) entryBlocker.enabled = locked;
        }

        // Does not instantiate enemies or change the user's existing combat code.
        public bool TryGetBroodPoint(Vector2 playerPosition, float minimumDistance, out Transform point)
        {
            point = null;
            float bestDistance = -1f, minSqr = minimumDistance * minimumDistance;
            if (broodPoints == null) return false;
            foreach (Transform candidate in broodPoints)
            {
                if (candidate == null) continue;
                float sqr = ((Vector2)candidate.position - playerPosition).sqrMagnitude;
                if (sqr < minSqr || sqr <= bestDistance) continue;
                bestDistance = sqr; point = candidate;
            }
            return point != null;
        }
    }
}
