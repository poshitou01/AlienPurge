using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AlienPurge Phase40 Step 3.4-L.
/// Non-interactive Toxic Fog status HUD and subtle exposure screen tint.
/// Reads existing route/area state; never changes fog, damage, or Storm.
/// Attach to HUDCanvas/HUDRoot/ToxicFogStatusPanel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ToxicFogFeedbackHUD : MonoBehaviour
{
    private enum FogHudState
    {
        Hidden,
        Advancing,
        Approaching,
        Exposed,
        ReachedExit
    }

    [Header("Gameplay References")]
    [SerializeField] private ToxicFogAreaController fogArea;
    [SerializeField] private ToxicFogRouteController fogRoute;
    [SerializeField] private Transform playerRoot;

    [Header("Front Cross Sections (Route Order)")]
    [SerializeField] private Transform fogRouteRoot;
    [SerializeField]
    private ToxicFogCrossSection[] crossSections =
        new ToxicFogCrossSection[0];

    [Header("UI References")]
    [SerializeField] private CanvasGroup statusPanelGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;

    [Tooltip("Optional full-screen UI Image behind HUDRoot. " +
             "It must NOT be a child of this Status Panel.")]
    [SerializeField] private Image exposureScreenTint;

    [Header("Warning Rules")]
    [Min(0.1f)]
    [SerializeField] private float approachingDistance = 6f;

    [Tooltip("Show a quiet status once fog starts, even when far away.")]
    [SerializeField] private bool showAdvancingStatus = true;

    [Header("Display Colors")]
    [SerializeField]
    private Color advancingColor =
        new Color(0.75f, 0.88f, 0.81f, 1f);

    [SerializeField]
    private Color approachingColor =
        new Color(1f, 0.82f, 0.37f, 1f);

    [SerializeField]
    private Color exposedColor =
        new Color(0.80f, 1f, 0.40f, 1f);

    [SerializeField]
    private Color tintColor =
        new Color(0.42f, 0.80f, 0.40f, 1f);

    [Range(0f, 0.12f)]
    [SerializeField] private float maxExposureTintAlpha = 0.045f;

    [Min(0f)]
    [SerializeField] private float warningPulseSpeed = 3f;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private FogHudState currentState;
    [SerializeField] private bool playerInsideFog;
    [SerializeField] private float playerToFrontDistance = -1f;

    private void Reset()
    {
        statusPanelGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (statusPanelGroup == null)
            statusPanelGroup = GetComponent<CanvasGroup>();

        if (statusPanelGroup != null)
        {
            statusPanelGroup.interactable = false;
            statusPanelGroup.blocksRaycasts = false;
        }

        if (exposureScreenTint != null)
            exposureScreenTint.raycastTarget = false;

        currentState = FogHudState.Hidden;
        SetPanelAlpha(0f);
        SetTintAlpha(0f);
    }

    [ContextMenu("Debug/Assign Sections From Fog Route Children")]
    private void AssignSectionsFromRouteChildren()
    {
        if (fogRouteRoot == null)
        {
            Debug.LogWarning(
                "[ToxicFogHUD] Assign Fog Route Root first.", this);
            return;
        }

        crossSections =
            new ToxicFogCrossSection[fogRouteRoot.childCount];

        for (int i = 0; i < fogRouteRoot.childCount; i++)
        {
            crossSections[i] = fogRouteRoot.GetChild(i)
                .GetComponent<ToxicFogCrossSection>();
        }
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;

        // Avoid obstructing pause, upgrade, module choice and result UI.
        if (manager == null || !manager.IsPlaying ||
            Time.timeScale <= 0f ||
            UpgradeManager.IsChoosingUpgrade ||
            WeaponModuleSelectionManager.IsChoosingModule ||
            fogArea == null || !fogArea.GeometryIsValid ||
            fogRoute == null || !fogRoute.HasStarted ||
            playerRoot == null)
        {
            HideAll();
            return;
        }

        playerInsideFog =
            fogArea.IsPositionInsideFog(playerRoot.position);

        playerToFrontDistance =
            TryGetCurrentFront(out Vector2 left, out Vector2 right)
                ? DistancePointToSegment(playerRoot.position, left, right)
                : Vector2.Distance(playerRoot.position,
                    fogRoute.FrontWorldPosition);

        FogHudState state;
        if (playerInsideFog)
            state = FogHudState.Exposed;
        else if (fogRoute.ReachedEnd)
            state = FogHudState.ReachedExit;
        else if (playerToFrontDistance <= approachingDistance)
            state = FogHudState.Approaching;
        else
            state = showAdvancingStatus
                ? FogHudState.Advancing
                : FogHudState.Hidden;

        if (state != currentState)
            ApplyText(state);

        currentState = state;

        float phase = manager.SurvivalTime * warningPulseSpeed;
        float pulse = 0.5f + 0.5f * Mathf.Sin(phase);

        switch (state)
        {
            case FogHudState.Hidden:
                SetPanelAlpha(0f);
                SetTintAlpha(0f);
                break;

            case FogHudState.Advancing:
                SetPanelAlpha(0.80f);
                SetTintAlpha(0f);
                break;

            case FogHudState.Approaching:
                SetPanelAlpha(0.85f + 0.15f * pulse);
                SetTintAlpha(0f);
                break;

            case FogHudState.Exposed:
                SetPanelAlpha(1f);
                SetTintAlpha(maxExposureTintAlpha *
                             (0.65f + 0.35f * pulse));
                break;

            case FogHudState.ReachedExit:
                SetPanelAlpha(0.95f);
                SetTintAlpha(0f);
                break;
        }
    }

    private void ApplyText(FogHudState state)
    {
        if (titleText != null)
        {
            switch (state)
            {
                case FogHudState.Advancing:
                    titleText.text = "毒雾正在推进";
                    titleText.color = advancingColor;
                    break;
                case FogHudState.Approaching:
                    titleText.text = "警告：毒雾逼近";
                    titleText.color = approachingColor;
                    break;
                case FogHudState.Exposed:
                    titleText.text = "污染暴露中";
                    titleText.color = exposedColor;
                    break;
                case FogHudState.ReachedExit:
                    titleText.text = "毒雾已抵达本区出口";
                    titleText.color = approachingColor;
                    break;
            }
        }

        if (detailText != null)
        {
            switch (state)
            {
                case FogHudState.Advancing:
                    detailText.text = "危险区域从入口持续扩张";
                    break;
                case FogHudState.Approaching:
                    detailText.text = "前线接近，向出口方向移动";
                    break;
                case FogHudState.Exposed:
                    detailText.text = "持续环境伤害 · 尽快脱离";
                    break;
                case FogHudState.ReachedExit:
                    detailText.text = "前往下一区域，远离污染区";
                    break;
            }
        }
    }

    private bool TryGetCurrentFront(out Vector2 left, out Vector2 right)
    {
        left = Vector2.zero;
        right = Vector2.zero;

        if (crossSections == null || crossSections.Length < 2)
            return false;

        float remaining = Mathf.Max(0f, fogArea.CoveredDistance);

        for (int i = 0; i < crossSections.Length - 1; i++)
        {
            ToxicFogCrossSection a = crossSections[i];
            ToxicFogCrossSection b = crossSections[i + 1];

            if (a == null || b == null || !a.IsValid || !b.IsValid)
                return false;

            float length = Vector2.Distance(a.Center, b.Center);
            if (length <= 0.0001f)
                return false;

            if (remaining <= length || i == crossSections.Length - 2)
            {
                float t = Mathf.Clamp01(remaining / length);
                left = Vector2.Lerp(a.LeftPosition, b.LeftPosition, t);
                right = Vector2.Lerp(a.RightPosition, b.RightPosition, t);
                return true;
            }

            remaining -= length;
        }

        return false;
    }

    private static float DistancePointToSegment(
        Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0.00001f)
            return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) /
                                lengthSquared);
        return Vector2.Distance(point, a + ab * t);
    }

    private void SetPanelAlpha(float alpha)
    {
        if (statusPanelGroup != null)
            statusPanelGroup.alpha = alpha;
    }

    private void SetTintAlpha(float alpha)
    {
        if (exposureScreenTint == null)
            return;

        Color color = tintColor;
        color.a = Mathf.Clamp01(alpha);
        exposureScreenTint.color = color;
    }

    private void HideAll()
    {
        currentState = FogHudState.Hidden;
        playerInsideFog = false;
        playerToFrontDistance = -1f;
        SetPanelAlpha(0f);
        SetTintAlpha(0f);
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnValidate()
    {
        approachingDistance = Mathf.Max(0.1f, approachingDistance);
        warningPulseSpeed = Mathf.Max(0f, warningPulseSpeed);
    }
}
