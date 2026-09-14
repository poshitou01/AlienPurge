using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimationDriver : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private PlayerMovement playerMovement;

    [SerializeField]
    private Animator bodyAnimator;


    [Header("Facing")]

    [Tooltip("游戏开始时默认朝向。(0,-1) = Down")]
    [SerializeField]
    private Vector2 defaultFacingDirection =
        Vector2.down;

    [Min(0.0001f)]
    [SerializeField]
    private float movementThreshold =
        0.01f;


    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    private static readonly int MoveXHash =
        Animator.StringToHash("MoveX");

    private static readonly int MoveYHash =
        Animator.StringToHash("MoveY");

    private static readonly int LastMoveXHash =
        Animator.StringToHash("LastMoveX");

    private static readonly int LastMoveYHash =
        Animator.StringToHash("LastMoveY");


    private Vector2 lastFacingDirection;


    public Vector2 LastFacingDirection =>
        lastFacingDirection;


    private void Awake()
    {
        ResolveReferences();

        if (defaultFacingDirection.sqrMagnitude
            <= 0.0001f)
        {
            defaultFacingDirection =
                Vector2.down;
        }

        lastFacingDirection =
            defaultFacingDirection.normalized;

        InitializeAnimator();
    }


    private void Update()
    {
        UpdateLocomotion();
    }


    private void ResolveReferences()
    {
        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement>();
        }

        if (playerMovement == null)
        {
            Debug.LogError(
                "PlayerAnimationDriver: "
                + "PlayerMovement reference is missing.",
                this
            );
        }

        if (bodyAnimator == null)
        {
            Debug.LogError(
                "PlayerAnimationDriver: "
                + "Body Animator reference is missing.",
                this
            );
        }
    }


    private void InitializeAnimator()
    {
        if (bodyAnimator == null)
        {
            return;
        }

        bodyAnimator.SetBool(
            IsMovingHash,
            false
        );

        bodyAnimator.SetFloat(
            MoveXHash,
            0f
        );

        bodyAnimator.SetFloat(
            MoveYHash,
            0f
        );

        bodyAnimator.SetFloat(
            LastMoveXHash,
            lastFacingDirection.x
        );

        bodyAnimator.SetFloat(
            LastMoveYHash,
            lastFacingDirection.y
        );
    }


    private void UpdateLocomotion()
    {
        if (playerMovement == null
            || bodyAnimator == null)
        {
            return;
        }

        Vector2 moveInput =
            playerMovement.CurrentMoveInput;

        float thresholdSquared =
            movementThreshold
            * movementThreshold;

        bool isMoving =
            moveInput.sqrMagnitude
            > thresholdSquared;

        bodyAnimator.SetBool(
            IsMovingHash,
            isMoving
        );

        if (!isMoving)
        {
            return;
        }

        Vector2 direction =
            moveInput.normalized;

        lastFacingDirection =
            direction;

        bodyAnimator.SetFloat(
            MoveXHash,
            direction.x
        );

        bodyAnimator.SetFloat(
            MoveYHash,
            direction.y
        );

        bodyAnimator.SetFloat(
            LastMoveXHash,
            direction.x
        );

        bodyAnimator.SetFloat(
            LastMoveYHash,
            direction.y
        );
    }


    private void OnValidate()
    {
        movementThreshold =
            Mathf.Max(
                0.0001f,
                movementThreshold
            );

        if (defaultFacingDirection.sqrMagnitude
            <= 0.0001f)
        {
            defaultFacingDirection =
                Vector2.down;
        }
        else
        {
            defaultFacingDirection =
                defaultFacingDirection.normalized;
        }
    }
}