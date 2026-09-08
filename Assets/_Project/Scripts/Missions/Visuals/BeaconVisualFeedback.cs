using UnityEngine;

[DisallowMultipleComponent]
public class BeaconVisualFeedback :
    MonoBehaviour
{
    private enum BeaconVisualState
    {
        Waiting,
        ActiveInside,
        ActiveOutside
    }


    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip(
        "只绑定 Beacon 的视觉子节点，"
        + "不要绑定带 Collider 的根对象。"
    )]
    [SerializeField]
    private Transform pulseTarget;

    [SerializeField]
    private SpriteRenderer bodyRenderer;

    [SerializeField]
    private SpriteRenderer glowRenderer;

    [SerializeField]
    private LineRenderer activationRangeVisual;


    // =========================================================
    // Colors
    // =========================================================

    [Header("Colors")]

    [SerializeField]
    private Color waitingColor =
        new Color(
            0.45f,
            0.85f,
            1f,
            1f
        );

    [SerializeField]
    private Color activeInsideColor =
        new Color(
            0.15f,
            1f,
            0.85f,
            1f
        );

    [SerializeField]
    private Color activeOutsideColor =
        new Color(
            1f,
            0.55f,
            0.15f,
            1f
        );


    // =========================================================
    // Pulse
    // =========================================================

    [Header("Pulse")]

    [Min(0f)]
    [SerializeField]
    private float waitingPulseSpeed = 2f;

    [Min(0f)]
    [SerializeField]
    private float activePulseSpeed = 5f;

    [Min(0f)]
    [SerializeField]
    private float outsidePulseSpeed = 1.5f;

    [Range(0f, 0.25f)]
    [SerializeField]
    private float pulseAmount = 0.06f;


    // =========================================================
    // Runtime
    // =========================================================

    private BeaconVisualState currentState =
        BeaconVisualState.Waiting;

    private Vector3 originalScale;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (pulseTarget != null)
        {
            originalScale =
                pulseTarget.localScale;
        }


        SetWaiting();
    }


    private void Update()
    {
        if (pulseTarget == null)
        {
            return;
        }


        float pulseSpeed =
            GetCurrentPulseSpeed();


        float pulse =
            1f
            + Mathf.Sin(
                Time.time
                * pulseSpeed
            )
            * pulseAmount;


        pulseTarget.localScale =
            originalScale
            * pulse;
    }


    private void OnDisable()
    {
        if (pulseTarget != null)
        {
            pulseTarget.localScale =
                originalScale;
        }
    }


    // =========================================================
    // Public State
    // =========================================================

    public void SetWaiting()
    {
        currentState =
            BeaconVisualState.Waiting;


        if (activationRangeVisual != null)
        {
            activationRangeVisual
                .gameObject
                .SetActive(false);
        }


        ApplyColor(
            waitingColor
        );
    }


    public void SetActivated(
        bool playerInside
    )
    {
        if (activationRangeVisual != null)
        {
            activationRangeVisual
                .gameObject
                .SetActive(true);
        }


        SetPlayerInside(
            playerInside
        );
    }


    public void SetPlayerInside(
        bool playerInside
    )
    {
        BeaconVisualState newState =
            playerInside
                ? BeaconVisualState
                    .ActiveInside
                : BeaconVisualState
                    .ActiveOutside;


        if (currentState == newState)
        {
            return;
        }


        currentState =
            newState;


        switch (currentState)
        {
            case BeaconVisualState.ActiveInside:

                ApplyColor(
                    activeInsideColor
                );

                break;


            case BeaconVisualState.ActiveOutside:

                ApplyColor(
                    activeOutsideColor
                );

                break;
        }
    }


    // =========================================================
    // Visual
    // =========================================================

    private void ApplyColor(
        Color color
    )
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.color =
                color;
        }


        if (glowRenderer != null)
        {
            Color glowColor =
                color;

            glowColor.a =
                0.35f;


            glowRenderer.color =
                glowColor;
        }


        if (activationRangeVisual != null)
        {
            activationRangeVisual.startColor =
                color;

            activationRangeVisual.endColor =
                color;
        }
    }


    private float GetCurrentPulseSpeed()
    {
        switch (currentState)
        {
            case BeaconVisualState.ActiveInside:

                return activePulseSpeed;


            case BeaconVisualState.ActiveOutside:

                return outsidePulseSpeed;


            case BeaconVisualState.Waiting:
            default:

                return waitingPulseSpeed;
        }
    }
}