using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.PreserveArenaV02.Editor
{
    internal static class PreservedArenaData
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static Type FindType(string name)
        {foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()){Type t=a.GetType(name,false);if(t!=null)return t;}return null;}
        internal static void Set(object target,string name,object value)
        {
            FieldInfo f=target.GetType().GetField(name,Flags);
            if(f==null)throw new InvalidOperationException("已安装的关卡工具缺少字段："+name+"。请使用 Fix05 或更新的 Level01 工具。");
            f.SetValue(target,value);
        }
        internal static object Get(object target,string name)
        {if(target==null)return null;FieldInfo f=target.GetType().GetField(name,Flags);return f!=null?f.GetValue(target):null;}
        internal static void Build(object target)
        {
            MethodInfo m=target.GetType().GetMethod("Build",Flags,null,System.Type.EmptyTypes,null);
            if(m==null)throw new InvalidOperationException("关卡生成方法缺失。");
            try{m.Invoke(target,null);}catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        internal static IEnumerable<T> Components<T>(Scene scene) where T:Component
        {foreach(GameObject r in scene.GetRootGameObjects())foreach(T t in r.GetComponentsInChildren<T>(true))yield return t;}
        internal static string PathOf(Transform t)
        {return t.parent!=null?PathOf(t.parent)+"/"+t.name:t.name;}
        internal static GameObject[] Arenas(Scene scene)
        {
            return Components<Transform>(scene).Where(t=>t.Find("Environment_Continuous")!=null
                &&t.Find("Collision_Wall_Footprints")!=null).Select(t=>t.gameObject).ToArray();
        }
        internal static MonoBehaviour Mono(Scene scene,string name)
        {
            MonoBehaviour[] values=Components<MonoBehaviour>(scene).Where(m=>m!=null&&m.GetType().Name==name).ToArray();
            return values.FirstOrDefault(m=>m.gameObject.activeInHierarchy)??values.FirstOrDefault();
        }
        internal static void Folder(string path)
        {
            string[] parts=path.Split('/');string current=parts[0];
            for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
        internal static string DiskPath(string assetPath)
        {return System.IO.Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath);}
        internal static string FileHash(string assetPath)
        {using(SHA256 h=SHA256.Create())using(FileStream f=File.OpenRead(DiskPath(assetPath)))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","");}
        internal static Dictionary<string,string> SceneHashes()
        {
            return AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)).Distinct().ToDictionary(p=>p,FileHash);
        }
        internal static void AssertUnchanged(Dictionary<string,string> hashes)
        {foreach(var pair in hashes)if(!File.Exists(DiskPath(pair.Key))||FileHash(pair.Key)!=pair.Value)throw new InvalidOperationException("现有场景文件发生变化："+pair.Key+"。请重新读取来源副本；工具不会覆盖它。");}
        static void F(StringBuilder s,float v){s.Append(v.ToString("R",CultureInfo.InvariantCulture)).Append(',');}
        static void V(StringBuilder s,Vector3 v){F(s,v.x);F(s,v.y);F(s,v.z);}
        internal static string ColliderSignature(GameObject root)
        {
            StringBuilder s=new StringBuilder();
            foreach(Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
            {
                s.Append(AnimationUtility.CalculateTransformPath(c.transform,root.transform)).Append('|').Append(c.GetType().FullName).Append('|');
                // Compare the stored geometry exactly. Root world placement is
                // checked separately with a small float-transform tolerance.
                for(Transform t=c.transform;t!=null&&t!=root.transform;t=t.parent)
                {V(s,t.localPosition);V(s,t.localScale);F(s,t.localRotation.x);F(s,t.localRotation.y);F(s,t.localRotation.z);F(s,t.localRotation.w);}
                V(s,c.offset);s.Append(c.isTrigger).Append('|').Append(c.gameObject.layer).Append('|');
                PolygonCollider2D polygon=c as PolygonCollider2D;
                if(polygon!=null)
                {s.Append(polygon.pathCount).Append('|');for(int i=0;i<polygon.pathCount;i++){Vector2[] p=polygon.GetPath(i);s.Append(p.Length).Append('|');foreach(Vector2 q in p)V(s,q);}}
                BoxCollider2D box=c as BoxCollider2D;if(box!=null){V(s,box.size);F(s,box.edgeRadius);}
                CapsuleCollider2D capsule=c as CapsuleCollider2D;if(capsule!=null){V(s,capsule.size);s.Append((int)capsule.direction).Append('|');}
                CircleCollider2D circle=c as CircleCollider2D;if(circle!=null)F(s,circle.radius);
                EdgeCollider2D edge=c as EdgeCollider2D;
                if(edge!=null){F(s,edge.edgeRadius);s.Append(edge.pointCount).Append('|');foreach(Vector2 q in edge.points)V(s,q);
                    s.Append(edge.useAdjacentStartPoint).Append('|').Append(edge.useAdjacentEndPoint).Append('|');V(s,edge.adjacentStartPoint);V(s,edge.adjacentEndPoint);}
                s.Append(';');
            }
            using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()))).Replace("-","");
        }
        internal static string Summary(GameObject root)
        {
            Collider2D[] values=root.GetComponentsInChildren<Collider2D>(true);
            PolygonCollider2D[] polygons=values.OfType<PolygonCollider2D>().ToArray();
            MonoBehaviour boundary=root.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(m=>m!=null&&(m.GetType().Name=="ArenaV031SolidBoundary"||m.GetType().Name=="ArenaV03Boundary"));
            return "来源根物体："+PathOf(root.transform)+"\n碰撞组件："+values.Length+"；PolygonCollider2D："+polygons.Length
                +"；多边形路径："+polygons.Sum(p=>p.pathCount)+"；多边形顶点："+polygons.Sum(p=>p.GetTotalPointCount())
                +"；EdgeCollider2D："+values.OfType<EdgeCollider2D>().Count()
                +"\n边界脚本："+(boundary!=null?boundary.GetType().Name:"直接使用已保存的 Collider")
                +"\n将复制这里实际保存的碰撞数据，不根据默认轮廓重建。";
        }
        internal static void Validate(GameObject root)
        {
            if(root==null)throw new InvalidOperationException("请选择来源副本里的最终 Arena。");
            if(root.GetComponentsInChildren<MonoBehaviour>(true).Any(m=>m==null))
                throw new InvalidOperationException("来源 Arena 有 Missing Script，请先恢复昨天的组件资源。不会用默认脚本重新生成场景。");
            foreach(MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
                if(f.sharedMesh==null)throw new InvalidOperationException("来源网格丢失："+f.name+"。请找到昨天正常的 Scene 或恢复其资源引用；不会用默认 Arena 替代。");
            if(!root.GetComponentsInChildren<Collider2D>(true).Any(c=>!c.isTrigger))throw new InvalidOperationException("所选 Arena 没有实体碰撞。");
            if(root.transform.Find("Collision_Wall_Footprints/Entry_Blocker")==null)
                throw new InvalidOperationException("没有找到原南门 Entry_Blocker，请选择完整 Arena 根物体。");
            if(root.GetComponentsInChildren<CompositeCollider2D>(true).Length>0)
                throw new InvalidOperationException("此来源使用 CompositeCollider2D，当前入口尚未验证它的生成状态。请提供该 Scene 检查；不会替换碰撞。");
        }
        internal sealed class FootSnapshot
        {
            Vector2 size,offset;
            Vector3 anchorOffset,axisX,axisY;
            CapsuleDirection2D direction;
            public string description;
            static Transform FeetAnchor(GameObject player)
            {
                foreach(MonoBehaviour m in player.GetComponentsInChildren<MonoBehaviour>(true))
                    if(m!=null&&m.GetType().Name=="YSortRenderer")
                    {Transform anchor=Get(m,"sortAnchor") as Transform;if(anchor!=null)return anchor;}
                Transform fallback=player.transform.Find("SortAnchor");return fallback!=null?fallback:player.transform;
            }
            internal static FootSnapshot Capture(GameObject arena)
            {
                GameObject[] players=Components<Transform>(arena.scene).Where(t=>t.CompareTag("Player")).Select(t=>t.gameObject).ToArray();
                if(players.Length!=1)throw new InvalidOperationException("最终 Arena 来源需包含一个实际 Player，以保留昨日脚部碰撞。当前数量："+players.Length);
                CapsuleCollider2D foot=players[0].GetComponentsInChildren<CapsuleCollider2D>(true).FirstOrDefault(c=>c.name.Contains("EnvironmentFeet"));
                if(foot==null)throw new InvalidOperationException("来源 Scene 没有找到 Player 的 EnvironmentFeet CapsuleCollider2D。不能将自动生成的脚部碰撞当成昨日最终版，请选择昨天实际测试且包含 Player 的 Scene。");
                Transform anchor=FeetAnchor(players[0]);
                return new FootSnapshot{size=foot.size,offset=foot.offset,direction=foot.direction,
                    anchorOffset=foot.transform.TransformPoint(foot.offset)-anchor.position,
                    axisX=foot.transform.TransformVector(Vector3.right),axisY=foot.transform.TransformVector(Vector3.up),
                    description=PathOf(foot.transform)+"；size="+foot.size+"；offset="+foot.offset};
            }
            internal bool Matches(GameObject player,Transform anchor)
            {
                if(player==null||anchor==null)return false;
                CapsuleCollider2D foot=player.GetComponentsInChildren<CapsuleCollider2D>(true).FirstOrDefault(c=>c.name.Contains("EnvironmentFeet"));
                if(foot==null||foot.size!=size||foot.offset!=offset||foot.direction!=direction)return false;
                return (foot.transform.TransformPoint(foot.offset)-anchor.position-anchorOffset).sqrMagnitude<.00000001f
                    &&(foot.transform.TransformVector(Vector3.right)-axisX).sqrMagnitude<.00000001f
                    &&(foot.transform.TransformVector(Vector3.up)-axisY).sqrMagnitude<.00000001f;
            }
        }
    }
}
