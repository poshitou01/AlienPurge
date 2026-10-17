using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.Level01ConnectedV01.Editor
{
    // Identification and preflight are read-only. ConnectCopy is used on saved
    // snapshots or the user's copied Level01 scene, never the original Arena scene.
    public static class L01ArenaImport
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        public static MonoBehaviour Component(GameObject root)
        {
            if(root==null)return null;
            L01ImportedArena bridge=root.GetComponent<L01ImportedArena>();
            if(bridge!=null)return bridge;
            foreach(MonoBehaviour m in root.GetComponents<MonoBehaviour>())
            {
                if(m==null)continue;
                Type t=m.GetType();
                bool width=t.GetField("worldCanvasWidth",Flags)!=null||t.GetProperty("worldCanvasWidth",Flags)!=null;
                if(width&&t.GetMethod("SetEntranceLocked",Flags,null,new[]{typeof(bool)},null)!=null)return m;
            }
            return null;
        }
        public static bool LooksLikeRoot(GameObject candidate)
        {
            if(candidate==null)return false;
            Transform t=candidate.transform;
            if(t.Find("Environment_Continuous")!=null&&t.Find("Collision_Wall_Footprints")!=null)return true;
            return candidate.name.StartsWith("ArenaRoot_Continuous_",StringComparison.Ordinal)
                &&candidate.GetComponentInChildren<MeshFilter>(true)!=null
                &&candidate.GetComponentInChildren<Collider2D>(true)!=null;
        }
        public static GameObject ResolveRoot(GameObject selection)
        {
            if(selection==null)return null;
            for(Transform t=selection.transform;t!=null;t=t.parent)
                if(Component(t.gameObject)!=null)return t.gameObject;
            for(Transform t=selection.transform;t!=null;t=t.parent)
                if(LooksLikeRoot(t.gameObject))return t.gameObject;
            GameObject fallback=null;
            foreach(Transform t in selection.GetComponentsInChildren<Transform>(true))
                if(Component(t.gameObject)!=null||LooksLikeRoot(t.gameObject))
                {
                    if(t.gameObject.activeInHierarchy)return t.gameObject;
                    if(fallback==null)fallback=t.gameObject;
                }
            return fallback;
        }
        public static GameObject FindRoot(Scene scene)
        {
            if(!scene.IsValid()||!scene.isLoaded)return null;
            GameObject fallback=null;
            foreach(GameObject root in scene.GetRootGameObjects())
            {
                GameObject arena=ResolveRoot(root);
                if(arena==null)continue;
                if(arena.activeInHierarchy)return arena;
                if(fallback==null)fallback=arena;
            }
            return fallback;
        }
        static Transform Named(GameObject root,string path,string name)
        {
            Transform direct=root.transform.Find(path);if(direct!=null)return direct;
            Transform fallback=null;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name)
                {
                    if(t.gameObject.activeInHierarchy)return t;
                    if(fallback==null)fallback=t;
                }
            return fallback;
        }
        public static BoxCollider2D EntryBlocker(GameObject root)
        {
            MonoBehaviour native=Component(root);
            BoxCollider2D bound=L01ProjectAccess.Get(native,"entryBlocker") as BoxCollider2D;
            if(bound!=null)return bound;
            Transform t=Named(root,"Collision_Wall_Footprints/Entry_Blocker","Entry_Blocker");
            return t!=null?t.GetComponent<BoxCollider2D>():null;
        }
        static float MeshWidth(GameObject root,MeshFilter filter)
        {
            if(filter==null||filter.sharedMesh==null)return 0;
            Bounds b=filter.sharedMesh.bounds;float min=float.PositiveInfinity,max=float.NegativeInfinity;
            for(int i=0;i<8;i++)
            {
                Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                float x=root.transform.InverseTransformPoint(filter.transform.TransformPoint(p)).x;
                min=Mathf.Min(min,x);max=Mathf.Max(max,x);
            }
            return max-min;
        }
        public static float ReadWidth(GameObject root)
        {
            object value=L01ProjectAccess.Get(Component(root),"worldCanvasWidth");
            if(value!=null)
            {
                try{float f=Convert.ToSingle(value);if(f>0&&!float.IsNaN(f)&&!float.IsInfinity(f))return f;}
                catch(InvalidCastException){}catch(FormatException){}catch(OverflowException){}
            }
            Transform plate=Named(root,"Environment_Continuous/Ground_And_Connected_Perimeter","Ground_And_Connected_Perimeter");
            float width=plate!=null?MeshWidth(root,plate.GetComponent<MeshFilter>()):0;
            if(width>0)return width;
            // Renamed roots still keep their existing continuous base geometry.
            foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if(filter.gameObject.activeInHierarchy)width=Mathf.Max(width,MeshWidth(root,filter));
            if(width<=0||float.IsNaN(width)||float.IsInfinity(width))
                throw new InvalidOperationException("Arena 地面网格无法读取尺寸；请选择完整的 ArenaRoot_Continuous 根物体。");
            return width;
        }
        public static void Preflight(GameObject root)
        {
            ReadWidth(root);
            if(EntryBlocker(root)==null)
                throw new InvalidOperationException("Arena 没有找到现有南门 Collision_Wall_Footprints/Entry_Blocker；请指定完整 Arena 根物体。未修改原碰撞。");
        }
        public static MonoBehaviour ConnectCopy(GameObject root,float width,bool registerUndo=false)
        {
            BoxCollider2D blocker=EntryBlocker(root);
            if(blocker==null)throw new InvalidOperationException("Arena 副本的南门 Collider 丢失。");
            MonoBehaviour native=Component(root);
            Transform leaf=Named(root,"Gates/Entry_ClosedLeaf","Entry_ClosedLeaf");
            if(native!=null)
            {
                if(registerUndo)Undo.RecordObject(native,"Connect copied Arena");
                if(L01ProjectAccess.Get(native,"entryBlocker")==null)L01ProjectAccess.Set(native,"entryBlocker",blocker);
                if(L01ProjectAccess.Get(native,"entryClosedLeaf")==null&&leaf!=null)L01ProjectAccess.Set(native,"entryClosedLeaf",leaf.gameObject);
                if(L01ProjectAccess.Get(native,"entryBlocker") is BoxCollider2D)return native;
            }
            L01ImportedArena adapter=root.GetComponent<L01ImportedArena>();
            if(adapter==null)adapter=registerUndo?Undo.AddComponent<L01ImportedArena>(root):root.AddComponent<L01ImportedArena>();
            else if(registerUndo)Undo.RecordObject(adapter,"Connect copied Arena");
            adapter.worldCanvasWidth=width;adapter.entryBlocker=blocker;
            adapter.entryClosedLeaf=leaf!=null?leaf.gameObject:null;
            if(registerUndo&&leaf!=null)Undo.RecordObject(leaf.gameObject,"Preserve Arena gate state");
            if(registerUndo)Undo.RecordObject(blocker,"Preserve Arena gate state");
            adapter.SetEntranceLocked(blocker.enabled);
            return adapter;
        }
    }
}
