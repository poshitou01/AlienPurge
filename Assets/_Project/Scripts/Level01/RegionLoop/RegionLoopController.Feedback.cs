using System.Linq;
using UnityEngine;
using Pathfinding;

namespace AlienPurge.Level01.RegionLoop
{
    public sealed partial class RegionLoopController
    {
        float nextGuideRefresh;
        string guideRegion;
        Vector3 guidePosition;
        Vector3 ForwardGuide(RegionRunState state, int index)
        {
            if (index + 1 >= states.Count) return Terminal(state.config.regionId).transform.position;
            if (Time.time < nextGuideRefresh && guideRegion == state.config.regionId) return guidePosition;
            nextGuideRefresh = Time.time + .75f; guideRegion = state.config.regionId;
            var destination = IntegratedNeedsBoss(state) ? world.World(new Vector2(0, -8)) : Terminal(states[index + 1].config.regionId).transform.position;
            var path = ABPath.Construct(world.PlayerFeet, destination, null);
            AstarPath.StartPath(path); path.BlockUntilCalculated();
            guidePosition = exitApproaches[index].position;
            if (!path.error && path.vectorPath != null)
            {
                guidePosition = destination;
                foreach (var point in path.vectorPath)
                    if (Vector2.Distance(point, world.PlayerFeet) > 5) { guidePosition = point; break; }
            }
            return guidePosition;
        }
        public static string RegionName(string id)
        {
            switch (id)
            {
                case "ColonyEntry": return "殖民地入口";
                case "CargoYard": return "货运场";
                case "PowerMaintenance": return "动力维护区";
                case "QuarantineCommons": return "隔离生活区";
                case "InfectedResearch": return "感染研究区";
                case "Extraction": return "撤离区";
                case "HiveTyrant": return "虫巢竞技场";
                default: return "连接通道";
            }
        }
        public static string DeviceName(string region)
        {
            switch (region)
            {
                case "ColonyEntry": return "入口控制台";
                case "CargoYard": return "货运通信台";
                case "PowerMaintenance": return "动力终端";
                case "QuarantineCommons": return "隔离控制台";
                case "InfectedResearch": return "研究终端";
                case "Extraction": return "撤离信标";
                default: return "区域终端";
            }
        }
        public RegionTaskSite TerminalTask(string region)
        {
            if (ActiveOptional != null) return ActiveOptional.definition.regionId == region ? ActiveOptional : null;
            return optionalSites.Where(s => s != null && s.definition.regionId == region)
                .OrderByDescending(s => s.Config.highRisk).FirstOrDefault(CanAcceptOptional);
        }
        bool TryDynamicIngress(RegionRunState state, RegionEnemyProfile profile, out Vector3 position)
        {
            var camera = Camera.main;
            float near = camera != null && camera.orthographic ? camera.orthographicSize + 3 : 14;
            float far = camera != null && camera.orthographic ? camera.orthographicSize * Mathf.Sqrt(1 + camera.aspect * camera.aspect) + 7 : 25;
            for (int n = 0; n < 40; n++)
            {
                float a = Random.value * Mathf.PI * 2;
                position = world.PlayerFeet + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(near, far);
                if (Vector2.Distance(position, world.PlayerFeet) < state.config.minimumPlayerDistance || !IsIngressClearV02(position)) continue;
                if (!ValidV02Ingress(state, profile, position, out _)) continue;
                var candidate = position;
                if (state.enemies.Any(t => t.lease != null && Vector2.Distance(candidate, t.lease.transform.position) < profile.clearance * 2)) continue;
                return true;
            }
            if (state.config.v02.extractionRegion)
            {
                var road = world.Data.roads.First(r => r.id == "G7_ApproachToExtraction");
                for (int i = road.centerline.Length - 1; i > 0; i--)
                {
                    var a = world.World(road.centerline[i].V); var b = world.World(road.centerline[i-1].V);
                    int steps = Mathf.CeilToInt(Vector3.Distance(a,b) / 2);
                    for (int n = 0; n <= steps; n++)
                    {
                        position = Vector3.Lerp(a,b,n/(float)steps);
                        if (!IsIngressClearV02(position) || Vector2.Distance(position,world.PlayerFeet)<state.config.minimumPlayerDistance || !ValidV02Ingress(state,profile,position,out _)) continue;
                        var candidate = position;
                        if(state.enemies.Any(t=>t.lease!=null&&Vector2.Distance(candidate,t.lease.transform.position)<profile.clearance*2))continue;
                        return true;
                    }
                }
            }
            position = default; return false;
        }
        bool ValidV02Ingress(RegionRunState state, RegionEnemyProfile profile, Vector3 position, out string reason)
        {
            string region = world.ResolveRegion(world.Local(position));
            if (region == state.config.regionId)
                return RegionSpawnSpace.Valid(world, region, position, Terminal(state.config.regionId).transform.position, profile.clearance, true, out reason);
            reason = "outside assigned region / connected extraction ingress";
            if (!state.config.v02.extractionRegion || region != null || !HasMilestone("Research")) return false;
            var road = world.Data.roads.First(r=>r.id=="G7_ApproachToExtraction");
            var local = world.Local(position);
            for (int i=1;i<road.centerline.Length;i++)
            {
                var a=road.centerline[i-1].V;var b=road.centerline[i].V;var delta=b-a;
                var closest=a+delta*Mathf.Clamp01(Vector2.Dot(local-a,delta)/delta.sqrMagnitude);
                if(Vector2.Distance(closest,local)>road.width*.5f-profile.clearance)continue;
                return RegionSpawnSpace.Valid(world,null,position,Terminal(state.config.regionId).transform.position,profile.clearance,true,out reason);
            }
            return false;
        }
        void PresentV02()
        {
            if (!v02Enabled || missionView == null) return;
            if (ActiveOptional != null)
            {
                var site = ActiveOptional; var state = State(site.definition.regionId);
                float distance = Vector2.Distance(world.PlayerFeet, site.transform.position);
                missionView.PresentRegionObjective((site.Config.highRisk ? "高风险驻守｜" : "可选驻守｜") + RegionName(state.config.regionId),
                    "驻守 " + Mathf.FloorToInt(site.beacon.RegionDefendedSeconds) + "/" + site.Config.defendSeconds + "秒 · 敌军 " + state.Remaining(RegionEnemyRole.Mission),
                    "限时", Mathf.RoundToInt(missionView.RegionTaskProgress * 100) + "%",
                    "+" + site.Config.experience + " 经验" + (site.Config.highRisk ? "＋稀有物资箱" : ""),
                    distance > site.Config.radius ? "已离点：进度暂停，返回终端" : site.Contested ? "敌军占领终端：清除后继续" :
                    "守点半径" + site.Config.radius + "米 · 按E放弃 · 毒雾" + Mathf.CeilToInt(Mathf.Max(0, FogBeginsAt(state) - Time.time)) + "秒",
                    storm.CurrentPhase != StormPhase.Calm && safeZone.HasActiveZone && Vector2.Distance(world.PlayerFeet, safeZone.CurrentCenter) > safeZone.Radius ? (Vector3)safeZone.CurrentCenter : site.transform.position,
                    missionView.RegionTaskProgress, missionView.RegionTaskRemaining);
                return;
            }
            var current = State(CurrentRegion);
            if (current == null)
            {
                if (CurrentRegion == "HiveTyrant")
                {
                    var arena = world.arenaBridge;
                    bool won = arena != null && arena.encounterFinished;
                    missionView.PresentRegionObjective("虫巢竞技场", won ? "首领已击败，前往撤离区" : "击败虫巢暴君，战斗结束后离开",
                        "", "", "撤离可保留本局物资", "沿通道前往地图东南方", world.World(new Vector2(won ? 47 : 0, won ? -57 : 0)), 0, 0);
                    return;
                }
                current = states.LastOrDefault(s => HasMilestone(s.config.prerequisite)) ?? states[0];
            }
            bool complete = HasMilestone(current.config.missionId);
            int index = states.IndexOf(current), guards = RemainingAccessGuards(current);
            int totalGuards = current.config.residents.Where(g => g.keyGuard).Sum(g => g.count);
            float progress = complete ? 1 : totalGuards == 0 ? 1 : 1 - guards / (float)totalGuards;
            var target = complete ? ForwardGuide(current,index) : Terminal(current.config.regionId).transform.position;
            string objective = guards > 0 ? "清除开门守卫：剩余 " + guards + " 名" : "靠近" + DeviceName(current.config.regionId) + "，按E";
            if (complete) objective = index + 1 < states.Count ? "通道已开启 → " + RegionName(states[index + 1].config.regionId) : "守住撤离信标，等待接应";
            if (complete && IntegratedNeedsBoss(current)) objective = "研究已完成 → 进入虫巢竞技场，击败首领";
            float remaining = Mathf.Max(0, FogBeginsAt(current) - Time.time);
            string timer = current.config.v02.extractionRegion ? "" : "毒雾侵入";
            string reward = complete ? "沿箭头前进，附近仍有敌人" : "+" + current.config.missionExperience + " 经验" + (current.config.moduleSlot > 0 ? "＋核心选择" : "");
            string risk = current.retired ? "毒雾已覆盖：立即向前撤离" : PressureStatus;
            var task = TerminalTask(current.config.regionId);
            if (complete && task != null && Vector2.Distance(world.PlayerFeet, task.transform.position) < 5)
            {
                reward = "按E可选：" + (task.Config.highRisk ? "风暴驻守＋稀有箱" : "驻守奖励＋" + task.Config.experience + "经验");
                risk = "守点" + task.Config.defendSeconds + "秒 / 限时" + task.Config.deadlineSeconds + "秒；也可直接前进";
            }
            if (storm.CurrentPhase == StormPhase.Warning || storm.CurrentPhase == StormPhase.Active)
            {
                target = safeZone.CurrentCenter;
                risk = storm.CurrentPhase == StormPhase.Warning ? "风暴将至：沿箭头进入安全区" : "风暴中：安全圈外受伤，圈内仍需战斗";
                timer = storm.CurrentPhase == StormPhase.Warning ? "风暴来临" : "风暴结束";
                remaining = storm.PhaseTimeRemaining;
            }
            if (current.config.v02.extractionRegion && extraction.IsDefending)
            {
                objective = extraction.IsPlayerInsideZone ? "守住信标，等待撤离" : "返回信标：撤离进度已暂停";
                progress = extraction.DefenseProgress01; remaining = extraction.DefenseTimeRemaining; timer = "撤离倒计时";
                risk = "敌军持续来袭，离开范围会暂停"; reward = "成功撤离可保留本局物资";
            }
            missionView.PresentRegionObjective("主线｜" + RegionName(current.config.regionId), objective, timer,
                Mathf.RoundToInt(progress * 100) + "%", reward, risk, target, progress, float.IsInfinity(remaining) ? 0 : remaining);
        }
    }
}
