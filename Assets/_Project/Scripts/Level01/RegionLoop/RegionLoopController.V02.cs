using System;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    public sealed partial class RegionLoopController
    {
        [Header("V02: reuse native missions, environment and UI")]
        public bool v02Enabled;
        public string v02InstallationId;
        public PlanetStormController storm;
        public ToxicFogRouteController fog;
        public ToxicFogAreaController fogArea;
        public ExtractionController extraction;
        public RegionTaskSite[] optionalSites = Array.Empty<RegionTaskSite>();
        public Transform[] stormCandidates = Array.Empty<Transform>();
        public Transform[] exitApproaches = Array.Empty<Transform>();
        public string PressureStatus { get; private set; }
        public string StormDiagnostic { get; private set; }
        public RegionTaskSite ActiveOptional { get; private set; }
        readonly HashSet<string> runningRegions = new HashSet<string>();
        float nextStormCheck;
        float nextStormDiagnostic;
        string lastStormDiagnostic;

        IEnumerable<RegionEnemyTicket> SpawnOrder(RegionRunState state) => v02Enabled
            ? state.enemies.OrderBy(t => t.role == RegionEnemyRole.Mission ? 0 : t.role == RegionEnemyRole.Resident ? 1 : 2)
            : state.enemies;
        bool ShouldRunV02(RegionRunState state) => !state.retired && runningRegions.Contains(state.config.regionId);
        public int RemainingAccessGuards(RegionRunState state) => state.enemies.Count(t => t.role == RegionEnemyRole.Resident && t.keyGuard && !t.dead);
        bool CanActivateV02(string region)
        {
            var state = State(region);
            return !ShuttingDown && state != null && state.initialized && !state.retired && HasMilestone(state.config.prerequisite) &&
                state.objective != RegionObjectiveState.Completed && (RemainingAccessGuards(state) == 0 || !string.IsNullOrEmpty(state.diagnostic));
        }
        bool TryActivateV02(string region)
        {
            if (!CanActivateV02(region) || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
            var state = State(region);
            if (RemainingAccessGuards(state) > 0)
            {
                state.diagnostic = null;
                foreach (var ticket in state.enemies) { ticket.failedSpawns = 0; ticket.due = Time.time; }
                return true;
            }
            if (!rewards.GrantOnce("mission/" + state.config.missionId, state.config.missionExperience, 0)) return false;
            state.objective = RegionObjectiveState.Completed; state.missionRewarded = true; state.deviceProgress = 1;
            state.lockedDevicePosition = Terminal(region).transform.position;
            milestones.Add(state.config.missionId);
            if (state.config.v02.mechanismSource == MechanismRewardSource.MainCompletion) GrantMechanism(state);
            // At most one bounded extension. It never freezes a late region indefinitely.
            state.fogBuffer = state.config.v02.oneTimeExitBuffer;
            state.quietUntil = Time.time + state.config.v02.searchSeconds;
            missionView.SetRegionObjectiveView(state.config.missionId, MissionState.Completed, state.lockedDevicePosition, 1, 0);
            RefreshDoors(); PresentV02();
            return true;
        }
        void GrantMechanism(RegionRunState state)
        {
            if (state.config.moduleSlot > 0) rewards.GrantOnce("mechanism/" + state.config.regionId, 0, state.config.moduleSlot);
        }
        void TickV02()
        {
            if (Time.time >= nextObserve)
            {
                nextObserve = Time.time + .25f;
                string resolved = world.ResolveRegion(world.Local(world.PlayerFeet));
                if (!string.IsNullOrEmpty(resolved)) CurrentRegion = resolved;
                MaintainRegionsV02();
                foreach (var site in optionalSites) if (site != null) site.RefreshPrompt();
                PresentV02();
            }
            AdvanceFogV02();
            CheckStormV02();
            TickSpawns();
        }
        void MaintainRegionsV02()
        {
            runningRegions.Clear();
            var current = State(CurrentRegion);
            if (current != null) runningRegions.Add(CurrentRegion);
            if (ActiveOptional != null) runningRegions.Add(ActiveOptional.definition.regionId);
            if (current != null)
            {
                int index = states.IndexOf(current);
                if (index + 1 < states.Count && exitApproaches.Length > index && exitApproaches[index] != null &&
                    Vector2.Distance(world.PlayerFeet, exitApproaches[index].position) <= current.config.v02.preloadDistance)
                    runningRegions.Add(states[index + 1].config.regionId);
            }
            foreach (var state in states)
            {
                if (state.firstEntered >= 0 && fog.DistanceTravelled >= state.config.v02.fogEndDistance - .01f) state.retired = true;
                if (!ShouldRunV02(state))
                {
                    foreach (var ticket in state.enemies) if (ticket.lease != null && !ticket.lease.Health.IsDead) ticket.lease.Release();
                    continue;
                }
                if (!state.initialized)
                {
                    state.initialized = true;
                    state.AddGroups(state.config.residents, RegionEnemyRole.Resident, state.config.encounterId, Time.time);
                    state.environmentDue = Time.time + 8;
                }
                if (state.config.regionId == CurrentRegion && state.firstEntered < 0)
                {
                    state.firstEntered = Time.time;
                    if (state.config.v02.mechanismSource == MechanismRewardSource.FirstEntry) GrantMechanism(state);
                }
                PreloadResidents(state);
                if (!state.encounterRewarded && state.Remaining(RegionEnemyRole.Resident) == 0)
                    state.encounterRewarded = rewards.GrantOnce("encounter/" + state.config.encounterId, state.config.encounterExperience, 0);
                state.enemies.RemoveAll(t => t.role == RegionEnemyRole.Patrol && (t.dead || t.failedSpawns >= state.config.spawnRetryLimit));
                if (state.config.regionId == CurrentRegion) ScheduleEnvironment(state);
            }
        }
        void PreloadResidents(RegionRunState state)
        {
            if (pool == null || !pool.IsInitialized) return;
            var homes = anchors.Where(a => a != null && a.residentSlot && a.regionId == state.config.regionId).ToArray();
            if (homes.Length == 0) return;
            int index = 0;
            foreach (var ticket in state.enemies)
            {
                if (ticket.role != RegionEnemyRole.Resident) continue;
                var home = homes[index++ % homes.Length];
                if (ticket.dead || ticket.lease != null || ticket.due > Time.time || ticket.failedSpawns >= state.config.spawnRetryLimit) continue;
                if (pool.ActiveCount >= globalAliveLimit - state.config.v02.missionCapacityReserve || CountAlive(state) >= state.config.aliveLimit - state.config.v02.missionCapacityReserve) return;
                var profile = state.config.Profile(ticket.type);
                if (Vector2.Distance(home.transform.position, world.PlayerFeet) < state.config.minimumPlayerDistance) continue;
                if (!RegionSpawnSpace.Valid(world, state.config.regionId, home.transform.position, Terminal(state.config.regionId).transform.position, profile.clearance, true, out string reason))
                { SpawnFailed(state, ticket, reason); continue; }
                var enemy = pool.GetEnemy(ticket.type, home.transform.position, Quaternion.identity);
                if (enemy == null) { ticket.due = Time.time + 1; continue; }
                var lease = enemy.GetComponent<RegionEnemyLease>() ?? enemy.gameObject.AddComponent<RegionEnemyLease>();
                lease.Bind(this, state, ticket); ticket.failedSpawns = 0;
            }
        }
        void ScheduleEnvironment(RegionRunState state)
        {
            var settings = state.config.v02;
            bool low = CountAlive(state) <= settings.pressureLowWatermark && state.Remaining(RegionEnemyRole.Patrol) == 0;
            if (low && state.lowPressureSince < 0)
            {
                state.lowPressureSince = Time.time;
                state.environmentDue = Mathf.Max(state.environmentDue, Time.time + settings.searchSeconds);
            }
            if (!low) state.lowPressureSince = -1;
            if (ActiveOptional != null) { PressureStatus = "TASK - waves paused"; return; }
            if (storm.CurrentPhase != StormPhase.Calm) { PressureStatus = "STORM - waves paused"; return; }
            if (Time.time < state.quietUntil || Time.time < state.environmentDue) { PressureStatus = "SEARCH WINDOW"; return; }
            if (state.Remaining(RegionEnemyRole.Patrol) > 0 || (state.environmentWave > 0 && !low)) { PressureStatus = "FINISH CURRENT WAVE"; return; }
            var choices = settings.environmentSquads;
            if (choices.Length == 0) return;
            var wave = choices[state.environmentWave % choices.Length];
            int amount = wave.groups.Sum(g => g.count);
            if (CountAlive(state) + amount > state.config.aliveLimit - settings.missionCapacityReserve || pool.ActiveCount + amount > globalAliveLimit - settings.missionCapacityReserve)
            { state.environmentDue = Time.time + settings.environmentInterval; PressureStatus = "CAPACITY - next wave deferred"; return; }
            state.environmentWave++; state.lowPressureSince = -1;
            state.AddGroups(wave.groups, RegionEnemyRole.Patrol, "ambient/" + state.environmentWave, Time.time);
            state.environmentDue = Time.time + settings.environmentInterval;
            PressureStatus = "ENVIRONMENT WAVE " + state.environmentWave;
        }
        bool MaySpawnV02(RegionRunState state, RegionEnemyTicket ticket)
        {
            if (ticket.role == RegionEnemyRole.Resident) return false; // Stable placed garrison, never an ingress wave.
            var profile = state.config.Profile(ticket.type);
            if (profile.simultaneousLimit > 0 && state.enemies.Count(t => t.type == ticket.type && t.lease != null && !t.dead) >= profile.simultaneousLimit) return false;
            if (ticket.role == RegionEnemyRole.Patrol && (ActiveOptional != null || storm.CurrentPhase != StormPhase.Calm ||
                pool.ActiveCount >= globalAliveLimit - state.config.v02.missionCapacityReserve ||
                CountAlive(state) >= state.config.aliveLimit - state.config.v02.missionCapacityReserve)) return false;
            return true;
        }
        bool IsIngressClearV02(Vector3 position)
        {
            if (fogArea.IsPositionInsideFog(position)) return false;
            foreach (var terminal in terminals) if (Vector2.Distance(position, terminal.transform.position) < 3) return false;
            foreach (var site in optionalSites) if (site != null && Vector2.Distance(position, site.transform.position) < 3) return false;
            return true;
        }
        public bool CanSenseV02(RegionRunState state, Vector3 from)
        {
            if (!v02Enabled) return true;
            return CurrentRegion == state.config.regionId && Physics2D.Linecast(from, world.PlayerFeet, 1 << LayerMask.NameToLayer("Environment")).collider == null;
        }
        public float FogBeginsAt(RegionRunState state) => state.firstEntered < 0 ? float.PositiveInfinity :
            state.firstEntered + state.config.v02.explorationSeconds + state.config.v02.fogWarningSeconds + state.fogBuffer;
        public bool CanAcceptOptional(RegionTaskSite site)
        {
            if (!v02Enabled || ShuttingDown || GameManager.Instance == null || !GameManager.Instance.IsPlaying || ActiveOptional != null || site.Succeeded || site.Attempts >= site.Config.maxAttempts || storm.CurrentPhase != StormPhase.Calm) return false;
            var state = State(site.definition.regionId);
            if (state == null || state.firstEntered < 0 || state.retired || CurrentRegion != state.config.regionId ||
                Time.time > state.firstEntered + site.Config.acceptSeconds || !HasMilestone(state.config.prerequisite)) return false;
            return FogBeginsAt(state) - Time.time >= site.Config.deadlineSeconds + site.Config.claimAndExitSeconds;
        }
        public void BeginOptional(RegionTaskSite site)
        {
            if (!CanAcceptOptional(site)) return;
            var state = State(site.definition.regionId);
            if (!missionView.BeginRegionTask(site.TaskId, state.config.regionId, site.Config.deadlineSeconds, site.Config.experience, rewards,
                () => state.Remaining(RegionEnemyRole.Mission) == 0 && (!site.Config.highRisk || site.rewardContainer != null),
                (success, reason) => FinishOptional(site, success, reason))) return;
            ActiveOptional = site;
            if (pendingTicket != null && pendingTicket.role == RegionEnemyRole.Patrol) CancelWarning();
            state.enemies.RemoveAll(t => t.role == RegionEnemyRole.Patrol && t.lease == null);
            state.attempt++;
            foreach (var wave in site.Config.waves) state.AddGroups(wave.groups, RegionEnemyRole.Mission, site.TaskId + "/" + wave.id, Time.time + wave.delay);
            site.Accepted(); PresentV02();
        }
        void FinishOptional(RegionTaskSite site, bool success, string reason)
        {
            var state = State(site.definition.regionId);
            ClearMissionEnemies(state); site.Finished(success, reason);
            if (success && site.Config.highRisk && !extraction.UnlockRegionHighRiskCache(site.rewardContainer))
                Debug.LogError("[RegionLoop] Validated high-risk reward container could not be unlocked", site);
            state.quietUntil = Time.time + state.config.v02.searchSeconds;
            state.lowPressureSince = -1;
            ActiveOptional = null; PresentV02();
        }
        void AdvanceFogV02()
        {
            string notice = null;
            foreach (var state in states)
            {
                if (state.firstEntered < 0) break;
                var settings = state.config.v02;
                float begins = FogBeginsAt(state);
                if (Time.time < begins)
                {
                    if (Time.time >= begins - settings.fogWarningSeconds) notice = state.config.regionId + " invasion in " + Mathf.CeilToInt(begins - Time.time) + "s - activate exit / leave";
                    break;
                }
                float speed = (settings.fogEndDistance - settings.fogStartDistance) / settings.fogAdvanceSeconds;
                fog.AdvanceRegionFront(settings.fogEndDistance, speed, Time.deltaTime);
                notice = state.config.regionId + " is being reclaimed - use the forward exit";
                if (fog.DistanceTravelled < settings.fogEndDistance - .01f) break;
            }
            fog.SetRegionNotice(notice);
        }
        public bool ValidateStormCandidate(RegionRunState state, Vector3 center, out float travelSeconds, out string reason)
        {
            travelSeconds = 0; reason = null;
            float radius = state.config.v02.safeRadius;
            if (!RegionSpawnSpace.Valid(world, state.config.regionId, center, center, radius + .35f, true, out reason))
            { reason = "full safety disk intersects solid collision: " + reason; return false; }
            for (int ring = 0; ring <= 3; ring++)
                for (int i = 0; i < (ring == 0 ? 1 : 24); i++)
                {
                    float angle = i * Mathf.PI * 2 / 24;
                    Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * ring / 3;
                    if (fogArea.IsPositionInsideFog(point) || !RegionSpawnSpace.Valid(world, state.config.regionId, point, center, .35f, true, out reason))
                    { reason = "safe area sample blocked / reclaimed: " + reason; return false; }
                }
            var path = ABPath.Construct(world.PlayerFeet, center, null);
            AstarPath.StartPath(path); path.BlockUntilCalculated();
            if (path.error || path.vectorPath == null) { reason = "no current-player route"; return false; }
            float distance = 0;
            for (int i = 1; i < path.vectorPath.Count; i++)
            {
                if (fogArea.IsPositionInsideFog(path.vectorPath[i])) { reason = "travel route intersects fog"; return false; }
                distance += Vector3.Distance(path.vectorPath[i - 1], path.vectorPath[i]);
            }
            float speed = Mathf.Max(1, world.player.GetComponent<PlayerMovement>().MoveSpeed);
            travelSeconds = distance / speed + 5;
            if (travelSeconds > 30) { reason = "travel warning would exceed 30 seconds"; return false; }
            return true;
        }
        void CheckStormV02()
        {
            if (Time.time < nextStormCheck || storm.CurrentPhase != StormPhase.Calm || ActiveOptional != null) return;
            nextStormCheck = Time.time + 3;
            var state = State(CurrentRegion);
            if (state == null || state.firstEntered < 0 || state.stormStarted || state.retired || Time.time < state.firstEntered + state.config.v02.stormAfterEntry) return;
            var settings = state.config.v02;
            if (FogBeginsAt(state) - Time.time < 30 + settings.stormActiveSeconds + settings.stormRecoverySeconds + 10)
            { StormDiagnostic = "Deferred: insufficient pre-fog transfer window in " + CurrentRegion; return; }
            foreach (var candidate in stormCandidates)
            {
                if (candidate == null || world.ResolveRegion(world.Local(candidate.position)) != CurrentRegion) continue;
                if (!ValidateStormCandidate(state, candidate.position, out float travel, out string reason)) { StormDiagnostic = reason; continue; }
                if (!storm.TryBeginRegionStorm(candidate.position, settings.safeRadius, Mathf.Max(settings.stormWarningMinimum, travel), settings.stormActiveSeconds, settings.stormRecoverySeconds)) return;
                state.stormStarted = true; StormDiagnostic = null;
                if (pendingTicket != null && pendingTicket.role == RegionEnemyRole.Patrol) CancelWarning();
                state.enemies.RemoveAll(t => t.role == RegionEnemyRole.Patrol && t.lease == null);
                state.quietUntil = Time.time + Mathf.Max(settings.stormWarningMinimum, travel) + settings.stormActiveSeconds + settings.stormRecoverySeconds + settings.searchSeconds;
                return;
            }
            if (StormDiagnostic != lastStormDiagnostic || Time.time >= nextStormDiagnostic)
            {
                lastStormDiagnostic = StormDiagnostic; nextStormDiagnostic = Time.time + 30;
                Debug.LogWarning("[RegionLoop] Storm not published; will retry: " + StormDiagnostic, this);
            }
        }
        void PresentV02()
        {
            if (!v02Enabled || missionView == null) return;
            if (ActiveOptional != null)
            {
                var site = ActiveOptional; var state = State(site.definition.regionId);
                missionView.PresentRegionObjective(site.Config.highRisk ? "HIGH RISK // " + state.config.regionId : "DEFENSE // " + state.config.regionId,
                    "Hold " + Mathf.FloorToInt(site.beacon.RegionDefendedSeconds) + "/" + site.Config.defendSeconds + "s | guards " + state.Remaining(RegionEnemyRole.Mission) + " | [E] stop", "DEADLINE",
                    Mathf.RoundToInt(missionView.RegionTaskProgress * 100) + "%", "+" + site.Config.experience + " EXP" + (site.Config.highRisk ? " + RARE CACHE" : ""),
                    "Finite mixed reinforcements", site.transform.position, missionView.RegionTaskProgress, missionView.RegionTaskRemaining);
                return;
            }
            var current = State(CurrentRegion);
            if (current == null && HasMilestone("Power"))
            { missionView.PresentRegionCompletion(world.PlayerFeet); return; }
            if (current == null) current = states.Last();
            bool complete = HasMilestone(current.config.missionId);
            float remaining = Mathf.Max(0, FogBeginsAt(current) - Time.time);
            string timerLabel = "FOG IN";
            int guards = RemainingAccessGuards(current);
            Vector3 target = complete ? exitApproaches[states.IndexOf(current)].position : Terminal(current.config.regionId).transform.position;
            var nearby = optionalSites.FirstOrDefault(s => s != null && !s.Succeeded && s.definition == current.config && Vector2.Distance(s.transform.position, world.PlayerFeet) < 9);
            string risk = current.retired ? "FOG EXPOSURE - LEAVE" : PressureStatus;
            string reward = "+" + current.config.missionExperience + " EXP" + (current.config.moduleSlot > 0 ? " + CORE CHOICE" : "");
            if (nearby != null)
            {
                bool available = CanAcceptOptional(nearby);
                risk = "Optional window closed";
                if (available)
                {
                    // Existing timer row shows the effective acceptance window; hold duration and
                    // post-acceptance limit are separately labelled in the existing risk row.
                    timerLabel = "ACCEPT";
                    remaining = Mathf.Max(0, Mathf.Min(current.firstEntered + nearby.Config.acceptSeconds,
                        FogBeginsAt(current) - nearby.Config.deadlineSeconds - nearby.Config.claimAndExitSeconds) - Time.time);
                    risk = (nearby.Config.highRisk ? "Mixed | " : "") + "hold " + nearby.Config.defendSeconds + "s | limit " + nearby.Config.deadlineSeconds + "s";
                    reward = (nearby.Config.highRisk ? "RISK: " : "OPTIONAL: ") + "+" + nearby.Config.experience + " EXP" + (nearby.Config.highRisk ? " + RARE CACHE" : "");
                }
            }
            missionView.PresentRegionObjective("MAIN // " + CurrentRegion, complete ? current.config.exitGateId + " OPEN - search / advance" :
                guards > 0 ? "Clear access guards: " + guards : "Activate " + current.config.missionId + " device [E]", timerLabel,
                complete || guards == 0 ? "100%" : "0%", reward,
                risk, target, complete || guards == 0 ? 1 : 0, float.IsInfinity(remaining) ? current.config.v02.explorationSeconds : remaining);
        }
        void StopV02()
        {
            if (missionView != null) missionView.CancelRegionTask("Run ended");
            if (storm != null) storm.EndRegionStorm();
        }
    }
}
