using UnityEngine;
using UnityEngine.EventSystems;


[DisallowMultipleComponent]
public class PlayerShooting : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    private Camera mainCamera;

    private PlayerAbilityState abilityState;

    private WeaponManager weaponManager;


    // =========================================================
    // Runtime State
    // =========================================================

    private bool canShoot = true;


    /// <summary>
    /// 当前这一轮鼠标左键按下，
    /// 是否是从 UI 上开始的。
    ///
    /// 如果从 UI 开始：
    /// 整次长按都不允许射击。
    ///
    /// 如果从游戏区域开始：
    /// 即使后来鼠标经过 HUD，
    /// 仍然允许持续射击。
    /// </summary>
    private bool fireHoldStartedOverUI;


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
        UpdateMouseHoldState();


        // Aim 和 Fire 分开处理。
        UpdateWeaponAim();


        if (!CanProcessShootingInput())
        {
            return;
        }


        // =====================================================
        // Continuous Fire
        // =====================================================
        //
        // 重点：
        //
        // 这里直接读取 GetMouseButton(0)，
        // 而不是依赖一次性的
        // gameplayFireHoldActive。
        //
        // 因此：
        //
        // 按住鼠标
        // → Dash暂时禁止射击
        // → Dash结束
        // → 鼠标仍然按着
        // → 自动恢复连续射击。
        if (Input.GetMouseButton(0)
            && !fireHoldStartedOverUI)
        {
            TryShoot();
        }
    }


    private void OnDisable()
    {
        fireHoldStartedOverUI =
            false;
    }


    // =========================================================
    // Mouse Hold State
    // =========================================================

    private void UpdateMouseHoldState()
    {
        // 只在“真正按下的第一帧”
        // 判断这次操作是不是从UI开始。
        if (Input.GetMouseButtonDown(0))
        {
            fireHoldStartedOverUI =
                IsPointerOverUI();
        }


        // 松开以后，
        // 当前这一轮长按正式结束。
        if (Input.GetMouseButtonUp(0))
        {
            fireHoldStartedOverUI =
                false;
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


        // =====================================================
        // UI Rule
        // =====================================================
        //
        // 鼠标没有进行Gameplay长按时：
        // 放在UI上不更新瞄准。
        //
        // 但是如果这一轮左键原本在游戏区域开始，
        // 即使后来经过HUD，也继续更新Aim。
        //
        // 防止：
        //
        // 朝右扫射
        // → 鼠标稍微下移经过某个UI Raycast区域
        // → 武器Aim突然冻结。
        bool gameplayFireHeld =
            Input.GetMouseButton(0)
            && !fireHoldStartedOverUI;


        if (IsPointerOverUI()
            && !gameplayFireHeld)
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
        // =====================================================
        // External Shooting Lock
        // =====================================================

        if (!canShoot)
        {
            return false;
        }


        // =====================================================
        // Ability Lock
        // =====================================================
        //
        // Dash 和 Casting 本身期间不能射击。
        //
        // 但是这里只是“暂时return false”。
        //
        // 绝对不能清除鼠标长按状态。
        //
        // 技能结束以后，只要左键仍然按着，
        // 下一帧自然恢复连续射击。
        if (abilityState != null
            && (abilityState.IsDashing
                || abilityState.IsCasting))
        {
            return false;
        }


        // =====================================================
        // Selection / Pause
        // =====================================================

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
    // UI Utility
    // =========================================================

    private bool IsPointerOverUI()
    {
        return
            EventSystem.current != null
            && EventSystem.current
                .IsPointerOverGameObject();
    }


    // =========================================================
    // External State Control
    // =========================================================

    /// <summary>
    /// Dash / Pulse / Death 等系统
    /// 可以暂时开启或关闭射击。
    ///
    /// 非常重要：
    ///
    /// 这里只改变 canShoot。
    ///
    /// 绝对不要清除鼠标左键的长按意图。
    ///
    /// 否则：
    ///
    /// 按住左键
    /// → Dash
    /// → SetCanShoot(false)
    /// → 长按状态丢失
    /// → Dash结束后不能自动恢复。
    /// </summary>
    public void SetCanShoot(
        bool value)
    {
        canShoot =
            value;
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Print Shooting State")]
    private void DebugPrintShootingState()
    {
        Debug.Log(
            "===== Player Shooting State =====\n"
            + "Can Shoot: "
            + canShoot

            + "\nLeft Mouse Held: "
            + Input.GetMouseButton(0)

            + "\nFire Hold Started Over UI: "
            + fireHoldStartedOverUI

            + "\nPointer Over UI: "
            + IsPointerOverUI()

            + "\nAbility Mode: "
            + (abilityState != null
                ? abilityState
                    .CurrentMode
                    .ToString()
                : "None")

            + "\nIs Dashing: "
            + (abilityState != null
               && abilityState.IsDashing)

            + "\nIs Casting: "
            + (abilityState != null
               && abilityState.IsCasting),

            this
        );
    }
}