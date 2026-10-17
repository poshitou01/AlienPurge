using UnityEngine;

// Temporary geometric feedback. Mesh and hit test use the same disk/sector.
public sealed class HiveTyrantTelegraph : MonoBehaviour
{
    private Mesh mesh;
    private Material material;
    public static HiveTyrantTelegraph Create(Transform owner, Vector2 origin, Vector2 direction, float radius, float angle, Color color)
    {
        GameObject go = new GameObject("TEMP_HiveTyrant_FilledWarning");
        go.transform.SetParent(owner, false); go.transform.position = new Vector3(origin.x, origin.y, 0);
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        var marker = go.AddComponent<HiveTyrantTelegraph>();
        marker.mesh = new Mesh { name = "Temporary filled sector" };
        const int segments = 64;
        var vertices = new Vector3[segments + 2];
        var triangles = new int[segments * 3];
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-angle * .5f, angle * .5f, i / (float)segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
            if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
        }
        marker.mesh.vertices = vertices; marker.mesh.triangles = triangles; marker.mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = marker.mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        marker.material = new Material(shader) { name = "TEMP_HiveTyrant_Warning", color = color };
        renderer.sharedMaterial = marker.material;
        renderer.sortingLayerName = "VFX"; renderer.sortingOrder = 10;
        return marker;
    }
    public void Released() { if (material != null) material.color = new Color(1, .85f, .25f, .65f); }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
}
