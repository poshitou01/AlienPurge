using UnityEngine;

// Runs after the existing follow camera. Its normal target, bounds and settings are never rewritten.
[DefaultExecutionOrder(15000), DisallowMultipleComponent]
public sealed class HiveTyrantArenaCamera : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    [SerializeField, Min(.1f)] private float margin = 2;
    private Camera view;
    private float originalSize, blend;
    private Vector3 focus;
    private float size;
    public float FramingBlend => blend;
    private void Awake() { view = GetComponent<Camera>(); originalSize = view.orthographicSize; size = originalSize; }
    private void LateUpdate()
    {
        bool active = boss != null && boss.Motor != null && boss.TargetInArena && boss.State != HiveTyrantState.Dormant &&
            boss.State != HiveTyrantState.Disabled && (!boss.Motor.IsMovementLocked || boss.Motor.HasActionOwner);
        blend = Mathf.MoveTowards(blend, active ? 1 : 0, Time.unscaledDeltaTime * 1.8f);
        if (active)
        {
            Bounds box = boss.GetComponent<HiveTyrantPresentation>().VisualBounds;
            box.Encapsulate(boss.Motor.Target.position);
            if (boss.Wave != null && boss.Wave.Plan != null && (boss.Wave.IsWarning || boss.Wave.IsTravelling))
            {
                var plan = boss.Wave.Plan;
                box.Encapsulate(plan.Point(-plan.width * 1.5f - plan.gap, plan.MaximumLength));
                box.Encapsulate(plan.Point(plan.width * 1.5f + plan.gap, plan.MaximumLength));
            }
            focus = new Vector3(box.center.x, box.center.y, transform.position.z);
            float wantedSize = Mathf.Max(originalSize, box.extents.y + margin, (box.extents.x + margin) / Mathf.Max(.5f, view.aspect));
            size = Mathf.Lerp(size, wantedSize, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5));
        }
        view.orthographicSize = Mathf.Lerp(originalSize, size, blend);
        transform.position = Vector3.Lerp(transform.position, focus, blend);
    }
    private void OnDisable() { if (view != null) view.orthographicSize = originalSize; blend = 0; }
}
