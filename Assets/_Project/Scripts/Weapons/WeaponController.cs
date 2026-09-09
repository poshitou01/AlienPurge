using UnityEngine;


[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    // =========================================================
    // Weapon Definition
    // =========================================================

    [Header("Weapon Definition")]

    [SerializeField]
    private WeaponData weaponData;


    // =========================================================
    // Visual References
    // =========================================================

    [Header("Visual References")]

    [Tooltip("负责围绕玩家旋转的武器瞄准 Pivot。")]
    [SerializeField]
    private Transform weaponHolder;

    [Tooltip("真实枪口位置。")]
    [SerializeField]
    private Transform muzzlePoint;

    [Tooltip("枪口瞬时闪光对象。")]
    [SerializeField]
    private GameObject muzzleFlash;


    // =========================================================
    // Runtime Aim State
    // =========================================================

    private Vector3 baseLocalScale;

    private Vector2 aimDirection =
        Vector2.right;

    private bool isAimingLeft;


    // =========================================================
    // Public Read Only Access
    // =========================================================

    public WeaponData Data =>
        weaponData;

    public Transform MuzzlePoint =>
        muzzlePoint;

    public Vector2 AimDirection =>
        aimDirection;

    public bool IsAimingLeft =>
        isAimingLeft;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        baseLocalScale =
            transform.localScale;

        if (weaponHolder == null)
        {
            weaponHolder =
                transform.parent;
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(false);
        }
    }


    // =========================================================
    // Aim
    // =========================================================

    public void AimAt(
        Vector2 worldPosition
    )
    {
        if (weaponHolder == null)
        {
            return;
        }

        Vector2 direction =
            worldPosition
            - (Vector2)weaponHolder.position;

        if (direction.sqrMagnitude
            <= 0.0001f)
        {
            return;
        }

        aimDirection =
            direction.normalized;

        float angle =
            Mathf.Atan2(
                aimDirection.y,
                aimDirection.x
            )
            * Mathf.Rad2Deg;

        weaponHolder.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );

        UpdateWeaponFlip();
    }


    private void UpdateWeaponFlip()
    {
        // 接近纯竖直瞄准时，
        // 保持上一帧左右朝向，
        // 避免鼠标经过正上/正下方时频繁抖动。
        if (aimDirection.x > 0.001f)
        {
            SetAimingLeft(false);
        }
        else if (aimDirection.x < -0.001f)
        {
            SetAimingLeft(true);
        }
    }


    private void SetAimingLeft(
        bool value
    )
    {
        if (isAimingLeft == value)
        {
            return;
        }

        isAimingLeft = value;

        Vector3 scale =
            baseLocalScale;

        scale.y =
            Mathf.Abs(
                baseLocalScale.y
            )
            * (isAimingLeft ? -1f : 1f);

        transform.localScale =
            scale;
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        if (weaponHolder == null
            && transform.parent != null)
        {
            weaponHolder =
                transform.parent;
        }
    }
}