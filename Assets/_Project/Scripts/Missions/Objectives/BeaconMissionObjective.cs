using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BeaconMissionObjective :
    InteractableBase
{
    // =========================================================
    // Activation Settings
    // =========================================================

    [Header("Beacon Activation")]

    [Tooltip("玩家开始 Beacon 激活后，需要停留在这个半径内。")]
    [Min(0.1f)]
    [SerializeField]
    private float activationRadius = 3f;

    [Tooltip("玩家累计需要在激活区域内停留多久。")]
    [Min(0.1f)]
    [SerializeField]
    private float activationDuration = 8f;


    // =========================================================
    // Visual References
    // =========================================================

    [Header("Visual References")]

    [Tooltip("用于显示 Beacon 激活范围的 LineRenderer。")]
    [SerializeField]
    private LineRenderer activationRangeVisual;

    [Tooltip("Beacon 尚未启动时显示的交互标记。")]
    [SerializeField]
    private GameObject interactionMarker;

    [SerializeField]
    private BeaconVisualFeedback
    visualFeedback;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug")]

    [SerializeField]
    private bool initialized;

    [SerializeField]
    private bool activationStarted;

    [SerializeField]
    private bool playerInsideActivationRadius;

    [SerializeField]
    private float activationElapsed;

    [Range(0f, 1f)]
    [SerializeField]
    private float activationProgress01;

    [SerializeField]
    private bool completionReported;


    // =========================================================
    // Runtime References
    // =========================================================

    private MissionManager missionManager;

    private Transform player;


    // =========================================================
    // Public Read Only State
    // =========================================================

    public bool ActivationStarted =>
        activationStarted;


    public bool PlayerInsideActivationRadius =>
        playerInsideActivationRadius;


    public float ActivationProgress01 =>
        activationProgress01;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ConfigureActivationRangeVisual();
        if (visualFeedback == null)
        {
            visualFeedback =
                GetComponent<
                    BeaconVisualFeedback
                >();
        }
    }


    private void Update()
    {
        if (!initialized)
        {
            return;
        }


        if (!activationStarted)
        {
            return;
        }


        if (completionReported)
        {
            return;
        }


        if (missionManager == null
            || !missionManager.IsMissionActive(
                MissionType.BeaconActivation
            ))
        {
            return;
        }


        if (!CanAdvanceActivation())
        {
            return;
        }


        UpdatePlayerInsideState();

        if (visualFeedback != null)
        {
            visualFeedback
                .SetPlayerInside(
                    playerInsideActivationRadius
                );
        }


        if (!playerInsideActivationRadius)
        {
            // 离开范围：
            //
            // 只暂停进度。
            // 不清零，也不倒退。
            return;
        }


        activationElapsed +=
            Time.deltaTime;


        activationElapsed =
            Mathf.Min(
                activationElapsed,
                activationDuration
            );


        activationProgress01 =
            Mathf.Clamp01(
                activationElapsed
                / activationDuration
            );


        missionManager
            .SetMissionProgress01(
                activationProgress01
            );


        if (activationProgress01 < 1f)
        {
            return;
        }


        completionReported = true;


        missionManager
            .CompleteCurrentMission();
    }


    private void OnValidate()
    {
        activationRadius =
            Mathf.Max(
                0.1f,
                activationRadius
            );


        activationDuration =
            Mathf.Max(
                0.1f,
                activationDuration
            );
    }


    // =========================================================
    // Initialization
    // =========================================================

    public void Initialize(
        MissionManager manager,
        Transform playerTransform
    )
    {
        missionManager =
            manager;

        player =
            playerTransform;


        activationStarted = false;

        playerInsideActivationRadius = false;

        activationElapsed = 0f;

        activationProgress01 = 0f;

        completionReported = false;


        initialized =
            missionManager != null
            && player != null;


        if (interactionMarker != null)
        {
            interactionMarker.SetActive(
                true
            );
        }

        if (visualFeedback != null)
        {
            visualFeedback
                .SetWaiting();
        }


        ConfigureActivationRangeVisual();


        if (!initialized)
        {
            Debug.LogError(
                "BeaconMissionObjective: "
                + "Initialize 失败。"
                + "\nMissionManager 或 Player 为空。",
                this
            );

            return;
        }


        Debug.Log(
            "Beacon Mission Objective Initialized."
            + "\nActivation Radius: "
            + activationRadius.ToString("F2")
            + "\nActivation Duration: "
            + activationDuration.ToString("F2")
            + "s",
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


        if (activationStarted)
        {
            return false;
        }


        if (completionReported)
        {
            return false;
        }


        if (missionManager == null)
        {
            return false;
        }


        return missionManager
            .IsMissionActive(
                MissionType.BeaconActivation
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


        activationStarted = true;

        if (visualFeedback != null)
        {
            visualFeedback
                .SetActivated(true);
        }


        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayBeaconActivated();
        }

        activationElapsed = 0f;

        activationProgress01 = 0f;


        missionManager
            .SetMissionProgress01(0f);


        if (interactionMarker != null)
        {
            interactionMarker.SetActive(
                false
            );
        }


        Debug.Log(
            "===== Beacon Activation Started ====="
            + "\nActivation Radius: "
            + activationRadius.ToString("F2")
            + "\nRequired Time: "
            + activationDuration.ToString("F2")
            + "s",
            this
        );
    }


    // =========================================================
    // Activation Runtime
    // =========================================================

    private bool CanAdvanceActivation()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }


        if (!GameManager.Instance.IsPlaying)
        {
            return false;
        }


        // Pause / Upgrade / Module / Overclock
        // 都会冻结 timeScale。
        if (Time.timeScale <= 0f)
        {
            return false;
        }


        if (player == null)
        {
            return false;
        }


        return true;
    }


    private void UpdatePlayerInsideState()
    {
        Vector2 offset =
            (Vector2)player.position
            - (Vector2)transform.position;


        float distanceSquared =
            offset.sqrMagnitude;


        float radiusSquared =
            activationRadius
            * activationRadius;


        playerInsideActivationRadius =
            distanceSquared
            <= radiusSquared;
    }


    // =========================================================
    // Range Visual
    // =========================================================

    private void ConfigureActivationRangeVisual()
    {
        if (activationRangeVisual == null)
        {
            return;
        }


        const int segmentCount = 64;


        activationRangeVisual
            .useWorldSpace = false;

        activationRangeVisual
            .loop = true;

        activationRangeVisual
            .positionCount = segmentCount;


        for (int i = 0;
             i < segmentCount;
             i++)
        {
            float normalized =
                i
                / (float)segmentCount;


            float angle =
                normalized
                * Mathf.PI
                * 2f;


            Vector3 position =
                new Vector3(
                    Mathf.Cos(angle)
                        * activationRadius,

                    Mathf.Sin(angle)
                        * activationRadius,

                    0f
                );


            activationRangeVisual
                .SetPosition(
                    i,
                    position
                );
        }
    }


    // =========================================================
    // Debug Visual
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            activationRadius
        );
    }
}