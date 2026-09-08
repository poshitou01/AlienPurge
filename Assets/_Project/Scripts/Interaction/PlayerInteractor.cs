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

    [Tooltip("玩家可以与世界对象进行交互的最大距离。")]
    [Min(0.1f)]
    [SerializeField]
    private float interactionDistance = 1.8f;

    [Tooltip("只查询可交互对象所在的 Physics Layer。")]
    [SerializeField]
    private LayerMask interactableLayerMask;


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

        // 在真正调用 Interact() 前再次检查。
        //
        // 因为目标的状态可能在这一帧刚刚发生变化。
        if (!currentInteractable.CanInteract(this))
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
        // 没有正式 GameManager 时，不执行世界交互。
        if (GameManager.Instance == null)
        {
            return false;
        }

        // GameOver / Victory 后禁止交互。
        if (!GameManager.Instance.IsPlaying)
        {
            return false;
        }

        // 当前项目中的：
        //
        // Pause
        // Upgrade
        // Weapon Module Selection
        // Overclock
        //
        // 都会通过 Time.timeScale = 0 暂停 Gameplay。
        //
        // 因此 PlayerInteractor 不需要分别依赖这些 Manager。
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        if (playerHealth != null
            && playerHealth.IsDead)
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


        for (int i = 0;
             i < hitCount;
             i++)
        {
            Collider2D hitCollider =
                overlapResults[i];

            if (hitCollider == null)
            {
                continue;
            }


            InteractableBase interactable =
                hitCollider
                    .GetComponentInParent<
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
                interactable.WorldPosition
                - playerPosition;


            float distanceSquared =
                offset.sqrMagnitude;

            float maxInteractionDistanceSquared =
    interactionDistance
    * interactionDistance;


            if (distanceSquared
                > maxInteractionDistanceSquared)
            {
                continue;
            }

            if (distanceSquared
                >= bestDistanceSquared)
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
    // Prompt
    // =========================================================

    private void ConfigurePromptUI()
    {
        if (interactionPromptCanvasGroup
            != null)
        {
            interactionPromptCanvasGroup
                .interactable = false;

            interactionPromptCanvasGroup
                .blocksRaycasts = false;
        }


        if (interactionPromptText != null)
        {
            interactionPromptText
                .raycastTarget = false;
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
                "[E] "
                + currentInteractable
                    .InteractionPrompt;
        }


        if (interactionPromptCanvasGroup
            != null)
        {
            interactionPromptCanvasGroup
                .alpha = 1f;
        }
    }


    private void HideInteractionPrompt()
    {
        if (interactionPromptCanvasGroup
            != null)
        {
            interactionPromptCanvasGroup
                .alpha = 0f;
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
    }
}