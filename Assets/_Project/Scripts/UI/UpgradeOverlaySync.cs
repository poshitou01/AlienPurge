using UnityEngine;

[DisallowMultipleComponent]
public class UpgradeOverlaySync : MonoBehaviour
{
    [Header("Upgrade UI")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private GameObject upgradeDimOverlay;

    private void Awake()
    {
        RefreshOverlayState();
    }

    private void OnEnable()
    {
        RefreshOverlayState();
    }

    private void LateUpdate()
    {
        RefreshOverlayState();
    }

    private void RefreshOverlayState()
    {
        if (upgradeDimOverlay == null)
        {
            return;
        }

        bool shouldShow =
            upgradePanel != null
            && upgradePanel.activeSelf;

        if (upgradeDimOverlay.activeSelf != shouldShow)
        {
            upgradeDimOverlay.SetActive(shouldShow);
        }
    }
}