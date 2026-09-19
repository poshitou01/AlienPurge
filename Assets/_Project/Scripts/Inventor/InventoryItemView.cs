using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class InventoryItemView :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    // =========================================================
    // UI References
    // =========================================================

    [Header("UI References")]

    [SerializeField]
    private Button button;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private Outline rarityOutline;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text quantityText;


    // =========================================================
    // Runtime
    // =========================================================

    private ItemStack stack;

    private Action<ItemStack>
        clickCallback;

    private InventoryGridView
        ownerGridView;

    private RectTransform
        rectTransform;

    private Canvas rootCanvas;

    private CanvasGroup canvasGroup;


    // =========================================================
    // Drag Runtime
    // =========================================================

    private bool isDragging;

    private Vector2
        dragStartAnchoredPosition;

    private int
        dragStartSiblingIndex;

    private int
        suppressClickUntilFrame;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        rectTransform =
            transform
            as RectTransform;


        rootCanvas =
            GetComponentInParent<
                Canvas
            >();


        canvasGroup =
            GetComponent<
                CanvasGroup
            >();


        if (canvasGroup == null)
        {
            canvasGroup =
                gameObject.AddComponent<
                    CanvasGroup
                >();
        }
    }


    // =========================================================
    // Setup
    // =========================================================

    public void Initialize(
        ItemStack itemStack,
        float cellSize,
        float spacing,
        Action<ItemStack> onClicked,
        InventoryGridView owner
    )
    {
        stack =
            itemStack;

        clickCallback =
            onClicked;

        ownerGridView =
            owner;


        if (stack == null ||
            stack.IsEmpty ||
            stack.Item == null)
        {
            gameObject.SetActive(
                false
            );

            return;
        }


        ConfigureRect(
            cellSize,
            spacing
        );


        RefreshVisuals();


        if (button != null)
        {
            button.onClick
                .RemoveAllListeners();


            button.onClick
                .AddListener(
                    HandleClick
                );
        }
    }


    // =========================================================
    // Rect
    // =========================================================

    private void ConfigureRect(
        float cellSize,
        float spacing
    )
    {
        if (rectTransform == null)
        {
            rectTransform =
                transform
                as RectTransform;
        }


        if (rectTransform == null)
        {
            return;
        }


        rectTransform.anchorMin =
            new Vector2(
                0f,
                1f
            );


        rectTransform.anchorMax =
            new Vector2(
                0f,
                1f
            );


        rectTransform.pivot =
            new Vector2(
                0f,
                1f
            );


        float step =
            cellSize +
            spacing;


        rectTransform.anchoredPosition =
            new Vector2(
                stack.GridX *
                step,

                -stack.GridY *
                step
            );


        float width =
            stack.Width *
            cellSize
            +
            Mathf.Max(
                0,
                stack.Width - 1
            )
            * spacing;


        float height =
            stack.Height *
            cellSize
            +
            Mathf.Max(
                0,
                stack.Height - 1
            )
            * spacing;


        rectTransform.sizeDelta =
            new Vector2(
                width,
                height
            );
    }


    // =========================================================
    // Visuals
    // =========================================================

    private void RefreshVisuals()
    {
        if (stack == null ||
            stack.Item == null)
        {
            return;
        }


        ItemData item =
            stack.Item;


        Color rarityColor =
            ItemRarityVisuals.GetColor(
                item.Rarity
            );


        if (backgroundImage != null)
        {
            backgroundImage.color =
                new Color(
                    0.09f,
                    0.10f,
                    0.12f,
                    0.96f
                );
        }


        if (rarityOutline != null)
        {
            rarityOutline.effectColor =
                rarityColor;


            rarityOutline.effectDistance =
                new Vector2(
                    2f,
                    -2f
                );
        }


        if (nameText != null)
        {
            nameText.text =
                item.DisplayName;


            nameText.color =
                rarityColor;
        }


        if (quantityText != null)
        {
            quantityText.text =
                stack.Quantity > 1
                    ? "x" +
                      stack.Quantity
                    : string.Empty;
        }
    }


    // =========================================================
    // Drag Begin
    // =========================================================

    public void OnBeginDrag(
        PointerEventData eventData
    )
    {
        if (stack == null ||
            stack.IsEmpty ||
            ownerGridView == null ||
            rectTransform == null)
        {
            return;
        }


        isDragging =
            true;


        dragStartAnchoredPosition =
            rectTransform
                .anchoredPosition;


        dragStartSiblingIndex =
            transform
                .GetSiblingIndex();


        transform.SetAsLastSibling();


        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                0.88f;

            canvasGroup.blocksRaycasts =
                false;
        }


        suppressClickUntilFrame =
            Time.frameCount + 2;
    }


    // =========================================================
    // Drag
    // =========================================================

    public void OnDrag(
        PointerEventData eventData
    )
    {
        if (!isDragging ||
            rectTransform == null)
        {
            return;
        }


        float canvasScale =
            rootCanvas != null
                ? rootCanvas.scaleFactor
                : 1f;


        if (canvasScale <= 0f)
        {
            canvasScale = 1f;
        }


        rectTransform.anchoredPosition +=
            eventData.delta /
            canvasScale;


        UpdateDragFeedback();
    }


    // =========================================================
    // Drag End
    // =========================================================

    public void OnEndDrag(
        PointerEventData eventData
    )
    {
        if (!isDragging)
        {
            return;
        }


        isDragging =
            false;


        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                1f;

            canvasGroup.blocksRaycasts =
                true;
        }


        bool moved =
            TryCommitDrag();


        if (!moved &&
            rectTransform != null)
        {
            rectTransform
                .anchoredPosition =
                    dragStartAnchoredPosition;


            transform.SetSiblingIndex(
                dragStartSiblingIndex
            );


            RefreshVisuals();
        }


        suppressClickUntilFrame =
            Time.frameCount + 1;
    }


    // =========================================================
    // Drag Commit
    // =========================================================

    private bool TryCommitDrag()
    {
        if (ownerGridView == null ||
            rectTransform == null ||
            stack == null ||
            stack.IsEmpty)
        {
            return false;
        }


        if (!ownerGridView
                .TryGetGridPosition(
                    rectTransform
                        .anchoredPosition,
                    out int targetX,
                    out int targetY
                ))
        {
            return false;
        }


        if (!ownerGridView
                .CanMoveStack(
                    stack,
                    targetX,
                    targetY
                ))
        {
            return false;
        }


        return
            ownerGridView
                .TryMoveStack(
                    stack,
                    targetX,
                    targetY
                );
    }


    // =========================================================
    // Drag Feedback
    // =========================================================

    private void UpdateDragFeedback()
    {
        if (ownerGridView == null ||
            rectTransform == null ||
            stack == null)
        {
            return;
        }


        bool hasGridPosition =
            ownerGridView
                .TryGetGridPosition(
                    rectTransform
                        .anchoredPosition,
                    out int targetX,
                    out int targetY
                );


        bool canPlace =
            hasGridPosition
            &&
            ownerGridView
                .CanMoveStack(
                    stack,
                    targetX,
                    targetY
                );


        if (backgroundImage != null)
        {
            backgroundImage.color =
                canPlace
                    ? new Color(
                        0.08f,
                        0.18f,
                        0.11f,
                        0.96f
                    )
                    : new Color(
                        0.20f,
                        0.07f,
                        0.07f,
                        0.96f
                    );
        }


        if (rarityOutline != null)
        {
            rarityOutline.effectColor =
                canPlace
                    ? new Color(
                        0.25f,
                        1f,
                        0.40f,
                        1f
                    )
                    : new Color(
                        1f,
                        0.25f,
                        0.25f,
                        1f
                    );
        }
    }


    // =========================================================
    // Click
    // =========================================================

    private void HandleClick()
    {
        if (isDragging)
        {
            return;
        }


        if (Time.frameCount <=
            suppressClickUntilFrame)
        {
            return;
        }


        if (stack == null ||
            stack.IsEmpty)
        {
            return;
        }


        clickCallback?.Invoke(
            stack
        );
    }
}