using UnityEngine;

[DisallowMultipleComponent]
public class PlayerExperience : MonoBehaviour
{
    [Header("Experience Settings")]
    [SerializeField] private int currentExperience = 0;
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int experienceToNextLevel = 5;

    [Header("Level Up Settings")]
    [SerializeField] private int experienceIncreasePerLevel = 5;

    public int CurrentExperience => currentExperience;
    public int CurrentLevel => currentLevel;
    public int ExperienceToNextLevel => experienceToNextLevel;

    private void Awake()
    {
        ValidateValues();
    }

    private void Start()
    {
        RefreshAllExperienceUI();

        Debug.Log(
            $"Player Experience Initialized. " +
            $"Level: {currentLevel}, " +
            $"EXP: {currentExperience}/{experienceToNextLevel}"
        );
    }

    private void OnValidate()
    {
        ValidateValues();
    }

    private void ValidateValues()
    {
        currentExperience = Mathf.Max(0, currentExperience);
        currentLevel = Mathf.Max(1, currentLevel);
        experienceToNextLevel = Mathf.Max(1, experienceToNextLevel);
        experienceIncreasePerLevel =
            Mathf.Max(0, experienceIncreasePerLevel);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExperience += amount;

        Debug.Log("Player gained EXP: " + amount);
        Debug.Log(
            "Current Level: " + currentLevel +
            " | EXP: " + currentExperience +
            " / " + experienceToNextLevel
        );

        while (currentExperience >= experienceToNextLevel)
        {
            LevelUp();
        }

        RefreshAllExperienceUI();
    }

    private void LevelUp()
    {
        currentExperience -= experienceToNextLevel;
        currentLevel++;

        experienceToNextLevel += experienceIncreasePerLevel;

        Debug.Log("LEVEL UP!");
        Debug.Log("New Level: " + currentLevel);
        Debug.Log(
            "Next Level Requires EXP: " +
            experienceToNextLevel
        );
        Debug.Log(
            "Remaining EXP: " +
            currentExperience +
            " / " +
            experienceToNextLevel
        );

        RefreshAllExperienceUI();

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.ShowUpgradePanel();
        }
        else
        {
            Debug.LogWarning(
                "UpgradeManager not found. " +
                "Cannot show upgrade panel."
            );
        }
    }

    private void RefreshExperienceUI()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateExperienceUI(
                currentExperience,
                experienceToNextLevel
            );
        }
    }

    private void RefreshLevelUI()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateLevelUI(currentLevel);
        }
    }

    private void RefreshAllExperienceUI()
    {
        RefreshExperienceUI();
        RefreshLevelUI();
    }

    [ContextMenu("Test Add 1 Experience")]
    private void TestAddOneExperience()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "经验测试只能在 Play 模式中执行。"
            );

            return;
        }

        AddExperience(1);
    }
}