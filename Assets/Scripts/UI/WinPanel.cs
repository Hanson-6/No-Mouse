using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to the WinPanel GameObject.
/// Automatically wires NextLevelButton and MainMenuButton on Start.
/// </summary>
public class WinPanel : MonoBehaviour
{
    void Start()
    {
        var nextBtn = transform.Find("NextLevelButton")?.GetComponent<Button>();
        if (nextBtn != null) nextBtn.onClick.AddListener(NextLevel);

        var menuBtn = transform.Find("MainMenuButton")?.GetComponent<Button>();
        if (menuBtn != null) menuBtn.onClick.AddListener(GoToMainMenu);
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        GameManager.Instance.LoadNextLevel();
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}
