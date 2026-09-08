using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionHUD :
    MonoBehaviour
{
    // =========================================================
    // Gameplay References
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private MissionManager missionManager;

    [SerializeField]
    private Transform player;


    // =========================================================
    // Panel
    // =========================================================

    [Header("Panel")]

    [SerializeField]
    private CanvasGroup missionPanelGroup;


    // =========================================================
    // Text
    // =========================================================

    [Header("Text References")]

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text objectiveText;

    [SerializeField]
    private TMP_Text timerText;

    [SerializeField]
    private TMP_Text distanceText;

    [SerializeField]
    private TMP_Text rewardText;

    [SerializeField]
    private TMP_Text riskText;


    // =========================================================
    // Progress
    // =========================================================

    [Header("Progress References")]

    [SerializeField]
    private GameObject progressRoot;

    [SerializeField]
    private Slider progressSlider;

    [SerializeField]
    private TMP_Text progressText;


    // =========================================================
    // Display Colors
    // =========================================================

    [Header("Display Colors")]

    [SerializeField]
    private Color normalColor =
        Color.white;

    [SerializeField]
    private Color riskColor =
        new Color(
            1f,
            0.55f,
            0.15f,
            1f
        );


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        ConfigureUI();

        HidePanel();
    }


    private void Update()
    {
        ResolveReferences();


        if (missionManager == null
            || !missionManager.HasActiveMission)
        {
            HidePanel();
            return;
        }


        ShowPanel();


        RefreshMissionIdentity();

        RefreshTimer();

        RefreshDistance();

        RefreshProgress();

        RefreshRewardAndRisk();
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                FindFirstObjectByType<
                    MissionManager
                >();
        }


        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );


            if (playerObject != null)
            {
                player =
                    playerObject.transform;
            }
        }
    }


    // =========================================================
    // Setup
    // =========================================================

    private void ConfigureUI()
    {
        if (missionPanelGroup != null)
        {
            missionPanelGroup.interactable =
                false;

            missionPanelGroup.blocksRaycasts =
                false;
        }


        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;

            progressSlider.maxValue = 1f;

            progressSlider.interactable =
                false;
        }
    }


    // =========================================================
    // Panel
    // =========================================================

    private void ShowPanel()
    {
        if (missionPanelGroup == null)
        {
            return;
        }


        missionPanelGroup.alpha = 1f;

        missionPanelGroup.interactable =
            false;

        missionPanelGroup.blocksRaycasts =
            false;
    }


    private void HidePanel()
    {
        if (missionPanelGroup == null)
        {
            return;
        }


        missionPanelGroup.alpha = 0f;

        missionPanelGroup.interactable =
            false;

        missionPanelGroup.blocksRaycasts =
            false;
    }


    // =========================================================
    // Mission Identity
    // =========================================================

    private void RefreshMissionIdentity()
    {
        switch (
            missionManager.CurrentMissionType
        )
        {
            case MissionType.BeaconActivation:

                if (titleText != null)
                {
                    titleText.text =
                        "MISSION // SIGNAL BEACON";
                }


                if (objectiveText != null)
                {
                    objectiveText.text =
                        "Activate and secure the beacon";
                }

                break;


            case MissionType.AlienCoreRecovery:

                if (titleText != null)
                {
                    titleText.text =
                        "MISSION // ALIEN CORE";
                }


                if (objectiveText != null)
                {
                    objectiveText.text =
                        "Recover the alien core";
                }

                break;
        }
    }


    // =========================================================
    // Timer
    // =========================================================

    private void RefreshTimer()
    {
        if (timerText == null)
        {
            return;
        }


        int remainingSeconds =
            Mathf.CeilToInt(
                Mathf.Max(
                    0f,
                    missionManager
                        .MissionTimeRemaining
                )
            );


        timerText.text =
            "TIME  "
            + remainingSeconds
                .ToString("00");
    }


    // =========================================================
    // Distance
    // =========================================================

    private void RefreshDistance()
    {
        if (distanceText == null)
        {
            return;
        }


        if (player == null)
        {
            distanceText.text =
                "DISTANCE  --";

            return;
        }


        float distance =
            Vector2.Distance(
                player.position,
                missionManager
                    .CurrentObjectivePosition
            );


        distanceText.text =
            "DISTANCE  "
            + Mathf.CeilToInt(distance)
            + "m";
    }


    // =========================================================
    // Progress
    // =========================================================

    private void RefreshProgress()
    {
        bool showProgress =
            missionManager
                .CurrentMissionType
            == MissionType
                .BeaconActivation;


        if (progressRoot != null)
        {
            progressRoot.SetActive(
                showProgress
            );
        }


        if (!showProgress)
        {
            return;
        }


        float progress =
            Mathf.Clamp01(
                missionManager
                    .MissionProgress01
            );


        if (progressSlider != null)
        {
            progressSlider.value =
                progress;
        }


        if (progressText != null)
        {
            int percent =
                Mathf.RoundToInt(
                    progress * 100f
                );


            progressText.text =
                percent + "%";
        }
    }


    // =========================================================
    // Reward / Risk
    // =========================================================

    private void RefreshRewardAndRisk()
    {
        bool hasRiskBonus =
            missionManager
                .CurrentMissionHasStormRiskBonus;


        int reward =
            missionManager
                .CurrentPotentialRewardExp;


        if (rewardText != null)
        {
            rewardText.text =
                "REWARD  +"
                + reward
                + " EXP";


            rewardText.color =
                hasRiskBonus
                    ? riskColor
                    : normalColor;
        }


        if (riskText != null)
        {
            if (hasRiskBonus)
            {
                riskText.text =
                    "RISK  STORM BONUS x"
                    + missionManager
                        .StormRiskRewardMultiplier
                        .ToString("0.0");


                riskText.color =
                    riskColor;
            }
            else
            {
                riskText.text =
                    "RISK  NORMAL";


                riskText.color =
                    normalColor;
            }
        }
    }
}