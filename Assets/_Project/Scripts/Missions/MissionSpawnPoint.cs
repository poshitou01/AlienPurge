using UnityEngine;

[DisallowMultipleComponent]
public class MissionSpawnPoint :
    MonoBehaviour
{
    public Vector2 Position =>
        transform.position;


    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            0.35f
        );
    }
}