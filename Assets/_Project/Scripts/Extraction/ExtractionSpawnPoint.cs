using UnityEngine;


[DisallowMultipleComponent]
public class ExtractionSpawnPoint :
    MonoBehaviour
{
    [Header("Extraction Spawn Point")]

    [SerializeField]
    private bool allowExtraction =
        true;


    public Vector2 Position =>
        transform.position;


    public bool IsUsable =>
        allowExtraction
        &&
        isActiveAndEnabled
        &&
        gameObject.activeInHierarchy;


    private void OnDrawGizmos()
    {
        Color previousColor =
            Gizmos.color;


        Gizmos.color =
            new Color(
                0.15f,
                0.95f,
                1f,
                1f
            );


        Gizmos.DrawWireSphere(
            transform.position,
            0.55f
        );


        Gizmos.DrawLine(
            transform.position,
            transform.position
            +
            Vector3.up * 1.5f
        );


        Gizmos.color =
            previousColor;
    }
}