using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    // Added only to the generated copy when the source art root has lost its
    // old runtime component. Existing meshes, artwork and collider paths stay intact.
    [DisallowMultipleComponent]
    public sealed class L01ImportedArena : MonoBehaviour
    {
        public float worldCanvasWidth=72f;
        public GameObject entryClosedLeaf;
        public Collider2D entryBlocker;
        [SerializeField] bool entranceLocked;
        public bool EntranceLocked {get{return entranceLocked;}}
        void Awake(){SetEntranceLocked(entranceLocked);}
        public void SetEntranceLocked(bool value)
        {
            entranceLocked=value;
            if(entryClosedLeaf!=null)entryClosedLeaf.SetActive(value);
            if(entryBlocker!=null)entryBlocker.enabled=value;
        }
    }
}
