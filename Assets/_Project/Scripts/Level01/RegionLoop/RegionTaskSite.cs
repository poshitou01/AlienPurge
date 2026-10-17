using System.Linq;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    // Adapter on an existing Beacon prefab. MissionManager owns the active optional task.
    [DisallowMultipleComponent]
    public sealed class RegionTaskSite : MonoBehaviour
    {
        public string stableId, taskDefinitionId;
        public RegionDefinition definition;
        public RegionLoopController controller;
        public BeaconMissionObjective beacon;
        public LootContainer rewardContainer;
        public int Attempts { get; private set; }
        public bool Succeeded { get; private set; }
        public string LastResult { get; private set; }
        public string TaskId => definition.regionId + "/" + taskDefinitionId;
        public RegionOptionalDefinition Config => definition.v02.optionalTasks.First(t => t.id == taskDefinitionId);
        public bool Active => controller != null && controller.missionView.IsRegionTaskActive(TaskId);
        public bool CanInteract() => Active || controller != null && controller.CanAcceptOptional(this);
        public void Interact()
        {
            if (Active) { controller.missionView.CancelRegionTask("Abandoned by player"); return; }
            if (!CanInteract()) return;
            controller.BeginOptional(this);
        }
        public void Accepted()
        {
            Attempts++; LastResult = "Active";
            beacon.BeginRegionDefense(controller.missionView, controller.world.player.transform, Config.radius, Config.defendSeconds);
        }
        public void Finished(bool success, string reason)
        {
            Succeeded |= success; LastResult = reason; beacon.EndRegionDefense(success);
        }
        public void RefreshPrompt()
        {
            if (beacon == null) return;
            // Detailed preview belongs in the existing MissionHUD rows, not the narrow E prompt.
            beacon.SetInteractionPrompt(Active ? "ABANDON" : Config.highRisk ? "HIGH RISK" : "DEFEND");
        }
    }
}
