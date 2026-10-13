
using UnityEngine;

/// <summary>
/// 根据敌人世界坐标 Y 自动调整绘制顺序。
///
/// Y 越小，Sorting Order 越大，
/// 敌人就越靠近屏幕前方。
///
/// 只影响视觉，不修改 Rigidbody2D、
/// Collider2D 或敌人移动逻辑。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyYSorting : MonoBehaviour
{
    [Header("Sorting Settings")]

    [Min(1)]
    [SerializeField]
    private int sortingPrecision = 100;

    [SerializeField]
    private int sortingOffset = 0;

    [Header("References")]

    [SerializeField]
    private SpriteRenderer targetRenderer;

    private void Awake()
    {
        CacheRenderer();
    }

    private void OnEnable()
    {
        CacheRenderer();
        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        UpdateSortingOrder();
    }

    private void CacheRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponent<SpriteRenderer>();
        }
    }

    private void UpdateSortingOrder()
    {
        if (targetRenderer == null)
        {
            return;
        }

        float worldY = transform.position.y;

        int newOrder = Mathf.RoundToInt(
            -worldY * sortingPrecision
        ) + sortingOffset;

        // 防止超过 Unity Sorting Order 的范围
        newOrder = Mathf.Clamp(
            newOrder,
            -32000,
            32000
        );

        if (targetRenderer.sortingOrder != newOrder)
        {
            targetRenderer.sortingOrder = newOrder;
        }
    }

    private void OnValidate()
    {
        sortingPrecision =
            Mathf.Max(1, sortingPrecision);
    }
}
