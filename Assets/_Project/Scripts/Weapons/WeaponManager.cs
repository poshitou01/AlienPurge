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
    // Public Access
    // =========================================================

    public WeaponController CurrentWeapon =>
        currentWeapon;


    // =========================================================
    // Aim
    // =========================================================

    public void AimAt(
        Vector2 worldPosition
    )
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
    // Weapon Assignment
    // =========================================================

    public void SetCurrentWeapon(
        WeaponController weapon
    )
    {
        currentWeapon =
            weapon;
    }
}