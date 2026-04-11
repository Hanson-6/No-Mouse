using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 备用主菜单脚本（简化版）。
/// 项目默认使用 MainMenuController.cs（由 MainMenuSetup 挂载）。
/// 此脚本保留作为兼容备用，逻辑与 MainMenuController 一致。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Build index of the first level scene.")]
    public int firstLevelIndex = 1;

    void Start()
    {
        // 尝试找新版按钮名
        var newGameBtn = GameObject.Find("NewGameButton")?.GetComponent<Button>();
        var continueBtn = GameObject.Find("ContinueButton")?.GetComponent<Button>();
        var quitBtn = GameObject.Find("QuitButton")?.GetComponent<Button>();

        // 兼容旧版按钮名
        if (newGameBtn == null)
        {
            var startBtn = GameObject.Find("StartButton")?.GetComponent<Button>();
            if (startBtn != null) startBtn.onClick.AddListener(NewGame);
        }
        else
        {
            newGameBtn.onClick.AddListener(NewGame);
        }

        if (continueBtn != null)
        {
            continueBtn.onClick.AddListener(ContinueGame);
            if (!SaveManager.HasSave())
                continueBtn.gameObject.SetActive(false);
        }

        if (quitBtn != null) quitBtn.onClick.AddListener(QuitGame);
    }

    public void NewGame()
    {
        SaveManager.DeleteSave();
        GameData.Reset();
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelIndex);
    }

    public void ContinueGame()
    {
        Time.timeScale = 1f;

        if (SaveManager.ContinueFromLatestSnapshot())
            return;

        Debug.LogWarning("[MainMenu] 未命中快照恢复，降级到旧存档流程。");

        SaveManager.Load();
        int levelIndex = GameData.CurrentLevel;
        if (levelIndex > 0 && levelIndex < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(levelIndex);
        else
        {
            GameData.Reset();
            SceneManager.LoadScene(firstLevelIndex);
        }
    }

    public void StartGame()
    {
        NewGame();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
