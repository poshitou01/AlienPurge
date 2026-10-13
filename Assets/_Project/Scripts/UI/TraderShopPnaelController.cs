
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TraderShopPanelController : MonoBehaviour
{
    // =========================================================
    // UI References
    // =========================================================

    [Header("Shop Catalog")]
    [SerializeField]
    private TraderShopCatalog shopCatalog;
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Credits")]
    [SerializeField] private TMP_Text creditsText;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
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
                this
            );
            return;
        }

        PersistentProfile profile =
            PersistentProfile.Instance;

        if (profile == null ||
            !profile.IsInitialized)
        {
            Debug.LogWarning(
                "[Trader Shop] Persistent Profile is not ready.",
                this
            );
            return;
        }

        RefreshCredits();

        if (shopCatalog == null)
        {
            Debug.LogWarning(
                "[Trader Shop] Shop Catalog is not assigned.",
                this
            );
        }
        else
        {
            int availableCount = 0;

            foreach (TraderShopOffer offer in shopCatalog.Offers)
            {
                if (offer != null &&
                    offer.IsValid &&
                    offer.IsAvailable)
                {
                    availableCount++;
                }
            }

            Debug.Log(
                "[Trader Shop] Catalog loaded: " +
                shopCatalog.name +
                " / Available products: " +
                availableCount,
                this
            );
        }
        panelRoot.SetActive(true);

        Debug.Log(
            "[Trader Shop] Shop opened. Credits: " +
            profile.Credits,
            this
        );
    }

    // =========================================================
    // Credits Display
    // =========================================================

    public void RefreshCredits()
    {
        if (creditsText == null)
        {
            return;
        }

        PersistentProfile profile =
            PersistentProfile.Instance;

        if (profile == null ||
            !profile.IsInitialized)
        {
            creditsText.text = "CREDITS  --";
            return;
        }

        creditsText.text =
            "CREDITS  " + profile.Credits;
    }

    // =========================================================
    // Close Shop
    // =========================================================

    public void ClosePanel()
    {
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
    }
}
