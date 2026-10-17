using System;
using System.Collections.Generic;
using AlienPurge.Level01ConnectedV01;
using TMPro;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    [DefaultExecutionOrder(-20000), DisallowMultipleComponent]
    public sealed class RegionLoopController : MonoBehaviour
    {
        public const string OwnerId = "AlienPurge.Level01.RegionLoop.V01";
        [HideInInspector] public string installationId = OwnerId, sourceSceneGuid, sourceSceneHash;
        [HideInInspector] public int installedVersion = 1;
        [Header("Explicit scene-owned mode / never inferred from scene name")]
        public bool regionModeEnabled = true;
        public L01World world;
        public EnemyPool pool;
        public MissionManager missionView;
        public RegionRewardQueue rewards;
        public RegionDefinition[] definitions;
        public RegionTerminal[] terminals;
        public RegionAnchor[] anchors;
        public TMP_Text hud;
        [Min(1)] public int globalAliveLimit = 24;
        [Min(0)] public int initialExperience;
        public Behaviour[] dormantBehaviours;
        public GameObject[] dormantObjects;
        public SafeZoneController safeZone;
        public string RunId { get; private set; }
        public string CurrentRegion { get; private set; } = "ColonyEntry";
        public bool ShuttingDown { get; private set; }
        public IReadOnlyList<RegionRunState> Regions => states;
        readonly List<RegionRunState> states = new List<RegionRunState>();
        readonly HashSet<string> milestones = new HashSet<string>();
        readonly List<PooledEnemy> deferredReturns = new List<PooledEnemy>();
        float nextObserve, nextSpawn;
        RegionRunState activeMission;
        RegionEnemyTicket pendingTicket;
        RegionRunState pendingRegion;
        RegionAnchor pendingAnchor;
        Vector3 pendingPosition;
        float warningUntil;
        Canvas hudCanvas;

        void Awake()
        {
            if (!regionModeEnabled) { enabled = false; return; }
            RunId = Guid.NewGuid().ToString("N"); ShuttingDown = false;
            hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            foreach (var definition in definitions) states.Add(new RegionRunState(definition));
            // All automatic scheduler flags are serialized by the installer, before ANY Awake.
            // This extra scene-only list hides hazard visuals and non-core legacy interaction triggers.
            foreach (var item in dormantBehaviours) if (item != null) item.enabled = false;
            foreach (var item in dormantObjects) if (item != null) item.SetActive(false);
            if (safeZone != null) safeZone.ClearSafeZone();
        }
        void Start()
        {
            if (initialExperience > 0) rewards.GrantOnce("initial", initialExperience, 0);
            RefreshDoors();
        }
        public RegionRunState State(string region)
        {
            foreach (var state in states) if (state.config.regionId == region) return state;
            return null;
        }
        public bool HasMilestone(string id) => string.IsNullOrEmpty(id) || milestones.Contains(id);
        public RegionTerminal Terminal(string region)
        {
            foreach (var terminal in terminals) if (terminal != null && terminal.regionId == region) return terminal;
            return null;
        }
        public bool CanActivate(string region)
        {
            var state = State(region);
            if (ShuttingDown || state == null || !HasMilestone(state.config.prerequisite) || activeMission != null ||
                !state.initialized || state.objective == RegionObjectiveState.Completed) return false;
            if (!string.IsNullOrEmpty(state.diagnostic)) return true;
            return state.config.missionId != "Arrival" || state.Remaining(RegionEnemyRole.Resident) == 0;
        }
        public bool TryActivate(string region)
        {
            if (!CanActivate(region) || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
            var state = State(region);
            if (!string.IsNullOrEmpty(state.diagnostic))
            {
                state.diagnostic = null;
                foreach (var ticket in state.enemies) { ticket.failedSpawns = 0; ticket.due = Time.time; }
                if (state.config.missionId == "Arrival" && state.Remaining(RegionEnemyRole.Resident) > 0) return true;
            }
            if (state.config.missionId == "Arrival") { Complete(state); return true; }
            // A retry starts a new attempt; old mission ownership is removed before any new wave.
            ClearMissionEnemies(state);
            state.attempt++; state.missionElapsed = 0; state.deviceProgress = 0;
            state.integrity = state.config.deviceIntegrity;
            state.lockedDevicePosition = Terminal(region).transform.position;
            state.objective = RegionObjectiveState.Active; activeMission = state;
            foreach (var wave in state.config.waves)
                state.AddGroups(wave.groups, RegionEnemyRole.Mission, wave.id, Time.time + wave.delay);
            Publish(state);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBeaconActivated();
            return true;
        }
        void Update()
        {
            if (!regionModeEnabled || ShuttingDown) return;
            FlushPoolReturns();
            if (GameManager.Instance == null) return;
            if (!GameManager.Instance.IsPlaying) { StopRun(); return; }
            if (Time.timeScale <= 0) return;
            if (Time.time >= nextObserve)
            {
                nextObserve = Time.time + .25f;
                string resolved = world.ResolveRegion(world.Local(world.PlayerFeet));
                if (!string.IsNullOrEmpty(resolved)) CurrentRegion = resolved;
                UpdateRegions(); RefreshHud();
            }
            if (activeMission != null) TickMission(activeMission);
            TickSpawns();
        }
        void LateUpdate()
        {
            // The scene's choice cards occupy the same screen space as the region HUD.
            // Keep modal content unobstructed, including frames where scaled time is paused.
            if (hudCanvas != null) hudCanvas.enabled = !ShuttingDown &&
                !UpgradeManager.IsChoosingUpgrade && !WeaponModuleSelectionManager.IsChoosingModule &&
                !PauseMenuController.IsPaused && !BackpackPanelController.IsOpen && !LootSearchPanelController.IsOpen;
        }
        void UpdateRegions()
        {
            foreach (var state in states)
            {
                bool nearby = state.config.regionId == CurrentRegion || state == activeMission;
                if (nearby && !state.initialized && HasMilestone(state.config.prerequisite))
                {
                    state.initialized = true;
                    state.AddGroups(state.config.residents, RegionEnemyRole.Resident, state.config.encounterId, Time.time);
                    state.nextPatrol = Time.time + state.config.patrolInterval;
                }
                if (!nearby)
                {
                    foreach (var ticket in state.enemies) if (ticket.lease != null && !ticket.lease.Health.IsDead) ticket.lease.Release();
                    continue;
                }
                if (state.initialized && !state.encounterRewarded && state.Remaining(RegionEnemyRole.Resident) == 0)
                    state.encounterRewarded = rewards.GrantOnce("encounter/" + state.config.encounterId, state.config.encounterExperience, 0);
                if (state.initialized && state != activeMission && state.config.regionId == CurrentRegion &&
                    state.patrolSpent < state.config.patrolBudget && Time.time >= state.nextPatrol && Time.time >= state.quietUntil &&
                    CountAlive(state) < state.config.aliveLimit)
                {
                    state.patrolSpent++; state.nextPatrol = Time.time + state.config.patrolInterval;
                    state.AddGroups(new[] { new RegionSpawnGroup { type = state.config.patrolType, count = 1 } },
                        RegionEnemyRole.Patrol, "patrol/" + state.patrolSpent, Time.time);
                }
            }
        }
        void TickMission(RegionRunState state)
        {
            state.missionElapsed += Time.deltaTime;
            // Device location is captured at activation and does not follow the player across regions.
            bool inRange = Vector2.Distance(world.PlayerFeet, state.lockedDevicePosition) <= state.config.deviceRadius;
            int attackers = 0;
            foreach (var ticket in state.enemies)
                if (ticket.lease != null && !ticket.dead && !ticket.lease.Health.IsDead &&
                    Vector2.Distance(ticket.lease.transform.position, state.lockedDevicePosition) < state.config.contestRadius) attackers++;
            if (attackers > 0) state.integrity -= attackers * state.config.contestedDamagePerSecond * Time.deltaTime;
            else if (inRange) state.deviceProgress = Mathf.Min(1, state.deviceProgress + Time.deltaTime / Mathf.Max(.1f, state.config.deviceSeconds));
            if (state.integrity <= 0 || state.missionElapsed >= state.config.missionDeadline)
            { Fail(state, state.integrity <= 0 ? "Device disabled: interact to retry" : "Objective timed out: interact to retry"); return; }
            if (state.deviceProgress >= 1 && state.Remaining(RegionEnemyRole.Mission) == 0) Complete(state);
            else Publish(state);
        }
        void Complete(RegionRunState state)
        {
            if (state.objective == RegionObjectiveState.Completed) return;
            // Persist the entitlement before opening a door. Module application is acknowledged by the queue later.
            if (!state.missionRewarded)
                state.missionRewarded = rewards.GrantOnce("mission/" + state.config.missionId, state.config.missionExperience, state.config.moduleSlot);
            if (!state.missionRewarded) { state.diagnostic = "Reward services unavailable; retry interaction"; return; }
            state.objective = RegionObjectiveState.Completed; state.deviceProgress = 1;
            state.quietUntil = Time.time + state.config.recoverySeconds;
            state.nextPatrol = state.quietUntil + state.config.patrolInterval;
            milestones.Add(state.config.missionId);
            if (activeMission == state) activeMission = null;
            Publish(state); RefreshDoors(); RefreshHud();
        }
        void Fail(RegionRunState state, string reason)
        {
            state.objective = RegionObjectiveState.Failed; state.diagnostic = reason;
            if (activeMission == state) activeMission = null;
            ClearMissionEnemies(state); Publish(state); RefreshHud();
        }
        void ClearMissionEnemies(RegionRunState state)
        {
            if (pendingRegion == state && pendingTicket != null && pendingTicket.role == RegionEnemyRole.Mission) CancelWarning();
            for (int i = state.enemies.Count - 1; i >= 0; i--)
            {
                var ticket = state.enemies[i];
                if (ticket.role != RegionEnemyRole.Mission) continue;
                if (ticket.lease != null) ticket.lease.Release();
                state.enemies.RemoveAt(i);
            }
        }
        public void OnEnemyReleased(RegionEnemyLease lease, bool death)
        {
            var ticket = lease.Ticket; var state = lease.Region;
            if (ticket == null || state == null || ticket.lease != lease) return;
            if (death && !ShuttingDown && gameObject.scene.isLoaded) state.RecordDeath(ticket);
            else { ticket.savedHealth = Mathf.Max(1, lease.Health.CurrentHealth); ticket.lease = null; ticket.due = Time.time + 1; }
        }
        public void DeferPoolReturn(PooledEnemy enemy)
        {
            if (enemy != null && !deferredReturns.Contains(enemy)) deferredReturns.Add(enemy);
        }
        void FlushPoolReturns()
        {
            foreach (var enemy in deferredReturns) if (enemy != null && !enemy.IsReturned) enemy.ReturnToPool();
            deferredReturns.Clear();
        }
        int CountAlive(RegionRunState state, bool specialOnly = false)
        {
            int count = 0;
            foreach (var ticket in state.enemies)
                if (!ticket.dead && ticket.lease != null && (!specialOnly || state.config.Profile(ticket.type).special)) count++;
            return count;
        }
        void TickSpawns()
        {
            if (pool == null || !pool.IsInitialized) return;
            if (pendingTicket != null)
            {
                if (pendingRegion.config.regionId != CurrentRegion && pendingRegion != activeMission) { CancelWarning(); return; }
                if (Time.time < warningUntil) return;
                var ticket = pendingTicket; var state = pendingRegion;
                var profile = state.config.Profile(ticket.type);
                if (Vector2.Distance(pendingPosition, world.PlayerFeet) < state.config.minimumPlayerDistance ||
                    !RegionSpawnSpace.Valid(world, state.config.regionId, pendingPosition, Terminal(state.config.regionId).transform.position,
                        profile.clearance, true, out _))
                { CancelWarning(); SpawnFailed(state, ticket, "warning position became occupied or unreachable"); nextSpawn = Time.time + 1; return; }
                var enemy = pool.GetEnemy(ticket.type, pendingPosition, Quaternion.identity);
                CancelWarning(); nextSpawn = Time.time + state.config.spawnInterval;
                if (enemy == null) { SpawnFailed(state, ticket, "enemy pool unavailable"); return; }
                var lease = enemy.GetComponent<RegionEnemyLease>();
                if (lease == null) lease = enemy.gameObject.AddComponent<RegionEnemyLease>();
                lease.Bind(this, state, ticket); ticket.failedSpawns = 0;
                return;
            }
            if (Time.time < nextSpawn || pool.ActiveCount >= globalAliveLimit) return;
            foreach (var state in states)
            {
                if (!state.initialized || (state.config.regionId != CurrentRegion && state != activeMission) || CountAlive(state) >= state.config.aliveLimit) continue;
                foreach (var ticket in state.enemies)
                {
                    if (ticket.dead || ticket.lease != null || ticket.due > Time.time || ticket.failedSpawns >= state.config.spawnRetryLimit) continue;
                    var profile = state.config.Profile(ticket.type);
                    if (profile.special && CountAlive(state, true) >= state.config.specialLimit) continue;
                    if (TryPosition(state, profile, out var anchor, out var position, out var reason))
                    {
                        pendingTicket = ticket; pendingRegion = state; pendingAnchor = anchor; pendingPosition = position;
                        warningUntil = Time.time + state.config.warningSeconds;
                        if (anchor.warning != null) anchor.warning.transform.position = position;
                        anchor.ShowWarning(true);
                    }
                    else { SpawnFailed(state, ticket, reason); nextSpawn = Time.time + 1; }
                    return;
                }
            }
        }
        bool TryPosition(RegionRunState state, RegionEnemyProfile profile, out RegionAnchor chosen, out Vector3 position, out string reason)
        {
            chosen = null; position = default; reason = "no valid anchors";
            int start = UnityEngine.Random.Range(0, anchors.Length);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[(start + i) % anchors.Length];
                if (anchor == null || anchor.regionId != state.config.regionId) continue;
                for (int trial = 0; trial < 4; trial++)
                {
                    position = anchor.transform.position + (trial == 0 ? Vector3.zero : (Vector3)(UnityEngine.Random.insideUnitCircle * anchor.radius));
                    if (Vector2.Distance(position, world.PlayerFeet) < state.config.minimumPlayerDistance) continue;
                    if (!RegionSpawnSpace.Valid(world, state.config.regionId, position, Terminal(state.config.regionId).transform.position,
                        profile.clearance, true, out reason)) continue;
                    bool occupied = false;
                    foreach (var ticket in state.enemies) if (ticket.lease != null && Vector2.Distance(position, ticket.lease.transform.position) < profile.clearance * 2) { occupied = true; break; }
                    if (occupied) continue;
                    chosen = anchor; return true;
                }
            }
            return false;
        }
        void SpawnFailed(RegionRunState state, RegionEnemyTicket ticket, string reason)
        {
            ticket.failedSpawns++; ticket.due = Time.time + 2;
            if (ticket.failedSpawns < state.config.spawnRetryLimit) return;
            string message = "Spawn blocked: " + ticket.id + " / " + reason + ". Move away from ingress and interact to retry.";
            Debug.LogWarning("[RegionLoop] " + message, this);
            if (ticket.role == RegionEnemyRole.Mission) Fail(state, message);
            else if (ticket.role == RegionEnemyRole.Resident) state.diagnostic = message;
            // A failed optional patrol consumes budget, but never holds a mission open.
        }
        void CancelWarning()
        {
            if (pendingAnchor != null) pendingAnchor.ShowWarning(false);
            pendingTicket = null; pendingRegion = null; pendingAnchor = null;
        }
        void Publish(RegionRunState state)
        {
            if (missionView != null) missionView.SetRegionObjectiveView(state.config.missionId,
                state.objective == RegionObjectiveState.Active ? MissionState.Active :
                state.objective == RegionObjectiveState.Completed ? MissionState.Completed : MissionState.Failed,
                state.lockedDevicePosition, state.deviceProgress, Mathf.Max(0, state.config.missionDeadline - state.missionElapsed));
        }
        void RefreshDoors() { if (world != null && world.progression != null) world.progression.RefreshRegionDoors(); }
        void RefreshHud()
        {
            if (hud == null) return;
            var state = activeMission ?? State(CurrentRegion);
            if (state == null) { hud.text = "REGION LOOP V01\n" + CurrentRegion + "\nG3 OPEN - Stage 1 complete\nFurther objectives offline"; return; }
            string objective = state.objective == RegionObjectiveState.Completed ? "COMPLETE - Search / proceed" :
                state.objective == RegionObjectiveState.Active ? "DEFEND DEVICE " + Mathf.RoundToInt(state.deviceProgress * 100) + "% | Integrity " + Mathf.CeilToInt(state.integrity) + "\nRequired guards: " + state.Remaining(RegionEnemyRole.Mission) + " | " + Mathf.CeilToInt(state.config.missionDeadline - state.missionElapsed) + "s" :
                state.config.missionId == "Arrival" && state.Remaining(RegionEnemyRole.Resident) > 0 ? "Clear entry guards: " + state.Remaining(RegionEnemyRole.Resident) : "Find " + state.config.missionId + " device - [E] start / retry";
            hud.text = "REGION: " + CurrentRegion + "\n" + objective + "\n" + state.config.exitGateId + ": " +
                (HasMilestone(state.config.missionId) ? "OPEN" : "Requires " + state.config.missionId) +
                (rewards.HasPending ? "\nChoices pending: " + rewards.PendingCount : "") +
                (!string.IsNullOrEmpty(state.diagnostic) ? "\n" + state.diagnostic : "");
        }
        void StopRun()
        {
            if (ShuttingDown) return;
            ShuttingDown = true; CancelWarning(); FlushPoolReturns();
            foreach (var state in states) foreach (var ticket in state.enemies) if (ticket.lease != null) ticket.lease.Release();
            if (rewards != null) rewards.CancelRun();
        }
        void OnDisable() { if (Application.isPlaying) StopRun(); }
    }
}
