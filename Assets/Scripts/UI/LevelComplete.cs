using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelComplete : MonoBehaviour
{
    void Start()
    {
        Time.timeScale = 1f;

        var nextBtn = GameObject.Find("NextLevelButton")?.GetComponent<Button>();
        if (nextBtn != null) nextBtn.onClick.AddListener(NextLevel);

        var menuBtn = GameObject.Find("MainMenuButton")?.GetComponent<Button>();
        if (menuBtn != null) menuBtn.onClick.AddListener(GoToMainMenu);
    }

    void NextLevel()
    {
        int next = GameData.CurrentLevel + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            SceneManager.LoadScene(0); // 没有下一关，回主菜单
    }

    void GoToMainMenu()
    {
        SceneManager.LoadScene(0);
    }
}
