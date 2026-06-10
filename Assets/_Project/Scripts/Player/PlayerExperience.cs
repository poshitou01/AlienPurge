using UnityEngine;

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

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExperience += amount;

        Debug.Log("Player gained EXP: " + amount);
        Debug.Log("Current Level: " + currentLevel + " | EXP: " + currentExperience + " / " + experienceToNextLevel);

        while (currentExperience >= experienceToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentExperience -= experienceToNextLevel;
        currentLevel++;

        experienceToNextLevel += experienceIncreasePerLevel;

        Debug.Log("LEVEL UP!");
        Debug.Log("New Level: " + currentLevel);
        Debug.Log("Next Level Requires EXP: " + experienceToNextLevel);
        Debug.Log("Remaining EXP: " + currentExperience + " / " + experienceToNextLevel);

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.ShowUpgradePanel();
        }
        else
        {
            Debug.LogWarning("UpgradeManager not found. Cannot show upgrade panel.");
        }
    }
}