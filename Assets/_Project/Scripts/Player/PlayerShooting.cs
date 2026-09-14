using UnityEngine;
using UnityEngine.EventSystems;


[DisallowMultipleComponent]
public class PlayerShooting : MonoBehaviour
{
    private Camera mainCamera;

    private PlayerAbilityState abilityState;

    private WeaponManager weaponManager;

    private bool canShoot = true;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        mainCamera =
            Camera.main;

        abilityState =
            GetComponent<PlayerAbilityState>();

        weaponManager =
            GetComponent<WeaponManager>();

        if (weaponManager == null)
        {
            Debug.LogWarning(
                "PlayerShooting: "
                + "WeaponManager was not found.",
                this
            );
        }
    }


    private void Update()
    {
        UpdateWeaponAim();

        if (!CanProcessShootingInput())
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            TryShoot();
        }
    }


    // =========================================================
    // Aim Input
    // =========================================================

    private void UpdateWeaponAim()
    {
        if (!CanProcessAimInput())
        {
            return;
        }

        if (weaponManager == null)
        {
            return;
        }

        if (!TryGetMouseWorldPosition(
                out Vector2 mouseWorldPosition))
        {
            return;
        }

        weaponManager.AimAt(
            mouseWorldPosition
        );
    }


    private bool CanProcessAimInput()
    {
        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        if (WeaponModuleSelectionManager.IsChoosingModule)
        {
            return false;
        }

        if (PauseMenuController.IsPaused)
        {
            return false;
        }

        if (GameManager.Instance != null
            && !GameManager.Instance.IsPlaying)
        {
            return false;
        }

        if (EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // Fire Input
    // =========================================================

    private bool CanProcessShootingInput()
    {
        if (!canShoot)
        {
            return false;
        }

        if (abilityState != null
            && (abilityState.IsDashing
                || abilityState.IsCasting))
        {
            return false;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        if (WeaponModuleSelectionManager.IsChoosingModule)
        {
            return false;
        }

        if (PauseMenuController.IsPaused)
        {
            return false;
        }

        if (GameManager.Instance != null
            && !GameManager.Instance.IsPlaying)
        {
            return false;
        }

        if (EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        return true;
    }


    private void TryShoot()
    {
        if (weaponManager == null)
        {
            return;
        }

        weaponManager.TryFire();
    }


    // =========================================================
    // Mouse Utility
    // =========================================================

    private bool TryGetMouseWorldPosition(
        out Vector2 mouseWorldPosition)
    {
        mouseWorldPosition =
            Vector2.zero;

        if (mainCamera == null)
        {
            mainCamera =
                Camera.main;

            if (mainCamera == null)
            {
                return false;
            }
        }

        Vector3 mouseScreenPosition =
            Input.mousePosition;

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(
                mouseScreenPosition
            );

        worldPosition.z =
            0f;

        mouseWorldPosition =
            worldPosition;

        return true;
    }


    // =========================================================
    // External State Control
    // =========================================================

    public void SetCanShoot(
        bool value)
    {
        canShoot =
            value;
    }
}