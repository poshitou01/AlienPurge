using System;
using AlienPurge.Level01.RegionLoop;
using UnityEngine;

public partial class MissionManager
{
    // One existing mission service owns the optional combat slot; the presentation slot is independent.
    public string RegionTaskId { get; private set; }
    public string RegionTaskRegion { get; private set; }
    public float RegionTaskRemaining { get; private set; }
    public float RegionTaskProgress { get; private set; }
    public bool RegionTaskActive => !string.IsNullOrEmpty(RegionTaskId);
    public bool HasRegionPresentation { get; private set; }
    public string RegionTitle, RegionObjective, RegionTimerLabel, RegionProgressText, RegionReward, RegionRisk;
    Action<bool, string> regionFinished;
    Func<bool> regionGuardsCleared;
    RegionRewardQueue regionTaskRewards;
    int regionTaskExperience;

    public bool BeginRegionTask(string id, string region, float deadline, int experience, RegionRewardQueue rewards,
        Func<bool> guardsCleared, Action<bool, string> finished)
    {
        if (!regionDriven || RegionTaskActive || regionCompletedIds.Contains("optional/" + id)) return false;
        RegionTaskId = id; RegionTaskRegion = region; RegionTaskRemaining = deadline; RegionTaskProgress = 0;
        regionTaskExperience = experience; regionTaskRewards = rewards; regionGuardsCleared = guardsCleared; regionFinished = finished;
        return true;
    }
    public bool IsRegionTaskActive(string id) => regionDriven && RegionTaskActive && RegionTaskId == id;
    public void ReportRegionTaskProgress(string id, float progress)
    { if (IsRegionTaskActive(id)) RegionTaskProgress = Mathf.Clamp01(progress); }
    public void TryCompleteRegionTask(string id)
    {
        if (!IsRegionTaskActive(id) || RegionTaskProgress < 1 || (regionGuardsCleared != null && !regionGuardsCleared())) return;
        if (regionTaskRewards == null || !regionTaskRewards.GrantOnce("optional/" + id, regionTaskExperience, 0)) return;
        regionCompletedIds.Add("optional/" + id); completedMissionCount++;
        lastGrantedExpReward = regionTaskExperience; lastRewardUsedStormRiskBonus = false;
        FinishRegionTask(true, "Completed");
    }
    public void CancelRegionTask(string reason)
    {
        if (!RegionTaskActive) return;
        failedMissionCount++; FinishRegionTask(false, reason);
    }
    void FinishRegionTask(bool success, string reason)
    {
        var callback = regionFinished;
        RegionTaskId = null; RegionTaskRegion = null; RegionTaskRemaining = 0;
        regionFinished = null; regionGuardsCleared = null; regionTaskRewards = null;
        callback?.Invoke(success, reason);
    }
    void TickRegionTask()
    {
        if (!RegionTaskActive) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) { CancelRegionTask("Run ended"); return; }
        RegionTaskRemaining = Mathf.Max(0, RegionTaskRemaining - Time.deltaTime);
        if (RegionTaskRemaining <= 0) CancelRegionTask("Deadline expired");
    }
    public void PresentRegionObjective(string title, string objective, string timerLabel, string progressText,
        string reward, string risk, Vector2 position, float progress, float remaining)
    {
        if (!regionDriven) return;
        HasRegionPresentation = true;
        RegionTitle = title; RegionObjective = objective; RegionTimerLabel = timerLabel; RegionProgressText = progressText;
        RegionReward = reward; RegionRisk = risk;
        currentMissionType = RegionTaskActive ? MissionType.BeaconActivation : MissionType.AlienCoreRecovery;
        currentMissionState = MissionState.Active; currentObjectivePosition = position;
        missionProgress01 = progress; missionTimeRemaining = remaining;
    }
    public void PresentRegionCompletion(Vector2 position)
    {
        PresentRegionObjective("LEVEL01 // V02", "QuarantineCommons reached", "", "100%", "REGION ROUTE COMPLETE",
            "Continue exploring the existing world", position, 1, 0);
        if (regionDriven) currentMissionState = MissionState.Completed; // Native arrow hides; never point back into reclaimed Power.
    }
}
public partial class BeaconMissionObjective
{
    public RegionTaskSite regionSite;
    public float RegionDefendedSeconds => activationElapsed;
    public void BeginRegionDefense(MissionManager manager, Transform playerTransform, float radius, float seconds)
    {
        activationRadius = radius; activationDuration = seconds;
        Initialize(manager, playerTransform);
        activationStarted = true;
        if (visualFeedback != null) visualFeedback.SetActivated(true);
        if (interactionMarker != null) interactionMarker.SetActive(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBeaconActivated();
    }
    public void EndRegionDefense(bool success)
    {
        activationStarted = false; completionReported = success;
        if (visualFeedback != null) visualFeedback.SetWaiting();
        if (interactionMarker != null) interactionMarker.SetActive(!success);
    }
    void TickRegionBeacon()
    {
        if (!initialized || !activationStarted || completionReported || missionManager == null ||
            !missionManager.IsRegionTaskActive(regionSite.TaskId) || !CanAdvanceActivation()) return;
        UpdatePlayerInsideState();
        if (visualFeedback != null) visualFeedback.SetPlayerInside(playerInsideActivationRadius);
        if (playerInsideActivationRadius) activationElapsed = Mathf.Min(activationDuration, activationElapsed + Time.deltaTime);
        activationProgress01 = Mathf.Clamp01(activationElapsed / activationDuration);
        missionManager.ReportRegionTaskProgress(regionSite.TaskId, activationProgress01);
        if (activationProgress01 >= 1) missionManager.TryCompleteRegionTask(regionSite.TaskId);
    }
}
public partial class MissionHUD
{
    bool RefreshRegionPresentation()
    {
        if (missionManager == null || !missionManager.HasRegionPresentation) return false;
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || UpgradeManager.IsChoosingUpgrade || WeaponModuleSelectionManager.IsChoosingModule || PauseMenuController.IsPaused)
        { HidePanel(); return true; }
        ShowPanel();
        if (titleText != null) titleText.text = missionManager.RegionTitle;
        if (objectiveText != null) objectiveText.text = missionManager.RegionObjective;
        if (timerText != null) timerText.text = string.IsNullOrEmpty(missionManager.RegionTimerLabel) ? "" : missionManager.RegionTimerLabel + " " + Mathf.CeilToInt(missionManager.MissionTimeRemaining) + "s";
        RefreshDistance();
        if (progressRoot != null) progressRoot.SetActive(true);
        if (progressSlider != null) progressSlider.value = missionManager.MissionProgress01;
        if (progressText != null) progressText.text = missionManager.RegionProgressText;
        if (rewardText != null) { rewardText.text = missionManager.RegionReward; rewardText.color = normalColor; }
        if (riskText != null) { riskText.text = missionManager.RegionRisk; riskText.color = normalColor; }
        return true;
    }
}
