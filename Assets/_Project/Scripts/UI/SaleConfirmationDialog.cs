
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SaleConfirmationDialog : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject overlayRoot;

    [Header("Item Preview")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text totalPriceText;
    [SerializeField] private TMP_Text warningText;

    [Header("Buttons")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmSellButton;

    private Action pendingConfirmAction;

    public bool IsOpen =>
        overlayRoot != null && overlayRoot.activeSelf;

    // Only Rare and above require confirmation.
    public static bool RequiresConfirmation(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        return item.Rarity == ItemRarity.Rare
            || item.Rarity == ItemRarity.Epic
            || item.Rarity == ItemRarity.Legendary;
    }

    private void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Cancel);
        }

        if (confirmSellButton != null)
        {
            confirmSellButton.onClick.AddListener(Confirm);
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    public bool Show(
        ItemData item,
        int quantity,
        Action onConfirmed)
    {
        if (item == null ||
            quantity <= 0 ||
            onConfirmed == null)
        {
            Debug.LogError(
                "[Sale Confirmation] Invalid sale request.",
                this
            );

            return false;
        }

        if (overlayRoot == null ||
            cancelButton == null ||
            confirmSellButton == null)
        {
            Debug.LogError(
                "[Sale Confirmation] Missing UI references.",
                this
            );

            return false;
        }

        if (IsOpen)
        {
            Debug.LogWarning(
                "[Sale Confirmation] A dialog is already open.",
                this
            );

            return false;
        }

        // Only display the value here.
        // The actual sale is handled by TraderService.
        long totalPrice =
            (long)item.BaseValue * quantity;

        if (itemIcon != null)
        {
            itemIcon.sprite = item.Icon;
            itemIcon.enabled = item.Icon != null;
            itemIcon.preserveAspect = true;
        }

        if (itemNameText != null)
        {
            itemNameText.text = item.DisplayName;
            itemNameText.color =
                ItemRarityVisuals.GetColor(item.Rarity);
        }

        if (quantityText != null)
        {
            quantityText.text = "QUANTITY x" + quantity;
        }

        if (totalPriceText != null)
        {
            totalPriceText.text =
                "TOTAL +" + totalPrice + " CREDITS";
        }

        if (warningText != null)
        {
            warningText.text =
                "The selected stack will be sold.";
        }

        pendingConfirmAction = onConfirmed;
        overlayRoot.SetActive(true);

        return true;
    }

    public void Cancel()
    {
        pendingConfirmAction = null;

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void Confirm()
    {
        if (!IsOpen || pendingConfirmAction == null)
        {
            return;
        }

        // Clear the request first so it cannot
        // accidentally execute more than once.
        Action action = pendingConfirmAction;
        pendingConfirmAction = null;

        overlayRoot.SetActive(false);

        action.Invoke();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Cancel);
        }

        if (confirmSellButton != null)
        {
            confirmSellButton.onClick.RemoveListener(Confirm);
        }
    }
}
