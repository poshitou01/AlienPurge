using UnityEngine;

// Follow the combatants, never the changing bounds of a skill's effects.
[DefaultExecutionOrder(15000), DisallowMultipleComponent]
public sealed class HiveTyrantArenaCamera : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    [SerializeField, Min(.1f)] private float margin = 2;
    [SerializeField, Min(.1f)] private float smoothTime = .4f;
    [SerializeField, Min(1)] private float maximumPanSpeed = 8;
    private Camera view;
    private HiveTyrantPresentation presentation;
    private float originalSize, blend, size, sizeVelocity;
    private Vector3 renderedPosition, velocity;
    private bool ownsCamera;
    public float FramingBlend => blend;
    private void Awake()
    {
        view = GetComponent<Camera>(); originalSize = view.orthographicSize; size = originalSize;
        renderedPosition = transform.position;
        if (boss != null) presentation = boss.GetComponent<HiveTyrantPresentation>();
    }
    private void LateUpdate()
    {
        if (Time.timeScale <= 0 && ownsCamera)
        { transform.position = renderedPosition; view.orthographicSize = size; return; }
        bool active = boss != null && boss.Motor != null && boss.Health != null && !boss.Health.IsDead &&
            boss.TargetInArena && boss.State != HiveTyrantState.Dormant && boss.State != HiveTyrantState.Disabled &&
            (!boss.Motor.IsMovementLocked || boss.Motor.HasActionOwner);
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        blend = Mathf.MoveTowards(blend, active ? 1 : 0, dt * 1.8f);
        if (active && !ownsCamera)
        {
            ownsCamera = true; renderedPosition = transform.position; size = view.orthographicSize;
            velocity = Vector3.zero; sizeVelocity = 0;
        }
        if (!ownsCamera) return;
        Vector3 wantedPosition = transform.position; float wantedSize = originalSize;
        if (active)
        {
            if (presentation == null) presentation = boss.GetComponent<HiveTyrantPresentation>();
            Bounds box = presentation.FramingBounds;
            box.Encapsulate(boss.Motor.Target.position);
            wantedPosition = new Vector3(box.center.x, box.center.y, transform.position.z);
            wantedSize = Mathf.Max(originalSize, box.extents.y + margin, (box.extents.x + margin) / Mathf.Max(.5f, view.aspect));
        }
        float damping = active ? smoothTime : .2f;
        renderedPosition = Vector3.SmoothDamp(renderedPosition, wantedPosition, ref velocity, damping, active ? maximumPanSpeed : 20, dt);
        size = Mathf.SmoothDamp(size, wantedSize, ref sizeVelocity, damping, 8, dt);
        if (!active && blend == 0 && Vector3.Distance(renderedPosition, wantedPosition) < .03f && Mathf.Abs(size - originalSize) < .01f)
        { ownsCamera = false; size = originalSize; view.orthographicSize = originalSize; return; }
        transform.position = renderedPosition; view.orthographicSize = size;
    }
    private void OnDisable()
    { if (view != null) view.orthographicSize = originalSize; blend = 0; ownsCamera = false; velocity = Vector3.zero; sizeVelocity = 0; }
}
