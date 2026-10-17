using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AlienPurge.Level01ConnectedV01
{
    [DefaultExecutionOrder(-17000),DisallowMultipleComponent]
    public sealed class L01Progression : MonoBehaviour
    {
        public AlienPurge.Level01.RegionLoop.RegionLoopController regionLoop;
        public L01World world;
        public L01Gate[] gates;
        public MonoBehaviour missionManager;
        public bool previewAllDoorsOpen;
        public UnityEvent onBeaconCompleted=new UnityEvent(),onResearchCompleted=new UnityEvent(),onBossDefeated=new UnityEvent();
        [SerializeField] string currentRegion="ColonyEntry";
        [SerializeField] List<string> completedFlags=new List<string>();
        HashSet<string> flags=new HashSet<string>();
        float nextObserve;
        bool combatLocked;
        public string CurrentRegion {get{return currentRegion;}}
        void Awake()
        {
            flags.Clear();completedFlags.Clear();combatLocked=false;currentRegion="ColonyEntry";
            if(missionManager==null)missionManager=L01ProjectAccess.Find(gameObject.scene,"MissionManager");
            RefreshDoors(true);
        }
        void Update()
        {
            if(Time.unscaledTime<nextObserve)return;nextObserve=Time.unscaledTime+.12f;
            if(world==null)return;
            if(regionLoop==null && missionManager!=null)
            {
                string kind=Convert.ToString(L01ProjectAccess.Get(missionManager,"CurrentMissionType"));
                string state=Convert.ToString(L01ProjectAccess.Get(missionManager,"CurrentMissionState"));
                if(kind=="BeaconActivation"&&state=="Completed")Complete("Beacon");
                if(kind=="BeaconActivation"&&state=="Failed")Complete("BeaconFailed");
            }
            string region=world.ResolveRegion(world.Local(world.PlayerFeet));
            if(!string.IsNullOrEmpty(region)&&region!=currentRegion)
            {
                currentRegion=region;world.MoveMissionPoints(region);
            }
        }
        public bool Has(string flag){return regionLoop!=null ? regionLoop.HasMilestone(flag) : string.IsNullOrEmpty(flag)||flags.Contains(flag);}
        public void RefreshRegionDoors(){if(regionLoop!=null)RefreshDoors();}
        public bool Complete(string flag)
        {
            if(regionLoop!=null)return false; // The region controller alone owns completion in this mode.
            if(string.IsNullOrEmpty(flag)||!flags.Add(flag))return false;
            completedFlags.Add(flag);RefreshDoors();
            if(flag=="Beacon")onBeaconCompleted.Invoke();
            if(flag=="Research")onResearchCompleted.Invoke();
            if(flag=="BossDefeated")onBossDefeated.Invoke();
            return true;
        }
        public void SetCombatLock(bool value)
        {
            combatLocked=value;RefreshDoors();
        }
        public void NotifyBossDefeated(){Complete("BossDefeated");SetCombatLock(false);}
        void RefreshDoors(bool force=false)
        {
            if(gates==null)return;
            foreach(L01Gate gate in gates)
            {
                if(gate==null)continue;
                bool shouldLock=!(previewAllDoorsOpen&&regionLoop==null)&&!Has(gate.requirement);
                if(gate.gateId=="G6"&&combatLocked)shouldLock=true;
                gate.SetLocked(shouldLock,force);
            }
        }
        [ContextMenu("Debug/Open all exploration gates")]
        public void DebugOpenAll(){previewAllDoorsOpen=true;RefreshDoors(true);}
    }
}
