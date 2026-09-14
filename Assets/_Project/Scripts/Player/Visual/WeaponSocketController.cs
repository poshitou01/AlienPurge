using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class WeaponSocketController : MonoBehaviour
{
    // =========================================================
    // Direction
    // =========================================================

    private enum FacingDirection
    {
        Down,
        DownRight,
        Right,
        UpRight,
        Up,
        UpLeft,
        Left,
        DownLeft
    }


    // =========================================================
    // Socket Pose
    // =========================================================

    [System.Serializable]
    private struct WeaponSocketPose
    {
        [Tooltip(
            "相对于 WeaponSocket 基础位置的偏移。"
            + "只负责身体持枪位置，不负责鼠标瞄准。"
        )]
        public Vector2 localOffset;

        [Tooltip(
            "该身体朝向下，整个 WeaponHolder "
            + "相对角色身体的 Sorting Order。"
        )]
        public int sortingOrder;
    }


    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip(
        "Player Root 上的 PlayerAnimationDriver。"
        + "WeaponSocket 从这里读取身体最后朝向。"
    )]
    [SerializeField]
    private PlayerAnimationDriver animationDriver;

    [Tooltip(
        "WeaponHolder 上的 SortingGroup。"
        + "负责整把武器相对 BodyVisual 的前后关系。"
    )]
    [SerializeField]
    private SortingGroup weaponSortingGroup;


    // =========================================================
    // Direction Poses
    // =========================================================

    [Header("Down")]
    [SerializeField]
    private WeaponSocketPose downPose;


    [Header("Down Right")]
    [SerializeField]
    private WeaponSocketPose downRightPose;


    [Header("Right")]
    [SerializeField]
    private WeaponSocketPose rightPose;


    [Header("Up Right")]
    [SerializeField]
    private WeaponSocketPose upRightPose;


    [Header("Up")]
    [SerializeField]
    private WeaponSocketPose upPose;


    [Header("Up Left")]
    [SerializeField]
    private WeaponSocketPose upLeftPose;


    [Header("Left")]
    [SerializeField]
    private WeaponSocketPose leftPose;


    [Header("Down Left")]
    [SerializeField]
    private WeaponSocketPose downLeftPose;


    // =========================================================
    // Runtime
    // =========================================================

    private Vector3 baseLocalPosition;

    private FacingDirection currentDirection;

    private bool hasAppliedDirection;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        baseLocalPosition =
            transform.localPosition;
    }


    private void Start()
    {
        ApplyCurrentPose(true);
    }


    private void LateUpdate()
    {
        ApplyCurrentPose(false);
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (animationDriver == null)
        {
            animationDriver =
                GetComponentInParent<
                    PlayerAnimationDriver
                >();
        }


        if (weaponSortingGroup == null)
        {
            Transform weaponHolder =
                transform.Find("WeaponHolder");

            if (weaponHolder != null)
            {
                weaponSortingGroup =
                    weaponHolder.GetComponent<
                        SortingGroup
                    >();
            }
        }


        if (animationDriver == null)
        {
            Debug.LogError(
                "WeaponSocketController: "
                + "PlayerAnimationDriver was not found.",
                this
            );
        }


        if (weaponSortingGroup == null)
        {
            Debug.LogError(
                "WeaponSocketController: "
                + "WeaponHolder SortingGroup was not found.",
                this
            );
        }
    }


    // =========================================================
    // Pose Update
    // =========================================================

    private void ApplyCurrentPose(
        bool forceApply)
    {
        if (animationDriver == null)
        {
            return;
        }


        Vector2 facingDirection =
            animationDriver.LastFacingDirection;


        if (facingDirection.sqrMagnitude
            <= 0.0001f)
        {
            facingDirection =
                Vector2.down;
        }


        FacingDirection newDirection =
            GetEightDirection(
                facingDirection
            );


        if (!forceApply
            && hasAppliedDirection
            && newDirection == currentDirection)
        {
            return;
        }


        currentDirection =
            newDirection;

        hasAppliedDirection = true;


        WeaponSocketPose pose =
            GetPose(
                currentDirection
            );


        Vector3 targetLocalPosition =
            baseLocalPosition;

        targetLocalPosition.x +=
            pose.localOffset.x;

        targetLocalPosition.y +=
            pose.localOffset.y;


        transform.localPosition =
            targetLocalPosition;


        if (weaponSortingGroup != null)
        {
            weaponSortingGroup.sortingOrder =
                pose.sortingOrder;
        }
    }


    // =========================================================
    // Direction Quantization
    // =========================================================

    private FacingDirection GetEightDirection(
        Vector2 direction)
    {
        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            )
            * Mathf.Rad2Deg;


        if (angle < 0f)
        {
            angle += 360f;
        }


        int sector =
            Mathf.RoundToInt(
                angle / 45f
            )
            % 8;


        switch (sector)
        {
            // 0°
            case 0:
                return
                    FacingDirection.Right;

            // 45°
            case 1:
                return
                    FacingDirection.UpRight;

            // 90°
            case 2:
                return
                    FacingDirection.Up;

            // 135°
            case 3:
                return
                    FacingDirection.UpLeft;

            // 180°
            case 4:
                return
                    FacingDirection.Left;

            // 225°
            case 5:
                return
                    FacingDirection.DownLeft;

            // 270°
            case 6:
                return
                    FacingDirection.Down;

            // 315°
            case 7:
                return
                    FacingDirection.DownRight;
        }


        return FacingDirection.Down;
    }


    // =========================================================
    // Pose Lookup
    // =========================================================

    private WeaponSocketPose GetPose(
        FacingDirection direction)
    {
        switch (direction)
        {
            case FacingDirection.Down:
                return downPose;

            case FacingDirection.DownRight:
                return downRightPose;

            case FacingDirection.Right:
                return rightPose;

            case FacingDirection.UpRight:
                return upRightPose;

            case FacingDirection.Up:
                return upPose;

            case FacingDirection.UpLeft:
                return upLeftPose;

            case FacingDirection.Left:
                return leftPose;

            case FacingDirection.DownLeft:
                return downLeftPose;
        }


        return downPose;
    }
}