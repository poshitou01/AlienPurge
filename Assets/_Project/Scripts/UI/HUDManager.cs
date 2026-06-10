using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("HP UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("EXP UI")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI expText;

    [Header("Level UI")]
    [SerializeField] private TextMeshProUGUI levelText;

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
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = currentHealth;
        }

        if (hpText != null)
        {
            hpText.text = $"HP: {currentHealth} / {maxHealth}";
        }
    }

    public void UpdateExperienceUI(int currentExperience, int experienceToNextLevel)
    {
        if (expSlider != null)
        {
            expSlider.maxValue = experienceToNextLevel;
            expSlider.value = currentExperience;
        }

        if (expText != null)
        {
            expText.text = $"EXP: {currentExperience} / {experienceToNextLevel}";
        }
    }

    public void UpdateLevelUI(int currentLevel)
    {
        if (levelText != null)
        {
            levelText.text = $"Level: {currentLevel}";
        }
    }
}