
using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// One product offered by the Trader.
// ============================================================

[Serializable]
public sealed class TraderShopOffer
{
    [Header("Item")]
    [SerializeField]
    private ItemData item;

    [Header("Purchase Settings")]
    [Min(1)]
    [SerializeField]
    private int purchasePrice = 100;

    [Min(1)]
    [SerializeField]
    private int quantityPerPurchase = 1;

    [Header("Availability")]
    [SerializeField]
    private bool isAvailable = true;

    // Read-only access

    public ItemData Item => item;

    public int PurchasePrice => purchasePrice;

    public int QuantityPerPurchase =>
        quantityPerPurchase;

    public bool IsAvailable => isAvailable;

    public bool IsValid =>
        item != null &&
        purchasePrice > 0 &&
        quantityPerPurchase > 0;

    // Inspector validation

    public void ValidateValues()
    {
        purchasePrice =
            Mathf.Max(1, purchasePrice);

        quantityPerPurchase =
            Mathf.Max(1, quantityPerPurchase);
    }
}

// ============================================================
// Trader product catalog.
// ============================================================

[CreateAssetMenu(
    fileName = "TraderShopCatalog",
    menuName = "AlienPurge/Trader/Shop Catalog"
)]
public sealed class TraderShopCatalog : ScriptableObject
{
    [Header("Products")]
    [SerializeField]
    private List<TraderShopOffer> offers =
        new List<TraderShopOffer>();

    public IReadOnlyList<TraderShopOffer> Offers =>
        offers;

    private void OnValidate()
    {
        if (offers == null)
        {
            offers = new List<TraderShopOffer>();
            return;
        }

        foreach (TraderShopOffer offer in offers)
        {
            if (offer == null)
            {
                continue;
            }

            offer.ValidateValues();
        }
    }
}
