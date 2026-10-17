using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    [DisallowMultipleComponent]
    public sealed class RegionRewardQueue : MonoBehaviour
    {
        public UpgradeManager upgrades;
        public WeaponModuleSelectionManager modules;
        public PlayerExperience experience;
        readonly Queue<Choice> choices = new Queue<Choice>();
        readonly HashSet<string> earned = new HashSet<string>(), applied = new HashSet<string>();
        sealed class Choice { public string id; public int moduleSlot; }
        Choice active;
        int upgradeSequence;
        bool shuttingDown, reportedFailure;
        public int PendingCount => choices.Count + (active != null ? 1 : 0);
        public bool HasPending => PendingCount > 0;
        public bool WasApplied(string id) => applied.Contains(id);
        // Future region milestones can subscribe without enabling timed evolution.
        public event Action<string> MilestoneApplied;

        public void EnqueueUpgrade()
        {
            if (!shuttingDown) choices.Enqueue(new Choice { id = "level/" + (++upgradeSequence) });
        }
        public void EnqueuePowerSpike()
        {
            if (!shuttingDown && earned.Add("power-spike")) choices.Enqueue(new Choice { id = "power-spike", moduleSlot = -1 });
        }
        public bool GrantOnce(string id, int xp, int moduleSlot)
        {
            if (shuttingDown || earned.Contains(id) || experience == null || upgrades == null || modules == null) return false;
            earned.Add(id);
            // Experience enqueues ALL levels before the associated module.
            experience.AddExperience(xp);
            if (moduleSlot > 0) choices.Enqueue(new Choice { id = id, moduleSlot = moduleSlot });
            else { applied.Add(id); MilestoneApplied?.Invoke(id); }
            return true;
        }
        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) { CancelRun(); return; }
            Advance();
        }
        void Advance()
        {
            if (shuttingDown || active != null || choices.Count == 0 ||
                UpgradeManager.IsChoosingUpgrade || WeaponModuleSelectionManager.IsChoosingModule ||
                GameManager.Instance == null || !GameManager.Instance.IsPlaying ||
                PauseMenuController.IsPaused || BackpackPanelController.IsOpen || LootSearchPanelController.IsOpen) return;
            var next = choices.Peek();
            bool immediate = false;
            bool shown = next.moduleSlot < 0 ? modules.TryShowRegionOverclock(out immediate) :
                next.moduleSlot == 0 ? upgrades.TryShowQueuedUpgrade() : modules.TryShowRegionModule(next.moduleSlot);
            if (!shown)
            {
                if (!reportedFailure) Debug.LogError("[RegionLoop] Pending choice could not open: " + next.id + ". Check panel references and legal options.", this);
                reportedFailure = true; return;
            }
            reportedFailure = false; active = choices.Dequeue();
            if (immediate) ChoiceClosed(true);
        }
        public void ChoiceClosed(bool success)
        {
            if (active != null)
            {
                if (success) { applied.Add(active.id); MilestoneApplied?.Invoke(active.id); }
                else choices.Enqueue(active); // Never silently claim an unapplied reward.
                active = null;
            }
            if (shuttingDown) return;
            Advance();
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying &&
                !UpgradeManager.IsChoosingUpgrade && !WeaponModuleSelectionManager.IsChoosingModule &&
                !PauseMenuController.IsPaused && !BackpackPanelController.IsOpen && !LootSearchPanelController.IsOpen)
                Time.timeScale = 1;
        }
        public void CancelRun()
        {
            if (shuttingDown) return;
            shuttingDown = true; choices.Clear(); active = null; earned.Clear(); applied.Clear();
            if (upgrades != null) upgrades.CancelRegionChoice();
            if (modules != null) modules.CancelRegionChoice();
        }
        void OnDestroy() { CancelRun(); }
    }
}
