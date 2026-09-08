using UnityEngine;

[DisallowMultipleComponent]
public class MissionFeedbackObserver :
    MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [SerializeField]
    private MissionManager missionManager;


    // =========================================================
    // Runtime
    // =========================================================

    private MissionState lastObservedState;

    private bool hasInitialState;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        InitializeObservedState();
    }


    private void Update()
    {
        ResolveReferences();


        if (missionManager == null)
        {
            return;
        }


        MissionState currentState =
            missionManager
                .CurrentMissionState;


        if (!hasInitialState)
        {
            lastObservedState =
                currentState;

            hasInitialState =
                true;

            return;
        }


        if (currentState
            == lastObservedState)
        {
            return;
        }


        HandleMissionStateChanged(
            currentState
        );


        lastObservedState =
            currentState;
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                GetComponent<
                    MissionManager
                >();
        }


        if (missionManager == null)
        {
            missionManager =
                FindFirstObjectByType<
                    MissionManager
                >();
        }
    }


    private void InitializeObservedState()
    {
        if (missionManager == null)
        {
            hasInitialState =
                false;

            return;
        }


        lastObservedState =
            missionManager
                .CurrentMissionState;


        hasInitialState =
            true;
    }


    // =========================================================
    // Feedback
    // =========================================================

    private void HandleMissionStateChanged(
        MissionState newState
    )
    {
        if (AudioManager.Instance == null)
        {
            return;
        }


        switch (newState)
        {
            case MissionState.Active:

                AudioManager.Instance
                    .PlayMissionStarted();

                break;


            case MissionState.Completed:

                AudioManager.Instance
                    .PlayMissionCompleted();

                break;


            case MissionState.Failed:

                AudioManager.Instance
                    .PlayMissionFailed();

                break;
        }
    }
}