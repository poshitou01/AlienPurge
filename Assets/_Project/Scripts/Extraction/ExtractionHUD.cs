using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class ExtractionHUD :
    MonoBehaviour
{
    // =========================================================
    // Gameplay
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private ExtractionController
        extractionController;


    [SerializeField]
    private Transform player;


    // =========================================================
    // Panel
    // =========================================================

    [Header("Panel")]

    [SerializeField]
    private CanvasGroup panelGroup;


    // =========================================================
    // Text
    // =========================================================

    [Header("Text References")]

    [SerializeField]
    private TMP_Text titleText;


    [SerializeField]
    private TMP_Text objectiveText;


    [SerializeField]
    private TMP_Text distanceText;


    // =========================================================
    // Defense Progress
    // =========================================================

    [Header("Defense Progress")]

    [SerializeField]
    private GameObject
        defenseProgressRoot;


    [SerializeField]
    private Slider
        defenseProgressSlider;


    [SerializeField]
    private TMP_Text
        defenseProgressText;


    // =========================================================
    // Direction
    // =========================================================

    [Header("Direction Arrow")]

    [SerializeField]
    private RectTransform
        arrowRectTransform;


    [Tooltip(
        "Arrow Sprite 默认朝右填0。"
        + "默认朝上通常填-90。"
    )]
    [SerializeField]
    private float spriteAngleOffset;


    [Min(0f)]
    [SerializeField]
    private float hideArrowWithinDistance =
        1.5f;

    [SerializeField]
    private Color normalTitleColor =
    Color.cyan;


    [SerializeField]
    private Color emergencyTitleColor =
        new Color(
            1f,
            0.35f,
            0.1f,
            1f
        );


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        ConfigureUI();

        HidePanel();
    }


    private void Start()
    {
        ResolveReferences();
    }


    private void Update()
    {
        if (extractionController ==
            null
            ||
            player == null)
        {
            HidePanel();

            return;
        }


        if (GameManager.Instance ==
            null
            ||
            !GameManager.Instance
                .IsPlaying)
        {
            HidePanel();

            return;
        }


        switch (
    extractionController
        .CurrentState
)
        {
            case ExtractionState.Available:

                RefreshAvailableHUD();

                break;


            case ExtractionState.Defending:

                RefreshDefendingHUD();

                break;


            case ExtractionState.Emergency:

                RefreshEmergencyHUD();

                break;


            default:

                HidePanel();

                break;
        }
    }


        private void RefreshEmergencyHUD()
    {
        ShowPanel();

        ShowDefenseProgress();

        RefreshDefenseProgress();


        if (titleText != null)
        {
            titleText.text =
                "EMERGENCY EXTRACTION";
        }


        bool playerInside =
            extractionController
                .IsPlayerInsideZone;


        if (playerInside)
        {
            if (objectiveText != null)
            {
                objectiveText.text =
                    "EVACUATE NOW\n"
                    + "HOLD THE ZONE";
            }


            if (distanceText != null)
            {
                distanceText.text =
                    "EMERGENCY // IN ZONE";
            }


            HideArrow();
        }
        else
        {
            if (objectiveText != null)
            {
                objectiveText.text =
                    "EVACUATE NOW\n"
                    + "RETURN TO EXTRACTION ZONE";
            }


            RefreshDistance(
                true
            );
        }
    }

    // =========================================================
    // Available
    // =========================================================

    private void RefreshAvailableHUD()
    {
        ShowPanel();

        HideDefenseProgress();


        if (titleText != null)
        {
            titleText.text =
                "EXTRACTION AVAILABLE";
        }


        if (objectiveText != null)
        {
            if (extractionController
                    .HasFinalRushTriggered)
            {
                objectiveText.text =
                    "FINAL RUSH ACTIVE\n"
                    + "HIGH-RISK CACHE DETECTED";
            }
            else
            {
                objectiveText.text =
                    "Proceed to evacuation zone\n"
                    + "or continue scavenging.";
            }
        }


        RefreshDistance(
            true
        );
    }

    // =========================================================
    // Defending
    // =========================================================

    private void RefreshDefendingHUD()
    {
        ShowPanel();

        ShowDefenseProgress();

        RefreshDefenseProgress();


        bool isLateDefense =
            extractionController
                .ActiveDefensePressure
            ==
            EnemyPressureState
                .ExtractionDefenseLate;


        if (titleText != null)
        {
            titleText.text =
                isLateDefense
                    ? "LATE EXTRACTION INBOUND"
                    : "EXTRACTION INBOUND";
        }


        bool playerInside =
            extractionController
                .IsPlayerInsideZone;


        if (playerInside)
        {
            if (objectiveText != null)
            {
                objectiveText.text =
                    isLateDefense
                        ? "HOLD ZONE // HEAVY PRESSURE"
                        : "HOLD THE EXTRACTION ZONE";
            }


            if (distanceText != null)
            {
                distanceText.text =
                    "IN ZONE";
            }


            HideArrow();
        }
        else
        {
            if (objectiveText != null)
            {
                objectiveText.text =
                    "RETURN TO EXTRACTION ZONE\n"
                    + "PROGRESS PAUSED";
            }


            RefreshDistance(
                true
            );
        }
    }


    // =========================================================
    // Progress
    // =========================================================

    private void RefreshDefenseProgress()
    {
        float progress01 =
            extractionController
                .DefenseProgress01;


        if (defenseProgressSlider !=
            null)
        {
            defenseProgressSlider.value =
                progress01;
        }


        if (defenseProgressText !=
            null)
        {
            defenseProgressText.text =
                "EVAC  "
                +
                extractionController
                    .DefenseTimeRemaining
                    .ToString("0.0")
                +
                "s";
        }
    }


    private void ShowDefenseProgress()
    {
        if (defenseProgressRoot ==
            null)
        {
            return;
        }


        if (!defenseProgressRoot
                .activeSelf)
        {
            defenseProgressRoot.SetActive(
                true
            );
        }
    }


    private void HideDefenseProgress()
    {
        if (defenseProgressRoot ==
            null)
        {
            return;
        }


        if (defenseProgressRoot
                .activeSelf)
        {
            defenseProgressRoot.SetActive(
                false
            );
        }
    }


    // =========================================================
    // Direction / Distance
    // =========================================================

    private void RefreshDistance(
        bool showDirectionArrow
    )
    {
        Vector2 playerPosition =
            player.position;


        Vector2 targetPosition =
            extractionController
                .CurrentExtractionPosition;


        Vector2 direction =
            targetPosition
            -
            playerPosition;


        float distance =
            direction.magnitude;


        if (distanceText != null)
        {
            distanceText.text =
                "DISTANCE  "
                +
                Mathf.CeilToInt(
                    distance
                )
                +
                "m";
        }


        if (!showDirectionArrow
            ||
            arrowRectTransform ==
            null)
        {
            HideArrow();

            return;
        }


        if (distance <=
            hideArrowWithinDistance)
        {
            HideArrow();

            return;
        }


        ShowArrow();


        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            )
            *
            Mathf.Rad2Deg;


        arrowRectTransform
            .localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    angle
                    +
                    spriteAngleOffset
                );
    }


    // =========================================================
    // Panel
    // =========================================================

    private void ShowPanel()
    {
        if (panelGroup == null)
        {
            return;
        }


        panelGroup.alpha =
            1f;


        panelGroup.interactable =
            false;


        panelGroup.blocksRaycasts =
            false;
    }


    private void HidePanel()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha =
                0f;


            panelGroup.interactable =
                false;


            panelGroup.blocksRaycasts =
                false;
        }


        HideDefenseProgress();

        HideArrow();
    }


    // =========================================================
    // Arrow
    // =========================================================

    private void ShowArrow()
    {
        if (arrowRectTransform ==
            null)
        {
            return;
        }


        if (!arrowRectTransform
                .gameObject
                .activeSelf)
        {
            arrowRectTransform
                .gameObject
                .SetActive(
                    true
                );
        }
    }


    private void HideArrow()
    {
        if (arrowRectTransform ==
            null)
        {
            return;
        }


        if (arrowRectTransform
                .gameObject
                .activeSelf)
        {
            arrowRectTransform
                .gameObject
                .SetActive(
                    false
                );
        }
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (extractionController ==
            null)
        {
            extractionController =
                FindFirstObjectByType<
                    ExtractionController
                >();
        }


        if (player == null)
        {
            GameObject playerObject =
                GameObject
                    .FindGameObjectWithTag(
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
        if (panelGroup != null)
        {
            panelGroup.interactable =
                false;


            panelGroup.blocksRaycasts =
                false;
        }


        if (defenseProgressSlider !=
            null)
        {
            defenseProgressSlider.minValue =
                0f;


            defenseProgressSlider.maxValue =
                1f;


            defenseProgressSlider.interactable =
                false;
        }
    }
}