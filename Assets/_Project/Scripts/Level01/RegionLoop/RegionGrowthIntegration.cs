using System;
using UnityEngine;

public partial class PlayerWeaponModifiers
{
    public event Action ModulesChanged;
}
public partial class WeaponModuleSelectionManager
{
    public bool eventDrivenEvolution;
    public bool RegionPowerSpikeTriggered => powerSpikeTriggered;
    public string RegionPowerSpikeResult => lastPowerSpikeResult;
    void SubscribeRegionBuild()
    {
        if (!eventDrivenEvolution || playerWeaponModifiers == null) return;
        playerWeaponModifiers.ModulesChanged += EvaluateRegionBuild;
        EvaluateRegionBuild();
    }
    void UnsubscribeRegionBuild()
    { if (playerWeaponModifiers != null) playerWeaponModifiers.ModulesChanged -= EvaluateRegionBuild; }
    void EvaluateRegionBuild()
    {
        if (!eventDrivenEvolution || !regionDriven || powerSpikeTriggered || GetUnlockedModuleCount() != 2 ||
            GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        EnsurePlayerReference();
        if (weaponEvolutionController != null && weaponEvolutionController.TryApplyEligibleEvolution())
        {
            powerSpikeTriggered = true;
            lastPowerSpikeResult = "Evolution: " + weaponEvolutionController.CurrentEvolution;
        }
        else if (regionRewards != null)
        {
            powerSpikeTriggered = true; lastPowerSpikeResult = "Overclock queued";
            regionRewards.EnqueuePowerSpike();
        }
    }
    public bool TryShowRegionOverclock(out bool appliedImmediately)
    {
        appliedImmediately = false;
        if (!eventDrivenEvolution || !powerSpikeTriggered || IsChoosingModule || UpgradeManager.IsChoosingUpgrade) return false;
        BeginOverclockSelection(GameManager.Instance.SurvivalTime);
        appliedImmediately = !IsChoosingModule && lastPowerSpikeResult == "Maxed Overclock Fallback";
        if (IsChoosingModule) lastPowerSpikeResult = "Overclock";
        return IsChoosingModule || appliedImmediately;
    }
}
public partial class WeaponEvolutionRevealUI
{
    public bool deferRegionFeedback;
}
