using UnityEngine;

public class ExperienceOrb : MonoBehaviour
{
    [Header("Experience Settings")]
    [SerializeField] private int experienceAmount = 1;

    private bool hasBeenCollected = false;

    public void SetExperienceAmount(int amount)
    {
        experienceAmount = amount;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        PlayerExperience playerExperience = other.GetComponent<PlayerExperience>();

        if (playerExperience == null)
        {
            Debug.LogWarning("ExperienceOrb touched Player, but PlayerExperience component was not found.");
            return;
        }

        hasBeenCollected = true;

        playerExperience.AddExperience(experienceAmount);

        Debug.Log("Player picked up Experience Orb. Exp Amount: " + experienceAmount);

        Destroy(gameObject);
    }
}