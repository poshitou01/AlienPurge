using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class LootSearchItemView :
    MonoBehaviour
{
    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]

    [SerializeField]
    private Button button;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private Outline outline;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text quantityText;


    // =========================================================
    // Search Ring
    // =========================================================

    [Header("Search Ring")]

    [SerializeField]
    private GameObject searchRingRoot;

    [SerializeField]
    private Image searchRingFill;


    [Header("Search Ring Size")]

    [Range(0.25f, 0.8f)]
    [SerializeField]
    private float searchRingSizeRatio =
        0.48f;

    [Min(1f)]
    [SerializeField]
    private float minSearchRingSize =
        22f;

    [Min(1f)]
    [SerializeField]
    private float maxSearchRingSize =
        32f;


    // =========================================================
    // Runtime
    // =========================================================

    private LootContainerSearchEntry
        entry;

    private Action<
        LootContainerSearchEntry
    > clickCallback;


    // =========================================================
    // Read Only
    // =========================================================

    public LootContainerSearchEntry
        Entry =>
            entry;


    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(
        LootContainerSearchEntry
            searchEntry,
        float cellSize,
        float spacing,
        Action<
            LootContainerSearchEntry
        > onClicked
    )
    {
        entry =
            searchEntry;

        clickCallback =
            onClicked;


        if (entry == null ||
            entry.Stack == null ||
            entry.Stack.IsEmpty)
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


        ConfigureSearchRing();

        RefreshVisual();


        SetSearchProgress(
            0f,
            false
        );


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
        RectTransform rect =
            transform
            as RectTransform;


        if (rect == null)
        {
            return;
        }


        rect.anchorMin =
            new Vector2(
                0f,
                1f
            );


        rect.anchorMax =
            new Vector2(
                0f,
                1f
            );


        rect.pivot =
            new Vector2(
                0f,
                1f
            );


        float step =
            cellSize +
            spacing;


        rect.anchoredPosition =
            new Vector2(
                entry.Stack.GridX *
                step,

                -entry.Stack.GridY *
                step
            );


        float width =
            entry.Stack.Width *
            cellSize
            +
            Mathf.Max(
                0,
                entry.Stack.Width - 1
            )
            * spacing;


        float height =
            entry.Stack.Height *
            cellSize
            +
            Mathf.Max(
                0,
                entry.Stack.Height - 1
            )
            * spacing;


        rect.sizeDelta =
            new Vector2(
                width,
                height
            );


        ConfigureSearchRingSize(
            width,
            height
        );
    }


    // =========================================================
    // Search Ring Size
    // =========================================================

    private void ConfigureSearchRingSize(
        float itemWidth,
        float itemHeight
    )
    {
        if (searchRingRoot == null)
        {
            return;
        }


        RectTransform ringRect =
            searchRingRoot.transform
            as RectTransform;


        if (ringRect == null)
        {
            return;
        }


        float shortestSide =
            Mathf.Min(
                itemWidth,
                itemHeight
            );


        float targetSize =
            shortestSide *
            searchRingSizeRatio;


        targetSize =
            Mathf.Clamp(
                targetSize,
                minSearchRingSize,
                maxSearchRingSize
            );


        ringRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );


        ringRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );


        ringRect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );


        ringRect.anchoredPosition =
            Vector2.zero;


        ringRect.sizeDelta =
            new Vector2(
                targetSize,
                targetSize
            );


        ringRect.localScale =
            Vector3.one;
    }


    // =========================================================
    // Search Ring Setup
    // =========================================================

    private void ConfigureSearchRing()
    {
        if (searchRingFill == null)
        {
            return;
        }


        searchRingFill.type =
            Image.Type.Filled;

        searchRingFill.fillMethod =
            Image.FillMethod.Radial360;

        searchRingFill.fillOrigin =
            (int)Image.Origin360.Top;

        searchRingFill.fillClockwise =
            true;

        searchRingFill.fillAmount =
            0f;

        searchRingFill.raycastTarget =
            false;
    }


    // =========================================================
    // Visual
    // =========================================================

    private void RefreshVisual()
    {
        if (entry == null ||
            entry.Stack == null ||
            entry.Stack.Item == null)
        {
            return;
        }


        ItemData item =
            entry.Stack.Item;


        bool identified =
            entry.State ==
                LootSearchState.Identified;


        Color borderColor =
            identified
                ? ItemRarityVisuals.GetColor(
                    item.Rarity
                )
                : new Color(
                    0.45f,
                    0.48f,
                    0.52f,
                    1f
                );


        if (backgroundImage != null)
        {
            backgroundImage.color =
                new Color(
                    0.07f,
                    0.09f,
                    0.11f,
                    0.96f
                );
        }


        if (outline != null)
        {
            outline.effectColor =
                borderColor;
        }


        if (nameText != null)
        {
            if (identified)
            {
                nameText.text =
                    item.DisplayName;

                nameText.color =
                    borderColor;
            }
            else
            {
                nameText.text =
                    "?";

                nameText.color =
                    Color.white;
            }
        }


        if (quantityText != null)
        {
            quantityText.text =
                identified &&
                entry.Stack.Quantity > 1
                    ? "x" +
                      entry.Stack.Quantity
                    : string.Empty;
        }


        if (button != null)
        {
            button.interactable =
                identified;
        }
    }


    // =========================================================
    // Search Progress
    // =========================================================

    public void SetSearchProgress(
        float progress01,
        bool isCurrentSearchItem
    )
    {
        bool show =
            isCurrentSearchItem
            &&
            entry != null
            &&
            entry.State ==
                LootSearchState.Searching;


        if (searchRingRoot != null)
        {
            searchRingRoot.SetActive(
                show
            );
        }


        if (searchRingFill != null)
        {
            searchRingFill.fillAmount =
                show
                    ? Mathf.Clamp01(
                        progress01
                    )
                    : 0f;
        }
    }


    // =========================================================
    // Click
    // =========================================================

    private void HandleClick()
    {
        if (entry == null ||
            entry.State !=
                LootSearchState.Identified)
        {
            return;
        }


        clickCallback?.Invoke(
            entry
        );
    }
}