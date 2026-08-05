using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class PlayerAbilityHUD : MonoBehaviour
{
    [Header("Player Ability References")]
    [SerializeField]
    private PlayerDash playerDash;

    [SerializeField]
    private PlayerJetJump playerJetJump;

    [SerializeField]
    private PlayerPulseSkill playerPulseSkill;

    [SerializeField]
    private PlayerHealth playerHealth;

    [Header("Dash Slot")]
    [SerializeField]
    private Image dashIcon;

    [SerializeField]
    private Image dashCooldownOverlay;

    [SerializeField]
    private TMP_Text dashCooldownText;

    [SerializeField]
    private TMP_Text dashKeyText;

    [Header("Jump Slot")]
    [SerializeField]
    private Image jumpIcon;

    [SerializeField]
    private Image jumpCooldownOverlay;

    [SerializeField]
    private TMP_Text jumpCooldownText;

    [SerializeField]
    private TMP_Text jumpKeyText;

    [Header("Pulse Slot")]
    [SerializeField]
    private Image pulseIcon;

    [SerializeField]
    private Image pulseCooldownOverlay;

    [SerializeField]
    private TMP_Text pulseCooldownText;

    [SerializeField]
    private TMP_Text pulseKeyText;

    [Header("Slot Visual State")]
    [SerializeField]
    private Color readyIconColor =
        Color.white;

    [SerializeField]
    private Color coolingIconColor =
        new Color(
            0.42f,
            0.48f,
            0.55f,
            1f
        );

    [SerializeField]
    private Color activeIconColor =
        new Color(
            0.35f,
            1f,
            0.95f,
            1f
        );

    [Range(0f, 1f)]
    [SerializeField]
    private float unavailablePanelAlpha = 0.55f;

    [Header("Display Text")]
    [SerializeField]
    private string readyText = "READY";

    [SerializeField]
    private string activeText = "ACTIVE";

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        ConfigureCanvasGroup();
        ResolvePlayerReferences();
        ConfigureKeyText();
        DisableUIRaycasts();
        RefreshAllSlots();
    }

    private void OnEnable()
    {
        ResolvePlayerReferences();
        RefreshAllSlots();
    }

    private void Update()
    {
        ResolvePlayerReferences();

        UpdateDashSlot();
        UpdateJumpSlot();
        UpdatePulseSlot();
        UpdatePanelAvailability();
    }

    private void ConfigureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.ignoreParentGroups = false;
    }

    private void ResolvePlayerReferences()
    {
        if (playerDash != null
            && playerJetJump != null
            && playerPulseSkill != null
            && playerHealth != null)
        {
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject == null)
        {
            return;
        }

        if (playerDash == null)
        {
            playerDash =
                playerObject.GetComponent<
                    PlayerDash
                >();
        }

        if (playerJetJump == null)
        {
            playerJetJump =
                playerObject.GetComponent<
                    PlayerJetJump
                >();
        }

        if (playerPulseSkill == null)
        {
            playerPulseSkill =
                playerObject.GetComponent<
                    PlayerPulseSkill
                >();
        }

        if (playerHealth == null)
        {
            playerHealth =
                playerObject.GetComponent<
                    PlayerHealth
                >();
        }
    }

    private void ConfigureKeyText()
    {
        if (dashKeyText != null)
        {
            dashKeyText.text = "SHIFT";
        }

        if (jumpKeyText != null)
        {
            jumpKeyText.text = "SPACE";
        }

        if (pulseKeyText != null)
        {
            pulseKeyText.text = "Q";
        }
    }

    private void UpdateDashSlot()
    {
        if (playerDash == null)
        {
            SetUnavailableSlot(
                dashIcon,
                dashCooldownOverlay,
                dashCooldownText
            );

            return;
        }

        UpdateSlot(
            dashIcon,
            dashCooldownOverlay,
            dashCooldownText,
            playerDash.IsActive,
            playerDash.IsReady,
            playerDash.CooldownRemaining,
            playerDash.CooldownNormalized
        );
    }

    private void UpdateJumpSlot()
    {
        if (playerJetJump == null)
        {
            SetUnavailableSlot(
                jumpIcon,
                jumpCooldownOverlay,
                jumpCooldownText
            );

            return;
        }

        UpdateSlot(
            jumpIcon,
            jumpCooldownOverlay,
            jumpCooldownText,
            playerJetJump.IsActive,
            playerJetJump.IsReady,
            playerJetJump.CooldownRemaining,
            playerJetJump.CooldownNormalized
        );
    }

    private void UpdatePulseSlot()
    {
        if (playerPulseSkill == null)
        {
            SetUnavailableSlot(
                pulseIcon,
                pulseCooldownOverlay,
                pulseCooldownText
            );

            return;
        }

        UpdateSlot(
            pulseIcon,
            pulseCooldownOverlay,
            pulseCooldownText,
            playerPulseSkill.IsActive,
            playerPulseSkill.IsReady,
            playerPulseSkill.CooldownRemaining,
            playerPulseSkill.CooldownNormalized
        );
    }

    private void UpdateSlot(
        Image icon,
        Image cooldownOverlay,
        TMP_Text cooldownText,
        bool isActive,
        bool isReady,
        float cooldownRemaining,
        float cooldownNormalized
    )
    {
        if (isActive)
        {
            if (icon != null)
            {
                icon.color =
                    activeIconColor;
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount =
                    0f;
            }

            if (cooldownText != null)
            {
                cooldownText.text =
                    activeText;
            }

            return;
        }

        if (isReady)
        {
            if (icon != null)
            {
                icon.color =
                    readyIconColor;
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount =
                    0f;
            }

            if (cooldownText != null)
            {
                cooldownText.text =
                    readyText;
            }

            return;
        }

        if (icon != null)
        {
            icon.color =
                coolingIconColor;
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount =
                Mathf.Clamp01(
                    cooldownNormalized
                );
        }

        if (cooldownText != null)
        {
            cooldownText.text =
                FormatCooldown(
                    cooldownRemaining
                );
        }
    }

    private string FormatCooldown(
        float remaining
    )
    {
        remaining =
            Mathf.Max(0f, remaining);

        if (remaining >= 1f)
        {
            return Mathf.CeilToInt(
                remaining
            ).ToString();
        }

        return remaining.ToString("0.0");
    }

    private void SetUnavailableSlot(
        Image icon,
        Image cooldownOverlay,
        TMP_Text cooldownText
    )
    {
        if (icon != null)
        {
            icon.color =
                coolingIconColor;
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount =
                1f;
        }

        if (cooldownText != null)
        {
            cooldownText.text = "--";
        }
    }

    private void UpdatePanelAvailability()
    {
        if (canvasGroup == null)
        {
            return;
        }

        bool gameplayAvailable =
            GameManager.Instance != null
            && GameManager.Instance.IsPlaying
            && !PauseMenuController.IsPaused
            && !UpgradeManager
                .IsChoosingUpgrade
            && playerHealth != null
            && !playerHealth.IsDead;

        canvasGroup.alpha =
            gameplayAvailable
                ? 1f
                : unavailablePanelAlpha;
    }

    private void RefreshAllSlots()
    {
        UpdateDashSlot();
        UpdateJumpSlot();
        UpdatePulseSlot();
        UpdatePanelAvailability();
    }

    private void DisableUIRaycasts()
    {
        SetImageRaycastOff(dashIcon);
        SetImageRaycastOff(
            dashCooldownOverlay
        );

        SetImageRaycastOff(jumpIcon);
        SetImageRaycastOff(
            jumpCooldownOverlay
        );

        SetImageRaycastOff(pulseIcon);
        SetImageRaycastOff(
            pulseCooldownOverlay
        );

        SetTextRaycastOff(
            dashCooldownText
        );
        SetTextRaycastOff(dashKeyText);

        SetTextRaycastOff(
            jumpCooldownText
        );
        SetTextRaycastOff(jumpKeyText);

        SetTextRaycastOff(
            pulseCooldownText
        );
        SetTextRaycastOff(pulseKeyText);
    }

    private void SetImageRaycastOff(
        Image image
    )
    {
        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    private void SetTextRaycastOff(
        TMP_Text text
    )
    {
        if (text != null)
        {
            text.raycastTarget = false;
        }
    }

    private void OnValidate()
    {
        unavailablePanelAlpha =
            Mathf.Clamp01(
                unavailablePanelAlpha
            );
    }
}