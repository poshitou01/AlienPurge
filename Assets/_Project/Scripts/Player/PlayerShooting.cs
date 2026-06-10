using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float spawnOffset = 0.45f;
    [SerializeField] private float fireCooldown = 0.15f;

    [Header("Bullet Damage Settings")]
    [SerializeField] private int bulletDamage = 1;

    [Header("Upgrade Limits")]
    [SerializeField] private float minimumFireCooldown = 0.05f;

    private Camera mainCamera;
    private float nextFireTime;

    private bool canShoot = true;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!canShoot || UpgradeManager.IsChoosingUpgrade)
        {
            return;
        }

        if (Input.GetMouseButton(0))
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
        Vector2 shootDirection = (Vector2)mouseWorldPosition - playerPosition;

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
            bullet.SetDamage(bulletDamage);
        }

        nextFireTime = Time.time + fireCooldown;
    }

    public void SetCanShoot(bool value)
    {
        canShoot = value;
    }

    public void ReduceFireCooldown(float amount)
    {
        fireCooldown -= amount;

        if (fireCooldown < minimumFireCooldown)
        {
            fireCooldown = minimumFireCooldown;
        }

        Debug.Log("Fire cooldown upgraded. Current fire cooldown: " + fireCooldown);
    }

    public void AddBulletDamage(int amount)
    {
        bulletDamage += amount;

        Debug.Log("Bullet damage upgraded. Current bullet damage: " + bulletDamage);
    }
}