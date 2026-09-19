using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class LootSearchPanelController :
    MonoBehaviour
{
    // =========================================================
    // Singleton
    // =========================================================

    public static LootSearchPanelController
        Instance
    {
        get;
        private set;
    }


    public static bool IsOpen =>
        Instance != null &&
        Instance.currentContainer != null;


    // =========================================================
    // Root
    // =========================================================

    [Header("Root")]

    [SerializeField]
    private GameObject searchPanelRoot;


    // =========================================================
    // Text
    // =========================================================

    [Header("Text")]

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text hintText;


    // =========================================================
    // Opening Progress
    // =========================================================

    [Header("Opening Progress")]

    [SerializeField]
    private GameObject progressRoot;

    [SerializeField]
    private Image progressFill;


    // =========================================================
    // Container Grid
    // =========================================================

    [Header("Container Grid")]

    [SerializeField]
    private RectTransform cellLayer;

    [SerializeField]
    private RectTransform itemLayer;

    [SerializeField]
    private GameObject cellPrefab;

    [SerializeField]
    private LootSearchItemView
        itemViewPrefab;


    [Min(1f)]
    [SerializeField]
    private float cellSize = 64f;

    [Min(0f)]
    [SerializeField]
    private float spacing = 4f;


    // =========================================================
    // Runtime
    // =========================================================

    private LootContainer currentContainer;


    private readonly List<
        LootSearchItemView
    > itemViews =
        new List<
            LootSearchItemView
        >();


    private float temporaryMessageTimer;

    private string temporaryMessage;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }


        Instance = this;


        if (searchPanelRoot != null)
        {
            searchPanelRoot.SetActive(
                false
            );
        }


        if (progressFill != null)
        {
            progressFill.type =
                Image.Type.Filled;

            progressFill.fillMethod =
                Image.FillMethod.Horizontal;

            progressFill.fillOrigin = 0;

            progressFill.fillAmount = 0f;
        }
    }


    private void Update()
    {
        if (currentContainer == null)
        {
            return;
        }


        // =====================================================
        // External State Protection
        // =====================================================
        //
        // Search 本身不暂停世界，
        // 但以下状态优先级都高于 Search：
        //
        // Backpack
        // Pause
        // Upgrade
        // Module Selection
        // GameOver
        // Victory
        //
        // 一旦它们出现，Search 必须立即安全退出。
        // =====================================================

        if (ShouldCloseForExternalState())
        {
            CloseActiveSearch();
            return;
        }


        RefreshOpeningProgress();

        RefreshItemSearchProgress();


        if (temporaryMessageTimer > 0f)
        {
            temporaryMessageTimer -=
                Time.unscaledDeltaTime;


            if (temporaryMessageTimer <= 0f)
            {
                temporaryMessage =
                    string.Empty;
            }
        }


        RefreshStatusText();
    }


    // =========================================================
    // External State
    // =========================================================

    private bool ShouldCloseForExternalState()
    {
        if (GameManager.Instance != null &&
            !GameManager.Instance.IsPlaying)
        {
            return true;
        }


        if (PauseMenuController.IsPaused)
        {
            return true;
        }


        if (BackpackPanelController.IsOpen)
        {
            return true;
        }


        if (UpgradeManager.IsChoosingUpgrade)
        {
            return true;
        }


        if (WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            return true;
        }


        return false;
    }


    // =========================================================
    // Open
    // =========================================================

    public void OpenContainer(
        LootContainer container
    )
    {
        if (container == null)
        {
            return;
        }


        if (currentContainer != null &&
            currentContainer != container)
        {
            Unsubscribe(
                currentContainer
            );
        }


        currentContainer =
            container;


        Subscribe(
            currentContainer
        );


        if (searchPanelRoot != null)
        {
            searchPanelRoot.SetActive(
                true
            );
        }


        BuildGridCells();

        RefreshItems();

        RefreshTitle();

        RefreshStatusText();

        RefreshOpeningProgress();

        RefreshItemSearchProgress();


        if (hintText != null)
        {
            hintText.text =
                "[E] CLOSE  //  "
                + "CLICK IDENTIFIED ITEM TO TAKE";
        }
    }


    // =========================================================
    // External Close Request
    // =========================================================

    /// <summary>
    /// 由 Backpack / Pause / Esc 等其他系统调用。
    ///
    /// 这里不能只隐藏 SearchPanel，
    /// 必须让 LootContainer 一起结束当前搜索，
    /// 否则隐藏 UI 后容器仍会在后台继续搜索。
    /// </summary>
    public void CloseActiveSearch()
    {
        if (currentContainer == null)
        {
            if (searchPanelRoot != null)
            {
                searchPanelRoot.SetActive(
                    false
                );
            }

            return;
        }


        LootContainer container =
            currentContainer;


        container
            .CancelSearchFromExternalState();


        // 正常情况下上面会经过：
        //
        // LootContainer
        // → CancelAndCloseSearch()
        // → CloseContainer(this)
        //
        // 这里留一个保险，
        // 防止以后容器实现发生变化。
        if (currentContainer ==
            container)
        {
            CloseContainer(
                container
            );
        }
    }


    // =========================================================
    // Close
    // =========================================================

    public void CloseContainer(
        LootContainer container
    )
    {
        if (container == null ||
            currentContainer != container)
        {
            return;
        }


        Unsubscribe(
            currentContainer
        );


        currentContainer = null;


        temporaryMessage =
            string.Empty;

        temporaryMessageTimer = 0f;


        itemViews.Clear();


        if (searchPanelRoot != null)
        {
            searchPanelRoot.SetActive(
                false
            );
        }
    }


    // =========================================================
    // Events
    // =========================================================

    private void Subscribe(
        LootContainer container
    )
    {
        container.SearchStateChanged -=
            HandleContainerStateChanged;

        container.LootChanged -=
            HandleLootChanged;


        container.SearchStateChanged +=
            HandleContainerStateChanged;

        container.LootChanged +=
            HandleLootChanged;
    }


    private void Unsubscribe(
        LootContainer container
    )
    {
        if (container == null)
        {
            return;
        }


        container.SearchStateChanged -=
            HandleContainerStateChanged;

        container.LootChanged -=
            HandleLootChanged;
    }


    private void HandleContainerStateChanged(
        LootContainer container
    )
    {
        RefreshItems();

        RefreshStatusText();

        RefreshOpeningProgress();

        RefreshItemSearchProgress();
    }


    private void HandleLootChanged(
        LootContainer container
    )
    {
        RefreshItems();

        RefreshStatusText();

        RefreshItemSearchProgress();
    }


    // =========================================================
    // Title
    // =========================================================

    private void RefreshTitle()
    {
        if (titleText == null ||
            currentContainer == null)
        {
            return;
        }


        titleText.text =
            currentContainer.DisplayName;
    }


    // =========================================================
    // Status
    // =========================================================


    private bool HasRemainingLoot()
    {
        if (currentContainer == null)
        {
            return false;
        }

        for (int i = 0;
             i < currentContainer.SearchEntries.Count;
             i++)
        {
            LootContainerSearchEntry entry =
                currentContainer.SearchEntries[i];

            if (entry == null ||
                entry.IsTaken ||
                entry.Stack == null ||
                entry.Stack.IsEmpty)
            {
                continue;
            }

            return true;
        }

        return false;
    }
    private void RefreshStatusText()
    {
        if (statusText == null ||
            currentContainer == null)
        {
            return;
        }


        if (!string.IsNullOrEmpty(
                temporaryMessage
            ))
        {
            statusText.text =
                temporaryMessage;

            return;
        }


        switch (currentContainer.State)
        {
            case LootContainerState.Closed:

                statusText.text =
                    "CLOSED";

                break;


            case LootContainerState.Opening:

                statusText.text =
                    "OPENING...";

                break;


            case LootContainerState.Searching:

                statusText.text =
                    "SEARCHING";

                break;


            case LootContainerState.SearchComplete:

                statusText.text =
                    HasRemainingLoot()
                        ? "SEARCH COMPLETE"
                        : "EMPTY";

                break;
        }
    }


    // =========================================================
    // Opening Progress
    // =========================================================

    private void RefreshOpeningProgress()
    {
        if (currentContainer == null)
        {
            return;
        }


        bool show =
            currentContainer.State ==
            LootContainerState.Opening;


        if (progressRoot != null)
        {
            progressRoot.SetActive(
                show
            );
        }


        if (progressFill != null)
        {
            progressFill.fillAmount =
                show
                    ? currentContainer
                        .CurrentSearchProgress01
                    : 0f;
        }
    }


    // =========================================================
    // Item Search Progress
    // =========================================================

    private void RefreshItemSearchProgress()
    {
        if (currentContainer == null)
        {
            return;
        }


        LootContainerSearchEntry
            currentEntry =
                currentContainer
                    .CurrentSearchEntry;


        float progress =
            currentContainer.State ==
            LootContainerState.Searching
                ? currentContainer
                    .CurrentSearchProgress01
                : 0f;


        for (int i = 0;
             i < itemViews.Count;
             i++)
        {
            LootSearchItemView view =
                itemViews[i];


            if (view == null)
            {
                continue;
            }


            bool isCurrent =
                currentEntry != null &&
                view.Entry ==
                currentEntry;


            view.SetSearchProgress(
                progress,
                isCurrent
            );
        }
    }


    // =========================================================
    // Grid
    // =========================================================

    private void BuildGridCells()
    {
        if (currentContainer == null ||
            cellLayer == null ||
            itemLayer == null ||
            cellPrefab == null)
        {
            return;
        }


        ClearChildren(
            cellLayer
        );


        int width =
            currentContainer.GridWidth;

        int height =
            currentContainer.GridHeight;


        float gridWidth =
            width * cellSize
            +
            Mathf.Max(
                0,
                width - 1
            ) * spacing;


        float gridHeight =
            height * cellSize
            +
            Mathf.Max(
                0,
                height - 1
            ) * spacing;


        cellLayer.sizeDelta =
            new Vector2(
                gridWidth,
                gridHeight
            );


        itemLayer.sizeDelta =
            new Vector2(
                gridWidth,
                gridHeight
            );


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
                width;


            layout.childAlignment =
                TextAnchor.UpperLeft;
        }


        int totalCells =
            width * height;


        for (int i = 0;
             i < totalCells;
             i++)
        {
            Instantiate(
                cellPrefab,
                cellLayer
            );
        }
    }


    // =========================================================
    // Items
    // =========================================================

    private void RefreshItems()
    {
        if (currentContainer == null ||
            itemLayer == null ||
            itemViewPrefab == null)
        {
            return;
        }


        ClearChildren(
            itemLayer
        );


        itemViews.Clear();


        for (int i = 0;
             i <
             currentContainer
                 .SearchEntries
                 .Count;
             i++)
        {
            LootContainerSearchEntry entry =
                currentContainer
                    .SearchEntries[i];


            if (entry == null ||
                entry.IsTaken ||
                entry.Stack == null ||
                entry.Stack.IsEmpty)
            {
                continue;
            }


            LootSearchItemView view =
                Instantiate(
                    itemViewPrefab,
                    itemLayer
                );


            view.Initialize(
                entry,
                cellSize,
                spacing,
                HandleItemClicked
            );


            itemViews.Add(
                view
            );
        }
    }


    private void HandleItemClicked(
        LootContainerSearchEntry entry
    )
    {
        if (currentContainer == null)
        {
            return;
        }


        currentContainer.TryTakeEntry(
            entry
        );
    }


    // =========================================================
    // Feedback
    // =========================================================

    public void ShowTemporaryMessage(
        string message
    )
    {
        temporaryMessage =
            message;


        temporaryMessageTimer =
            1.25f;


        RefreshStatusText();
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
        if (currentContainer != null)
        {
            Unsubscribe(
                currentContainer
            );
        }


        if (Instance == this)
        {
            Instance = null;
        }
    }
}