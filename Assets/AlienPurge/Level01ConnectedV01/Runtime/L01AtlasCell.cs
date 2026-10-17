using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class L01AtlasCell : MonoBehaviour
    {
        public Renderer target;
        public Vector4 cell=new Vector4(0,0,1,1);
        MaterialPropertyBlock properties;
        void OnEnable(){Apply();}
        void OnValidate(){Apply();}
        public void Apply()
        {
            if(target==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);properties.SetVector("_Cell",cell);target.SetPropertyBlock(properties);
        }
    }
}
