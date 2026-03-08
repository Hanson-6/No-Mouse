using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Build index of the first level scene.")]
    public int firstLevelIndex = 1;

    void Start()
    {
        var startBtn = GameObject.Find("StartButton")?.GetComponent<Button>();
        if (startBtn != null) startBtn.onClick.AddListener(StartGame);

        var quitBtn = GameObject.Find("QuitButton")?.GetComponent<Button>();
        if (quitBtn != null) quitBtn.onClick.AddListener(QuitGame);
    }

    public void StartGame()
    {
        GameData.Reset();
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
