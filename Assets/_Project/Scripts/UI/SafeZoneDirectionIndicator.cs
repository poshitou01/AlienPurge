using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class SafeZoneDirectionIndicator
    : MonoBehaviour
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


    // =========================================================
    // UI References
    // =========================================================

    [Header("UI References")]

    [SerializeField]
    private RectTransform
        indicatorRect;

    [SerializeField]
    private TMP_Text
        indicatorText;

    [SerializeField]
    private RectTransform
        canvasRect;


    // =========================================================
    // Screen Settings
    // =========================================================

    [Header("Screen Edge Settings")]

    [Tooltip(
        "箭头与屏幕边缘之间保留的距离。"
    )]
    [Min(0f)]
    [SerializeField]
    private float screenPadding = 70f;


    // =========================================================
    // Runtime
    // =========================================================

    private Camera mainCamera;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        HideIndicator();
    }


    private void Update()
    {
        ResolveReferences();


        if (!CanShowIndicator())
        {
            HideIndicator();
            return;
        }


        Vector3 safeZoneWorldPosition =
            new Vector3(
                safeZoneController
                    .CurrentCenter.x,

                safeZoneController
                    .CurrentCenter.y,

                0f
            );


        Vector3 viewportPosition =
            mainCamera
                .WorldToViewportPoint(
                    safeZoneWorldPosition
                );


        // -----------------------------------------------------
        // Safe Zone Center 已经在当前屏幕中。
        // -----------------------------------------------------

        bool isOnScreen =
            viewportPosition.z > 0f
            && viewportPosition.x >= 0f
            && viewportPosition.x <= 1f
            && viewportPosition.y >= 0f
            && viewportPosition.y <= 1f;


        if (isOnScreen)
        {
            HideIndicator();
            return;
        }


        ShowIndicator();


        UpdateIndicatorPosition(
            viewportPosition
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


        if (indicatorRect == null)
        {
            indicatorRect =
                GetComponent<
                    RectTransform
                >();
        }


        if (indicatorText == null)
        {
            indicatorText =
                GetComponent<
                    TMP_Text
                >();
        }


        if (canvasRect == null)
        {
            Canvas canvas =
                GetComponentInParent<
                    Canvas
                >();

            if (canvas != null)
            {
                canvasRect =
                    canvas.GetComponent<
                        RectTransform
                    >();
            }
        }


        if (mainCamera == null)
        {
            mainCamera =
                Camera.main;
        }
    }


    // =========================================================
    // Visibility Rules
    // =========================================================

    private bool CanShowIndicator()
    {
        if (safeZoneController == null
            || planetStormController == null
            || mainCamera == null
            || indicatorRect == null
            || canvasRect == null)
        {
            return false;
        }


        if (!safeZoneController
                .HasActiveZone)
        {
            return false;
        }


        StormPhase phase =
            planetStormController
                .CurrentPhase;


        return phase
                == StormPhase.Warning
            || phase
                == StormPhase.Active;
    }


    // =========================================================
    // Position / Rotation
    // =========================================================

    private void UpdateIndicatorPosition(
        Vector3 viewportPosition
    )
    {
        // -----------------------------------------------------
        // WorldToViewportPoint 在目标位于摄像机后方时
        // 会产生反向坐标。
        //
        // 2D 游戏通常不会真正发生这种情况，
        // 但这里仍做保护。
        // -----------------------------------------------------

        if (viewportPosition.z < 0f)
        {
            viewportPosition.x =
                1f - viewportPosition.x;

            viewportPosition.y =
                1f - viewportPosition.y;
        }


        Vector2 direction =
            new Vector2(
                viewportPosition.x - 0.5f,
                viewportPosition.y - 0.5f
            );


        if (direction.sqrMagnitude
            < 0.0001f)
        {
            direction =
                Vector2.up;
        }


        direction.Normalize();


        // -----------------------------------------------------
        // Canvas 实际半宽 / 半高。
        //
        // 因为我们使用 HUDCanvas 的 RectTransform，
        // 所以这里天然适配 CanvasScaler，
        // 而不是硬编码 1920 × 1080。
        // -----------------------------------------------------

        float halfWidth =
            canvasRect.rect.width
            * 0.5f
            - screenPadding;


        float halfHeight =
            canvasRect.rect.height
            * 0.5f
            - screenPadding;


        halfWidth =
            Mathf.Max(
                0f,
                halfWidth
            );

        halfHeight =
            Mathf.Max(
                0f,
                halfHeight
            );


        // -----------------------------------------------------
        // 从屏幕中心沿目标方向发射一条射线，
        // 求它与屏幕安全矩形边缘的交点。
        // -----------------------------------------------------

        float scaleX =
            Mathf.Abs(direction.x)
            > 0.0001f
                ? halfWidth
                    / Mathf.Abs(
                        direction.x
                    )
                : float.PositiveInfinity;


        float scaleY =
            Mathf.Abs(direction.y)
            > 0.0001f
                ? halfHeight
                    / Mathf.Abs(
                        direction.y
                    )
                : float.PositiveInfinity;


        float edgeScale =
            Mathf.Min(
                scaleX,
                scaleY
            );


        Vector2 anchoredPosition =
            direction
            * edgeScale;


        indicatorRect
            .anchoredPosition =
                anchoredPosition;


        // -----------------------------------------------------
        // TMP 的 ▲ 默认指向上方。
        //
        // atan2 算出来的是：
        // 右 = 0°
        //
        // 所以减 90°，
        // 让箭头顶部对准 direction。
        // -----------------------------------------------------

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            )
            * Mathf.Rad2Deg
            - 90f;


        indicatorRect.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );
    }


    // =========================================================
    // Show / Hide
    // =========================================================

    private void ShowIndicator()
    {
        if (indicatorText != null)
        {
            indicatorText.enabled =
                true;
        }
    }


    private void HideIndicator()
    {
        if (indicatorText != null)
        {
            indicatorText.enabled =
                false;
        }
    }


    private void OnDisable()
    {
        HideIndicator();
    }
}