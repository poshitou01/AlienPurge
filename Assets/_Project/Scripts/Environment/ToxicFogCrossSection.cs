
using UnityEngine;

/// <summary>
/// Phase40 - Toxic Fog Cross Section.
///
/// 定义毒雾路线某个位置的左右覆盖边界。
/// 当前阶段只负责空间标记与 Gizmos 可视化。
///
/// 后续 Toxic Fog Area Controller
/// 会读取这些边界构建危险区域。
/// </summary>
[DisallowMultipleComponent]
public class ToxicFogCrossSection : MonoBehaviour
{
    [Header("Fog Coverage Boundaries")]

    [SerializeField]
    private Transform leftBoundary;

    [SerializeField]
    private Transform rightBoundary;

    [Header("Debug")]

    [SerializeField]
    private bool drawGizmos = true;


    public bool IsValid =>
        leftBoundary != null &&
        rightBoundary != null;


    public Vector2 Center =>
        transform.position;


    public Vector2 LeftPosition =>
        leftBoundary != null
            ? (Vector2)leftBoundary.position
            : (Vector2)transform.position;


    public Vector2 RightPosition =>
        rightBoundary != null
            ? (Vector2)rightBoundary.position
            : (Vector2)transform.position;


    private void Reset()
    {
        leftBoundary =
            transform.Find("FogLeft");

        rightBoundary =
            transform.Find("FogRight");
    }


    [ContextMenu("Debug/Find Boundary Children")]
    private void FindBoundaryChildren()
    {
        leftBoundary =
            transform.Find("FogLeft");

        rightBoundary =
            transform.Find("FogRight");
    }


    public bool TryGetBoundary(
        out Vector2 left,
        out Vector2 right)
    {
        left = LeftPosition;
        right = RightPosition;

        return IsValid;
    }


    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        if (!IsValid)
        {
            return;
        }

        Vector3 left =
            leftBoundary.position;

        Vector3 right =
            rightBoundary.position;

        // 毒雾横截面
        Gizmos.color =
            new Color(
                0.65f,
                1f,
                0.25f,
                0.95f
            );

        Gizmos.DrawLine(left, right);

        // 左边界
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            left,
            0.35f
        );

        // 右边界
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            right,
            0.35f
        );

        // 路线中心
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            transform.position,
            0.22f
        );
    }
}
