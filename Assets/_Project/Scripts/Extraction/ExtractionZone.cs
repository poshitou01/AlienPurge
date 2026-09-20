using UnityEngine;


[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ExtractionZone :
    InteractableBase
{
    // =========================================================
    // Defense
    // =========================================================

    [Header("Extraction Defense")]

    [Tooltip(
        "正式 Extraction Defense 的世界半径。"
        + "玩家在这个半径内时 Defense Progress 才会推进。"
    )]
    [Min(0.1f)]
    [SerializeField]
    private float defenseRadius =
        4.5f;


    // =========================================================
    // Runtime
    // =========================================================

    private ExtractionController controller;


    // =========================================================
    // Read Only
    // =========================================================

    public float DefenseRadius =>
        defenseRadius;


    public bool IsInitialized =>
        controller != null;


    // =========================================================
    // Initialization
    // =========================================================

    public void Initialize(
        ExtractionController owner
    )
    {
        controller =
            owner;
    }


    // =========================================================
    // Interaction
    // =========================================================

    public override bool CanInteract(
        PlayerInteractor interactor
    )
    {
        if (!base.CanInteract(
                interactor
            ))
        {
            return false;
        }


        if (controller == null)
        {
            return false;
        }


        return controller
            .CanCallExtraction(
                this
            );
    }


    public override void Interact(
        PlayerInteractor interactor
    )
    {
        if (!CanInteract(
                interactor
            ))
        {
            return;
        }


        controller.TryStartExtraction(
            this
        );
    }


    // =========================================================
    // Defense Zone Query
    // =========================================================

    public bool IsPlayerInsideDefenseZone(
        Transform player
    )
    {
        if (player == null)
        {
            return false;
        }


        Vector2 offset =
            (Vector2)player.position
            -
            WorldPosition;


        float radiusSquared =
            defenseRadius
            *
            defenseRadius;


        return offset.sqrMagnitude
            <= radiusSquared;
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        defenseRadius =
            Mathf.Max(
                0.1f,
                defenseRadius
            );
    }


    // =========================================================
    // Gizmos
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Color previousColor =
            Gizmos.color;


        Gizmos.color =
            new Color(
                0.1f,
                0.9f,
                1f,
                1f
            );


        Gizmos.DrawWireSphere(
            transform.position,
            defenseRadius
        );


        Gizmos.color =
            previousColor;
    }
}