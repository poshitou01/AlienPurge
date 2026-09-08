using UnityEngine;

[DisallowMultipleComponent]
public class MissionObjectiveFloatVisual :
    MonoBehaviour
{
    // =========================================================
    // Reference
    // =========================================================

    [Header("Reference")]

    [Tooltip(
        "只绑定视觉子对象，"
        + "不要绑定带 Collider 的 Objective Root。"
    )]
    [SerializeField]
    private Transform visualTarget;


    // =========================================================
    // Float
    // =========================================================

    [Header("Floating")]

    [Min(0f)]
    [SerializeField]
    private float bobAmplitude =
        0.08f;

    [Min(0f)]
    [SerializeField]
    private float bobSpeed =
        2.5f;


    // =========================================================
    // Rotation
    // =========================================================

    [Header("Rotation")]

    [SerializeField]
    private float rotationSpeed =
        25f;


    // =========================================================
    // Runtime
    // =========================================================

    private Vector3 originalLocalPosition;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (visualTarget == null)
        {
            return;
        }


        originalLocalPosition =
            visualTarget
                .localPosition;
    }


    private void Update()
    {
        if (visualTarget == null)
        {
            return;
        }


        float verticalOffset =
            Mathf.Sin(
                Time.time
                * bobSpeed
            )
            * bobAmplitude;


        Vector3 targetPosition =
            originalLocalPosition;


        targetPosition.y +=
            verticalOffset;


        visualTarget.localPosition =
            targetPosition;


        visualTarget.Rotate(
            0f,
            0f,
            rotationSpeed
                * Time.deltaTime
        );
    }


    private void OnDisable()
    {
        if (visualTarget == null)
        {
            return;
        }


        visualTarget.localPosition =
            originalLocalPosition;
    }
}