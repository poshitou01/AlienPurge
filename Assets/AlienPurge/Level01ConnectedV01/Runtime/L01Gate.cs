using UnityEngine;
using TMPro;

namespace AlienPurge.Level01ConnectedV01
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class L01Gate : MonoBehaviour
    {
        public string gateId,requirement;
        public L01World world;
        public MeshRenderer visual;
        public BoxCollider2D blocker;
        public TMP_Text sign;
        public bool vertical,externalArena;
        public bool locked;
        bool requestedLocked;
        MaterialPropertyBlock properties;
        void OnEnable(){requestedLocked=locked;Apply(locked);}
        public Bounds CollisionBounds
        {
            get
            {
                if(blocker==null)return new Bounds(transform.position,Vector3.one*2);
                Vector3 scale=blocker.transform.lossyScale;
                return new Bounds(blocker.transform.TransformPoint(blocker.offset),
                    new Vector3(Mathf.Abs(scale.x)*blocker.size.x,Mathf.Abs(scale.y)*blocker.size.y,4));
            }
        }
        public void SetLocked(bool value,bool force=false)
        {
            requestedLocked=value;
            if(value && !force && OccupiedByPlayer())return;
            Apply(value);
        }
        void Update(){if(requestedLocked!=locked && (!requestedLocked||!OccupiedByPlayer()))Apply(requestedLocked);}
        bool OccupiedByPlayer()
        {
            if(world==null||world.player==null||blocker==null)return false;
            Vector2 p=blocker.transform.InverseTransformPoint(world.PlayerFeet);
            Vector2 h=blocker.size*.5f+Vector2.one*.45f;
            return Mathf.Abs(p.x-blocker.offset.x)<h.x && Mathf.Abs(p.y-blocker.offset.y)<h.y;
        }
        void Apply(bool value)
        {
            bool changed=locked!=value;locked=value;
            if(externalArena)
            {
                if(world!=null&&world.existingArena!=null)L01ProjectAccess.Call(world.existingArena,"SetEntranceLocked",value);
            }
            else if(blocker!=null)blocker.enabled=value;
            if(visual!=null)
            {
                if(properties==null)properties=new MaterialPropertyBlock();
                visual.GetPropertyBlock(properties);
                properties.SetVector("_Cell",vertical ? new Vector4(value ? .5f : 0f,0,.5f,.5f) :
                    new Vector4(value ? .5f : 0f,804f/1254f,.5f,420f/1254f));
                visual.SetPropertyBlock(properties);
            }
            if(sign!=null)sign.color=value?new Color(1,.70f,.28f):new Color(.58f,.96f,1);
            if(changed&&world!=null&&world.navigation!=null)world.navigation.Refresh(CollisionBounds);
        }
        [ContextMenu("Preview/Open this gate")]
        void PreviewOpen(){SetLocked(false,true);}
        [ContextMenu("Preview/Close this gate")]
        void PreviewClose(){SetLocked(true,true);}
    }
}
