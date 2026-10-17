using UnityEngine;
using Pathfinding;

namespace AlienPurge.Level01ConnectedV01
{
    [DefaultExecutionOrder(-9000),DisallowMultipleComponent]
    public sealed class L01Navigation : MonoBehaviour
    {
        public AstarPath astar;
        public L01World world;
        public bool scanOnStart=true;
        bool ready;
        void Start()
        {
            if(astar==null)astar=AstarPath.active;
            if(astar==null){Debug.LogWarning("Level01 needs the project's A* component.",this);return;}
            ConfigureGrid();
            Physics2D.SyncTransforms();
            if(scanOnStart)astar.Scan();
            ready=true;
        }
        void ConfigureGrid()
        {
            if(world==null||world.Data==null)return;
            GridGraph graph=EnsureGrid(astar);
            Vector2 min=world.World(world.Data.boundsMin.V),max=world.World(world.Data.boundsMax.V);
            float node=.6f*Mathf.Max(.75f,world.transform.lossyScale.x);
            graph.center=(Vector3)((min+max)*.5f);graph.rotation=new Vector3(90,0,0);
            graph.SetDimensions(Mathf.CeilToInt((max.x-min.x)/node),Mathf.CeilToInt((max.y-min.y)/node),node);
            if(graph.collision==null)graph.collision=new GraphCollision();
            graph.collision.use2D=true;graph.collision.type=Pathfinding.ColliderType.Sphere;
            graph.collision.heightCheck=false;graph.collision.unwalkableWhenNoGround=false;
            graph.collision.collisionCheck=true;graph.collision.mask=1<<LayerMask.NameToLayer("Environment");graph.collision.diameter=1.1f;
            graph.erodeIterations=1;
        }
        // A* 4.2.17 does not discover graphTypes from its editor-mode Awake.
        // Both the builder and the runtime must prepare the owning instance
        // before deserializing, creating or serializing its navigation graphs.
        public static GridGraph EnsureGrid(AstarPath target)
        {
            if(target==null)throw new System.ArgumentNullException("target");
            target.ConfigureReferencesInternal();
            AstarData data=target.data;
            if(data.graphTypes==null||data.graphTypes.Length==0)data.FindGraphTypes();
            if(data.graphs==null)data.graphs=new NavGraph[0];
            if(data.graphs.Length==0)data.DeserializeGraphs();
            data.UpdateShortcuts();
            GridGraph graph=data.gridGraph;
            if(graph==null)graph=data.AddGraph(typeof(GridGraph)) as GridGraph;
            if(graph==null)throw new System.InvalidOperationException("A* 无法创建 GridGraph，请检查插件是否启用了 Grid Graph。");
            return graph;
        }
        public void Refresh(Bounds changedArea)
        {
            if(!Application.isPlaying||!ready||astar==null||astar.isScanning)return;
            Physics2D.SyncTransforms();
            changedArea.Expand(3f);
            astar.UpdateGraphs(changedArea);
        }
    }
}
