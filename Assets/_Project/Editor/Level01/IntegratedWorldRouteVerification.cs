using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using AlienPurge.Level01ConnectedV01;
using Pathfinding;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienPurge.Level01.Editor
{
    // Tests the new gate dependency using the actual authored collision and native navigation grid.
    // All door changes are an unsaved Editor fixture; the installed scene is reloaded afterwards.
    public static class IntegratedWorldRouteVerification
    {
        public static void Run()
        {
            var lines=new List<string>();
            string target=IntegratedWorldInstaller.Target;
            byte[] before=File.ReadAllBytes(target);
            try
            {
                var scene=EditorSceneManager.OpenScene(target);
                var loop=HiveTyrantCombatSetup.SceneComponents<RegionLoopController>(scene).Single();
                var world=loop.world;var navigation=world.navigation;var gates=world.progression.gates;
                // L01ArenaBridge.Awake removes this art-scene end stop at runtime. Edit Mode
                // does not invoke that non-ExecuteAlways bridge, so mirror exactly its fixture.
                var stops=world.existingArena.GetComponentsInChildren<Collider2D>(true)
                    .Where(c=>c.enabled&&c.name=="Approach_End_Stop").ToArray();
                foreach(var stop in stops)stop.enabled=false;
                lines.Add("FIXTURE: mirrored native L01ArenaBridge.Awake removal of "+stops.Length+" Approach_End_Stop collider(s), without saving the scene.");
                navigation.ConfigureGrid();
                foreach(var gate in gates)gate.SetLocked(gate.gateId=="G6"||gate.gateId=="G7",true);
                Physics2D.SyncTransforms();navigation.astar.Scan();
                Vector3 research=loop.Terminal("InfectedResearch").transform.position;
                Vector3 arena=world.World(new Vector2(0,-10));
                Vector3 extraction=loop.Terminal("Extraction").transform.position;
                Require(!Connected(navigation.astar,research,arena),"Before Research, the authored G6 collision blocks Arena access",lines);
                gates.Single(g=>g.gateId=="G6").SetLocked(false,true);
                Physics2D.SyncTransforms();navigation.astar.Scan();
                for(int i=0;i<4;i++)
                    Require(Connected(navigation.astar,loop.terminals[i].transform.position,loop.terminals[i+1].transform.position),
                        "Native navigation connects "+loop.terminals[i].regionId+" -> "+loop.terminals[i+1].regionId+" with completed gates",lines);
                Require(Connected(navigation.astar,research,arena),"Research-open G6 provides a traversable route into the preserved Arena",lines);
                Require(!Connected(navigation.astar,research,extraction),"G7 collision blocks the extraction branch before Boss defeat",lines);
                gates.Single(g=>g.gateId=="G7").SetLocked(false,true);
                Physics2D.SyncTransforms();navigation.astar.Scan();
                Require(Connected(navigation.astar,arena,extraction),"Boss-open G7 provides a traversable Arena -> extraction route",lines);
                lines.Add("LIMITS: actual A* grid and authored collider connectivity; gate completion is an unsaved fixture. Actual mission/Boss events were tested in PlayMode.txt. No keyboard traversal is claimed.");
            }
            catch(Exception e){lines.Add("FAIL: "+e);throw;}
            finally
            {
                EditorSceneManager.OpenScene(target);
                if(!before.SequenceEqual(File.ReadAllBytes(target)))throw new InvalidOperationException("Route fixture changed the installed scene file.");
                File.WriteAllLines(IntegratedWorldInstaller.Reports+"/RouteConnectivity.txt",lines);
            }
        }
        static bool Connected(AstarPath astar,Vector3 from,Vector3 to)
        {
            var a=astar.GetNearest(from,NNConstraint.Default);var b=astar.GetNearest(to,NNConstraint.Default);
            if(a.node==null||b.node==null||!a.node.Walkable||!b.node.Walkable||
                Vector3.Distance(a.position,from)>=1.8f||Vector3.Distance(b.position,to)>=1.8f)
                throw new InvalidOperationException("No valid navigation endpoint: "+from+" -> "+to+"; nearest="+a.position+" / "+b.position);
            return PathUtilities.IsPathPossible(a.node,b.node);
        }
        static void Require(bool condition,string message,List<string> lines)
        { if(!condition)throw new InvalidOperationException(message);lines.Add("PASS: "+message); }
    }
}
