using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    // The existing Storm controller generates its zone in the warning phase.
    // Correct an unreachable legacy random center before the active damage phase.
    [DefaultExecutionOrder(-14000),DisallowMultipleComponent]
    public sealed class L01SafeZoneBridge : MonoBehaviour
    {
        public L01World world;
        public MonoBehaviour safeZone;
        public PolygonCollider2D playableArea;
        Vector2 lastChecked=new Vector2(float.MaxValue,float.MaxValue);
        int lastStorm=-1;
        void Awake()
        {
            if(world!=null&&playableArea==null)
            {
                Transform area=world.transform.Find("Spawn_Playable_Area");if(area!=null)playableArea=area.GetComponent<PolygonCollider2D>();
            }
        }
        void LateUpdate()
        {
            if(world==null||safeZone==null||world.player==null||PlanetStormController.Instance==null||!PlanetStormController.Instance.IsWarning)return;
            if(!L01ProjectAccess.Bool(safeZone,"HasActiveZone"))return;
            object centerValue=L01ProjectAccess.Get(safeZone,"CurrentCenter");if(!(centerValue is Vector2))return;
            Vector2 center=(Vector2)centerValue;
            int storm=PlanetStormController.Instance.CurrentStormNumber;
            if((lastChecked-center).sqrMagnitude<.0001f&&lastStorm==storm)return;
            float radius=8;object value=L01ProjectAccess.Get(safeZone,"Radius");if(value is float)radius=(float)value;
            if(!Valid(center,radius))
            {
                Vector2 chosen=Vector2.zero;bool found=false;float best=float.MaxValue;
                if(world.safeZoneAnchors!=null)foreach(Transform anchor in world.safeZoneAnchors)
                {
                    if(anchor==null||!Valid(anchor.position,radius))continue;
                    float distance=((Vector2)anchor.position-(Vector2)world.PlayerFeet).sqrMagnitude;
                    if(distance<best){best=distance;chosen=anchor.position;found=true;}
                }
                if(!found)
                {
                    Vector2 origin=world.PlayerFeet;
                    for(int ring=0;ring<=7&&!found;ring++)for(int a=0;a<16;a++)
                    {
                        float angle=a*Mathf.PI/8;Vector2 p=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*ring*2;
                        if(Valid(p,radius)){chosen=p;found=true;break;}
                    }
                }
                if(found){L01ProjectAccess.Set(safeZone,"currentCenter",chosen);center=chosen;}
            }
            lastChecked=center;lastStorm=storm;
        }
        bool Valid(Vector2 p,float radius)
        {
            int mask=1<<LayerMask.NameToLayer("Environment");
            if(Physics2D.OverlapCircle(p,.5f,mask)!=null||!Playable(p))return false;
            string current=world.progression!=null?world.progression.CurrentRegion:null;
            string candidate=world.ResolveRegion(world.Local(p));
            if(!string.IsNullOrEmpty(current)&&candidate!=current)return false;
            float checkedRadius=current=="Extraction"?Mathf.Min(radius,3f):radius;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;
                if(!Playable(p+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*checkedRadius))return false;
            }
            return true;
        }
        bool Playable(Vector2 p)
        {
            if(playableArea!=null&&playableArea.OverlapPoint(p))return true;
            Vector2 local=world.Local(p);return local.x*local.x/(24*24)+local.y*local.y/(13*13)<1;
        }
    }
}
