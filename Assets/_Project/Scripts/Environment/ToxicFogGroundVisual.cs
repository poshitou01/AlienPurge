
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase40 - Toxic Fog visual pass 1.
/// Builds a transparent ground mesh using the SAME ordered
/// ToxicFogCrossSections and covered distance as the gameplay area.
/// No colliders, damage or changes to the existing Storm system.
/// Put this component on a SIBLING of CargoYard_FogRoute,
/// not on a child of CargoYard_FogRoute.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ToxicFogGroundVisual : MonoBehaviour
{
    [Header("Existing Toxic Fog References")]
    [SerializeField] private ToxicFogAreaController fogArea;
    [SerializeField] private Transform fogRouteRoot;
    [SerializeField]
    private ToxicFogCrossSection[] crossSections =
        new ToxicFogCrossSection[0];

    [Header("Visual Material")]
    [Tooltip("Assign a transparent URP Unlit material. Do not use an opaque material.")]
    [SerializeField] private Material fogMaterial;

    [Tooltip("World Z coordinate of the ground tint mesh. Match your ground art sorting setup.")]
    [SerializeField] private float worldZ = -0.05f;

    [Header("Renderer Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 1;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool geometryReady;
    [SerializeField] private float visibleCoveredDistance;
    [SerializeField] private int visibleSegmentCount;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh runtimeMesh;
    private float[] segmentLengths;

    private readonly List<Vector3> vertices = new List<Vector3>(32);
    private readonly List<Vector2> uvs = new List<Vector2>(32);
    private readonly List<int> triangles = new List<int>(48);

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        runtimeMesh = new Mesh { name = "ToxicFogGround_Runtime" };
        runtimeMesh.MarkDynamic();
        meshFilter.sharedMesh = runtimeMesh;
    }

    private void Start()
    {
        // All ToxicFogAreaController.Awake calls have completed by Start.
        InitializeGeometry();
    }

    private void LateUpdate()
    {
        if (!geometryReady || fogArea == null)
            return;

        // The route and area have both updated by LateUpdate.
        float distance = fogArea.CoveredDistance;

        visibleCoveredDistance = distance;
        BuildVisibleMesh(distance);
    }

    private void OnDestroy()
    {
        if (runtimeMesh != null)
        {
            if (meshFilter != null &&
                meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            Destroy(runtimeMesh);
        }
    }

    [ContextMenu("Debug/Assign Sections From Fog Route Children")]
    private void AssignSectionsFromRouteChildren()
    {
        if (fogRouteRoot == null)
        {
            Debug.LogWarning(
                "[ToxicFogGroundVisual] Assign Fog Route Root first.",
                this);
            return;
        }

        crossSections =
            new ToxicFogCrossSection[fogRouteRoot.childCount];

        for (int i = 0; i < fogRouteRoot.childCount; i++)
        {
            crossSections[i] =
                fogRouteRoot.GetChild(i)
                .GetComponent<ToxicFogCrossSection>();
        }
    }

    private void InitializeGeometry()
    {
        geometryReady = false;
        visibleCoveredDistance = 0f;
        visibleSegmentCount = 0;

        if (fogArea == null ||
            !fogArea.GeometryIsValid ||
            crossSections == null ||
            crossSections.Length < 2 ||
            fogMaterial == null)
        {
            Debug.LogWarning(
                "[ToxicFogGroundVisual] Missing Fog Area, " +
                "valid geometry, Cross Sections or Material.",
                this);

            meshRenderer.enabled = false;
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
                    "[ToxicFogGroundVisual] Invalid section: " + i,
                    this);

                meshRenderer.enabled = false;
                return;
            }

            if (i == 0)
                continue;

            segmentLengths[i - 1] = Vector2.Distance(
                crossSections[i - 1].Center,
                crossSections[i].Center);
        }

        meshRenderer.sharedMaterial = fogMaterial;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.enabled = true;

        geometryReady = true;
    }

    private void BuildVisibleMesh(float coveredDistance)
    {
        vertices.Clear();
        uvs.Clear();
        triangles.Clear();

        visibleSegmentCount = 0;

        float remaining =
            Mathf.Max(0f, coveredDistance);

        for (int i = 0;
             i < segmentLengths.Length &&
             remaining > 0.0001f;
             i++)
        {
            float length = segmentLengths[i];

            if (length <= 0.0001f)
                continue;

            float t =
                Mathf.Clamp01(remaining / length);

            ToxicFogCrossSection a = crossSections[i];
            ToxicFogCrossSection b = crossSections[i + 1];

            Vector2 leftEnd = Vector2.Lerp(
                a.LeftPosition,
                b.LeftPosition,
                t);

            Vector2 rightEnd = Vector2.Lerp(
                a.RightPosition,
                b.RightPosition,
                t);

            int offset = vertices.Count;

            vertices.Add(WorldToLocal(a.LeftPosition));
            vertices.Add(WorldToLocal(a.RightPosition));
            vertices.Add(WorldToLocal(rightEnd));
            vertices.Add(WorldToLocal(leftEnd));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, t));
            uvs.Add(new Vector2(0f, t));

            triangles.Add(offset);
            triangles.Add(offset + 1);
            triangles.Add(offset + 2);

            triangles.Add(offset);
            triangles.Add(offset + 2);
            triangles.Add(offset + 3);

            visibleSegmentCount++;
            remaining -= length;
        }

        runtimeMesh.Clear();

        if (vertices.Count == 0)
            return;

        runtimeMesh.SetVertices(vertices);
        runtimeMesh.SetUVs(0, uvs);
        runtimeMesh.SetTriangles(triangles, 0);
        runtimeMesh.RecalculateBounds();
    }

    private Vector3 WorldToLocal(Vector2 worldXY)
    {
        return transform.InverseTransformPoint(
            new Vector3(
                worldXY.x,
                worldXY.y,
                worldZ));
    }
}
