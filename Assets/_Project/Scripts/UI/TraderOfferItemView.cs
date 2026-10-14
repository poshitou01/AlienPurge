
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TraderOfferItemView : MonoBehaviour
{
    // =========================================================
    // Item UI
    // =========================================================

    [Header("Item")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text metaText;
    [SerializeField] private TMP_Text quantityText;

    // =========================================================
    // Purchase UI
    // =========================================================

    [Header("Purchase")]
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    // =========================================================
    // Selection
    // =========================================================

    [Header("Selection")]
    [SerializeField] private Button selectButton;

    [SerializeField]
    private GameObject selectedMarker;

    // =========================================================
    // Runtime
    // =========================================================

    private TraderShopOffer currentOffer;

    private Action<TraderShopOffer> selectCallback;
    private Action<TraderShopOffer> buyCallback;

    public TraderShopOffer CurrentOffer => currentOffer;

    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(
        TraderShopOffer offer,
        Action<TraderShopOffer> onSelected,
        Action<TraderShopOffer> onBuy)
    {
        currentOffer = offer;
        selectCallback = onSelected;
        buyCallback = onBuy;


        SetSelected(false);
        if (offer == null ||
            !offer.IsValid ||
            !offer.IsAvailable)
        {
            gameObject.SetActive(false);
            return;
        }

        ItemData item = offer.Item;

        // Icon
        if (itemIcon != null)
        {
            itemIcon.sprite = item.Icon;
            itemIcon.enabled = item.Icon != null;
            itemIcon.preserveAspect = true;
        }

        // Name
        if (nameText != null)
        {
            nameText.text = item.DisplayName;

            nameText.color =
                ItemRarityVisuals.GetColor(item.Rarity);
        }

        // Rarity and category
        if (metaText != null)
        {
            metaText.text =
                item.Rarity + " // " + item.Category;
        }

        // Quantity
        if (quantityText != null)
        {
            quantityText.text =
                "QUANTITY x" + offer.QuantityPerPurchase;
        }

        // Price
        if (priceText != null)
        {
            priceText.text =
                offer.PurchasePrice.ToString("N0") + " CR";
        }

        // Select item
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelect);
            selectButton.onClick.AddListener(HandleSelect);

            selectButton.interactable = true;
        }

        // Buy item
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(HandleBuy);
            buyButton.onClick.AddListener(HandleBuy);

            buyButton.interactable = true;
        }

        if (buyButtonText != null)
        {
            buyButtonText.text = "BUY";
        }
    }

    // =========================================================
    // Selected Visual
    // =========================================================

    public void SetSelected(bool isSelected)
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetActive(isSelected);
        }
    }

    // =========================================================
    // Selection
    // =========================================================

    private void HandleSelect()
    {
        if (currentOffer == null ||
            !currentOffer.IsValid ||
            !currentOffer.IsAvailable)
        {
            return;
        }

        selectCallback?.Invoke(currentOffer);
    }

    // =========================================================
    // Purchase
    // =========================================================

    private void HandleBuy()
    {
        if (currentOffer == null ||
            !currentOffer.IsValid ||
            !currentOffer.IsAvailable)
        {
            return;
        }

        buyCallback?.Invoke(currentOffer);
    }

    // =========================================================
    // Cleanup
    // =========================================================

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelect);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(HandleBuy);
        }
    }
}
