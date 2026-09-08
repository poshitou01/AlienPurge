using UnityEngine;

[DisallowMultipleComponent]
public class MissionDirectionArrow :
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
    // UI References
    // =========================================================

    [Header("UI References")]

    [SerializeField]
    private RectTransform arrowRectTransform;


    // =========================================================
    // Settings
    // =========================================================

    [Header("Direction Settings")]

    [Tooltip(
        "如果 Arrow Sprite 默认朝右，填写 0。"
        + "如果默认朝上，通常填写 -90。"
    )]
    [SerializeField]
    private float spriteAngleOffset = 0f;


    [Tooltip(
        "距离目标非常近时隐藏箭头，"
        + "避免箭头在玩家已经到达时抖动。"
    )]
    [Min(0f)]
    [SerializeField]
    private float hideWithinDistance = 1.5f;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        HideArrow();
    }


    private void Update()
    {
        ResolveReferences();


        if (missionManager == null
            || player == null
            || arrowRectTransform == null)
        {
            HideArrow();
            return;
        }


        if (!missionManager.HasActiveMission)
        {
            HideArrow();
            return;
        }


        Vector2 playerPosition =
            player.position;


        Vector2 objectivePosition =
            missionManager
                .CurrentObjectivePosition;


        Vector2 direction =
            objectivePosition
            - playerPosition;


        float distanceSquared =
            direction.sqrMagnitude;


        float hideDistanceSquared =
            hideWithinDistance
            * hideWithinDistance;


        if (distanceSquared
            <= hideDistanceSquared)
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
            * Mathf.Rad2Deg;


        arrowRectTransform
            .localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    angle
                    + spriteAngleOffset
                );
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
    // Visibility
    // =========================================================

    private void ShowArrow()
    {
        if (arrowRectTransform == null)
        {
            return;
        }


        if (!arrowRectTransform
            .gameObject.activeSelf)
        {
            arrowRectTransform
                .gameObject
                .SetActive(true);
        }
    }


    private void HideArrow()
    {
        if (arrowRectTransform == null)
        {
            return;
        }


        if (arrowRectTransform
            .gameObject.activeSelf)
        {
            arrowRectTransform
                .gameObject
                .SetActive(false);
        }
    }
}