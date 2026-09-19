using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class LootTableEntry
{
    // =========================================================
    // Item
    // =========================================================

    [SerializeField]
    private ItemData item;


    // =========================================================
    // Weight
    // =========================================================

    [Min(0f)]
    [SerializeField]
    private float weight = 1f;


    // =========================================================
    // Quantity
    // =========================================================

    [Min(1)]
    [SerializeField]
    private int minQuantity = 1;

    [Min(1)]
    [SerializeField]
    private int maxQuantity = 1;


    // =========================================================
    // Read Only
    // =========================================================

    public ItemData Item =>
        item;


    public float Weight =>
        Mathf.Max(
            0f,
            weight
        );


    public int MinQuantity =>
        Mathf.Max(
            1,
            minQuantity
        );


    public int MaxQuantity =>
        Mathf.Max(
            MinQuantity,
            maxQuantity
        );


    // =========================================================
    // Quantity Roll
    // =========================================================

    public int RollQuantity()
    {
        return
            UnityEngine.Random.Range(
                MinQuantity,
                MaxQuantity + 1
            );
    }
}


[CreateAssetMenu(
    fileName = "LootTable_",
    menuName = "AlienPurge/Loot/Loot Table"
)]
public class LootTableData :
    ScriptableObject
{
    // =========================================================
    // Roll Count
    // =========================================================

    [Header("Roll Count")]

    [Min(1)]
    [SerializeField]
    private int minRolls = 2;

    [Min(1)]
    [SerializeField]
    private int maxRolls = 4;


    // =========================================================
    // Entries
    // =========================================================

    [Header("Loot Entries")]

    [SerializeField]
    private List<LootTableEntry>
        entries =
            new List<LootTableEntry>();


    // =========================================================
    // Read Only
    // =========================================================

    public int MinRolls =>
        Mathf.Max(
            1,
            minRolls
        );


    public int MaxRolls =>
        Mathf.Max(
            MinRolls,
            maxRolls
        );


    public IReadOnlyList<
        LootTableEntry
    > Entries =>
        entries;


    // =========================================================
    // Roll Count
    // =========================================================

    public int GetRandomRollCount()
    {
        return
            UnityEngine.Random.Range(
                MinRolls,
                MaxRolls + 1
            );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        minRolls =
            Mathf.Max(
                1,
                minRolls
            );

        maxRolls =
            Mathf.Max(
                minRolls,
                maxRolls
            );
    }
}