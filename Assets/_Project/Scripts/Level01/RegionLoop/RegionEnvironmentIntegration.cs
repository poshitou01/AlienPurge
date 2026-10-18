using UnityEngine;

public partial class SafeZoneController
{
    public void SetRegionZone(Vector2 center, float radius)
    {
        currentCenter = center; safeZoneRadius = Mathf.Max(1, radius); hasActiveZone = true;
    }
}
public partial class PlanetStormController
{
    public bool RegionZoneLocked => regionDriven && currentPhase != StormPhase.Calm;
    public bool TryBeginRegionStorm(Vector2 center, float radius, float warning, float active, float recovery)
    {
        if (!regionDriven || currentPhase != StormPhase.Calm || !CanUpdateStorm()) return false;
        ResolveReferences();
        if (safeZoneController == null) return false;
        safeZoneController.SetRegionZone(center, radius);
        warningDuration = Mathf.Max(1, warning); activeDuration = Mathf.Max(1, active); recoveryDuration = Mathf.Max(1, recovery);
        currentStormNumber++; currentPhase = StormPhase.Warning; phaseTimeRemaining = warningDuration;
        lastStormTriggeredAt = GameManager.Instance.SurvivalTime;
        Debug.Log("[RegionLoop] Storm warning #" + currentStormNumber + " locked at " + center + " radius=" + radius + " travel=" + warningDuration);
        return true;
    }
    public void EndRegionStorm() { if (regionDriven && currentPhase != StormPhase.Calm) FinishStorm(); }
}
public partial class ToxicFogRouteController
{
    public string RegionNotice { get; private set; }
    public void SetRegionNotice(string notice) { if (regionDriven) RegionNotice = notice; }
    // Target distance is a cap, not a value reconstructed from total survival time.
    public void AdvanceRegionFront(float target, float speed, float delta)
    {
        if (!regionDriven || !routeIsValid || delta <= 0 || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        target = Mathf.Clamp(target, distanceTravelled, totalRouteDistance);
        if (target <= distanceTravelled) return;
        distanceTravelled = Mathf.MoveTowards(distanceTravelled, target, Mathf.Max(.01f, speed) * delta);
        frontWorldPosition = EvaluateRoute(distanceTravelled);
        currentState = distanceTravelled >= totalRouteDistance ? FogRouteState.ReachedRouteEnd : FogRouteState.Advancing;
    }
}
public sealed partial class ToxicFogFeedbackHUD
{
    bool RefreshRegionWarning()
    {
        if (fogRoute == null || !fogRoute.regionDriven || string.IsNullOrEmpty(fogRoute.RegionNotice)) return false;
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || Time.timeScale <= 0 || UpgradeManager.IsChoosingUpgrade || WeaponModuleSelectionManager.IsChoosingModule)
        { HideAll(); return true; }
        if (fogArea != null && playerRoot != null && fogArea.IsPositionInsideFog(playerRoot.position)) return false;
        if (titleText != null) { titleText.text = "毒雾正在收回后方区域"; titleText.color = approachingColor; }
        if (detailText != null) detailText.text = fogRoute.RegionNotice;
        SetPanelAlpha(.9f); SetTintAlpha(0); return true;
    }
}
