
using System;
using UnityEngine;

// ============================================================
// Purchase Result
// ============================================================

public sealed class TraderPurchaseResult
{
    public bool Succeeded { get; private set; }
    public string FailureReason { get; private set; }

    public string ItemName { get; private set; }
    public int Quantity { get; private set; }
    public int TotalPrice { get; private set; }
    public int CreditsRemaining { get; private set; }

    private TraderPurchaseResult()
    {
    }

    public static TraderPurchaseResult Failed(string reason)
    {
        return new TraderPurchaseResult
        {
            Succeeded = false,
            FailureReason = reason ?? "Unknown failure."
        };
    }

    public static TraderPurchaseResult Successful(
        ItemData item,
        int quantity,
        int price,
        int creditsRemaining)
    {
        return new TraderPurchaseResult
        {
            Succeeded = true,
            FailureReason = string.Empty,
            ItemName = item.DisplayName,
            Quantity = quantity,
            TotalPrice = price,
            CreditsRemaining = creditsRemaining
        };
    }
}

// ============================================================
// Trader Purchase Service
// ============================================================

public sealed class TraderPurchaseService
{
    private bool purchaseInProgress;

    public bool TryPurchase(
        TraderShopOffer offer,
        PersistentProfile profile,
        SaveManager saveManager,
        out TraderPurchaseResult result)
    {
        result = TraderPurchaseResult.Failed(
            "Purchase was not completed.");

        // ----------------------------------------------------
        // 1. Guard against re-entry
        // ----------------------------------------------------

        if (purchaseInProgress)
        {
            result = TraderPurchaseResult.Failed(
                "Another purchase is already in progress.");
            return false;
        }

        // ----------------------------------------------------
        // 2. Validate persistent systems
        // ----------------------------------------------------

        if (profile == null ||
            !profile.IsInitialized ||
            profile.Stash == null ||
            profile.ItemCatalog == null ||
            saveManager == null ||
            !saveManager.IsInitialized)
        {
            result = TraderPurchaseResult.Failed(
                "Persistent systems are not ready.");
            return false;
        }

        // ----------------------------------------------------
        // 3. Validate offer
        // ----------------------------------------------------

        if (offer == null ||
            !offer.IsValid ||
            !offer.IsAvailable ||
            offer.Item == null)
        {
            result = TraderPurchaseResult.Failed(
                "The shop offer is invalid or unavailable.");
            return false;
        }

        ItemData item = offer.Item;
        int quantity = offer.QuantityPerPurchase;
        int price = offer.PurchasePrice;

        // Ensure the item is known to the SaveSystem.
        if (!profile.ItemCatalog.TryGetItem(
                item.ItemId,
                out ItemData catalogItem) ||
            catalogItem != item)
        {
            result = TraderPurchaseResult.Failed(
                "The item is not registered correctly " +
                "in ItemCatalog.");
            return false;
        }

        // ----------------------------------------------------
        // 4. Check Credits
        // ----------------------------------------------------

        if (!profile.CanAffordCredits(price))
        {
            result = TraderPurchaseResult.Failed(
                "Not enough Credits.");
            return false;
        }

        // ----------------------------------------------------
        // 5. Capture rollback snapshot
        // ----------------------------------------------------

        ItemCatalog catalog = profile.ItemCatalog;

        SaveGameData snapshot = profile.BuildSaveData(
            SaveManager.CurrentSaveVersion);

        if (snapshot == null ||
            snapshot.stashItems == null ||
            snapshot.stashItems.Count !=
                profile.Stash.Grid.Items.Count)
        {
            result = TraderPurchaseResult.Failed(
                "Could not create a complete Stash snapshot.");
            return false;
        }

        // ----------------------------------------------------
        // 6. Simulate the purchase on a temporary Stash
        // ----------------------------------------------------
        // This does NOT modify the real player inventory.

        PersistentStash previewStash = new PersistentStash();

        previewStash.LoadFromSave(
            snapshot.stashItems,
            catalog,
            out int loaded,
            out int skipped);

        if (skipped != 0 ||
            loaded != snapshot.stashItems.Count)
        {
            result = TraderPurchaseResult.Failed(
                "Could not reconstruct Stash for capacity check.");
            return false;
        }

        bool previewSuccess = previewStash.TryStore(
            item,
            quantity,
            out int previewStored,
            out int previewRemaining);

        if (!previewSuccess ||
            previewStored != quantity ||
            previewRemaining != 0)
        {
            result = TraderPurchaseResult.Failed(
                "Not enough Stash space.");
            return false;
        }

        // ----------------------------------------------------
        // 7. Apply the real transaction
        // ----------------------------------------------------

        purchaseInProgress = true;
        bool committed = false;

        try
        {
            // Add the COMPLETE quantity to real Stash.
            bool storeSuccess = profile.Stash.TryStore(
                item,
                quantity,
                out int stored,
                out int remaining);

            if (!storeSuccess ||
                stored != quantity ||
                remaining != 0)
            {
                result = TraderPurchaseResult.Failed(
                    "Stash changed during purchase.");
                return false;
            }

            // Spend Credits only after storage succeeded.
            if (!profile.TrySpendCredits(price))
            {
                result = TraderPurchaseResult.Failed(
                    "Credit deduction failed.");
                return false;
            }

            // Persist both the item and Credit changes.
            if (!saveManager.Save())
            {
                result = TraderPurchaseResult.Failed(
                    "Saving purchase failed.");
                return false;
            }

            // Save succeeded: purchase is committed.
            committed = true;

            result = TraderPurchaseResult.Successful(
                item,
                quantity,
                price,
                profile.Credits);

            Debug.Log(
                "[Trader Purchase] SUCCESS" +
                "\nItem: " + item.DisplayName +
                "\nQuantity: " + quantity +
                "\nPrice: " + price +
                "\nCredits Remaining: " + profile.Credits);

            return true;
        }
        catch (Exception exception)
        {
            result = TraderPurchaseResult.Failed(
                "Unexpected purchase error: " +
                exception.Message);

            Debug.LogError(
                "[Trader Purchase] Exception:\n" +
                exception);

            return false;
        }
        finally
        {
            // ------------------------------------------------
            // 8. Rollback any uncommitted transaction
            // ----------------------------------------------------

            if (!committed)
            {
                try
                {
                    profile.LoadFromSaveData(
                        snapshot,
                        catalog);

                    Debug.LogWarning(
                        "[Trader Purchase] Runtime rolled back.");
                }
                catch (Exception rollbackException)
                {
                    Debug.LogError(
                        "[Trader Purchase] ROLLBACK FAILED:\n" +
                        rollbackException);
                }
            }

            purchaseInProgress = false;
        }
    }
}
