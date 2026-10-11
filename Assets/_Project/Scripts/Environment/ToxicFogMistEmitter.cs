
using UnityEngine;

/// <summary>
/// AlienPurge - Phase40 Toxic Fog Mist (Enhanced).
///
/// 1. Front particles scale with current front width.
/// 2. Background mist scales with the covered area.
/// 3. All particles spawn inside existing Toxic Fog geometry.
/// 4. Pauses with gameplay and respects performance budgets.
///
/// Does not modify Route, Area, Damage or Planet Storm.
/// Attach to CargoYard_FogMist.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class ToxicFogMistEmitter : MonoBehaviour
{
    [Header("Existing Toxic Fog References")]
    [SerializeField] private ToxicFogAreaController fogArea;
    [SerializeField] private Transform fogRouteRoot;
    [SerializeField]
    private ToxicFogCrossSection[] crossSections =
        new ToxicFogCrossSection[0];

    [Header("Front Mist - Base Settings")]
    [Tooltip("Front emission per second at reference width.")]
    [Min(0f)]
    [SerializeField] private float particlesPerGameSecond = 36f;

    [Tooltip("Depth of the mist strip behind the front.")]
    [Min(0.05f)]
    [SerializeField] private float mistDepthBehindFront = 3.2f;

    [Min(0.1f)]
    [SerializeField] private float particleLifetime = 3f;

    [Min(0.01f)]
    [SerializeField] private float minParticleSize = 0.9f;

    [Min(0.01f)]
    [SerializeField] private float maxParticleSize = 1.9f;

    [SerializeField]
    private Color particleTint =
        new Color(0.69f, 0.89f, 0.57f, 0.24f);

    [Header("Auto Density - Wider Front = More Mist")]
    [Min(0.1f)]
    [SerializeField] private float referenceFrontWidth = 12f;

    [Min(0f)]
    [SerializeField] private float minFrontDensityMultiplier = 0.75f;

    [Min(0.1f)]
    [SerializeField] private float maxFrontDensityMultiplier = 3f;

    [Header("Background Mist - Large Covered Areas")]
    [Min(0f)]
    [SerializeField]
    private float backgroundParticlesPerAreaPerSecond = 0.035f;

    [Min(0f)]
    [SerializeField]
    private float maxBackgroundParticlesPerSecond = 28f;

    [SerializeField]
    private Color backgroundParticleTint =
        new Color(0.62f, 0.81f, 0.60f, 0.10f);

    [Min(0.1f)]
    [SerializeField] private float backgroundSizeMultiplier = 1.25f;

    [Min(0.1f)]
    [SerializeField] private float backgroundLifetimeMultiplier = 1.25f;

    [Header("Performance / Sorting")]
    [Min(1)]
    [SerializeField] private int maxParticleCount = 450;

    [Min(1)]
    [SerializeField] private int maxParticlesPerFrame = 10;

    [Range(0.02f, 0.25f)]
    [SerializeField] private float maxEmissionDelta = 0.12f;

    [SerializeField] private float particleWorldZ = -0.035f;

    [Header("Runtime Debug - Read Only")]
    [SerializeField] private bool geometryReady;
    [SerializeField] private float coveredDistance;
    [SerializeField] private float estimatedCoveredArea;
    [SerializeField] private float currentFrontWidth;
    [SerializeField] private float currentFrontEmissionRate;
    [SerializeField] private float currentBackgroundEmissionRate;
    [SerializeField] private int activeParticleCount;
    [SerializeField] private int totalEmitted;

    private ParticleSystem mistParticles;
    private float[] segmentLengths = new float[0];
    private float totalRouteLength;
    private float frontEmissionRemainder;
    private float backgroundEmissionRemainder;
    private float lastSurvivalTime;

    private void Reset()
    {
        mistParticles = GetComponent<ParticleSystem>();
    }

    private void Awake()
    {
        mistParticles = GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = mistParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.maxParticles = maxParticleCount;

        // Disable automatic emission at world origin.
        ParticleSystem.EmissionModule emission = mistParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = mistParticles.shape;
        shape.enabled = false;

        mistParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private void Start()
    {
        BuildRouteCache();

        if (GameManager.Instance != null)
        {
            lastSurvivalTime =
                GameManager.Instance.SurvivalTime;
        }
    }

    [ContextMenu(
        "Debug/Assign Sections From Fog Route Children"
    )]
    private void AssignSectionsFromRouteChildren()
    {
        if (fogRouteRoot == null)
        {
            Debug.LogWarning(
                "[ToxicFogMist] Assign Fog Route Root first.",
                this
            );
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

    private void BuildRouteCache()
    {
        geometryReady = false;
        totalRouteLength = 0f;

        if (fogArea == null ||
            !fogArea.GeometryIsValid ||
            crossSections == null ||
            crossSections.Length < 2)
        {
            Debug.LogWarning(
                "[ToxicFogMist] Missing valid Fog Area or Cross Sections.",
                this
            );
            return;
        }

        segmentLengths =
            new float[crossSections.Length - 1];

        for (int i = 0; i < crossSections.Length; i++)
        {
            if (crossSections[i] == null ||
                !crossSections[i].IsValid)
            {
                Debug.LogWarning(
                    "[ToxicFogMist] Invalid Cross Section at index " + i,
                    this
                );
                return;
            }

            if (i == 0)
                continue;

            float length = Vector2.Distance(
                crossSections[i - 1].Center,
                crossSections[i].Center
            );

            if (length <= 0.0001f)
            {
                Debug.LogWarning(
                    "[ToxicFogMist] Duplicate section centers at index " + i,
                    this
                );
                return;
            }

            segmentLengths[i - 1] = length;
            totalRouteLength += length;
        }

        geometryReady = true;
    }

    private void LateUpdate()
    {
        GameManager manager = GameManager.Instance;

        bool canSimulate =
            manager != null &&
            manager.IsPlaying &&
            Time.timeScale > 0f &&
            !UpgradeManager.IsChoosingUpgrade &&
            !WeaponModuleSelectionManager.IsChoosingModule;

        if (!canSimulate)
        {
            if (mistParticles != null && mistParticles.isPlaying)
                mistParticles.Pause(true);

            return;
        }

        float now = manager.SurvivalTime;

        float gameDelta = Mathf.Clamp(
            now - lastSurvivalTime,
            0f,
            maxEmissionDelta
        );

        lastSurvivalTime = now;

        if (!geometryReady || fogArea == null)
            return;

        coveredDistance = Mathf.Clamp(
            fogArea.CoveredDistance,
            0f,
            totalRouteLength
        );

        if (coveredDistance <= 0.0001f)
        {
            activeParticleCount = mistParticles.particleCount;
            return;
        }

        if (!mistParticles.isPlaying)
            mistParticles.Play(true);

        Vector2 frontLeft;
        Vector2 frontRight;

        if (!TryGetSectionAtDistance(
            coveredDistance,
            out frontLeft,
            out frontRight))
        {
            return;
        }

        // =====================================================
        // Front emission scales automatically with front width.
        // =====================================================

        currentFrontWidth =
            Vector2.Distance(frontLeft, frontRight);

        estimatedCoveredArea =
            CalculateCoveredArea(coveredDistance);

        float densityMultiplier = Mathf.Clamp(
            currentFrontWidth /
            Mathf.Max(0.1f, referenceFrontWidth),
            minFrontDensityMultiplier,
            maxFrontDensityMultiplier
        );

        currentFrontEmissionRate =
            particlesPerGameSecond * densityMultiplier;

        // =====================================================
        // Background emission scales with covered area.
        // =====================================================

        currentBackgroundEmissionRate = Mathf.Min(
            maxBackgroundParticlesPerSecond,
            estimatedCoveredArea *
            backgroundParticlesPerAreaPerSecond
        );

        frontEmissionRemainder +=
            gameDelta * currentFrontEmissionRate;

        backgroundEmissionRemainder +=
            gameDelta * currentBackgroundEmissionRate;

        // =====================================================
        // Limit new particles and total active particles.
        // =====================================================

        activeParticleCount = mistParticles.particleCount;

        int budget = Mathf.Min(
            maxParticlesPerFrame,
            Mathf.Max(0, maxParticleCount - activeParticleCount)
        );

        int frontCount = Mathf.Min(
            Mathf.FloorToInt(frontEmissionRemainder),
            budget
        );

        frontEmissionRemainder = Mathf.Max(
            0f,
            frontEmissionRemainder - frontCount
        );

        budget -= frontCount;

        int backgroundCount = Mathf.Min(
            Mathf.FloorToInt(backgroundEmissionRemainder),
            budget
        );

        backgroundEmissionRemainder = Mathf.Max(
            0f,
            backgroundEmissionRemainder - backgroundCount
        );

        // Do not build up an enormous backlog.
        frontEmissionRemainder =
            Mathf.Min(frontEmissionRemainder, 1f);

        backgroundEmissionRemainder =
            Mathf.Min(backgroundEmissionRemainder, 1f);

        // =====================================================
        // Emit denser mist near the moving front.
        // =====================================================

        for (int i = 0; i < frontCount; i++)
        {
            Vector2 position;

            if (TrySampleNearFront(out position))
                EmitMist(position, false);
        }

        // =====================================================
        // Emit light background mist in the covered region.
        // =====================================================

        for (int i = 0; i < backgroundCount; i++)
        {
            Vector2 position;

            if (TrySampleCoveredArea(out position))
                EmitMist(position, true);
        }

        activeParticleCount = mistParticles.particleCount;
    }

    private bool TrySampleNearFront(out Vector2 position)
    {
        position = Vector2.zero;

        float backDistance = Random.Range(
            0f,
            Mathf.Min(mistDepthBehindFront, coveredDistance)
        );

        float distance = Mathf.Max(
            0.001f,
            coveredDistance - backDistance
        );

        Vector2 left;
        Vector2 right;

        if (!TryGetSectionAtDistance(
            distance, out left, out right))
        {
            return false;
        }

        position = Vector2.Lerp(
            left,
            right,
            Random.Range(0.06f, 0.94f)
        );

        return fogArea.IsPositionInsideFog(position);
    }

    private bool TrySampleCoveredArea(out Vector2 position)
    {
        position = Vector2.zero;

        if (estimatedCoveredArea <= 0.0001f)
            return false;

        float targetArea =
            Random.value * estimatedCoveredArea;

        float remainingDistance = coveredDistance;
        float previousDistance = 0f;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = segmentLengths[i];

            float coveredLength = Mathf.Clamp(
                remainingDistance,
                0f,
                length
            );

            if (coveredLength <= 0f)
                break;

            float tEnd = coveredLength / length;

            float startWidth = GetWidthAtSection(i);

            float endWidth = Mathf.Lerp(
                startWidth,
                GetWidthAtSection(i + 1),
                tEnd
            );

            float segmentArea =
                coveredLength * (startWidth + endWidth) * 0.5f;

            if (targetArea <= segmentArea ||
                i == segmentLengths.Length - 1 ||
                remainingDistance <= length)
            {
                float sampleDistance = previousDistance +
                    Random.Range(
                        0.001f,
                        Mathf.Max(0.001f, coveredLength)
                    );

                sampleDistance = Mathf.Min(
                    sampleDistance,
                    coveredDistance
                );

                Vector2 left;
                Vector2 right;

                if (!TryGetSectionAtDistance(
                    sampleDistance,
                    out left,
                    out right))
                {
                    return false;
                }

                position = Vector2.Lerp(
                    left,
                    right,
                    Random.Range(0.07f, 0.93f)
                );

                return fogArea.IsPositionInsideFog(position);
            }

            targetArea -= segmentArea;
            previousDistance += length;
            remainingDistance -= length;
        }

        return false;
    }

    private float CalculateCoveredArea(float distance)
    {
        float remaining = distance;
        float area = 0f;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = segmentLengths[i];

            float partial = Mathf.Clamp(
                remaining,
                0f,
                length
            );

            if (partial <= 0f)
                break;

            float t = partial / length;

            float widthA = GetWidthAtSection(i);

            float widthB = Mathf.Lerp(
                widthA,
                GetWidthAtSection(i + 1),
                t
            );

            area += partial *
                (widthA + widthB) * 0.5f;

            remaining -= length;
        }

        return area;
    }

    private float GetWidthAtSection(int index)
    {
        return Vector2.Distance(
            crossSections[index].LeftPosition,
            crossSections[index].RightPosition
        );
    }

    private bool TryGetSectionAtDistance(
        float distance,
        out Vector2 left,
        out Vector2 right)
    {
        left = Vector2.zero;
        right = Vector2.zero;

        if (!geometryReady)
            return false;

        float remaining = Mathf.Clamp(
            distance,
            0f,
            totalRouteLength
        );

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = segmentLengths[i];

            if (remaining <= length ||
                i == segmentLengths.Length - 1)
            {
                float t = Mathf.Clamp01(
                    remaining / length
                );

                left = Vector2.Lerp(
                    crossSections[i].LeftPosition,
                    crossSections[i + 1].LeftPosition,
                    t
                );

                right = Vector2.Lerp(
                    crossSections[i].RightPosition,
                    crossSections[i + 1].RightPosition,
                    t
                );

                return true;
            }

            remaining -= length;
        }

        return false;
    }

    private void EmitMist(Vector2 position, bool background)
    {
        ParticleSystem.EmitParams particle =
            new ParticleSystem.EmitParams();

        particle.position = new Vector3(
            position.x,
            position.y,
            particleWorldZ
        );

        particle.velocity = Vector3.zero;

        particle.startLifetime = particleLifetime *
            (background ? backgroundLifetimeMultiplier : 1f);

        particle.startSize = Random.Range(
            minParticleSize,
            Mathf.Max(minParticleSize, maxParticleSize)
        ) * (background ? backgroundSizeMultiplier : 1f);

        particle.startColor = background
            ? backgroundParticleTint
            : particleTint;

        mistParticles.Emit(particle, 1);
        totalEmitted++;
    }

    private void OnValidate()
    {
        particlesPerGameSecond =
            Mathf.Max(0f, particlesPerGameSecond);

        mistDepthBehindFront =
            Mathf.Max(0.05f, mistDepthBehindFront);

        particleLifetime =
            Mathf.Max(0.1f, particleLifetime);

        minParticleSize =
            Mathf.Max(0.01f, minParticleSize);

        maxParticleSize =
            Mathf.Max(minParticleSize, maxParticleSize);

        referenceFrontWidth =
            Mathf.Max(0.1f, referenceFrontWidth);

        minFrontDensityMultiplier =
            Mathf.Max(0f, minFrontDensityMultiplier);

        maxFrontDensityMultiplier = Mathf.Max(
            0.1f,
            maxFrontDensityMultiplier
        );

        maxFrontDensityMultiplier = Mathf.Max(
            minFrontDensityMultiplier,
            maxFrontDensityMultiplier
        );

        backgroundParticlesPerAreaPerSecond = Mathf.Max(
            0f,
            backgroundParticlesPerAreaPerSecond
        );

        maxBackgroundParticlesPerSecond = Mathf.Max(
            0f,
            maxBackgroundParticlesPerSecond
        );

        backgroundSizeMultiplier =
            Mathf.Max(0.1f, backgroundSizeMultiplier);

        backgroundLifetimeMultiplier =
            Mathf.Max(0.1f, backgroundLifetimeMultiplier);

        maxParticleCount = Mathf.Max(1, maxParticleCount);
        maxParticlesPerFrame = Mathf.Max(1, maxParticlesPerFrame);
    }
}
