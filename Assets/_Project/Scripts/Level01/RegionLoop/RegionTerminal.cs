using TMPro;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    [DisallowMultipleComponent]
    public sealed class RegionTerminal : InteractableBase
    {
        public string stableId, regionId;
        public RegionLoopController controller;
        public TMP_Text status;
        public LineRenderer deviceRange;
        float nextRefresh;
        public override bool CanInteract(PlayerInteractor interactor)
        {
            return base.CanInteract(interactor) && controller != null && controller.CanActivate(regionId);
        }
        public override void Interact(PlayerInteractor interactor)
        {
            if (CanInteract(interactor)) controller.TryActivate(regionId);
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh || controller == null) return;
            nextRefresh = Time.unscaledTime + .2f;
            var state = controller.State(regionId);
            if (state == null) return;
            if (status != null) status.text = state.config.missionId.ToUpperInvariant() + "\n" +
                (state.objective == RegionObjectiveState.Completed ? "ONLINE" :
                 state.objective == RegionObjectiveState.Active ? Mathf.RoundToInt(state.deviceProgress * 100) + "% / " + Mathf.CeilToInt(state.integrity) + " HP" :
                 CanInteract(null) ? "[E] START / RETRY" : "CLEAR ENTRY GUARDS");
            if (deviceRange != null) deviceRange.startColor = deviceRange.endColor =
                state.objective == RegionObjectiveState.Failed ? new Color(1,.3f,.2f) :
                state.objective == RegionObjectiveState.Completed ? Color.green : Color.cyan;
        }
    }
}
