using UnityEngine;


[DisallowMultipleComponent]
public class HighRiskCacheSpawnPoint :
    MonoBehaviour
{
    [Header("High-Risk Cache Spawn Point")]

    [SerializeField]
    private bool allowCacheSpawn =
        true;


    public Vector2 Position =>
        transform.position;


    public bool IsUsable =>
        allowCacheSpawn
        &&
        isActiveAndEnabled
        &&
        gameObject.activeInHierarchy;


    private void OnDrawGizmos()
    {
        Color oldColor =
            Gizmos.color;


        Gizmos.color =
            new Color(
                1f,
                0.45f,
                0.1f,
                1f
            );


        Gizmos.DrawWireSphere(
            transform.position,
            0.5f
        );


        Gizmos.DrawLine(
            transform.position,
            transform.position
            +
            Vector3.up * 1.2f
        );


        Gizmos.color =
            oldColor;
    }
}