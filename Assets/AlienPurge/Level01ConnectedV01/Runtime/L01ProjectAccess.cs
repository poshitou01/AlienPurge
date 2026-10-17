using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.Level01ConnectedV01
{
    public static class L01ProjectAccess
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        public static object Get(object target,string name)
        {
            if(target==null)return null;
            Type t=target.GetType();
            PropertyInfo p=t.GetProperty(name,Flags);
            if(p!=null && p.GetIndexParameters().Length==0)return p.GetValue(target,null);
            FieldInfo f=t.GetField(name,Flags);return f!=null?f.GetValue(target):null;
        }
        public static bool Set(object target,string name,object value)
        {
            if(target==null)return false;
            FieldInfo f=target.GetType().GetField(name,Flags);if(f==null)return false;
            f.SetValue(target,value);return true;
        }
        public static bool Call(object target,string name,params object[] args)
        {
            if(target==null)return false;
            foreach(MethodInfo m in target.GetType().GetMethods(Flags))
            {
                if(m.Name!=name || m.GetParameters().Length!=args.Length)continue;
                m.Invoke(target,args);return true;
            }
            return false;
        }
        public static bool Bool(object target,string name,bool fallback=false)
        {
            object v=Get(target,name);return v is bool?(bool)v:fallback;
        }
        public static MonoBehaviour Find(Scene scene,string typeName)
        {
            MonoBehaviour fallback=null;
            foreach(GameObject root in scene.GetRootGameObjects())
                foreach(MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component!=null && component.GetType().Name==typeName)
                    {if(component.gameObject.activeInHierarchy)return component;if(fallback==null)fallback=component;}
            return fallback;
        }
        public static Transform Feet(GameObject player)
        {
            if(player==null)return null;
            foreach(MonoBehaviour m in player.GetComponentsInChildren<MonoBehaviour>(true))
                if(m!=null && m.GetType().Name=="YSortRenderer")
                {
                    Transform t=Get(m,"sortAnchor") as Transform;if(t!=null)return t;
                }
            Transform anchor=player.transform.Find("SortAnchor");return anchor!=null?anchor:player.transform;
        }
        public static bool IsRunActive()
        {
            return GameManager.Instance==null || GameManager.Instance.IsPlaying;
        }
    }
}
