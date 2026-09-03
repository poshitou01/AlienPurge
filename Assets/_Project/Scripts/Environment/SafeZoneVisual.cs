using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class SafeZoneVisual : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip("提供 Safe Zone Center、Radius 和激活状态。")]
    [SerializeField]
    private SafeZoneController safeZoneController;

    [Tooltip("提供当前 Storm Phase。")]
    [SerializeField]
    private PlanetStormController
        planetStormController;


    // =========================================================
    // Circle Settings
    // =========================================================

    [Header("Circle Settings")]

    [Range(16, 128)]
    [SerializeField]
    private int segments = 64;

    [Min(0.01f)]
    [SerializeField]
    private float lineWidth = 0.10f;


    // =========================================================
    // Warning Feedback
    // =========================================================

    [Header("Warning Feedback")]

    [SerializeField]
    private Color warningColor =
        new Color(
            0.20f,
            1.00f,
            0.75f,
            0.85f
        );

    [Min(0f)]
    [SerializeField]
    private float warningPulseSpeed = 3f;

    [Range(0f, 0.5f)]
    [SerializeField]
    private float warningWidthPulseAmount =
        0.18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float warningMinimumAlpha =
        0.45f;


    // =========================================================
    // Active Feedback
    // =========================================================

    [Header("Active Feedback")]

    [SerializeField]
    private Color activeColor =
        new Color(
            0.25f,
            0.90f,
            1.00f,
            1.00f
        );


    // =========================================================
    // Recovery Feedback
    // =========================================================

    [Header("Recovery Feedback")]

    [SerializeField]
    private Color recoveryColor =
        new Color(
            0.25f,
            0.90f,
            1.00f,
            0.75f
        );


    // =========================================================
    // Runtime
    // =========================================================

    private LineRenderer lineRenderer;

    private float lastBuiltRadius = -1f;
    private int lastBuiltSegments = -1;

    private StormPhase lastPhase;

    private float recoveryInitialTime;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        lineRenderer =
            GetComponent<LineRenderer>();

        ResolveReferences();

        ConfigureLineRenderer();

        HideVisual();

        if (planetStormController != null)
        {
            lastPhase =
                planetStormController
                    .CurrentPhase;
        }
        else
        {
            lastPhase =
                StormPhase.Calm;
        }
    }


    private void Update()
    {
        if (safeZoneController == null
            || planetStormController == null)
        {
            HideVisual();
            return;
        }


        // -----------------------------------------------------
        // 没有有效 Safe Zone 时，
        // 不允许显示任何圆环。
        // -----------------------------------------------------

        if (!safeZoneController
                .HasActiveZone)
        {
            HideVisual();
            return;
        }


        // -----------------------------------------------------
        // SafeZoneVisual 自己不保存 Center。
        //
        // 永远读取 SafeZoneController
        // 当前真正的 Gameplay Center。
        // -----------------------------------------------------

        Vector2 center =
            safeZoneController
                .CurrentCenter;

        transform.position =
            new Vector3(
                center.x,
                center.y,
                0f
            );


        // -----------------------------------------------------
        // Radius 也只有一个数据源。
        //
        // 只有 Radius 或 Segments 改变时
        // 才重新生成圆上的顶点。
        //
        // 不需要每帧重复计算 64 个点。
        // -----------------------------------------------------

        RebuildCircleIfNeeded();


        StormPhase currentPhase =
            planetStormController
                .CurrentPhase;


        HandlePhaseChange(
            currentPhase
        );


        switch (currentPhase)
        {
            case StormPhase.Calm:
                UpdateCalmVisual();
                break;

            case StormPhase.Warning:
                UpdateWarningVisual();
                break;

            case StormPhase.Active:
                UpdateActiveVisual();
                break;

            case StormPhase.Recovery:
                UpdateRecoveryVisual();
                break;
        }


        lastPhase =
            currentPhase;
    }


    private void OnValidate()
    {
        segments =
            Mathf.Clamp(
                segments,
                16,
                128
            );

        lineWidth =
            Mathf.Max(
                0.01f,
                lineWidth
            );

        warningPulseSpeed =
            Mathf.Max(
                0f,
                warningPulseSpeed
            );

        warningWidthPulseAmount =
            Mathf.Clamp(
                warningWidthPulseAmount,
                0f,
                0.5f
            );

        warningMinimumAlpha =
            Mathf.Clamp01(
                warningMinimumAlpha
            );
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
    }


    // =========================================================
    // LineRenderer Configuration
    // =========================================================

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }


        // 圆上的点全部使用当前对象的局部坐标。
        //
        // SafeZoneVisual GameObject 本身
        // 移动到 Safe Zone Center。
        lineRenderer.useWorldSpace =
            false;


        // LineRenderer 自己负责闭合最后一段。
        lineRenderer.loop =
            true;


        lineRenderer.positionCount =
            segments;


        lineRenderer.startWidth =
            lineWidth;

        lineRenderer.endWidth =
            lineWidth;


        lineRenderer.numCornerVertices =
            2;

        lineRenderer.numCapVertices =
            0;


        lineRenderer.alignment =
            LineAlignment.View;


        lineRenderer.textureMode =
            LineTextureMode.Stretch;


        lineRenderer.enabled =
            false;
    }


    // =========================================================
    // Circle Geometry
    // =========================================================

    private void RebuildCircleIfNeeded()
    {
        float radius =
            safeZoneController.Radius;


        bool radiusChanged =
            !Mathf.Approximately(
                radius,
                lastBuiltRadius
            );


        bool segmentsChanged =
            segments
            != lastBuiltSegments;


        if (!radiusChanged
            && !segmentsChanged)
        {
            return;
        }


        BuildCircle(
            radius
        );


        lastBuiltRadius =
            radius;

        lastBuiltSegments =
            segments;
    }


    private void BuildCircle(
        float radius
    )
    {
        if (lineRenderer == null)
        {
            return;
        }


        lineRenderer.positionCount =
            segments;


        float angleStep =
            Mathf.PI
            * 2f
            / segments;


        for (int i = 0;
             i < segments;
             i++)
        {
            float angle =
                angleStep * i;


            float x =
                Mathf.Cos(angle)
                * radius;

            float y =
                Mathf.Sin(angle)
                * radius;


            lineRenderer.SetPosition(
                i,
                new Vector3(
                    x,
                    y,
                    0f
                )
            );
        }
    }


    // =========================================================
    // Phase Change
    // =========================================================

    private void HandlePhaseChange(
        StormPhase currentPhase
    )
    {
        if (currentPhase
            == lastPhase)
        {
            return;
        }


        // Recovery 开始时记录：
        //
        // PlanetStormController 当时真正拥有的
        // PhaseTimeRemaining。
        //
        // 因此 SafeZoneVisual 不需要再复制
        // Recovery Duration = 2
        // 这样的 Gameplay 数据。
        if (currentPhase
            == StormPhase.Recovery)
        {
            recoveryInitialTime =
                Mathf.Max(
                    0.01f,
                    planetStormController
                        .PhaseTimeRemaining
                );
        }
    }


    // =========================================================
    // Calm
    // =========================================================

    private void UpdateCalmVisual()
    {
        HideVisual();
    }


    // =========================================================
    // Warning
    // =========================================================

    private void UpdateWarningVisual()
    {
        ShowVisual();


        // Sin 返回 -1 ～ +1。
        //
        // 转换以后 pulse 为：
        // 0 ～ 1。
        float pulse =
            (
                Mathf.Sin(
                    Time.time
                    * warningPulseSpeed
                )
                + 1f
            )
            * 0.5f;


        float widthMultiplier =
            1f
            + pulse
            * warningWidthPulseAmount;


        SetLineWidth(
            lineWidth
            * widthMultiplier
        );


        float alpha =
            Mathf.Lerp(
                warningMinimumAlpha,
                warningColor.a,
                pulse
            );


        Color color =
            warningColor;

        color.a =
            alpha;


        SetLineColor(
            color
        );
    }


    // =========================================================
    // Active
    // =========================================================

    private void UpdateActiveVisual()
    {
        ShowVisual();


        SetLineWidth(
            lineWidth
        );


        SetLineColor(
            activeColor
        );
    }


    // =========================================================
    // Recovery
    // =========================================================

    private void UpdateRecoveryVisual()
    {
        ShowVisual();


        float fade =
            0f;


        if (recoveryInitialTime > 0f)
        {
            fade =
                Mathf.Clamp01(
                    planetStormController
                        .PhaseTimeRemaining
                    / recoveryInitialTime
                );
        }


        Color color =
            recoveryColor;

        color.a *=
            fade;


        SetLineColor(
            color
        );


        SetLineWidth(
            lineWidth
            * Mathf.Lerp(
                0.75f,
                1f,
                fade
            )
        );
    }


    // =========================================================
    // Renderer Helpers
    // =========================================================

    private void ShowVisual()
    {
        if (lineRenderer != null
            && !lineRenderer.enabled)
        {
            lineRenderer.enabled =
                true;
        }
    }


    private void HideVisual()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled =
                false;
        }
    }


    private void SetLineWidth(
        float width
    )
    {
        if (lineRenderer == null)
        {
            return;
        }


        lineRenderer.startWidth =
            width;

        lineRenderer.endWidth =
            width;
    }


    private void SetLineColor(
        Color color
    )
    {
        if (lineRenderer == null)
        {
            return;
        }


        lineRenderer.startColor =
            color;

        lineRenderer.endColor =
            color;
    }


    // =========================================================
    // Disable Cleanup
    // =========================================================

    private void OnDisable()
    {
        HideVisual();
    }
}
