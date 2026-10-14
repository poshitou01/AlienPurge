
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AlienPurge Phase40 Step 3.5-D2.
/// Confirms entry into the physically connected Early Extraction region.
/// Uses the existing Level01RegionProgressionController unlock gate.
/// Does not move the player, load scenes, touch missions, or reset hazards.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class EarlyExtractionEntryTrigger : MonoBehaviour
{
    [Header("Existing Level01 References")]
    [SerializeField] private Level01RegionProgressionController regionProgression;
    [SerializeField] private Transform playerRoot;

    [Header("Cargo Yard HUD Cleanup (After Successful Entry Only)")]
    [SerializeField] private CargoYardExitGuide cargoYardExitGuide;
    [SerializeField] private CanvasGroup cargoExitGuidePanel;
    [SerializeField] private bool hideCargoExitGuideAfterEntry = true;

    [Header("Entry Rules")]
    [SerializeField] private bool requireGameplayRunning = true;
    [SerializeField] private bool logStateChanges = true;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool playerInsideEntryTrigger;
    [SerializeField] private bool entryBlockedBeforeUnlock;
    [SerializeField] private bool entryConfirmed;
    [SerializeField] private int successfulEntryCount;

    public bool PlayerInsideEntryTrigger => playerInsideEntryTrigger;
    public bool EntryConfirmed => entryConfirmed;
    public bool EntryBlockedBeforeUnlock => entryBlockedBeforeUnlock;

    private readonly HashSet<Collider2D> overlappingPlayerColliders =
        new HashSet<Collider2D>();

    private void Reset()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size = new Vector2(2.5f, 3.5f);
        }
    }

    private void Awake()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null && !box.isTrigger)
        {
            Debug.LogWarning(
                "[EarlyExtractionEntry] BoxCollider2D must be Is Trigger. Fixing it.",
                this);
            box.isTrigger = true;
        }

        if (regionProgression == null)
            regionProgression = FindFirstObjectByType<Level01RegionProgressionController>();

        if (playerRoot == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                playerRoot = playerObject.transform;
        }

        overlappingPlayerColliders.Clear();
        playerInsideEntryTrigger = false;
        entryBlockedBeforeUnlock = false;
        entryConfirmed = false;
        successfulEntryCount = 0;
    }

    private void Start()
    {
        if (regionProgression == null || playerRoot == null)
        {
            Debug.LogWarning(
                "[EarlyExtractionEntry] Assign Region Progression and Player Root.",
                this);
        }

        if (hideCargoExitGuideAfterEntry &&
            (cargoYardExitGuide == null || cargoExitGuidePanel == null))
        {
            Debug.LogWarning(
                "[EarlyExtractionEntry] Assign CargoYardExitGuide and its CanvasGroup " +
                "to hide the old Cargo Yard guidance after entering the next region.",
                this);
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null || playerRoot == null)
            return false;

        Transform t = other.transform;
        return t == playerRoot || t.IsChildOf(playerRoot);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckPlayerEntry(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Also handles physics ordering when the Cargo Yard exit unlocks
        // the next region in the same frame that the Player crosses the seam.
        CheckPlayerEntry(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        overlappingPlayerColliders.Remove(other);
        playerInsideEntryTrigger = overlappingPlayerColliders.Count > 0;
    }

    private void CheckPlayerEntry(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        overlappingPlayerColliders.Add(other);
        playerInsideEntryTrigger = true;

        if (entryConfirmed || regionProgression == null)
            return;

        if (requireGameplayRunning &&
            (GameManager.Instance == null ||
             !GameManager.Instance.IsPlaying ||
             Time.timeScale <= 0f))
        {
            return;
        }

        bool unlocked =
            regionProgression.CurrentRegion ==
                Level01RegionProgressionController.Level01Region.CargoYard &&
            regionProgression.CargoYardExitReached &&
            regionProgression.EarlyExtractionUnlocked &&
            regionProgression.AwaitingEarlyExtractionEntry;

        if (!unlocked)
        {
            entryBlockedBeforeUnlock = true;
            return;
        }

        // The existing progression controller is the single authority.
        if (!regionProgression.TryConfirmEarlyExtractionEntry())
            return;

        entryConfirmed = true;
        successfulEntryCount++;

        if (hideCargoExitGuideAfterEntry)
        {
            // Stop the old HUD writer before hiding its canvas group.
            if (cargoYardExitGuide != null)
                cargoYardExitGuide.enabled = false;

            if (cargoExitGuidePanel != null)
            {
                cargoExitGuidePanel.alpha = 0f;
                cargoExitGuidePanel.interactable = false;
                cargoExitGuidePanel.blocksRaycasts = false;
            }
        }

        if (logStateChanges)
        {
            Debug.Log(
                "[EarlyExtractionEntry] Entry confirmed once. " +
                "Current Region = EarlyExtraction. " +
                "No scene load, teleport, mission reset or fog reset.",
                this);
        }
    }

    private void OnDisable()
    {
        overlappingPlayerColliders.Clear();
        playerInsideEntryTrigger = false;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.25f, 0.85f, 0.6f, 0.95f);
        Gizmos.DrawWireCube(box.offset, box.size);
        Gizmos.matrix = oldMatrix;
    }
}
