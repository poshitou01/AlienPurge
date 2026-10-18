using System;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    public enum MechanismRewardSource { MainCompletion, FirstEntry }
    [Serializable]
    public sealed class RegionOptionalDefinition
    {
        public string id;
        public bool highRisk;
        public bool triggersStorm;
        [Min(1)] public float acceptSeconds = 90, defendSeconds = 18, deadlineSeconds = 50;
        [Min(1)] public float radius = 5, claimAndExitSeconds = 35;
        [Min(1)] public int maxAttempts = 1;
        [Min(0)] public int experience = 10;
        public RegionWave[] waves = Array.Empty<RegionWave>();
    }
    [Serializable]
    public sealed class RegionV02Settings
    {
        public RegionWave[] environmentSquads = Array.Empty<RegionWave>();
        public bool weightedEnvironmentSquads;
        public bool extractionRegion;
        [Min(1)] public float environmentInterval = 12, searchSeconds = 12, preloadDistance = 32;
        [Min(0)] public int pressureLowWatermark = 2, missionCapacityReserve = 6;
        [Min(1)] public int ambientTarget = 12;
        public MechanismRewardSource mechanismSource;
        public RegionOptionalDefinition[] optionalTasks = Array.Empty<RegionOptionalDefinition>();
        [Header("Storm: relative to first entry, never global survival time")]
        [Min(0)] public float stormAfterEntry = 42;
        [Min(1)] public float stormWarningMinimum = 10, stormActiveSeconds = 12, stormRecoverySeconds = 3;
        [Min(1)] public float safeRadius = 4;
        [Header("Fog: first-entry clock persists across travel")]
        [Min(1)] public float explorationSeconds = 120, fogWarningSeconds = 30, fogAdvanceSeconds = 70;
        [Min(0)] public float oneTimeExitBuffer = 20;
        [Min(0)] public float fogStartDistance, fogEndDistance;
    }
}
