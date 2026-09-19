using UnityEngine;


[CreateAssetMenu(
    fileName = "ConsumableItemData",
    menuName = "AlienPurge/Inventory/Consumable Item Data"
)]
public class ConsumableItemData : ItemData
{
    // =========================================================
    // Healing
    // =========================================================

    [Header("Consumable Effect")]

    [Min(1)]
    [SerializeField]
    private int healAmount = 1;


    // =========================================================
    // Read Only Access
    // =========================================================

    public int HealAmount => healAmount;


    // =========================================================
    // Validation
    // =========================================================

    protected override void OnValidate()
    {
        base.OnValidate();

        healAmount =
            Mathf.Max(
                1,
                healAmount
            );
    }
}