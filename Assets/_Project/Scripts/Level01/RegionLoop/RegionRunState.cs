using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    // Scene-owned run data, never static or stored in a shared ScriptableObject.
    public sealed class RegionEnemyTicket
    {
        public string id, waveId;
        public EnemyType type;
        public RegionEnemyRole role;
        public bool keyGuard, dead;
        public int attempt, failedSpawns, savedHealth = -1;
        public float due;
        public RegionEnemyLease lease;
        // Reinforcements freeze on spawn; untouched preloads lock on entry. Retained across pool release/re-entry.
        public RegionCombatStats combatStats;
    }

    public sealed class RegionRunState
    {
        public readonly RegionDefinition config;
        public readonly List<RegionEnemyTicket> enemies = new List<RegionEnemyTicket>();
        public RegionObjectiveState objective;
        public bool initialized, encounterRewarded, missionRewarded;
        public int attempt, patrolSpent;
        public float nextPatrol, missionElapsed, deviceProgress, integrity, quietUntil;
        public Vector3 lockedDevicePosition;
        public string diagnostic;
        public float firstEntered = -1, lowPressureSince = -1, environmentDue, fogBuffer;
        public int environmentWave;
        public bool retired, stormStarted;
        public RegionRunState(RegionDefinition definition) { config = definition; }

        public int Remaining(RegionEnemyRole role)
        {
            int count = 0;
            foreach (var ticket in enemies) if (ticket.role == role && !ticket.dead) count++;
            return count;
        }
        public bool RecordDeath(RegionEnemyTicket ticket)
        {
            if (ticket == null || ticket.dead || !enemies.Contains(ticket)) return false;
            ticket.dead = true;
            ticket.lease = null;
            return true;
        }
        public void AddGroups(RegionSpawnGroup[] groups, RegionEnemyRole role, string wave, float due)
        {
            foreach (var group in groups)
                for (int i = 0; i < group.count; i++)
                    enemies.Add(new RegionEnemyTicket {
                        id = config.regionId + "/" + role + "/" + attempt + "/" + wave + "/" + enemies.Count,
                        waveId = wave, role = role, attempt = attempt, type = group.type,
                        keyGuard = group.keyGuard, due = due
                    });
        }
    }
}
