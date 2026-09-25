using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(
    fileName = "ItemCatalog",
    menuName = "AlienPurge/Persistence/Item Catalog"
)]
public sealed class ItemCatalog : ScriptableObject
{
    // =========================================================
    // Resources
    // =========================================================

    public const string ResourcesPath =
        "Persistence/ItemCatalog";


    // =========================================================
    // Items
    // =========================================================

    [Header("Persistent Item Definitions")]

    [SerializeField]
    private List<ItemData> items =
        new List<ItemData>();


    // =========================================================
    // Runtime Lookup
    // =========================================================

    private readonly Dictionary<string, ItemData>
        lookup =
            new Dictionary<string, ItemData>(
                StringComparer.Ordinal
            );


    private bool isValid;


    // =========================================================
    // Read Only
    // =========================================================

    public IReadOnlyList<ItemData> Items =>
        items;


    public bool IsValid =>
        isValid;


    // =========================================================
    // Unity
    // =========================================================

    private void OnEnable()
    {
        RebuildLookup(
            false
        );
    }


    private void OnValidate()
    {
        RebuildLookup(
            true
        );
    }


    // =========================================================
    // Lookup
    // =========================================================

    public bool TryGetItem(
        string itemId,
        out ItemData item
    )
    {
        item = null;


        if (string.IsNullOrWhiteSpace(
                itemId
            ))
        {
            return false;
        }


        if (lookup.Count == 0)
        {
            RebuildLookup(
                false
            );
        }


        return lookup.TryGetValue(
            itemId,
            out item
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    public bool ValidateCatalog()
    {
        return RebuildLookup(
            true
        );
    }


    private bool RebuildLookup(
        bool logErrors
    )
    {
        lookup.Clear();


        bool valid =
            true;


        if (items == null)
        {
            items =
                new List<ItemData>();
        }


        for (int i = 0;
             i < items.Count;
             i++)
        {
            ItemData item =
                items[i];


            if (item == null)
            {
                valid =
                    false;


                if (logErrors)
                {
                    Debug.LogError(
                        "[ItemCatalog] "
                        + "Null ItemData entry at index "
                        + i
                        + ".",
                        this
                    );
                }


                continue;
            }


            string itemId =
                item.ItemId;


            // -------------------------------------------------
            // Missing ID
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                    itemId
                ))
            {
                valid =
                    false;


                if (logErrors)
                {
                    Debug.LogError(
                        "[ItemCatalog] "
                        + "Item has empty ItemId: "
                        + item.name,
                        item
                    );
                }


                continue;
            }


            // -------------------------------------------------
            // Leading / Trailing Whitespace
            // -------------------------------------------------

            if (!string.Equals(
                    itemId,
                    itemId.Trim(),
                    StringComparison.Ordinal
                ))
            {
                valid =
                    false;


                if (logErrors)
                {
                    Debug.LogError(
                        "[ItemCatalog] "
                        + "ItemId contains leading "
                        + "or trailing whitespace: \""
                        + itemId
                        + "\" on "
                        + item.name,
                        item
                    );
                }


                continue;
            }


            // -------------------------------------------------
            // Duplicate
            // -------------------------------------------------

            if (lookup.ContainsKey(
                    itemId
                ))
            {
                valid =
                    false;


                if (logErrors)
                {
                    ItemData existing =
                        lookup[itemId];


                    Debug.LogError(
                        "[ItemCatalog] "
                        + "Duplicate ItemId detected: \""
                        + itemId
                        + "\"\nExisting: "
                        + (
                            existing != null
                                ? existing.name
                                : "NULL"
                        )
                        + "\nDuplicate: "
                        + item.name,
                        item
                    );
                }


                continue;
            }


            lookup.Add(
                itemId,
                item
            );
        }


        isValid =
            valid;


        if (logErrors &&
            valid)
        {
            Debug.Log(
                "[ItemCatalog] Validation passed. "
                + lookup.Count
                + " persistent items registered.",
                this
            );
        }


        return valid;
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Validate Item Catalog"
    )]
    private void DebugValidateCatalog()
    {
        ValidateCatalog();
    }
}