using System.Collections.Generic;
using UnityEngine;

public partial class LootTableData
{
    [SerializeField] private List<LootTableEntry> guaranteedEntries = new List<LootTableEntry>();
    public IReadOnlyList<LootTableEntry> GuaranteedEntries => guaranteedEntries;
}
public partial class LootContainer
{
    public bool ConfigureRegionLoot(LootTableData table)
    {
        if (lootGenerated || table == null) return false;
        lootTable = table; return true;
    }
    void AddGuaranteedRegionLoot(List<GeneratedLootRecord> generated, Dictionary<ItemData, GeneratedLootRecord> lookup)
    {
        if (lootTable.GuaranteedEntries == null) return;
        foreach (var entry in lootTable.GuaranteedEntries)
        {
            if (entry == null || entry.Item == null || lookup.ContainsKey(entry.Item)) continue;
            var record = new GeneratedLootRecord(entry.Item, Mathf.Min(entry.Item.MaxStackSize, entry.RollQuantity()), -1);
            generated.Add(record); lookup.Add(entry.Item, record);
        }
    }
}
public partial class ExtractionController
{
    public bool UnlockRegionHighRiskCache(LootContainer cache)
    {
        if (!regionDriven || hasHighRiskCacheSpawned || cache == null) return false;
        currentHighRiskCacheObject = cache.gameObject;
        currentHighRiskCachePosition = cache.transform.position;
        currentHighRiskCacheSpawnPointName = "Region objective reward";
        hasHighRiskCacheSpawned = true;
        cache.gameObject.SetActive(true);
        return true;
    }
}
