using UnityEngine;

// Reusable warning. Damage and this filled geometry share radius, angle and locked heading.
public sealed class HiveTyrantTelegraph : MonoBehaviour
{
    private Mesh mesh;
    private MeshRenderer view;
    private readonly Vector3[] vertices = new Vector3[66];
    private readonly Color[] colors = new Color[66];
    private Color tint;
    public static HiveTyrantTelegraph Create(Transform owner, Vector2 origin, Vector2 direction, float radius, float angle, Color color)
    { return owner.GetComponent<HiveTyrantEffects>().Warning(origin, direction, radius, angle, color); }
    public void Initialize(Material material)
    {
        mesh = new Mesh { name = "HiveTyrant pooled filled warning" }; mesh.MarkDynamic();
        int[] triangles = new int[64 * 3];
        for (int i = 0; i < 64; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
        mesh.vertices = vertices; mesh.triangles = triangles;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        view = gameObject.AddComponent<MeshRenderer>(); view.sharedMaterial = material;
        view.sortingLayerName = "Enemies"; view.sortingOrder = -20;
    }
    public void Show(Vector2 origin, Vector2 direction, float radius, float angle, Color color)
    {
        transform.position = origin; transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        for (int i = 0; i <= 64; i++)
        { float a = Mathf.Lerp(-angle * .5f, angle * .5f, i / 64f) * Mathf.Deg2Rad; vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius; }
        mesh.vertices = vertices; mesh.RecalculateBounds(); tint = color; SetColor(color); gameObject.SetActive(true);
    }
    private void SetColor(Color color) { for (int i = 0; i < colors.Length; i++) colors[i] = color; mesh.colors = colors; }
    public void Released() { SetColor(new Color(1, .9f, .7f, .4f)); }
    public void Release() { gameObject.SetActive(false); }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
