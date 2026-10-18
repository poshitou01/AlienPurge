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
            return base.CanInteract(interactor) && controller != null && (controller.CanActivate(regionId) || controller.v02Enabled && controller.TerminalTask(regionId) != null);
        }
        public override void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor)) return;
            if (controller.CanActivate(regionId)) controller.TryActivate(regionId);
            else if (controller.v02Enabled) controller.TerminalTask(regionId)?.Interact();
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh || controller == null) return;
            nextRefresh = Time.unscaledTime + .2f;
            var state = controller.State(regionId);
            if (state == null) return;
            if (controller.v02Enabled)
            {
                if (deviceRange != null) deviceRange.enabled = false;
                var task = controller.TerminalTask(regionId);
                string prompt = task != null ? task.Active ? "放弃驻守" : task.Config.highRisk ? "风暴驻守" : "可选驻守" : "启用终端";
                SetInteractionPrompt(prompt);
                if (status != null) status.text = RegionLoopController.DeviceName(regionId) + "\n" +
                    (controller.CanActivate(regionId) ? "[E] " + (state.config.v02.extractionRegion ? "呼叫撤离" : "开启通道") :
                    task != null ? "[E] " + prompt : state.config.v02.extractionRegion && controller.extraction.IsDefending ? "守住信标，等待撤离" :
                    state.objective == RegionObjectiveState.Completed ? "已启用，请沿箭头前进" : "守卫剩余 " + controller.RemainingAccessGuards(state));
                return;
            }
            if (status != null) status.text = state.config.missionId.ToUpperInvariant() + "\n" +
                (state.objective == RegionObjectiveState.Completed ? "ONLINE" :
                 state.objective == RegionObjectiveState.Active ? Mathf.RoundToInt(state.deviceProgress * 100) + "% / " + Mathf.CeilToInt(state.integrity) + " HP" :
                 CanInteract(null) ? controller.v02Enabled ?
                    (string.IsNullOrEmpty(state.diagnostic) ? "[E] OPEN " + state.config.exitGateId : "[E] RETRY") :
                    "[E] START / RETRY" : "CLEAR ENTRY GUARDS");
            if (deviceRange != null) deviceRange.startColor = deviceRange.endColor =
                state.objective == RegionObjectiveState.Failed ? new Color(1,.3f,.2f) :
                state.objective == RegionObjectiveState.Completed ? Color.green : Color.cyan;
        }
    }
}
