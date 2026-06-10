using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("HP UI")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Slider hpSlider;

    [Header("EXP UI")]
    [SerializeField] private TMP_Text expText;
    [SerializeField] private Slider expSlider;

    [Header("Level UI")]
    [SerializeField] private TMP_Text levelText;

    [Header("Time UI")]
    [SerializeField] private TMP_Text timeText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        if (hpText != null)
        {
            hpText.text = "HP: " + currentHealth + " / " + maxHealth;
        }

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = currentHealth;
        }
    }

    public void UpdateExperienceUI(int currentExperience, int experienceToNextLevel)
    {
        if (expText != null)
        {
            expText.text = "EXP: " + currentExperience + " / " + experienceToNextLevel;
        }

        if (expSlider != null)
        {
            expSlider.maxValue = experienceToNextLevel;
            expSlider.value = currentExperience;
        }
    }

    public void UpdateLevelUI(int currentLevel)
    {
        if (levelText != null)
        {
            levelText.text = "Level: " + currentLevel;
        }
    }

    public void UpdateTimeUI(float survivalTime, float targetSurvivalTime, bool showRemainingTime)
    {
        if (timeText == null)
        {
            return;
        }

        if (showRemainingTime)
        {
            float remainingTime = Mathf.Max(0f, targetSurvivalTime - survivalTime);
            timeText.text = "Time Left: " + FormatTime(remainingTime);
        }
        else
        {
            timeText.text = "Time: " + FormatTime(survivalTime);
        }
    }

    private string FormatTime(float time)
    {
        int totalSeconds = Mathf.FloorToInt(time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }
}