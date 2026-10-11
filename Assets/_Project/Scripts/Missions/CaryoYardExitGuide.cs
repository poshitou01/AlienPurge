
using TMPro;
using UnityEngine;

/// <summary>
/// AlienPurge Phase40 - Cargo Yard exit guidance.
///
/// Watches Beacon Mission completion, shows an exit guide,
/// and detects the Player arriving at the right-side exit.
///
/// Does NOT change scenes, extract, or teleport the Player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CargoYardExitGuide : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Existing Gameplay References")]
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private Transform playerRoot;

    [Header("HUD References")]
    [SerializeField] private CanvasGroup guidePanelGroup;
    [SerializeField] private TMP_Text guideTitleText;
    [SerializeField] private TMP_Text guideDetailText;

    // =========================================================
    // Display
    // =========================================================

    [Header("Display Settings")]
    [SerializeField] private bool hideWhileGameplayPaused = true;

    [SerializeField]
    private string readyTitle =
        "前往右侧出口";

    [SerializeField]
    private string readyDetail =
        "仓储任务已完成。可以继续搜索，或前往 Early Extraction。";

    [SerializeField]
    private string arrivedTitle =
        "已到达 Cargo Yard 出口";

    [SerializeField]
    private string arrivedDetail =
        "出口位置已验证，下一阶段将接入正式区域切换。";

    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug - Read Only")]
    [SerializeField] private bool beaconMissionCompleted;
    [SerializeField] private bool playerInsideExitTrigger;
    [SerializeField] private bool reachedExitAfterMission;

    public bool BeaconMissionCompleted =>
        beaconMissionCompleted;

    public bool PlayerInsideExitTrigger =>
        playerInsideExitTrigger;

    public bool ReachedExitAfterMission =>
        reachedExitAfterMission;

    private BoxCollider2D exitCollider;
    private bool lastPanelVisible;
    private bool lastPanelAtExit;

    // =========================================================
    // Lifecycle
    // =========================================================

    private void Awake()
    {
        exitCollider = GetComponent<BoxCollider2D>();

        if (exitCollider != null && !exitCollider.isTrigger)
        {
            Debug.LogWarning(
                "[CargoYardExitGuide] BoxCollider2D must be Is Trigger.",
                this);

            exitCollider.isTrigger = true;
        }

        if (missionManager == null)
        {
            missionManager =
                FindFirstObjectByType<MissionManager>();
        }

        if (playerRoot == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
            }
        }

        beaconMissionCompleted = false;
        playerInsideExitTrigger = false;
        reachedExitAfterMission = false;
        lastPanelVisible = false;
        lastPanelAtExit = false;

        if (guidePanelGroup != null)
        {
            guidePanelGroup.interactable = false;
            guidePanelGroup.blocksRaycasts = false;
            guidePanelGroup.alpha = 0f;
        }
    }

    private void Start()
    {
        if (missionManager == null ||
            playerRoot == null ||
            guidePanelGroup == null ||
            guideTitleText == null ||
            guideDetailText == null)
        {
            Debug.LogWarning(
                "[CargoYardExitGuide] Missing references. " +
                "Check the Inspector.",
                this);
        }
    }

    private void Update()
    {
        // Detect successful completion of the existing Beacon.
        if (!beaconMissionCompleted &&
            missionManager != null &&
            missionManager.CurrentMissionType ==
                MissionType.BeaconActivation &&
            missionManager.CurrentMissionState ==
                MissionState.Completed)
        {
            beaconMissionCompleted = true;

            Debug.Log(
                "[CargoYardExitGuide] Beacon completed. " +
                "Exit guidance enabled.",
                this);
        }

        if (beaconMissionCompleted &&
            playerInsideExitTrigger)
        {
            ConfirmExitReached();
        }

        bool gameplayActive =
            GameManager.Instance != null &&
            GameManager.Instance.IsPlaying;

        bool uiPaused =
            hideWhileGameplayPaused &&
            (
                Time.timeScale <= 0f ||
                UpgradeManager.IsChoosingUpgrade ||
                WeaponModuleSelectionManager.IsChoosingModule
            );

        bool shouldShow =
            beaconMissionCompleted &&
            gameplayActive &&
            !uiPaused;

        if (!shouldShow)
        {
            SetPanelVisible(false);
            return;
        }

        bool refreshText =
            !lastPanelVisible ||
            playerInsideExitTrigger != lastPanelAtExit;

        SetPanelVisible(true);

        if (refreshText &&
            guideTitleText != null &&
            guideDetailText != null)
        {
            lastPanelAtExit = playerInsideExitTrigger;

            guideTitleText.text =
                playerInsideExitTrigger
                    ? arrivedTitle
                    : readyTitle;

            guideDetailText.text =
                playerInsideExitTrigger
                    ? arrivedDetail
                    : readyDetail;
        }
    }

    // =========================================================
    // HUD
    // =========================================================

    private void SetPanelVisible(bool visible)
    {
        if (guidePanelGroup == null)
            return;

        guidePanelGroup.alpha =
            visible ? 1f : 0f;

        guidePanelGroup.interactable = false;
        guidePanelGroup.blocksRaycasts = false;

        lastPanelVisible = visible;
    }

    // =========================================================
    // Player Collider Detection
    // =========================================================

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null || playerRoot == null)
            return false;

        Transform t = other.transform;

        return t == playerRoot ||
               t.IsChildOf(playerRoot);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInsideExitTrigger = true;

        if (beaconMissionCompleted)
            ConfirmExitReached();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInsideExitTrigger = true;

        if (beaconMissionCompleted)
            ConfirmExitReached();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInsideExitTrigger = false;
    }

    // =========================================================
    // Exit Confirmation (No Scene Change Yet)
    // =========================================================

    private void ConfirmExitReached()
    {
        if (reachedExitAfterMission)
            return;

        reachedExitAfterMission = true;

        Debug.Log(
            "[CargoYardExitGuide] Player reached the " +
            "RIGHT exit after completing Beacon Mission. " +
            "Scene transition is not implemented yet.",
            this);
    }

    // =========================================================
    // Scene Debug
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box =
            GetComponent<BoxCollider2D>();

        if (box == null)
            return;

        Gizmos.color =
            new Color(0.2f, 0.85f, 0.85f, 0.95f);

        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.DrawWireCube(
            box.offset,
            box.size
        );

        Gizmos.matrix = previousMatrix;
    }
}
