using UnityEngine;


public static class ItemRarityVisuals
{
    public static Color GetColor(
        ItemRarity rarity
    )
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return new Color(
                    0.35f,
                    0.85f,
                    0.45f,
                    1f
                );

            case ItemRarity.Rare:
                return new Color(
                    0.30f,
                    0.55f,
                    1f,
                    1f
                );

            case ItemRarity.Epic:
                return new Color(
                    0.72f,
                    0.35f,
                    1f,
                    1f
                );

            case ItemRarity.Legendary:
                return new Color(
                    1f,
                    0.65f,
                    0.15f,
                    1f
                );

            default:
                return new Color(
                    0.78f,
                    0.78f,
                    0.78f,
                    1f
                );
        }
    }
}