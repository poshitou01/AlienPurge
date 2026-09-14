using UnityEngine;


[DisallowMultipleComponent]
public class WeaponManager : MonoBehaviour
{
    // =========================================================
    // Current Weapon
    // =========================================================

    [Header("Current Weapon")]

    [SerializeField]
    private WeaponController currentWeapon;


    // =========================================================
    // Runtime Dependencies
    // =========================================================

    [Header("Runtime Dependencies")]

    [Tooltip("玩家武器使用的 Bullet 对象池。")]
    [SerializeField]
    private BulletPool bulletPool;


    private PlayerWeaponModifiers weaponModifiers;


    // =========================================================
    // Public Access
    // =========================================================

    public WeaponController CurrentWeapon =>
        currentWeapon;


    public bool CanReduceFireCooldown =>
        currentWeapon != null
        && currentWeapon.CanReduceFireCooldown;

    public bool CanIncreaseBulletSpeed =>
        currentWeapon != null
        && currentWeapon.CanIncreaseProjectileSpeed;

    public bool CanIncreaseBulletScale =>
        currentWeapon != null
        && currentWeapon.CanIncreaseProjectileScale;

    public bool CanIncreaseProjectileCount =>
        currentWeapon != null
        && currentWeapon.CanIncreaseProjectileCount;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        weaponModifiers =
            GetComponent<PlayerWeaponModifiers>();

        if (weaponModifiers == null)
        {
            Debug.LogWarning(
                "WeaponManager: "
                + "PlayerWeaponModifiers was not found.",
                this
            );
        }
    }


    private void Start()
    {
        BindCurrentWeaponDependencies();
    }


    // =========================================================
    // Aim
    // =========================================================

    public void AimAt(
        Vector2 worldPosition)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.AimAt(
            worldPosition
        );
    }


    // =========================================================
    // Fire
    // =========================================================

    public bool TryFire()
    {
        if (currentWeapon == null)
        {
            return false;
        }

        return
            currentWeapon.TryFire();
    }


    // =========================================================
    // Weapon Assignment
    // =========================================================

    public void SetCurrentWeapon(
        WeaponController weapon)
    {
        if (currentWeapon != null)
        {
            currentWeapon.SetOperational(
                false
            );
        }

        currentWeapon =
            weapon;

        BindCurrentWeaponDependencies();

        if (currentWeapon != null)
        {
            currentWeapon.SetOperational(
                true
            );
        }
    }


    private void BindCurrentWeaponDependencies()
    {
        if (currentWeapon == null)
        {
            Debug.LogWarning(
                "WeaponManager: "
                + "CurrentWeapon has not been assigned.",
                this
            );

            return;
        }

        currentWeapon.SetRuntimeDependencies(
            bulletPool,
            weaponModifiers
        );
    }


    // =========================================================
    // Runtime Weapon Upgrade API
    // =========================================================

    public void IncreaseWeaponLevel()
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.IncreaseWeaponLevel();
    }
    public void ReduceFireCooldown(
        float amount)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.ReduceFireCooldown(
            amount
        );
    }


    public void AddBulletDamage(
        int amount)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.AddDamage(
            amount
        );
    }


    public void AddBulletSpeed(
        float amount)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.AddProjectileSpeed(
            amount
        );
    }


    public void AddBulletScaleMultiplier(
        float amount)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.AddProjectileScale(
            amount
        );
    }


    public void AddProjectileCount(
        int amount)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.AddProjectileCount(
            amount
        );
    }


    // =========================================================
    // Operational State
    // =========================================================

    public void SetOperational(
        bool value)
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.SetOperational(
            value
        );
    }
}