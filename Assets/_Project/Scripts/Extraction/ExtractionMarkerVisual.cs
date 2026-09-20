using UnityEngine;


[DisallowMultipleComponent]
public class ExtractionMarkerVisual :
    MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Transform groundCircle;


    [SerializeField]
    private SpriteRenderer groundRenderer;


    [SerializeField]
    private SpriteRenderer beamRenderer;


    [Header("Pulse")]

    [SerializeField]
    private float pulseSpeed = 2f;


    [SerializeField]
    private float minScaleMultiplier = 0.94f;


    [SerializeField]
    private float maxScaleMultiplier = 1.06f;


    [SerializeField]
    private float minBeamAlpha = 0.18f;


    [SerializeField]
    private float maxBeamAlpha = 0.35f;


    private Vector3 baseGroundScale;


    private void Awake()
    {
        if (groundCircle != null)
        {
            baseGroundScale =
                groundCircle.localScale;
        }
    }


    private void Update()
    {
        float pulse01 =
            (
                Mathf.Sin(
                    Time.time * pulseSpeed
                )
                + 1f
            )
            * 0.5f;


        if (groundCircle != null)
        {
            float scaleMultiplier =
                Mathf.Lerp(
                    minScaleMultiplier,
                    maxScaleMultiplier,
                    pulse01
                );


            groundCircle.localScale =
                baseGroundScale
                * scaleMultiplier;
        }


        if (beamRenderer != null)
        {
            Color beamColor =
                beamRenderer.color;


            beamColor.a =
                Mathf.Lerp(
                    minBeamAlpha,
                    maxBeamAlpha,
                    pulse01
                );


            beamRenderer.color =
                beamColor;
        }
    }
}