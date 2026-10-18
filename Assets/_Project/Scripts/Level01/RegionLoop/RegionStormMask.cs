using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    // Presentation only. The native storm and safe-zone controllers remain the damage authority.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RegionStormMask : MonoBehaviour
    {
        public RegionLoopController controller;
        public Material material;
        public float CurrentAlpha { get; private set; }
        Mesh mesh;
        MeshRenderer display;
        MaterialPropertyBlock properties;
        void Awake()
        {
            mesh = new Mesh { name = "Region storm coverage" };
            mesh.vertices = new[] { new Vector3(-1024,-1024), new Vector3(1024,-1024), new Vector3(1024,1024), new Vector3(-1024,1024) };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            display = GetComponent<MeshRenderer>(); display.sharedMaterial = material;
            display.sortingLayerName = "VFX"; display.sortingOrder = 2; display.enabled = false;
            properties = new MaterialPropertyBlock();
        }
        void LateUpdate()
        {
            bool show = controller != null && controller.v02Enabled && !controller.ShuttingDown && controller.safeZone.HasActiveZone && controller.storm.CurrentPhase != StormPhase.Calm;
            display.enabled = show;
            if (!show) { CurrentAlpha = 0; return; }
            var phase = controller.storm.CurrentPhase;
            CurrentAlpha = phase == StormPhase.Warning ? .08f : phase == StormPhase.Recovery ? .22f * Mathf.Clamp01(controller.storm.PhaseTimeRemaining / 3) : .22f;
            var center = controller.safeZone.CurrentCenter;
            properties.SetVector("_SafeZone", new Vector4(center.x, center.y, controller.safeZone.Radius, 0));
            properties.SetColor("_Color", new Color(.16f,.23f,.42f,CurrentAlpha));
            display.SetPropertyBlock(properties);
        }
        void OnDisable() { if (display != null) display.enabled = false; }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
