using UnityEngine;


public enum PlayerFacingDirection
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


[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerEightDirectionAnimator : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip("玩家身体的 SpriteRenderer。")]
    [SerializeField]
    private SpriteRenderer bodySpriteRenderer;

    [Tooltip("玩家根节点上的 PlayerMovement。")]
    [SerializeField]
    private PlayerMovement playerMovement;

    [Tooltip("用于在玩家死亡后停止移动动画。")]
    [SerializeField]
    private PlayerHealth playerHealth;


    // =========================================================
    // Direction Frames
    // =========================================================

    [Header("Down Frames")]

    [Tooltip("向下移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] downFrames;


    [Header("Down Right Frames")]

    [Tooltip("右下移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] downRightFrames;


    [Header("Right Frames")]

    [Tooltip("向右移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] rightFrames;


    [Header("Up Right Frames")]

    [Tooltip("右上移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] upRightFrames;


    [Header("Up Frames")]

    [Tooltip("向上移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] upFrames;


    [Header("Up Left Frames")]

    [Tooltip("左上移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] upLeftFrames;


    [Header("Left Frames")]

    [Tooltip("向左移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] leftFrames;


    [Header("Down Left Frames")]

    [Tooltip("左下移动动画。按照播放顺序放入4帧。")]
    [SerializeField]
    private Sprite[] downLeftFrames;


    // =========================================================
    // Animation Settings
    // =========================================================

    [Header("Animation Settings")]

    [Tooltip("每秒播放多少帧。建议先使用 8。")]
    [Min(1f)]
    [SerializeField]
    private float framesPerSecond = 8f;

    [Tooltip(
        "没有移动输入时，显示当前朝向动画的第几帧。"
        + " 默认 0，即第一帧。"
    )]
    [Min(0)]
    [SerializeField]
    private int idleFrameIndex = 0;

    [Tooltip("游戏开始时角色默认朝向。")]
    [SerializeField]
    private PlayerFacingDirection defaultFacingDirection =
        PlayerFacingDirection.Down;


    // =========================================================
    // Runtime State
    // =========================================================

    private PlayerFacingDirection currentFacingDirection;

    private int currentFrameIndex;

    private float frameTimer;

    private bool wasMoving;


    // =========================================================
    // Public Read Only Access
    // =========================================================

    public PlayerFacingDirection CurrentFacingDirection =>
        currentFacingDirection;

    public bool IsMoving =>
        wasMoving;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        currentFacingDirection =
            defaultFacingDirection;

        currentFrameIndex = 0;
        frameTimer = 0f;
        wasMoving = false;

        ApplyIdleSprite();
    }


    private void Update()
    {
        if (bodySpriteRenderer == null
            || playerMovement == null)
        {
            return;
        }

        // 玩家死亡之后不再播放走路动画。
        if (playerHealth != null
            && playerHealth.IsDead)
        {
            StopMovementAnimation();
            return;
        }

        // PlayerMovement 被禁用时，
        // 不读取其中可能残留的上一帧 moveInput。
        if (!playerMovement.isActiveAndEnabled)
        {
            StopMovementAnimation();
            return;
        }

        Vector2 moveDirection =
            playerMovement.CurrentMoveInput;

        bool isMoving =
            moveDirection.sqrMagnitude
            > 0.0001f;

        if (!isMoving)
        {
            StopMovementAnimation();
            return;
        }

        UpdateFacingDirection(
            moveDirection
        );

        UpdateMovementAnimation();
    }


    // =========================================================
    // Reference Setup
    // =========================================================

    private void ResolveReferences()
    {
        if (bodySpriteRenderer == null)
        {
            bodySpriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponentInParent<PlayerMovement>();
        }

        if (playerHealth == null)
        {
            playerHealth =
                GetComponentInParent<PlayerHealth>();
        }

        if (bodySpriteRenderer == null)
        {
            Debug.LogError(
                "PlayerEightDirectionAnimator: "
                + "Body SpriteRenderer was not found.",
                this
            );
        }

        if (playerMovement == null)
        {
            Debug.LogError(
                "PlayerEightDirectionAnimator: "
                + "PlayerMovement was not found.",
                this
            );
        }
    }


    // =========================================================
    // Direction
    // =========================================================

    private void UpdateFacingDirection(
        Vector2 moveDirection)
    {
        PlayerFacingDirection newDirection =
            GetEightDirection(
                moveDirection
            );

        if (newDirection
            == currentFacingDirection)
        {
            return;
        }

        currentFacingDirection =
            newDirection;

        // 换方向以后从第一帧开始播放，
        // 避免不同方向切换时跳到随机动画帧。
        currentFrameIndex = 0;
        frameTimer = 0f;

        ApplyCurrentMovementSprite();
    }


    private PlayerFacingDirection GetEightDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude
            <= 0.0001f)
        {
            return currentFacingDirection;
        }

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
                    PlayerFacingDirection.Right;

            // 45°
            case 1:
                return
                    PlayerFacingDirection.UpRight;

            // 90°
            case 2:
                return
                    PlayerFacingDirection.Up;

            // 135°
            case 3:
                return
                    PlayerFacingDirection.UpLeft;

            // 180°
            case 4:
                return
                    PlayerFacingDirection.Left;

            // 225°
            case 5:
                return
                    PlayerFacingDirection.DownLeft;

            // 270°
            case 6:
                return
                    PlayerFacingDirection.Down;

            // 315°
            case 7:
                return
                    PlayerFacingDirection.DownRight;
        }

        return
            PlayerFacingDirection.Down;
    }


    // =========================================================
    // Movement Animation
    // =========================================================

    private void UpdateMovementAnimation()
    {
        Sprite[] frames =
            GetFramesForDirection(
                currentFacingDirection
            );

        if (frames == null
            || frames.Length == 0)
        {
            return;
        }

        // 刚从静止进入移动时，
        // 立即从第一帧开始。
        if (!wasMoving)
        {
            wasMoving = true;
            currentFrameIndex = 0;
            frameTimer = 0f;

            ApplyCurrentMovementSprite();
        }

        frameTimer +=
            Time.deltaTime;

        float secondsPerFrame =
            1f / framesPerSecond;

        while (frameTimer
               >= secondsPerFrame)
        {
            frameTimer -=
                secondsPerFrame;

            currentFrameIndex++;

            if (currentFrameIndex
                >= frames.Length)
            {
                currentFrameIndex = 0;
            }
        }

        ApplyCurrentMovementSprite();
    }


    private void ApplyCurrentMovementSprite()
    {
        Sprite[] frames =
            GetFramesForDirection(
                currentFacingDirection
            );

        if (frames == null
            || frames.Length == 0)
        {
            return;
        }

        currentFrameIndex =
            Mathf.Clamp(
                currentFrameIndex,
                0,
                frames.Length - 1
            );

        Sprite targetSprite =
            frames[currentFrameIndex];

        if (targetSprite != null)
        {
            bodySpriteRenderer.sprite =
                targetSprite;
        }
    }


    // =========================================================
    // Idle
    // =========================================================

    private void StopMovementAnimation()
    {
        if (!wasMoving)
        {
            return;
        }

        wasMoving = false;

        currentFrameIndex = 0;
        frameTimer = 0f;

        ApplyIdleSprite();
    }


    private void ApplyIdleSprite()
    {
        if (bodySpriteRenderer == null)
        {
            return;
        }

        Sprite[] frames =
            GetFramesForDirection(
                currentFacingDirection
            );

        if (frames == null
            || frames.Length == 0)
        {
            return;
        }

        int safeIdleIndex =
            Mathf.Clamp(
                idleFrameIndex,
                0,
                frames.Length - 1
            );

        Sprite idleSprite =
            frames[safeIdleIndex];

        if (idleSprite != null)
        {
            bodySpriteRenderer.sprite =
                idleSprite;
        }
    }


    // =========================================================
    // Direction Frame Lookup
    // =========================================================

    private Sprite[] GetFramesForDirection(
        PlayerFacingDirection direction)
    {
        switch (direction)
        {
            case PlayerFacingDirection.Down:
                return downFrames;

            case PlayerFacingDirection.DownRight:
                return downRightFrames;

            case PlayerFacingDirection.Right:
                return rightFrames;

            case PlayerFacingDirection.UpRight:
                return upRightFrames;

            case PlayerFacingDirection.Up:
                return upFrames;

            case PlayerFacingDirection.UpLeft:
                return upLeftFrames;

            case PlayerFacingDirection.Left:
                return leftFrames;

            case PlayerFacingDirection.DownLeft:
                return downLeftFrames;
        }

        return downFrames;
    }


    // =========================================================
    // Editor Validation
    // =========================================================

    private void OnValidate()
    {
        framesPerSecond =
            Mathf.Max(
                1f,
                framesPerSecond
            );

        idleFrameIndex =
            Mathf.Max(
                0,
                idleFrameIndex
            );

        if (bodySpriteRenderer == null)
        {
            bodySpriteRenderer =
                GetComponent<SpriteRenderer>();
        }
    }
}