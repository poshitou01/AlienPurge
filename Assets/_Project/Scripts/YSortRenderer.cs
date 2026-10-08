using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class YSortRenderer : MonoBehaviour
{
    [Header("Sort Reference")]
    [SerializeField] private Transform sortAnchor;

    [Header("Renderer Target")]
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private SpriteRenderer fallbackSpriteRenderer;

    [Header("Sort Settings")]
    [SerializeField] private bool updateEveryFrame = true;

    [Tooltip("通常保持 0。需要整体前后偏移时再调整。")]
    [SerializeField] private int baseOrder = 0;

    [Tooltip("每 1 个世界单位对应多少 Sorting Order。推荐 100。")]
    [SerializeField] private int orderPerUnit = 100;

    [Tooltip("用于个别物体微调前后关系。通常保持 0。")]
    [SerializeField] private int orderOffset = 0;


    private void Reset()
    {
        sortingGroup = GetComponent<SortingGroup>();

        if (sortingGroup == null)
        {
            fallbackSpriteRenderer = GetComponent<SpriteRenderer>();

            if (fallbackSpriteRenderer == null)
            {
                fallbackSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }
    }


    private void Awake()
    {
        ResolveRendererReferences();
        RefreshSortingOrder();
    }


    private void OnEnable()
    {
        RefreshSortingOrder();
    }


    private void LateUpdate()
    {
        if (!updateEveryFrame)
            return;

        RefreshSortingOrder();
    }


    private void ResolveRendererReferences()
    {
        if (sortingGroup == null)
        {
            sortingGroup = GetComponent<SortingGroup>();
        }

        if (sortingGroup == null && fallbackSpriteRenderer == null)
        {
            fallbackSpriteRenderer = GetComponent<SpriteRenderer>();

            if (fallbackSpriteRenderer == null)
            {
                fallbackSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }
    }


    public void RefreshSortingOrder()
    {
        Transform anchor = sortAnchor != null ? sortAnchor : transform;

        int sortingOrder =
            baseOrder
            - Mathf.RoundToInt(anchor.position.y * orderPerUnit)
            + orderOffset;

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = sortingOrder;
            return;
        }

        if (fallbackSpriteRenderer != null)
        {
            fallbackSpriteRenderer.sortingOrder = sortingOrder;
        }
    }


    public void SetSortAnchor(Transform newAnchor)
    {
        sortAnchor = newAnchor;
        RefreshSortingOrder();
    }


    public void SetOrderOffset(int newOffset)
    {
        orderOffset = newOffset;
        RefreshSortingOrder();
    }


    private void OnDrawGizmosSelected()
    {
        Transform anchor = sortAnchor != null ? sortAnchor : transform;

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            anchor.position,
            0.12f
        );

        Gizmos.DrawLine(
            anchor.position + Vector3.left * 1.5f,
            anchor.position + Vector3.right * 1.5f
        );
    }
}