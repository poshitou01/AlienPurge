using TMPro;
using UnityEngine;


[DisallowMultipleComponent]
public class HighRiskCacheHUD :
    MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private ExtractionController
        extractionController;


    [SerializeField]
    private Transform player;


    // =========================================================
    // UI
    // =========================================================

    [Header("UI References")]

    [SerializeField]
    private CanvasGroup panelGroup;


    [SerializeField]
    private TMP_Text distanceText;


    [SerializeField]
    private RectTransform
        arrowRectTransform;


    [Header("Direction")]

    [Tooltip(
        "Arrow Sprite 默认朝右填0，"
        + "默认朝上通常填-90。"
    )]
    [SerializeField]
    private float spriteAngleOffset;


    [Tooltip(
        "玩家已经靠近 Cache 到这个距离后，"
        + "认为已经找到位置，HUD永久隐藏。"
    )]
    [Min(0f)]
    [SerializeField]
    private float reachedDistance =
        2.5f;


    // =========================================================
    // Runtime
    // =========================================================

    private bool hasReachedCache;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        HideHUD();
    }


    private void Update()
    {
        if (extractionController == null
            ||
            player == null)
        {
            HideHUD();

            return;
        }


        if (GameManager.Instance == null
            ||
            !GameManager.Instance
                .IsPlaying)
        {
            HideHUD();

            return;
        }


        if (!extractionController
                .HasHighRiskCacheSpawned)
        {
            HideHUD();

            return;
        }


        if (hasReachedCache)
        {
            HideHUD();

            return;
        }


        RefreshDirection();
    }


    // =========================================================
    // Direction
    // =========================================================

    private void RefreshDirection()
    {
        Vector2 playerPosition =
            player.position;


        Vector2 cachePosition =
            extractionController
                .CurrentHighRiskCachePosition;


        Vector2 direction =
            cachePosition
            -
            playerPosition;


        float distance =
            direction.magnitude;


        // -----------------------------------------------------
        // 玩家已经真正找到 Cache。
        //
        // 后面依靠世界里的橙色 Beam 即可，
        // 不继续占HUD。
        // -----------------------------------------------------

        if (distance <=
            reachedDistance)
        {
            hasReachedCache =
                true;


            HideHUD();

            return;
        }


        ShowHUD();


        if (distanceText != null)
        {
            distanceText.text =
                "HIGH-RISK CACHE  "
                + Mathf.CeilToInt(
                    distance
                )
                + "m";
        }


        if (arrowRectTransform == null)
        {
            return;
        }


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
    // Visibility
    // =========================================================

    private void ShowHUD()
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


    private void HideHUD()
    {
        if (panelGroup == null)
        {
            return;
        }


        panelGroup.alpha =
            0f;


        panelGroup.interactable =
            false;


        panelGroup.blocksRaycasts =
            false;
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
}