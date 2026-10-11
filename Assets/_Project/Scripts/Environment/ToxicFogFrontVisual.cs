
using UnityEngine;

/// <summary>
/// Phase40 Step 3.4-J
/// Game-view visual for the advancing Toxic Fog front.
///
/// Reads the existing ToxicFogAreaController's covered distance and
/// the same ordered ToxicFogCrossSections used by gameplay.
/// This component does NOT alter fog geometry, damage or Storm.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ToxicFogFrontVisual : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Existing Toxic Fog References")]
    [SerializeField] private ToxicFogAreaController fogArea;
    [SerializeField] private ToxicFogRouteController fogRoute;
    [SerializeField] private Transform fogRouteRoot;

    [Tooltip("Route_00 through Route_05 in their travel order.")]
    [SerializeField]
    private ToxicFogCrossSection[] crossSections =
        new ToxicFogCrossSection[0];

    [Header("Front Material")]
    [Tooltip("Use a separate transparent URP/Unlit material.")]
    [SerializeField] private Material frontMaterial;

    // =========================================================
    // Appearance
    // =========================================================

    [Header("Front Appearance")]
    [Min(2)]
    [SerializeField] private int sampleCount = 32;

    [Min(0.01f)]
    [SerializeField] private float baseWidth = 0.32f;

    [Range(0f, 0.5f)]
    [SerializeField] private float pulseWidth = 0.06f;

    [Tooltip(
        "World-unit deviation of the visual ONLY. " +
        "Set to zero for an exact edge."
    )]
    [Range(0f, 0.5f)]
    [SerializeField] private float waveAmplitude = 0.12f;

    [Min(0f)]
    [SerializeField] private float waveCycles = 3f;

    [Min(0f)]
    [SerializeField] private float waveSpeed = 2f;

    [Min(0f)]
    [SerializeField] private float pulseSpeed = 2.5f;

    [Tooltip(
        "World Z of the line; actual visibility " +
        "primarily depends on sorting."
    )]
    [SerializeField] private float worldZ = -0.04f;

    [Header("Renderer Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 2;

    // =========================================================
    // Runtime State
    // =========================================================

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool visualReady;
    [SerializeField] private bool frontVisible;
    [SerializeField] private int currentSegmentIndex = -1;
    [SerializeField] private float frontDistance;

    private LineRenderer frontLine;
    private Vector3[] linePositions;
    private float[] segmentLengths;
    private float totalRouteLength;

    public bool VisualReady => visualReady;
    public bool FrontVisible => frontVisible;
    public int CurrentSegmentIndex => currentSegmentIndex;
    public float FrontDistance => frontDistance;

    private void Reset()
    {
        frontLine = GetComponent<LineRenderer>();
        ConfigureLineRenderer();
    }

    private void Awake()
    {
        frontLine = GetComponent<LineRenderer>();
        ConfigureLineRenderer();
        frontLine.enabled = false;
    }

    private void Start()
    {
        InitializeVisual();
    }

    private void OnValidate()
    {
        sampleCount = Mathf.Max(2, sampleCount);
        baseWidth = Mathf.Max(0.01f, baseWidth);
        waveCycles = Mathf.Max(0f, waveCycles);
        waveSpeed = Mathf.Max(0f, waveSpeed);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
    }

    private void ConfigureLineRenderer()
    {
        if (frontLine == null)
            return;

        frontLine.useWorldSpace = true;
        frontLine.loop = false;
        frontLine.alignment = LineAlignment.View;
        frontLine.textureMode = LineTextureMode.Stretch;
        frontLine.startColor = Color.white;
        frontLine.endColor = Color.white;
        frontLine.numCapVertices = 4;
        frontLine.numCornerVertices = 2;
        frontLine.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        frontLine.receiveShadows = false;
        frontLine.sortingLayerName = sortingLayerName;
        frontLine.sortingOrder = sortingOrder;
    }

    [ContextMenu("Debug/Assign Sections From Fog Route Children")]
    private void AssignSectionsFromRouteChildren()
    {
        if (fogRouteRoot == null)
        {
            Debug.LogWarning(
                "[ToxicFogFrontVisual] Assign Fog Route Root first.",
                this);
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

    private void InitializeVisual()
    {
        visualReady = false;
        frontVisible = false;
        currentSegmentIndex = -1;

        if (fogArea == null ||
            !fogArea.GeometryIsValid ||
            fogRoute == null ||
            frontMaterial == null ||
            crossSections == null ||
            crossSections.Length < 2)
        {
            Debug.LogWarning(
                "[ToxicFogFrontVisual] Missing valid Fog Area, Route, " +
                "Front Material, or Cross Sections.",
                this);

            frontLine.enabled = false;
            return;
        }

        segmentLengths = new float[crossSections.Length - 1];
        totalRouteLength = 0f;

        for (int i = 0; i < crossSections.Length; i++)
        {
            if (crossSections[i] == null ||
                !crossSections[i].IsValid)
            {
                Debug.LogWarning(
                    "[ToxicFogFrontVisual] Invalid cross-section at index " + i,
                    this);

                frontLine.enabled = false;
                return;
            }

            if (i == 0)
                continue;

            float length = Vector2.Distance(
                crossSections[i - 1].Center,
                crossSections[i].Center);

            if (length <= 0.0001f)
            {
                Debug.LogWarning(
                    "[ToxicFogFrontVisual] Zero-length segment at index " +
                    (i - 1),
                    this);

                frontLine.enabled = false;
                return;
            }

            segmentLengths[i - 1] = length;
            totalRouteLength += length;
        }

        if (Mathf.Abs(
                totalRouteLength - fogRoute.TotalRouteDistance
            ) > 0.05f)
        {
            Debug.LogWarning(
                "[ToxicFogFrontVisual] Route length mismatch. " +
                "Check the cross-section order against the route points.",
                this);

            frontLine.enabled = false;
            return;
        }

        linePositions = new Vector3[sampleCount];

        frontLine.positionCount = sampleCount;
        frontLine.sharedMaterial = frontMaterial;

        ConfigureLineRenderer();

        frontLine.enabled = false;
        visualReady = true;
    }

    private void LateUpdate()
    {
        if (!visualReady ||
            fogArea == null ||
            fogRoute == null ||
            !fogRoute.HasStarted)
        {
            if (frontLine != null)
                frontLine.enabled = false;

            frontVisible = false;
            return;
        }

        float distance = Mathf.Clamp(
            fogArea.CoveredDistance,
            0f,
            totalRouteLength);

        if (distance <= 0f)
        {
            frontLine.enabled = false;
            frontVisible = false;
            return;
        }

        frontDistance = distance;

        float segmentDistance = distance;
        int index = segmentLengths.Length - 1;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            if (segmentDistance <= segmentLengths[i] ||
                i == segmentLengths.Length - 1)
            {
                index = i;
                break;
            }

            segmentDistance -= segmentLengths[i];
        }

        currentSegmentIndex = index;

        float t = Mathf.Clamp01(
            segmentDistance / segmentLengths[index]);

        ToxicFogCrossSection a = crossSections[index];
        ToxicFogCrossSection b = crossSections[index + 1];

        Vector2 left = Vector2.Lerp(
            a.LeftPosition,
            b.LeftPosition,
            t);

        Vector2 right = Vector2.Lerp(
            a.RightPosition,
            b.RightPosition,
            t);

        Vector2 travelDirection =
            (b.Center - a.Center).normalized;

        // GameManager.SurvivalTime freezes during UI pauses.
        float visualTime = GameManager.Instance != null
            ? GameManager.Instance.SurvivalTime
            : 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float u = (float)i / (sampleCount - 1);

            Vector2 baseline = Vector2.Lerp(
                left,
                right,
                u);

            float envelope =
                Mathf.Sin(Mathf.PI * u);

            float wave = Mathf.Sin(
                u * waveCycles * Mathf.PI * 2f +
                visualTime * waveSpeed);

            Vector2 p = baseline +
                travelDirection *
                (waveAmplitude * envelope * wave);

            linePositions[i] = new Vector3(
                p.x,
                p.y,
                worldZ);
        }

        float width = baseWidth + pulseWidth *
            (0.5f + 0.5f *
             Mathf.Sin(visualTime * pulseSpeed));

        frontLine.startWidth = width;
        frontLine.endWidth = width;

        frontLine.SetPositions(linePositions);
        frontLine.enabled = true;
        frontVisible = true;
    }
}
