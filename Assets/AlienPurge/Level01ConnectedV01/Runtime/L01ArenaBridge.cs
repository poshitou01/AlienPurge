using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    // Scene-owned bridge: never changes the source Arena prefab or scene asset.
    [DefaultExecutionOrder(-19000),DisallowMultipleComponent]
    public sealed class L01ArenaBridge : MonoBehaviour
    {
        public L01World world;
        public bool lockDuringBossCombat=true;
        public MonoBehaviour bossHealth,bossMotor;
        public bool encounterStarted,encounterFinished;
        readonly List<Collider2D> removedStops=new List<Collider2D>();
        bool initialEntranceLocked,initialMovementLocked,prepared;
        void Awake()
        {
            if(world==null||world.existingArena==null)return;
            initialEntranceLocked=L01ProjectAccess.Bool(world.existingArena,"EntranceLocked");
            foreach(Collider2D c in world.existingArena.GetComponentsInChildren<Collider2D>(true))
                if(c.enabled&&c.name=="Approach_End_Stop"){removedStops.Add(c);c.enabled=false;}
            if(world.boss!=null)
            {
                foreach(MonoBehaviour m in world.boss.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(m==null)continue;
                    if(m.GetType().Name=="EnemyHealth"||m.GetType().Name=="BossHealth")bossHealth=m;
                    if(m.GetType().Name=="HiveTyrantBossMotor")bossMotor=m;
                }
            }
            initialMovementLocked=L01ProjectAccess.Bool(bossMotor,"IsMovementLocked");
            L01ProjectAccess.Call(bossMotor,"SetMovementLocked",true);
            prepared=true;
        }
        void Update()
        {
            if(world==null||world.progression==null||world.player==null)return;
            if(!encounterStarted&&(world.progression.Has("Research")||world.progression.previewAllDoorsOpen))
            {
                Vector2 foot=world.Local(world.PlayerFeet);
                if(Mathf.Abs(foot.x)<4.5f&&foot.y>-15.5f&&foot.y<8)
                {
                    encounterStarted=true;
                    L01ProjectAccess.Call(bossMotor,"SetTarget",world.player.transform);
                    L01ProjectAccess.Call(bossMotor,"SetMovementLocked",false);
                    // An art-only Boss has no death signal. Keep its exit usable
                    // until a real health component is supplied by the project.
                    if(lockDuringBossCombat&&bossHealth!=null)world.progression.SetCombatLock(true);
                }
            }
            if(encounterStarted&&!encounterFinished)
            {
                bool dead=world.boss==null||L01ProjectAccess.Bool(bossHealth,"IsDead")||L01ProjectAccess.Bool(bossMotor,"IsDead");
                if(dead)NotifyBossDefeated();
            }
        }
        public void NotifyBossDefeated()
        {
            if(encounterFinished)return;encounterFinished=true;
            if(world!=null&&world.progression!=null)world.progression.NotifyBossDefeated();
        }
        void OnDisable()
        {
            if(!prepared)return;
            foreach(Collider2D c in removedStops)if(c!=null)c.enabled=true;
            removedStops.Clear();
            if(world!=null&&world.existingArena!=null)L01ProjectAccess.Call(world.existingArena,"SetEntranceLocked",initialEntranceLocked);
            L01ProjectAccess.Call(bossMotor,"SetMovementLocked",initialMovementLocked);
            prepared=false;
        }
    }
}
