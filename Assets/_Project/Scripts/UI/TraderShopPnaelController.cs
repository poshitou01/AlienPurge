
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class TraderShopPanelController : MonoBehaviour
{
    // =========================================================
    // Panel
    // =========================================================

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Credits")]
    [SerializeField] private TMP_Text creditsText;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    // =========================================================
    // Shop Catalog
    // =========================================================

    [Header("Shop Catalog")]
    [SerializeField] private TraderShopCatalog shopCatalog;

    // =========================================================
    // Offer List
    // =========================================================

    [Header("Offer List")]
    [SerializeField] private RectTransform offersContentRoot;
    [SerializeField] private TraderOfferItemView offerViewPrefab;

    // =========================================================
    // Selected Item Details
    // =========================================================

    [Header("Selected Item Details")]
    [SerializeField] private Image detailItemIcon;

    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailMetaText;
    [SerializeField] private TMP_Text detailSizeText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailQuantityText;
    [SerializeField] private TMP_Text detailPriceText;

    [SerializeField] private Button detailsBuyButton;

    // =========================================================
    // Purchase Feedback
    // =========================================================

    [Header("Purchase Feedback")]
    [SerializeField] private TMP_Text purchaseFeedbackText;

    // =========================================================
    // Runtime
    // =========================================================

    private TraderShopOffer selectedOffer;
    private readonly List<TraderOfferItemView> offerViews =
    new List<TraderOfferItemView>();
    private TraderPurchaseService purchaseService;

    // Prevent duplicate requests in the same frame.
    private int lastPurchaseFrame = -1;

    private static readonly Color successColor =
        new Color(0.75f, 0.88f, 0.68f, 1f);

    private static readonly Color failureColor =
        new Color(0.93f, 0.55f, 0.47f, 1f);

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        purchaseService = new TraderPurchaseService();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }

        ClearSelection();
    }

    // =========================================================
    // Open Shop
    // =========================================================

    public void OpenPanel()
    {
        if (panelRoot == null)
        {
            Debug.LogError(
                "[Trader Shop] Panel Root is missing.",
                this);
            return;
        }

        PersistentProfile profile =
            PersistentProfile.Instance;

        if (profile == null || !profile.IsInitialized)
        {
            Debug.LogWarning(
                "[Trader Shop] Profile is not ready.",
                this);
            return;
        }

        ClearSelection();
        RefreshCredits();
        RefreshOffers();

        panelRoot.SetActive(true);

        Debug.Log(
            "[Trader Shop] Opened. Credits: " +
            profile.Credits,
            this);
    }

    // =========================================================
    // Credits
    // =========================================================

    public void RefreshCredits()
    {
        if (creditsText == null)
        {
            return;
        }

        PersistentProfile profile =
            PersistentProfile.Instance;

        if (profile == null || !profile.IsInitialized)
        {
            creditsText.text = "CREDITS --";
            return;
        }

        creditsText.text =
            "CREDITS " + profile.Credits.ToString("N0");
    }

    // =========================================================
    // Generate Offers
    // =========================================================

    public void RefreshOffers()
    {
        if (shopCatalog == null ||
            offersContentRoot == null ||
            offerViewPrefab == null)
        {
            Debug.LogError(
                "[Trader Shop] Missing catalog or list references.",
                this);
            return;
        }

        ClearSelection();
        ClearOfferViews();

        int createdCount = 0;

        foreach (TraderShopOffer offer in shopCatalog.Offers)
        {
            if (offer == null ||
                !offer.IsValid ||
                !offer.IsAvailable)
            {
                continue;
            }

            TraderOfferItemView view =
                Instantiate(
                    offerViewPrefab,
                    offersContentRoot,
                    false);

            // Selection and purchase use separate callbacks.
            view.Initialize(
                offer,
                SelectOffer,
                TryBuyOffer);
            offerViews.Add(view);
            createdCount++;
        }

        Debug.Log(
            "[Trader Shop] Catalog loaded: " +
            shopCatalog.name +
            " / Available products: " +
            createdCount,
            this);
    }

    // =========================================================
    // Select Offer
    // =========================================================

    private void SelectOffer(TraderShopOffer offer)
    {
        if (offer == null ||
            !offer.IsValid ||
            !offer.IsAvailable)
        {
            return;
        }

        selectedOffer = offer;
        for (int i = 0; i < offerViews.Count; i++)
        {
            TraderOfferItemView view = offerViews[i];

            if (view == null)
            {
                continue;
            }

            bool isSelected =
                view.CurrentOffer == offer;

            view.SetSelected(isSelected);
        }
        ItemData item = offer.Item;

        if (detailItemIcon != null)
        {
            detailItemIcon.sprite = item.Icon;
            detailItemIcon.enabled = item.Icon != null;
            detailItemIcon.preserveAspect = true;
        }

        if (detailNameText != null)
        {
            detailNameText.text = item.DisplayName;

            detailNameText.color =
                ItemRarityVisuals.GetColor(item.Rarity);
        }

        if (detailMetaText != null)
        {
            detailMetaText.text =
                item.Rarity + " // " + item.Category;
        }

        if (detailSizeText != null)
        {
            detailSizeText.text =
                "SIZE " +
                item.GridWidth + "x" + item.GridHeight;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = item.Description;
        }

        if (detailQuantityText != null)
        {
            detailQuantityText.text =
                "PURCHASE QUANTITY x" +
                offer.QuantityPerPurchase;
        }

        if (detailPriceText != null)
        {
            detailPriceText.text =
                "TOTAL PRICE " +
                offer.PurchasePrice.ToString("N0") +
                " CR";
        }

        if (detailsBuyButton != null)
        {
            detailsBuyButton.interactable = true;
        }

        ShowPurchaseFeedback(
            string.Empty,
            Color.white);
    }

    // =========================================================
    // Purchase From Either BUY Button
    // =========================================================

    private void TryBuyOffer(TraderShopOffer offer)
    {
        if (offer == null ||
            !offer.IsValid ||
            !offer.IsAvailable)
        {
            ShowPurchaseFeedback(
                "ITEM UNAVAILABLE",
                failureColor);
            return;
        }

        if (panelRoot == null ||
            !panelRoot.activeInHierarchy)
        {
            return;
        }

        // Ignore duplicate UI calls within one frame.
        if (lastPurchaseFrame == Time.frameCount)
        {
            return;
        }

        lastPurchaseFrame = Time.frameCount;

        // Select the item whose BUY button was clicked.
        SelectOffer(offer);

        if (purchaseService == null)
        {
            purchaseService = new TraderPurchaseService();
        }

        PersistentProfile profile =
            PersistentProfile.Instance;

        SaveManager saveManager =
            SaveManager.Instance;

        bool success =
            purchaseService.TryPurchase(
                offer,
                profile,
                saveManager,
                out TraderPurchaseResult result);

        if (success && result != null &&
            result.Succeeded)
        {
            // Refresh the shared persistent currency display.
            RefreshCredits();

            ShowPurchaseFeedback(
                "PURCHASED: " +
                result.ItemName +
                " x" + result.Quantity +
                "  (-" + result.TotalPrice + " CR)",
                successColor);

            Debug.Log(
                "[Trader Shop] Purchase completed: " +
                result.ItemName,
                this);
        }
        else
        {
            string reason =
                result != null
                    ? result.FailureReason
                    : "Unknown purchase failure.";

            ShowPurchaseFeedback(
                "PURCHASE FAILED: " + reason,
                failureColor);

            Debug.LogWarning(
                "[Trader Shop] Purchase failed: " + reason,
                this);
        }
    }

    // =========================================================
    // Purchase Selected Item From Detail Panel
    // =========================================================

    private void BuySelectedOffer()
    {
        if (selectedOffer == null)
        {
            return;
        }

        TryBuyOffer(selectedOffer);
    }

    // =========================================================
    // Purchase Feedback
    // =========================================================

    private void ShowPurchaseFeedback(
        string message,
        Color color)
    {
        if (purchaseFeedbackText == null)
        {
            return;
        }

        purchaseFeedbackText.text = message;
        purchaseFeedbackText.color = color;
    }

    // =========================================================
    // Clear Selection
    // =========================================================

    private void ClearSelection()
    {
        selectedOffer = null;
        for (int i = 0; i < offerViews.Count; i++)
        {
            if (offerViews[i] != null)
            {
                offerViews[i].SetSelected(false);
            }
        }
        if (detailItemIcon != null)
        {
            detailItemIcon.sprite = null;
            detailItemIcon.enabled = false;
        }

        if (detailNameText != null)
        {
            detailNameText.text = "SELECT AN ITEM";
            detailNameText.color = Color.white;
        }

        if (detailMetaText != null)
        {
            detailMetaText.text = string.Empty;
        }

        if (detailSizeText != null)
        {
            detailSizeText.text = string.Empty;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text =
                "Select an item to inspect its details.";
        }

        if (detailQuantityText != null)
        {
            detailQuantityText.text =
                "PURCHASE QUANTITY --";
        }

        if (detailPriceText != null)
        {
            detailPriceText.text = "TOTAL PRICE --";
        }

        if (detailsBuyButton != null)
        {
            detailsBuyButton.interactable = false;
        }

        ShowPurchaseFeedback(string.Empty, Color.white);
    }

    // =========================================================
    // Clear Generated Views
    // =========================================================

    private void ClearOfferViews()
    {
        offerViews.Clear();
        if (offersContentRoot == null)
        {
            return;
        }

        for (int i = offersContentRoot.childCount - 1;
             i >= 0;
             i--)
        {
            GameObject child =
                offersContentRoot.GetChild(i).gameObject;

            child.SetActive(false);
            Destroy(child);
        }
    }

    // =========================================================
    // Close
    // =========================================================

    public void ClosePanel()
    {
        ClearSelection();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    // =========================================================
    // Cleanup
    // =========================================================

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
        }

        if (detailsBuyButton != null)
        {
            detailsBuyButton.onClick.RemoveListener(
                BuySelectedOffer);
        }
    }

    // =========================================================
    // Register Detail Purchase Button
    // =========================================================

    private void Start()
    {
        if (detailsBuyButton != null)
        {
            detailsBuyButton.onClick.RemoveListener(
                BuySelectedOffer);

            detailsBuyButton.onClick.AddListener(
                BuySelectedOffer);
        }
    }
}
