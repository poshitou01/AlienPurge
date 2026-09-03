using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SafeZoneEdgeIndicator : MonoBehaviour
{
    // =========================================================
    // Gameplay References
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private SafeZoneController
        safeZoneController;

    [SerializeField]
    private PlanetStormController
        planetStormController;

    [SerializeField]
    private StormDamageController
        stormDamageController;

    [Tooltip(
        "必须绑定 Player 逻辑根对象，"
        + "不能绑定 VisualRoot。"
    )]
    [SerializeField]
    private Transform player;


    // =========================================================
    // UI References
    // =========================================================

    [Header("Edge References")]

    [SerializeField]
    private Image topEdge;

    [SerializeField]
    private Image bottomEdge;

    [SerializeField]
    private Image leftEdge;

    [SerializeField]
    private Image rightEdge;


    // =========================================================
    // Colors
    // =========================================================

    [Header("Colors")]

    [SerializeField]
    private Color towardSafeZoneColor =
        new Color(
            0.15f,
            1f,
            0.55f,
            1f
        );

    [SerializeField]
    private Color awayFromSafeZoneColor =
        new Color(
            1f,
            0.18f,
            0.12f,
            1f
        );

    [SerializeField]
    private Color safeColor =
        new Color(
            0.15f,
            1f,
            0.55f,
            1f
        );


    // =========================================================
    // Pulse Settings
    // =========================================================

    [Header("Danger Pulse")]

    [Min(0f)]
    [SerializeField]
    private float pulseSpeed = 3f;

    [Range(0f, 1f)]
    [SerializeField]
    private float warningMinAlpha = 0.06f;

    [Range(0f, 1f)]
    [SerializeField]
    private float warningMaxAlpha = 0.22f;

    [Range(0f, 1f)]
    [SerializeField]
    private float activeMinAlpha = 0.10f;

    [Range(0f, 1f)]
    [SerializeField]
    private float activeMaxAlpha = 0.38f;


    [Header("Safe Feedback")]

    [Range(0f, 1f)]
    [SerializeField]
    private float safeAlpha = 0.16f;


    [Header("Direction Settings")]

    [Tooltip(
        "方向分量低于该值时，"
        + "对应的上下或左右边不显示。"
    )]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float directionDeadZone = 0.12f;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        ConfigureImages();

        HideAllEdges();
    }


    private void Update()
    {
        ResolveReferences();


        if (!CanEvaluate())
        {
            HideAllEdges();
            return;
        }


        StormPhase phase =
            planetStormController
                .CurrentPhase;


        // Calm / Recovery 均不需要导航提示。
        if (phase != StormPhase.Warning
            && phase != StormPhase.Active)
        {
            HideAllEdges();
            return;
        }


        bool isSafe =
            GetPlayerSafeState();


        // -----------------------------------------------------
        // 已进入 Safe Zone：
        // 四条边统一淡绿色常亮。
        // -----------------------------------------------------

        if (isSafe)
        {
            ShowSafeFeedback();
            return;
        }


        // -----------------------------------------------------
        // 玩家仍然处于危险区域：
        // 根据 Player → Safe Zone 的方向
        // 决定四条边的颜色。
        // -----------------------------------------------------

        Vector2 playerPosition =
            player.position;


        Vector2 direction =
            safeZoneController
                .CurrentCenter
            - playerPosition;


        if (direction.sqrMagnitude
            < 0.0001f)
        {
            ShowSafeFeedback();
            return;
        }


        direction.Normalize();


        UpdateDirectionalFeedback(
            direction,
            phase
        );
    }


    private void OnValidate()
    {
        pulseSpeed =
            Mathf.Max(
                0f,
                pulseSpeed
            );


        warningMinAlpha =
            Mathf.Clamp01(
                warningMinAlpha
            );

        warningMaxAlpha =
            Mathf.Clamp(
                warningMaxAlpha,
                warningMinAlpha,
                1f
            );


        activeMinAlpha =
            Mathf.Clamp01(
                activeMinAlpha
            );

        activeMaxAlpha =
            Mathf.Clamp(
                activeMaxAlpha,
                activeMinAlpha,
                1f
            );


        safeAlpha =
            Mathf.Clamp01(
                safeAlpha
            );


        directionDeadZone =
            Mathf.Clamp(
                directionDeadZone,
                0f,
                0.5f
            );
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (safeZoneController == null)
        {
            safeZoneController =
                FindFirstObjectByType<
                    SafeZoneController
                >();
        }


        if (planetStormController == null)
        {
            planetStormController =
                PlanetStormController
                    .Instance;
        }


        if (stormDamageController == null)
        {
            stormDamageController =
                FindFirstObjectByType<
                    StormDamageController
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
    // Validation
    // =========================================================

    private bool CanEvaluate()
    {
        if (safeZoneController == null
            || planetStormController == null
            || player == null)
        {
            return false;
        }


        if (!safeZoneController
                .HasActiveZone)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // Safe State
    // =========================================================

    private bool GetPlayerSafeState()
    {
        if (stormDamageController != null)
        {
            return stormDamageController
                .PlayerCurrentlySafe;
        }


        return safeZoneController
            .IsPositionSafe(
                player.position
            );
    }


    // =========================================================
    // Direction Feedback
    // =========================================================

    private void UpdateDirectionalFeedback(
        Vector2 direction,
        StormPhase phase
    )
    {
        float pulse01 =
            (
                Mathf.Sin(
                    Time.time
                    * pulseSpeed
                )
                + 1f
            )
            * 0.5f;


        float minimumAlpha;
        float maximumAlpha;


        if (phase
            == StormPhase.Active)
        {
            minimumAlpha =
                activeMinAlpha;

            maximumAlpha =
                activeMaxAlpha;
        }
        else
        {
            minimumAlpha =
                warningMinAlpha;

            maximumAlpha =
                warningMaxAlpha;
        }


        float pulseAlpha =
            Mathf.Lerp(
                minimumAlpha,
                maximumAlpha,
                pulse01
            );


        UpdateHorizontalEdges(
            direction.x,
            pulseAlpha
        );


        UpdateVerticalEdges(
            direction.y,
            pulseAlpha
        );
    }


    private void UpdateHorizontalEdges(
        float horizontalDirection,
        float pulseAlpha
    )
    {
        float strength =
            Mathf.Abs(
                horizontalDirection
            );


        if (strength
            <= directionDeadZone)
        {
            SetHidden(
                leftEdge
            );

            SetHidden(
                rightEdge
            );

            return;
        }


        float normalizedStrength =
            Mathf.InverseLerp(
                directionDeadZone,
                1f,
                strength
            );


        float alpha =
            pulseAlpha
            * Mathf.Lerp(
                0.45f,
                1f,
                normalizedStrength
            );


        if (horizontalDirection > 0f)
        {
            // Safe Zone 在右边。
            SetEdge(
                rightEdge,
                towardSafeZoneColor,
                alpha
            );

            SetEdge(
                leftEdge,
                awayFromSafeZoneColor,
                alpha
            );
        }
        else
        {
            // Safe Zone 在左边。
            SetEdge(
                leftEdge,
                towardSafeZoneColor,
                alpha
            );

            SetEdge(
                rightEdge,
                awayFromSafeZoneColor,
                alpha
            );
        }
    }


    private void UpdateVerticalEdges(
        float verticalDirection,
        float pulseAlpha
    )
    {
        float strength =
            Mathf.Abs(
                verticalDirection
            );


        if (strength
            <= directionDeadZone)
        {
            SetHidden(
                topEdge
            );

            SetHidden(
                bottomEdge
            );

            return;
        }


        float normalizedStrength =
            Mathf.InverseLerp(
                directionDeadZone,
                1f,
                strength
            );


        float alpha =
            pulseAlpha
            * Mathf.Lerp(
                0.45f,
                1f,
                normalizedStrength
            );


        if (verticalDirection > 0f)
        {
            // Safe Zone 在上方。
            SetEdge(
                topEdge,
                towardSafeZoneColor,
                alpha
            );

            SetEdge(
                bottomEdge,
                awayFromSafeZoneColor,
                alpha
            );
        }
        else
        {
            // Safe Zone 在下方。
            SetEdge(
                bottomEdge,
                towardSafeZoneColor,
                alpha
            );

            SetEdge(
                topEdge,
                awayFromSafeZoneColor,
                alpha
            );
        }
    }


    // =========================================================
    // Safe Feedback
    // =========================================================

    private void ShowSafeFeedback()
    {
        SetEdge(
            topEdge,
            safeColor,
            safeAlpha
        );

        SetEdge(
            bottomEdge,
            safeColor,
            safeAlpha
        );

        SetEdge(
            leftEdge,
            safeColor,
            safeAlpha
        );

        SetEdge(
            rightEdge,
            safeColor,
            safeAlpha
        );
    }


    // =========================================================
    // Image Helpers
    // =========================================================

    private void ConfigureImages()
    {
        ConfigureImage(
            topEdge
        );

        ConfigureImage(
            bottomEdge
        );

        ConfigureImage(
            leftEdge
        );

        ConfigureImage(
            rightEdge
        );
    }


    private void ConfigureImage(
        Image image
    )
    {
        if (image == null)
        {
            return;
        }


        image.raycastTarget =
            false;
    }


    private void SetEdge(
        Image image,
        Color baseColor,
        float alpha
    )
    {
        if (image == null)
        {
            return;
        }


        Color color =
            baseColor;


        color.a =
            Mathf.Clamp01(
                alpha
            );


        image.color =
            color;
    }


    private void SetHidden(
        Image image
    )
    {
        if (image == null)
        {
            return;
        }


        Color color =
            image.color;

        color.a = 0f;

        image.color =
            color;
    }


    private void HideAllEdges()
    {
        SetHidden(
            topEdge
        );

        SetHidden(
            bottomEdge
        );

        SetHidden(
            leftEdge
        );

        SetHidden(
            rightEdge
        );
    }


    private void OnDisable()
    {
        HideAllEdges();
    }
}