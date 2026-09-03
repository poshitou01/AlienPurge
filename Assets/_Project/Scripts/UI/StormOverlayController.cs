using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StormOverlayController : MonoBehaviour
{
    // =========================================================
    // Gameplay References
    // =========================================================

    [Header("Gameplay References")]

    [Tooltip("提供当前 Storm Phase 和阶段剩余时间。")]
    [SerializeField]
    private PlanetStormController planetStormController;

    [Tooltip("提供玩家当前是否处于 Safe Zone 内。")]
    [SerializeField]
    private StormDamageController stormDamageController;


    // =========================================================
    // UI References
    // =========================================================

    [Header("UI References")]

    [Tooltip("整个风暴期间使用的轻微环境冷色遮罩。")]
    [SerializeField]
    private Image stormOverlay;

    [Tooltip("仅在 Active + DANGER 时使用的轻微红色危险遮罩。")]
    [SerializeField]
    private Image dangerOverlay;


    // =========================================================
    // Storm Overlay Settings
    // =========================================================

    [Header("Storm Overlay Settings")]

    [Tooltip("Warning 阶段 Storm Overlay 的透明度。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float warningStormAlpha = 0.04f;

    [Tooltip("Active 阶段 Storm Overlay 的透明度。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float activeStormAlpha = 0.08f;


    // =========================================================
    // Danger Overlay Settings
    // =========================================================

    [Header("Danger Overlay Settings")]

    [Tooltip("Active + DANGER 时红色呼吸效果的最低透明度。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float dangerMinAlpha = 0.02f;

    [Tooltip("Active + DANGER 时红色呼吸效果的最高透明度。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float dangerMaxAlpha = 0.08f;

    [Tooltip("危险红色 Overlay 的呼吸速度。")]
    [Min(0f)]
    [SerializeField]
    private float dangerPulseSpeed = 2.5f;


    // =========================================================
    // Runtime
    // =========================================================

    private StormPhase lastPhase =
        (StormPhase)(-1);

    private float recoveryInitialTime;

    private float recoveryStartStormAlpha;

    private Color stormBaseColor;

    private Color dangerBaseColor;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        CacheBaseColors();

        ConfigureImages();

        HideAllOverlays();
    }


    private void Update()
    {
        ResolveReferences();


        if (planetStormController == null)
        {
            HideAllOverlays();
            return;
        }


        StormPhase currentPhase =
            planetStormController.CurrentPhase;


        HandlePhaseChange(
            currentPhase
        );


        switch (currentPhase)
        {
            case StormPhase.Calm:

                UpdateCalm();

                break;


            case StormPhase.Warning:

                UpdateWarning();

                break;


            case StormPhase.Active:

                UpdateActive();

                break;


            case StormPhase.Recovery:

                UpdateRecovery();

                break;
        }


        lastPhase =
            currentPhase;
    }


    private void OnValidate()
    {
        warningStormAlpha =
            Mathf.Clamp01(
                warningStormAlpha
            );


        activeStormAlpha =
            Mathf.Clamp01(
                activeStormAlpha
            );


        dangerMinAlpha =
            Mathf.Clamp01(
                dangerMinAlpha
            );


        dangerMaxAlpha =
            Mathf.Clamp(
                dangerMaxAlpha,
                dangerMinAlpha,
                1f
            );


        dangerPulseSpeed =
            Mathf.Max(
                0f,
                dangerPulseSpeed
            );
    }


    private void OnDisable()
    {
        HideAllOverlays();
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (planetStormController == null)
        {
            planetStormController =
                PlanetStormController.Instance;
        }


        if (stormDamageController == null)
        {
            stormDamageController =
                FindFirstObjectByType<
                    StormDamageController
                >();
        }
    }


    // =========================================================
    // Initialization
    // =========================================================

    private void CacheBaseColors()
    {
        if (stormOverlay != null)
        {
            stormBaseColor =
                stormOverlay.color;
        }
        else
        {
            stormBaseColor =
                Color.white;
        }


        if (dangerOverlay != null)
        {
            dangerBaseColor =
                dangerOverlay.color;
        }
        else
        {
            dangerBaseColor =
                Color.red;
        }
    }


    private void ConfigureImages()
    {
        if (stormOverlay != null)
        {
            stormOverlay.raycastTarget =
                false;
        }


        if (dangerOverlay != null)
        {
            dangerOverlay.raycastTarget =
                false;
        }
    }


    // =========================================================
    // Phase Change
    // =========================================================

    private void HandlePhaseChange(
        StormPhase currentPhase
    )
    {
        if (currentPhase == lastPhase)
        {
            return;
        }


        // -----------------------------------------------------
        // Recovery 开始时，
        // 记录这一刻真正的剩余时间和当前 Storm Alpha。
        //
        // 这样 Overlay 不需要自己再保存一份
        // Recovery Duration = 2。
        // -----------------------------------------------------

        if (currentPhase
            == StormPhase.Recovery)
        {
            recoveryInitialTime =
                Mathf.Max(
                    0.01f,
                    planetStormController
                        .PhaseTimeRemaining
                );


            if (stormOverlay != null)
            {
                recoveryStartStormAlpha =
                    stormOverlay.color.a;
            }
            else
            {
                recoveryStartStormAlpha =
                    activeStormAlpha;
            }


            // Recovery 阶段不再显示危险红色。
            SetDangerOverlayAlpha(
                0f
            );
        }
    }


    // =========================================================
    // Calm
    // =========================================================

    private void UpdateCalm()
    {
        SetStormOverlayAlpha(
            0f
        );

        SetDangerOverlayAlpha(
            0f
        );
    }


    // =========================================================
    // Warning
    // =========================================================

    private void UpdateWarning()
    {
        // Warning 只有轻微环境冷色。
        SetStormOverlayAlpha(
            warningStormAlpha
        );


        // Warning 不使用红色 Danger Overlay。
        //
        // 玩家方向和安全状态已经由：
        // StormHUD + SafeZoneEdgeIndicator
        // 提供。
        SetDangerOverlayAlpha(
            0f
        );
    }


    // =========================================================
    // Active
    // =========================================================

    private void UpdateActive()
    {
        // Active 整体环境比 Warning 稍强。
        SetStormOverlayAlpha(
            activeStormAlpha
        );


        // 如果没有 DamageController，
        // 为了避免错误制造危险视觉，
        // 默认不显示红色 Overlay。
        if (stormDamageController == null)
        {
            SetDangerOverlayAlpha(
                0f
            );

            return;
        }


        bool playerIsSafe =
            stormDamageController
                .PlayerCurrentlySafe;


        // -----------------------------------------------------
        // Active + SAFE
        // -----------------------------------------------------

        if (playerIsSafe)
        {
            SetDangerOverlayAlpha(
                0f
            );

            return;
        }


        // -----------------------------------------------------
        // Active + DANGER
        //
        // 使用缩放时间 Time.time，
        // 所以 Pause 时 Pulse 会自然冻结。
        // -----------------------------------------------------

        float pulse =
            (
                Mathf.Sin(
                    Time.time
                    * dangerPulseSpeed
                )
                + 1f
            )
            * 0.5f;


        float dangerAlpha =
            Mathf.Lerp(
                dangerMinAlpha,
                dangerMaxAlpha,
                pulse
            );


        SetDangerOverlayAlpha(
            dangerAlpha
        );
    }


    // =========================================================
    // Recovery
    // =========================================================

    private void UpdateRecovery()
    {
        // Recovery 不再显示红色危险 Overlay。
        SetDangerOverlayAlpha(
            0f
        );


        if (recoveryInitialTime
            <= 0f)
        {
            SetStormOverlayAlpha(
                0f
            );

            return;
        }


        // -----------------------------------------------------
        // PhaseTimeRemaining：
        //
        // Recovery 开始约为 2
        // ↓
        // 最后变为 0
        //
        // 因此 fade：
        //
        // 1 → 0
        // -----------------------------------------------------

        float fade =
            Mathf.Clamp01(
                planetStormController
                    .PhaseTimeRemaining
                / recoveryInitialTime
            );


        float stormAlpha =
            recoveryStartStormAlpha
            * fade;


        SetStormOverlayAlpha(
            stormAlpha
        );
    }


    // =========================================================
    // Overlay Helpers
    // =========================================================

    private void SetStormOverlayAlpha(
        float alpha
    )
    {
        if (stormOverlay == null)
        {
            return;
        }


        Color color =
            stormBaseColor;


        color.a =
            Mathf.Clamp01(
                alpha
            );


        stormOverlay.color =
            color;
    }


    private void SetDangerOverlayAlpha(
        float alpha
    )
    {
        if (dangerOverlay == null)
        {
            return;
        }


        Color color =
            dangerBaseColor;


        color.a =
            Mathf.Clamp01(
                alpha
            );


        dangerOverlay.color =
            color;
    }


    private void HideAllOverlays()
    {
        SetStormOverlayAlpha(
            0f
        );

        SetDangerOverlayAlpha(
            0f
        );
    }
}