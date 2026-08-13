using UnityEngine;

[DisallowMultipleComponent]
public class ExperienceOrbMagnet : MonoBehaviour
{
    [Header("Magnet Settings")]

    [Tooltip("玩家进入这个范围后，经验球开始被吸引")]
    [Min(0f)]
    [SerializeField]
    private float attractionRadius = 3.5f;

    [Tooltip("经验球飞向玩家的速度")]
    [Min(0f)]
    [SerializeField]
    private float attractionSpeed = 8f;


    [Header("Player Detection")]

    [SerializeField]
    private string playerTag = "Player";


    [Header("Runtime Debug")]

    [SerializeField]
    private bool isAttracted;

    [SerializeField]
    private float currentDistanceToPlayer;


    private Transform player;


    private void OnEnable()
    {
        isAttracted = false;
        currentDistanceToPlayer = 0f;

        FindPlayer();
    }


    private void Update()
    {
        if (player == null)
        {
            FindPlayer();

            if (player == null)
            {
                return;
            }
        }


        Vector3 toPlayer =
            player.position - transform.position;

        currentDistanceToPlayer =
            toPlayer.magnitude;


        // 一旦进入吸附范围，
        // 这颗经验球就会持续追向玩家，
        // 不会因为玩家再次跑远而停止。
        if (!isAttracted)
        {
            float radiusSqr =
                attractionRadius
                * attractionRadius;

            if (toPlayer.sqrMagnitude
                <= radiusSqr)
            {
                isAttracted = true;
            }
        }


        if (!isAttracted)
        {
            return;
        }


        transform.position =
            Vector3.MoveTowards(
                transform.position,
                player.position,
                attractionSpeed
                * Time.deltaTime
            );
    }


    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                playerTag
            );

        if (playerObject == null)
        {
            player = null;
            return;
        }

        player =
            playerObject.transform;
    }


    private void OnDisable()
    {
        // 对象池回收后清除这一轮状态。
        isAttracted = false;
        currentDistanceToPlayer = 0f;
    }


    private void OnValidate()
    {
        attractionRadius =
            Mathf.Max(
                0f,
                attractionRadius
            );

        attractionSpeed =
            Mathf.Max(
                0f,
                attractionSpeed
            );
    }
}