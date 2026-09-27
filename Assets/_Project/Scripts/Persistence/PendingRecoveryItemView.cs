using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public sealed class PendingRecoveryItemView :
    MonoBehaviour
{
    // =========================================================
    // UI
    // =========================================================

    [SerializeField]
    private Image iconImage;


    [SerializeField]
    private TMP_Text nameText;


    [SerializeField]
    private TMP_Text metaText;


    [SerializeField]
    private TMP_Text quantityText;


    [SerializeField]
    private TMP_Text valueText;


    [SerializeField]
    private Button moveButton;


    [SerializeField]
    private TMP_Text moveButtonText;


    [SerializeField]
    private Button sellButton;


    [SerializeField]
    private TMP_Text sellButtonText;


    // =========================================================
    // Runtime
    // =========================================================

    private ItemData item;


    private Action<ItemData>
        moveCallback;


    private Action<ItemData>
        sellCallback;


    // =========================================================
    // Setup
    // =========================================================

    public void Initialize(
        PendingRecoveryItem entry,
        Action<ItemData> onMove,
        Action<ItemData> onSell,
        bool allowSelling
    )
    {
        moveCallback =
            onMove;


        sellCallback =
            onSell;


        if (entry == null ||
            entry.Item == null ||
            entry.Quantity <= 0)
        {
            gameObject.SetActive(
                false
            );


            return;
        }


        item =
            entry.Item;


        if (iconImage != null)
        {
            iconImage.sprite =
                item.Icon;


            iconImage.enabled =
                item.Icon != null;
        }


        Color rarityColor =
            ItemRarityVisuals.GetColor(
                item.Rarity
            );


        if (nameText != null)
        {
            nameText.text =
                item.DisplayName;


            nameText.color =
                rarityColor;
        }


        if (metaText != null)
        {
            metaText.text =
                item.Rarity
                + "  //  "
                + item.Category;
        }


        if (quantityText != null)
        {
            quantityText.text =
                "x"
                + entry.Quantity;
        }


        if (valueText != null)
        {
            valueText.text =
                "VALUE  "
                + (
                    item.BaseValue
                    *
                    entry.Quantity
                );
        }


        if (moveButtonText != null)
        {
            moveButtonText.text =
                "MOVE TO STASH";
        }


        if (moveButton != null)
        {
            moveButton.onClick
                .RemoveAllListeners();


            moveButton.onClick
                .AddListener(
                    HandleMove
                );
        }


        if (sellButtonText != null)
        {
            sellButtonText.text =
                "SELL";
        }


        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(
                allowSelling
            );


            sellButton.onClick
                .RemoveAllListeners();


            sellButton.onClick
                .AddListener(
                    HandleSell
                );
        }
    }


    // =========================================================
    // Move
    // =========================================================

    private void HandleMove()
    {
        if (item == null)
        {
            return;
        }


        moveCallback?.Invoke(
            item
        );
    }


    // =========================================================
    // Sell
    // =========================================================

    private void HandleSell()
    {
        if (item == null)
        {
            return;
        }


        sellCallback?.Invoke(
            item
        );
    }
}