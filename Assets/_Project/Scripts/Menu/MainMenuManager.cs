using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string gameSceneName = "Day01_TestGround";

    public void StartGame()
    {
        Time.timeScale = 1f;

        Debug.Log("Start Game");

        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game button clicked. In a built game, the application will quit.");

        Application.Quit();
    }
}