using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float spawnOffset = 0.45f;
    [SerializeField] private float fireCooldown = 0.15f;

    private Camera mainCamera;
    private float nextFireTime;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryShoot();
        }
    }

    private void TryShoot()
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        if (bulletPrefab == null)
        {
            Debug.LogWarning("PlayerShooting: Bullet Prefab has not been assigned.");
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning("PlayerShooting: Main Camera not found.");
                return;
            }
        }

        Vector3 mouseScreenPosition = Input.mousePosition;
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;

        Vector2 playerPosition = transform.position;
        Vector2 shootDirection = ((Vector2)mouseWorldPosition - playerPosition);

        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        shootDirection.Normalize();

        Vector2 spawnPosition = playerPosition + shootDirection * spawnOffset;

        GameObject bulletObject = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.identity
        );

        Bullet bullet = bulletObject.GetComponent<Bullet>();

        if (bullet != null)
        {
            bullet.Initialize(shootDirection);
        }

        nextFireTime = Time.time + fireCooldown;
    }
}
