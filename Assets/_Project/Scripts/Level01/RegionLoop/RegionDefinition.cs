using System;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    public enum RegionEnemyRole { Resident, Mission, Patrol }
    public enum RegionObjectiveState { Waiting, Active, Failed, Completed }

    [Serializable]
    public sealed class RegionEnemyProfile
    {
        public string archetypeId;
        public EnemyType type;
        [Min(1)] public int health = 4, damage = 1, experience = 2;
        [Min(.1f)] public float speed = 2f;
        [Min(.2f)] public float clearance = .65f;
        public bool special;
        [Min(0), Tooltip("0 uses only the region total; V02 can cap each archetype separately.")]
        public int simultaneousLimit;
        [Min(0)] public int globalSimultaneousLimit;
    }

    [Serializable]
    public sealed class RegionSpawnGroup
    {
        public EnemyType type;
        [Min(1)] public int count = 3;
        public bool keyGuard;
    }

    [Serializable]
    public sealed class RegionWave
    {
        public string id;
        [Min(0)] public float weight = 1;
        [Min(0)] public float delay;
        public RegionSpawnGroup[] groups = Array.Empty<RegionSpawnGroup>();
    }

    [CreateAssetMenu(menuName = "AlienPurge/Level01/Region Loop Definition")]
    public sealed class RegionDefinition : ScriptableObject
    {
        [Header("Identity / all V01 numbers are first-playtest suggestions")]
        public string regionId, missionId, encounterId, exitGateId, prerequisite;
        public RegionEnemyProfile[] enemies = Array.Empty<RegionEnemyProfile>();
        [Tooltip("Reserved integration identifiers. No spawns or new behavior until a matching pool type/profile is supplied.")]
        public string[] reservedEnemyIds = { "Spitter", "Exploder", "Support" };
        public RegionSpawnGroup[] residents = Array.Empty<RegionSpawnGroup>();
        public RegionWave[] waves = Array.Empty<RegionWave>();
        [Header("Finite population")]
        [Min(1)] public int aliveLimit = 12;
        [Min(0)] public int specialLimit = 3, patrolBudget = 3;
        public EnemyType patrolType = EnemyType.Normal;
        [Min(1)] public float patrolInterval = 35, recoverySeconds = 40;
        [Min(.1f)] public float spawnInterval = .8f, warningSeconds = 1.2f;
        [Min(1)] public int spawnRetryLimit = 8;
        [Min(0)] public float minimumPlayerDistance = 5;
        [Min(1)] public float perceptionRadius = 13, leashRadius = 26;
        [Header("Device objective (Arrival only needs resident clear)")]
        [Min(0)] public float deviceSeconds = 20;
        [Min(1)] public float deviceRadius = 6, missionDeadline = 150;
        [Min(1)] public float deviceIntegrity = 100, contestedDamagePerSecond = 6;
        [Min(0)] public float contestRadius = 2.5f;
        [Header("One-time progression rewards")]
        [Min(0)] public int encounterExperience = 4, missionExperience = 12;
        [Range(0, 2)] public int moduleSlot;
        [Header("V02 opt-in settings (used only by a V02 scene)")]
        public RegionV02Settings v02;

        public RegionEnemyProfile Profile(EnemyType type)
        {
            foreach (var profile in enemies) if (profile != null && profile.type == type) return profile;
            return null;
        }
    }
}
