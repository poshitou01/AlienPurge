using System;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class InventoryGridView :
    MonoBehaviour
{
    // =========================================================
    // Layers
    // =========================================================

    [Header("Layers")]

    [SerializeField]
    private RectTransform cellLayer;

    [SerializeField]
    private RectTransform itemLayer;


    // =========================================================
    // Prefabs
    // =========================================================

    [Header("Prefabs")]

    [SerializeField]
    private GameObject cellPrefab;

    [SerializeField]
    private InventoryItemView
        itemViewPrefab;


    // =========================================================
    // Layout
    // =========================================================

    [Header("Layout")]

    [Min(1f)]
    [SerializeField]
    private float cellSize = 64f;

    [Min(0f)]
    [SerializeField]
    private float spacing = 4f;


    // =========================================================
    // Runtime
    // =========================================================

    private InventoryGrid inventory;

    private Action<ItemStack>
        itemClickCallback;

    private bool initialized;


    // =========================================================
    // Read Only Access
    // =========================================================

    public RectTransform ItemLayer =>
        itemLayer;

    public float CellSize =>
        cellSize;

    public float Spacing =>
        spacing;

    public float CellStep =>
        cellSize + spacing;


    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(
        InventoryGrid targetInventory,
        Action<ItemStack> onItemClicked
    )
    {
        if (inventory != null)
        {
            inventory.Changed -=
                HandleInventoryChanged;
        }

        inventory =
            targetInventory;

        itemClickCallback =
            onItemClicked;

        if (inventory == null)
        {
            initialized = false;

            return;
        }

        inventory.Changed +=
            HandleInventoryChanged;

        ConfigureLayers();

        BuildCells();

        Refresh();

        initialized = true;
    }


    // =========================================================
    // Layer Setup
    // =========================================================

    private void ConfigureLayers()
    {
        float width =
            inventory.Width *
            cellSize
            +
            Mathf.Max(
                0,
                inventory.Width - 1
            )
            * spacing;

        float height =
            inventory.Height *
            cellSize
            +
            Mathf.Max(
                0,
                inventory.Height - 1
            )
            * spacing;


        Vector2 size =
            new Vector2(
                width,
                height
            );


        if (cellLayer != null)
        {
            cellLayer.sizeDelta =
                size;


            GridLayoutGroup layout =
                cellLayer.GetComponent<
                    GridLayoutGroup
                >();


            if (layout != null)
            {
                layout.cellSize =
                    new Vector2(
                        cellSize,
                        cellSize
                    );


                layout.spacing =
                    new Vector2(
                        spacing,
                        spacing
                    );


                layout.startCorner =
                    GridLayoutGroup
                        .Corner
                        .UpperLeft;


                layout.startAxis =
                    GridLayoutGroup
                        .Axis
                        .Horizontal;


                layout.constraint =
                    GridLayoutGroup
                        .Constraint
                        .FixedColumnCount;


                layout.constraintCount =
                    inventory.Width;


                layout.childAlignment =
                    TextAnchor.UpperLeft;
            }
        }


        if (itemLayer != null)
        {
            itemLayer.sizeDelta =
                size;
        }
    }


    // =========================================================
    // Cells
    // =========================================================

    private void BuildCells()
    {
        if (cellLayer == null ||
            cellPrefab == null)
        {
            return;
        }


        ClearChildren(
            cellLayer
        );


        int cellCount =
            inventory.Width *
            inventory.Height;


        for (int i = 0;
             i < cellCount;
             i++)
        {
            Instantiate(
                cellPrefab,
                cellLayer
            );
        }
    }


    // =========================================================
    // Refresh
    // =========================================================

    public void Refresh()
    {
        if (inventory == null ||
            itemLayer == null ||
            itemViewPrefab == null)
        {
            return;
        }


        ClearChildren(
            itemLayer
        );


        for (int i = 0;
             i < inventory.Items.Count;
             i++)
        {
            ItemStack stack =
                inventory.Items[i];


            if (stack == null ||
                stack.IsEmpty)
            {
                continue;
            }


            InventoryItemView view =
                Instantiate(
                    itemViewPrefab,
                    itemLayer
                );


            view.Initialize(
                stack,
                cellSize,
                spacing,
                itemClickCallback,
                this
            );
        }
    }


    // =========================================================
    // Drag Query
    // =========================================================

    public bool CanMoveStack(
        ItemStack stack,
        int targetX,
        int targetY
    )
    {
        if (!initialized ||
            inventory == null)
        {
            return false;
        }


        return
            inventory.CanMoveStack(
                stack,
                targetX,
                targetY
            );
    }


    // =========================================================
    // Drag Commit
    // =========================================================

    public bool TryMoveStack(
        ItemStack stack,
        int targetX,
        int targetY
    )
    {
        if (!initialized ||
            inventory == null)
        {
            return false;
        }


        return
            inventory.TryMoveStack(
                stack,
                targetX,
                targetY
            );
    }


    // =========================================================
    // Grid Coordinate
    // =========================================================

    public bool TryGetGridPosition(
        Vector2 anchoredPosition,
        out int gridX,
        out int gridY
    )
    {
        gridX =
            Mathf.RoundToInt(
                anchoredPosition.x /
                CellStep
            );


        gridY =
            Mathf.RoundToInt(
                -anchoredPosition.y /
                CellStep
            );


        if (inventory == null)
        {
            return false;
        }


        return
            gridX >= 0 &&
            gridY >= 0 &&
            gridX < inventory.Width &&
            gridY < inventory.Height;
    }


    // =========================================================
    // Events
    // =========================================================

    private void HandleInventoryChanged()
    {
        Refresh();
    }


    // =========================================================
    // Utility
    // =========================================================

    private void ClearChildren(
        RectTransform parent
    )
    {
        for (int i =
                 parent.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                parent
                    .GetChild(i)
                    .gameObject
            );
        }
    }


    // =========================================================
    // Cleanup
    // =========================================================

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -=
                HandleInventoryChanged;
        }
    }
}