using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class StormHUD : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private PlanetStormController
        planetStormController;

    [SerializeField]
    private SafeZoneController
        safeZoneController;

    [SerializeField]
    private StormDamageController
        stormDamageController;


    [Header("UI References")]

    [SerializeField]
    private CanvasGroup stormPanelGroup;

    [SerializeField]
    private TMP_Text stormTitleText;

    [SerializeField]
    private TMP_Text stormTimerText;

    [SerializeField]
    private TMP_Text safeStatusText;


    // =========================================================
    // Display Settings
    // =========================================================

    [Header("Display Settings")]

    [SerializeField]
    private Color safeColor =
        new Color(
            0.25f,
            1f,
            0.65f,
            1f
        );

    [SerializeField]
    private Color dangerColor =
        new Color(
            1f,
            0.30f,
            0.25f,
            1f
        );

    [SerializeField]
    private Color warningColor =
        new Color(
            1f,
            0.78f,
            0.20f,
            1f
        );


    // =========================================================
    // Runtime Cache
    // =========================================================

    private StormPhase
        lastDisplayedPhase =
            (StormPhase)(-1);

    private int
        lastDisplayedTenths =
            -1;

    private bool
        lastDisplayedSafeState;

    private bool
        hasDisplayedSafeState;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        ConfigureCanvasGroup();

        HidePanel();
    }


    private void Update()
    {
        ResolveReferences();


        if (planetStormController == null)
        {
            HidePanel();
            return;
        }


        StormPhase currentPhase =
            planetStormController
                .CurrentPhase;


        // Calm 阶段完全隐藏。
        if (currentPhase
            == StormPhase.Calm)
        {
            HidePanel();

            lastDisplayedPhase =
                currentPhase;

            return;
        }


        ShowPanel();


        // Phase 改变时，
        // 更新不会每帧变化的标题。
        if (currentPhase
            != lastDisplayedPhase)
        {
            RefreshPhaseDisplay(
                currentPhase
            );

            lastDisplayedPhase =
                currentPhase;

            // 强制下一次刷新 Timer。
            lastDisplayedTenths =
                -1;
        }


        RefreshTimerIfNeeded();

        RefreshSafeStatusIfNeeded();
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (planetStormController == null)
        {
            planetStormController =
                PlanetStormController
                    .Instance;
        }


        if (safeZoneController == null)
        {
            safeZoneController =
                FindFirstObjectByType<
                    SafeZoneController
                >();
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
    // Panel
    // =========================================================

    private void ConfigureCanvasGroup()
    {
        if (stormPanelGroup == null)
        {
            return;
        }


        // Storm HUD 永远不能拦截鼠标。
        stormPanelGroup.interactable =
            false;

        stormPanelGroup.blocksRaycasts =
            false;
    }


    private void ShowPanel()
    {
        if (stormPanelGroup == null)
        {
            return;
        }


        stormPanelGroup.alpha =
            1f;

        stormPanelGroup.interactable =
            false;

        stormPanelGroup.blocksRaycasts =
            false;
    }


    private void HidePanel()
    {
        if (stormPanelGroup == null)
        {
            return;
        }


        stormPanelGroup.alpha =
            0f;

        stormPanelGroup.interactable =
            false;

        stormPanelGroup.blocksRaycasts =
            false;
    }


    // =========================================================
    // Phase Display
    // =========================================================

    private void RefreshPhaseDisplay(
        StormPhase phase
    )
    {
        if (stormTitleText == null)
        {
            return;
        }


        switch (phase)
        {
            case StormPhase.Warning:

                stormTitleText.text =
                    "PLANET STORM INCOMING";

                stormTitleText.color =
                    warningColor;

                break;


            case StormPhase.Active:

                stormTitleText.text =
                    "PLANET STORM";

                stormTitleText.color =
                    dangerColor;

                break;


            case StormPhase.Recovery:

                stormTitleText.text =
                    "STORM DISSIPATING";

                stormTitleText.color =
                    safeColor;

                break;
        }
    }


    // =========================================================
    // Timer
    // =========================================================

    private void RefreshTimerIfNeeded()
    {
        if (stormTimerText == null)
        {
            return;
        }


        float remaining =
            Mathf.Max(
                0f,
                planetStormController
                    .PhaseTimeRemaining
            );


        // 只显示到 0.1 秒。
        //
        // 例如：
        // 7.84 → 7.8
        int tenths =
            Mathf.CeilToInt(
                remaining * 10f
            );


        if (tenths
            == lastDisplayedTenths)
        {
            return;
        }


        lastDisplayedTenths =
            tenths;


        stormTimerText.SetText(
            "{0:0.0}s",
            tenths / 10f
        );
    }


    // =========================================================
    // Safe / Danger
    // =========================================================

    private void RefreshSafeStatusIfNeeded()
    {
        if (safeStatusText == null)
        {
            return;
        }


        bool isSafe =
            GetCurrentSafeState();


        if (hasDisplayedSafeState
            && isSafe
                == lastDisplayedSafeState)
        {
            return;
        }


        hasDisplayedSafeState =
            true;

        lastDisplayedSafeState =
            isSafe;


        if (isSafe)
        {
            safeStatusText.text =
                "SAFE";

            safeStatusText.color =
                safeColor;
        }
        else
        {
            safeStatusText.text =
                "DANGER";

            safeStatusText.color =
                dangerColor;
        }
    }


    private bool GetCurrentSafeState()
    {
        // 优先使用 StormDamageController
        // 已经计算好的结果。
        if (stormDamageController != null)
        {
            return stormDamageController
                .PlayerCurrentlySafe;
        }


        // 如果 DamageController 没有绑定，
        // 再直接进行一次空间判断作为后备。
        if (safeZoneController == null
            || !safeZoneController
                .HasActiveZone)
        {
            return true;
        }


        GameObject playerObject =
            GameObject
                .FindGameObjectWithTag(
                    "Player"
                );


        if (playerObject == null)
        {
            return true;
        }


        return safeZoneController
            .IsPositionSafe(
                playerObject
                    .transform
                    .position
            );
    }


    // =========================================================
    // Disable
    // =========================================================

    private void OnDisable()
    {
        HidePanel();
    }
}