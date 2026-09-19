using UnityEngine;


[CreateAssetMenu(
    fileName = "ItemData",
    menuName = "AlienPurge/Inventory/Item Data"
)]
public class ItemData : ScriptableObject
{
    // =========================================================
    // Identity
    // =========================================================

    [Header("Identity")]

    [SerializeField]
    private string itemId = "item_id";

    [SerializeField]
    private string displayName = "New Item";

    [TextArea(2, 5)]
    [SerializeField]
    private string description;


    // =========================================================
    // Classification
    // =========================================================

    [Header("Classification")]

    [SerializeField]
    private ItemCategory category = ItemCategory.Material;

    [SerializeField]
    private ItemRarity rarity = ItemRarity.Common;


    // =========================================================
    // Visual
    // =========================================================

    [Header("Visual")]

    [SerializeField]
    private Sprite icon;

    [SerializeField]
    private Sprite worldSprite;


    // =========================================================
    // Grid Settings
    // =========================================================

    [Header("Grid Settings")]

    [Min(1)]
    [SerializeField]
    private int gridWidth = 1;

    [Min(1)]
    [SerializeField]
    private int gridHeight = 1;


    // =========================================================
    // Stack Settings
    // =========================================================

    [Header("Stack Settings")]

    [Min(1)]
    [SerializeField]
    private int maxStackSize = 1;


    // =========================================================
    // Economy
    // =========================================================

    [Header("Economy")]

    [Min(0)]
    [SerializeField]
    private int baseValue = 0;


    // =========================================================
    // Read Only Access
    // =========================================================

    public string ItemId => itemId;

    public string DisplayName => displayName;

    public string Description => description;

    public ItemCategory Category => category;

    public ItemRarity Rarity => rarity;

    public Sprite Icon => icon;

    public Sprite WorldSprite => worldSprite;

    public int GridWidth => gridWidth;

    public int GridHeight => gridHeight;

    public int GridArea =>
        gridWidth * gridHeight;

    public int MaxStackSize => maxStackSize;

    public int BaseValue => baseValue;

    public bool IsStackable =>
        maxStackSize > 1;


    // =========================================================
    // Validation
    // =========================================================

    protected virtual void OnValidate()
    {
        gridWidth =
            Mathf.Max(
                1,
                gridWidth
            );

        gridHeight =
            Mathf.Max(
                1,
                gridHeight
            );

        maxStackSize =
            Mathf.Max(
                1,
                maxStackSize
            );

        baseValue =
            Mathf.Max(
                0,
                baseValue
            );
    }
}