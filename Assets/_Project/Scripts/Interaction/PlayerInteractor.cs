
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerInteractor : MonoBehaviour
{
    // =========================================================
    // Interaction Settings
    // =========================================================

    [Header("Interaction Settings")]

    [Tooltip("玩家可以与世界对象交互的最大距离。")]
    [Min(0.1f)]
    [SerializeField]
    private float interactionDistance = 1.8f;

    [Tooltip("只查询可交互对象所在的 Physics Layer。")]
    [SerializeField]
    private LayerMask interactableLayerMask;


    // =========================================================
    // Phase40 - Environment Occlusion
    // =========================================================

    [Header("Phase40 - Environment Occlusion")]

    [Tooltip(
        "是否阻止隔着实体墙与物体交互。" +
        "默认关闭，以保留旧场景行为。" +
        "仅在 Level01 测试场景中开启。"
    )]
    [SerializeField]
    private bool blockInteractionsThroughEnvironment = false;

    [Tooltip(
        "能够阻挡交互视线的物理层。" +
        "Level01 应设置为 Environment。"
    )]
    [SerializeField]
    private LayerMask environmentBlockingLayers = 1 << 7;


    // =========================================================
    // Player State
    // =========================================================

    [Header("Player State")]

    [SerializeField]
    private PlayerHealth playerHealth;


    // =========================================================
    // Interaction Prompt UI
    // =========================================================

    [Header("Interaction Prompt UI")]

    [SerializeField]
    private CanvasGroup interactionPromptCanvasGroup;

    [SerializeField]
    private TextMeshProUGUI interactionPromptText;


    // =========================================================
    // Runtime
    // =========================================================

    private const int QueryBufferSize = 8;

    private readonly Collider2D[] overlapResults =
        new Collider2D[QueryBufferSize];

    private const int EnvironmentHitBufferSize = 32;

    private readonly RaycastHit2D[] environmentHits =
        new RaycastHit2D[EnvironmentHitBufferSize];

    private InteractableBase currentInteractable;


    public InteractableBase CurrentInteractable =>
        currentInteractable;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth =
                GetComponent<PlayerHealth>();
        }

        ConfigurePromptUI();

        ClearCurrentInteractable();

        if (blockInteractionsThroughEnvironment &&
            environmentBlockingLayers.value == 0)
        {
            Debug.LogWarning(
                "PlayerInteractor: Environment occlusion " +
                "is enabled, but Environment Blocking " +
                "Layers is empty.",
                this
            );
        }
    }


    private void Update()
    {
        if (!CanPlayerInteract())
        {
            ClearCurrentInteractable();
            return;
        }

        FindClosestInteractable();

        RefreshInteractionPrompt();

        if (currentInteractable == null)
        {
            return;
        }

        if (!Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        // 在实际交互之前再次检查。
        //
        // 目标状态或墙体遮挡可能已经变化。
        if (!currentInteractable.CanInteract(this) ||
            IsInteractionBlocked(currentInteractable))
        {
            ClearCurrentInteractable();
            return;
        }

        currentInteractable.Interact(this);
    }


    private void OnDisable()
    {
        ClearCurrentInteractable();
    }


    // =========================================================
    // Player State
    // =========================================================

    private bool CanPlayerInteract()
    {
        // 没有正式 GameManager 时，
        // 不执行世界交互。
        if (GameManager.Instance == null)
        {
            return false;
        }

        // GameOver / Victory 后禁止交互。
        if (!GameManager.Instance.IsPlaying)
        {
            return false;
        }

        // Pause、Upgrade、Module Selection 等
        // 状态通过 Time.timeScale 暂停 Gameplay。
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        if (playerHealth != null &&
            playerHealth.IsDead)
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // Query
    // =========================================================

    private void FindClosestInteractable()
    {
        int hitCount =
            Physics2D.OverlapCircleNonAlloc(
                transform.position,
                interactionDistance,
                overlapResults,
                interactableLayerMask
            );

        InteractableBase bestInteractable = null;

        float bestDistanceSquared =
            float.PositiveInfinity;

        Vector2 playerPosition =
            transform.position;

        float maxInteractionDistanceSquared =
            interactionDistance * interactionDistance;


        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider =
                overlapResults[i];

            if (hitCollider == null)
            {
                continue;
            }

            InteractableBase interactable =
                hitCollider.GetComponentInParent<
                    InteractableBase
                >();

            if (interactable == null)
            {
                continue;
            }

            if (!interactable.CanInteract(this))
            {
                continue;
            }

            Vector2 offset =
                interactable.WorldPosition -
                playerPosition;

            float distanceSquared =
                offset.sqrMagnitude;

            if (distanceSquared >
                maxInteractionDistanceSquared)
            {
                continue;
            }

            if (distanceSquared >=
                bestDistanceSquared)
            {
                continue;
            }

            // Phase40:
            // 只允许选择无遮挡的交互对象。
            //
            // 被货柜遮挡的对象会被跳过，
            // 不会挡住其他合法交互对象。
            if (IsInteractionBlocked(interactable))
            {
                continue;
            }

            bestDistanceSquared =
                distanceSquared;

            bestInteractable =
                interactable;
        }

        currentInteractable =
            bestInteractable;
    }


    // =========================================================
    // Phase40 - Environment Line of Sight
    // =========================================================

    private bool IsInteractionBlocked(
        InteractableBase interactable)
    {
        // 默认关闭：
        // 原有 Day01 行为保持不变。
        if (!blockInteractionsThroughEnvironment)
        {
            return false;
        }

        if (interactable == null)
        {
            return false;
        }

        if (environmentBlockingLayers.value == 0)
        {
            return false;
        }

        Vector2 origin =
            transform.position;

        Vector2 target =
            interactable.WorldPosition;

        Vector2 offset =
            target - origin;

        float distanceSquared =
            offset.sqrMagnitude;

        // 极近距离直接允许交互，
        // 避免除以零。
        if (distanceSquared <= 0.000001f)
        {
            return false;
        }

        float distance =
            Mathf.Sqrt(distanceSquared);

        Vector2 direction =
            offset / distance;


        // 只检测指定 Environment Layer
        // 中的非 Trigger 实体 Collider。
        //
        // LootContainer 自身的交互 Trigger
        // 不会被当作阻挡物。
        ContactFilter2D contactFilter =
            new ContactFilter2D();

        contactFilter.SetLayerMask(
            environmentBlockingLayers
        );

        contactFilter.useTriggers = false;


        // 使用非分配版本的射线检测，
        // 避免每帧产生新数组。
        int hitCount =
            Physics2D.Raycast(
                origin,
                direction,
                contactFilter,
                environmentHits,
                distance
            );


        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider =
                environmentHits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            // 如果交互对象将来拥有自己的
            // 实体 Collider，不要把目标自身
            // 误判成隔墙障碍。
            Transform hitTransform =
                hitCollider.transform;

            Transform targetTransform =
                interactable.transform;

            if (hitTransform == targetTransform ||
                hitTransform.IsChildOf(targetTransform))
            {
                continue;
            }

            // 射线在抵达目标之前
            // 命中了其他实体 Environment。
            return true;
        }

        return false;
    }


    // =========================================================
    // Prompt
    // =========================================================

    private void ConfigurePromptUI()
    {
        if (interactionPromptCanvasGroup != null)
        {
            interactionPromptCanvasGroup.interactable =
                false;

            interactionPromptCanvasGroup.blocksRaycasts =
                false;
        }

        if (interactionPromptText != null)
        {
            interactionPromptText.raycastTarget =
                false;
        }
    }


    private void RefreshInteractionPrompt()
    {
        if (currentInteractable == null)
        {
            HideInteractionPrompt();
            return;
        }

        if (interactionPromptText != null)
        {
            interactionPromptText.text =
                "[E] " +
                currentInteractable.InteractionPrompt;
        }

        if (interactionPromptCanvasGroup != null)
        {
            interactionPromptCanvasGroup.alpha = 1f;
        }
    }


    private void HideInteractionPrompt()
    {
        if (interactionPromptCanvasGroup != null)
        {
            interactionPromptCanvasGroup.alpha = 0f;
        }

        if (interactionPromptText != null)
        {
            interactionPromptText.text =
                string.Empty;
        }
    }


    private void ClearCurrentInteractable()
    {
        currentInteractable = null;

        HideInteractionPrompt();
    }


    // =========================================================
    // Debug
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            interactionDistance
        );

        if (!Application.isPlaying ||
            !blockInteractionsThroughEnvironment ||
            currentInteractable == null)
        {
            return;
        }

        Gizmos.color =
            IsInteractionBlocked(currentInteractable)
                ? Color.red
                : Color.green;

        Gizmos.DrawLine(
            transform.position,
            currentInteractable.WorldPosition
        );
    }
}
