using System;


[Serializable]
public class LootContainerSearchEntry
{
    private ItemStack stack;

    private LootSearchState state =
        LootSearchState.Unknown;


    public ItemStack Stack =>
        stack;

    public LootSearchState State =>
        state;

    public bool IsTaken =>
        state == LootSearchState.Taken;


    public LootContainerSearchEntry(
        ItemStack stack
    )
    {
        this.stack = stack;

        state =
            LootSearchState.Unknown;
    }


    public void BeginSearching()
    {
        if (state !=
            LootSearchState.Unknown)
        {
            return;
        }

        state =
            LootSearchState.Searching;
    }


    public void Identify()
    {
        if (state ==
            LootSearchState.Taken)
        {
            return;
        }

        state =
            LootSearchState.Identified;
    }


    public void CancelSearching()
    {
        if (state !=
            LootSearchState.Searching)
        {
            return;
        }

        state =
            LootSearchState.Unknown;
    }


    public void MarkTaken()
    {
        state =
            LootSearchState.Taken;
    }
}