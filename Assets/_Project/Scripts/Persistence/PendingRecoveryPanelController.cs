using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public sealed class PendingRecoveryPanelController :
    MonoBehaviour
{
    // =========================================================
    // UI
    // =========================================================

    [Header("List")]

    [SerializeField]
    private RectTransform contentRoot;


    [SerializeField]
    private PendingRecoveryItemView
        itemViewPrefab;


    [Header("Summary")]

    [SerializeField]
    private TMP_Text itemCountText;


    [SerializeField]
    private TMP_Text totalValueText;


    [SerializeField]
    private TMP_Text emptyStateText;


    [Header("Buttons")]

    [SerializeField]
    private Button moveAllButton;


    [SerializeField]
    private TMP_Text moveAllButtonText;


    // =========================================================
    // Runtime
    // =========================================================

    private PersistentProfile profile;


    private PendingRecoveryService service;


    private bool initialized;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        service =
            new PendingRecoveryService();


        if (moveAllButton != null)
        {
            moveAllButton.onClick
                .AddListener(
                    MoveAll
                );
        }


        if (moveAllButtonText != null)
        {
            moveAllButtonText.text =
                "MOVE ALL TO STASH";
        }
    }


    private void OnEnable()
    {
        TryInitialize();

        Refresh();
    }


    private void Start()
    {
        TryInitialize();

        Refresh();
    }


    // =========================================================
    // Initialization
    // =========================================================

    private void TryInitialize()
    {
        if (initialized)
        {
            return;
        }


        profile =
            PersistentProfile.Instance;


        if (profile == null ||
            !profile.IsInitialized)
        {
            return;
        }


        profile.Changed +=
            HandleProfileChanged;


        initialized =
            true;
    }


    // =========================================================
    // Refresh
    // =========================================================

    public void Refresh()
    {
        if (!initialized)
        {
            TryInitialize();
        }


        if (!initialized ||
            profile == null)
        {
            return;
        }


        ClearViews();


        int entryCount =
            0;


        for (int i = 0;
             i <
             profile.PendingRecovery.Count;
             i++)
        {
            PendingRecoveryItem entry =
                profile.PendingRecovery[i];


            if (entry == null ||
                entry.Item == null ||
                entry.Quantity <= 0)
            {
                continue;
            }


            entryCount++;


            if (contentRoot == null ||
                itemViewPrefab == null)
            {
                continue;
            }


            PendingRecoveryItemView view =
                Instantiate(
                    itemViewPrefab,
                    contentRoot
                );


            view.Initialize(
                entry,
                MoveItem
            );
        }


        if (itemCountText != null)
        {
            itemCountText.text =
                "PENDING ITEMS  "
                + profile
                    .PendingRecoveryItemCount;
        }


        if (totalValueText != null)
        {
            totalValueText.text =
                "PENDING VALUE  "
                + profile
                    .PendingRecoveryTotalValue;
        }


        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(
                entryCount == 0
            );


            emptyStateText.text =
                "NO PENDING RECOVERY";
        }


        if (moveAllButton != null)
        {
            moveAllButton.interactable =
                profile
                    .PendingRecoveryItemCount
                >
                0;
        }
    }


    // =========================================================
    // Move One
    // =========================================================

    private void MoveItem(
        ItemData item
    )
    {
        if (service == null)
        {
            service =
                new PendingRecoveryService();
        }


        bool success =
            service.TryMoveToStash(
                profile,
                SaveManager.Instance,
                item,
                int.MaxValue,
                out int moved,
                out int remaining
            );


        Debug.Log(
            "[Pending Recovery UI] "
            + "Move result: "
            + success
            + ", Moved: "
            + moved
            + ", Remaining: "
            + remaining,
            this
        );


        Refresh();
    }


    // =========================================================
    // Move All
    // =========================================================

    private void MoveAll()
    {
        if (service == null)
        {
            service =
                new PendingRecoveryService();
        }


        bool success =
            service.TryMoveAllToStash(
                profile,
                SaveManager.Instance,
                out int moved,
                out int remaining
            );


        Debug.Log(
            "[Pending Recovery UI] "
            + "Move All result: "
            + success
            + ", Moved: "
            + moved
            + ", Remaining: "
            + remaining,
            this
        );


        Refresh();
    }


    // =========================================================
    // Events
    // =========================================================

    private void HandleProfileChanged()
    {
        Refresh();
    }


    // =========================================================
    // Views
    // =========================================================

    private void ClearViews()
    {
        if (contentRoot == null)
        {
            return;
        }


        for (int i =
                 contentRoot.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                contentRoot
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
        if (profile != null)
        {
            profile.Changed -=
                HandleProfileChanged;
        }


        if (moveAllButton != null)
        {
            moveAllButton.onClick
                .RemoveListener(
                    MoveAll
                );
        }
    }
}