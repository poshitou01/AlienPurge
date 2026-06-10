using UnityEngine;
using UnityEngine.UI;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public static bool IsChoosingUpgrade { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private Button moveSpeedButton;
    [SerializeField] private Button fireCooldownButton;
    [SerializeField] private Button bulletDamageButton;

    [Header("Upgrade Values")]
    [SerializeField] private float moveSpeedIncrease = 0.8f;
    [SerializeField] private float fireCooldownDecrease = 0.03f;
    [SerializeField] private int bulletDamageIncrease = 1;

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(false);
        }

        IsChoosingUpgrade = false;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        FindPlayerComponents();

        if (moveSpeedButton != null)
        {
            moveSpeedButton.onClick.AddListener(ChooseMoveSpeedUpgrade);
        }

        if (fireCooldownButton != null)
        {
            fireCooldownButton.onClick.AddListener(ChooseFireCooldownUpgrade);
        }

        if (bulletDamageButton != null)
        {
            bulletDamageButton.onClick.AddListener(ChooseBulletDamageUpgrade);
        }
    }

    private void FindPlayerComponents()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("UpgradeManager 找不到 Tag 为 Player 的对象。请确认 Player 的 Tag 是 Player。");
            return;
        }

        playerMovement = player.GetComponent<PlayerMovement>();
        playerShooting = player.GetComponent<PlayerShooting>();

        if (playerMovement == null)
        {
            Debug.LogWarning("UpgradeManager 找不到 PlayerMovement。");
        }

        if (playerShooting == null)
        {
            Debug.LogWarning("UpgradeManager 找不到 PlayerShooting。");
        }
    }

    public void ShowUpgradePanel()
    {
        if (upgradePanel == null)
        {
            Debug.LogWarning("UpgradePanel 没有赋值。");
            return;
        }

        if (playerMovement == null || playerShooting == null)
        {
            FindPlayerComponents();
        }

        IsChoosingUpgrade = true;
        upgradePanel.SetActive(true);

        Time.timeScale = 0f;

        Debug.Log("玩家升级，游戏暂停，显示升级选择面板。");
    }

    private void ChooseMoveSpeedUpgrade()
    {
        if (playerMovement != null)
        {
            playerMovement.AddMoveSpeed(moveSpeedIncrease);
            Debug.Log("选择升级：增加玩家移动速度。");
        }

        CloseUpgradePanel();
    }

    private void ChooseFireCooldownUpgrade()
    {
        if (playerShooting != null)
        {
            playerShooting.ReduceFireCooldown(fireCooldownDecrease);
            Debug.Log("选择升级：减少射击冷却时间。");
        }

        CloseUpgradePanel();
    }

    private void ChooseBulletDamageUpgrade()
    {
        if (playerShooting != null)
        {
            playerShooting.AddBulletDamage(bulletDamageIncrease);
            Debug.Log("选择升级：增加子弹伤害。");
        }

        CloseUpgradePanel();
    }

    private void CloseUpgradePanel()
    {
        if (upgradePanel != null)
        {
            upgradePanel.SetActive(false);
        }

        IsChoosingUpgrade = false;
        if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing)
        {
            Time.timeScale = 1f;
        }

        Debug.Log("升级选择完成，游戏恢复。");
    }
}
