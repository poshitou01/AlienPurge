using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AlienPurge.Level01.RegionLoop;
using Pathfinding;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienPurge.Level01.Editor
{
    public static class IntegratedRoadProbe
    {
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);var loop=HiveTyrantCombatSetup.SceneComponents<RegionLoopController>(scene).Single();var world=loop.world;
            foreach(var c in world.existingArena.GetComponentsInChildren<Collider2D>(true).Where(c=>c.name=="Approach_End_Stop"))c.enabled=false;
            foreach(var g in world.progression.gates)g.SetLocked(false,true);
            Physics2D.SyncTransforms();world.navigation.ConfigureGrid();world.navigation.astar.Scan();
            var lines=new List<string>();
            foreach(var p in new[]{new Vector2(16.53906f,43),new Vector2(16.53906f,33),new Vector2(30,33),new Vector2(48,33),new Vector2(48,20),new Vector2(48,-25),new Vector2(48,-32),new Vector2(30,-32),new Vector2(16,-32),new Vector2(0,-32),new Vector2(0,-24),new Vector2(0,-20),new Vector2(0,-10),new Vector2(16,-38),new Vector2(16,-50),new Vector2(16,-56.48438f),new Vector2(23,-56.48438f),new Vector2(40,-56.48438f)})
            {
                var at=world.World(p);var nn=world.navigation.astar.GetNearest(at,NNConstraint.Default);
                lines.Add(p+" area="+nn.node.Area+" nearest="+world.Local(nn.position)+" solids="+string.Join(";",Physics2D.OverlapCircleAll(at,.6f,1<<LayerMask.NameToLayer("Environment")).Where(c=>!c.isTrigger).Select(c=>IntegratedFeedbackAudit.PathOf(c.transform))));
            }
            File.WriteAllLines(IntegratedFeedbackAudit.Folder+"/RoadProbe.txt",lines);
            int i=0;foreach(var p in new[]{new Vector2(18,33),new Vector2(48,33),new Vector2(48,-32),new Vector2(0,-25),new Vector2(16,-35),new Vector2(17,-55)})
                IntegratedFeedbackAudit.Render(world.World(p),10,IntegratedFeedbackAudit.Folder+"/AfterCorner_"+(i++)+".png");
            EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
        }
    }
}
