using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float stoppingDistance = 1.0f;

    [Header("Target")]
    [SerializeField] private Transform target;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        Debug.Log("Enemy start position: " + transform.position);

        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                target = playerObject.transform;
                Debug.Log("Enemy target found: " + target.name + ", target position: " + target.position);
            }
            else
            {
                Debug.LogWarning("EnemyMovement: Could not find an object with the Player tag.");
            }
        }
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 currentPosition = rb.position;
        Vector2 targetPosition = target.position;

        float distance = Vector2.Distance(currentPosition, targetPosition);

        if (distance <= stoppingDistance)
        {
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            moveSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);
    }
}