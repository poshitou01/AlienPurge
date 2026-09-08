using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class AlienCoreMissionObjective :
    InteractableBase
{
    // =========================================================
    // Visual References
    // =========================================================

    [Header("Visual References")]

    [Tooltip("Alien Core 尚未被回收时显示的交互标记。")]
    [SerializeField]
    private GameObject interactionMarker;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug")]

    [SerializeField]
    private bool initialized;

    [SerializeField]
    private bool recoveryReported;


    // =========================================================
    // Runtime References
    // =========================================================

    private MissionManager missionManager;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        recoveryReported = false;
    }


    // =========================================================
    // Initialization
    // =========================================================

    public void Initialize(
        MissionManager manager
    )
    {
        missionManager = manager;

        recoveryReported = false;


        initialized =
            missionManager != null;


        if (interactionMarker != null)
        {
            interactionMarker.SetActive(
                true
            );
        }


        if (!initialized)
        {
            Debug.LogError(
                "AlienCoreMissionObjective: "
                + "Initialize 失败，"
                + "MissionManager 为空。",
                this
            );

            return;
        }


        Debug.Log(
            "Alien Core Mission Objective Initialized.",
            this
        );
    }


    // =========================================================
    // Interaction
    // =========================================================

    public override bool CanInteract(
        PlayerInteractor interactor
    )
    {
        if (!base.CanInteract(interactor))
        {
            return false;
        }


        if (!initialized)
        {
            return false;
        }


        if (recoveryReported)
        {
            return false;
        }


        if (missionManager == null)
        {
            return false;
        }


        return missionManager
            .IsMissionActive(
                MissionType.AlienCoreRecovery
            );
    }


    public override void Interact(
        PlayerInteractor interactor
    )
    {
        if (!CanInteract(interactor))
        {
            return;
        }


        // -----------------------------------------------------
        // Recovery Guard
        //
        // 从这一刻起，同一个 Core 不允许再次回收。
        // -----------------------------------------------------

        recoveryReported = true;


        if (interactionMarker != null)
        {
            interactionMarker.SetActive(
                false
            );
        }


        Debug.Log(
            "===== Alien Core Recovered =====",
            this
        );


        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayAlienCoreRecovered();
        }


        missionManager
            .CompleteCurrentMission();
    }
}