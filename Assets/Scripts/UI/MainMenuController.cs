using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单控制器。
/// 挂载到 MenuManager GameObject 上。
/// 管理主菜单的四个按钮：新游戏、继续游戏、退出。
///
/// 按钮会在 Awake 中自动按名字查找并连接：
///   - NewGameButton  → 新游戏（删除旧存档，从第一关开始）
///   - ContinueButton → 继续游戏（读取存档，从存档关卡开始；无存档时隐藏）
///   - QuitButton     → 退出游戏
///
/// 兼容旧版按钮名：如果场景中有 StartButton 而没有 NewGameButton，
/// 则 StartButton 会被当作新游戏按钮使用。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Button References (auto-found if left empty)")]
    public Button newGameButton;
    public Button continueButton;
    public Button quitButton;

    // 保留旧字段名以兼容 Inspector 中可能已有的序列化引用
    [HideInInspector]
    public Button startButton;

    void Awake()
    {
        // 自动查找按钮
        if (newGameButton == null)
            newGameButton = FindButtonByName("NewGameButton");

        // 兼容旧版：如果没有 NewGameButton，尝试找 StartButton
        if (newGameButton == null && startButton == null)
            startButton = FindButtonByName("StartButton");

        if (newGameButton == null && startButton != null)
            newGameButton = startButton;

        if (continueButton == null)
            continueButton = FindButtonByName("ContinueButton");

        if (quitButton == null)
            quitButton = FindButtonByName("QuitButton");

        // 连接按钮事件
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(NewGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ContinueGame);

            // 如果没有存档，隐藏/禁用"继续游戏"按钮
            if (!SaveManager.HasSave())
            {
                continueButton.gameObject.SetActive(false);
            }
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    static Button FindButtonByName(string buttonName)
    {
        var go = GameObject.Find(buttonName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    // ── Public methods ─────────────────────────────────────────────────────

    /// <summary>
    /// 新游戏：删除旧存档，重置 GameData，从第一关开始。
    /// </summary>
    public void NewGame()
    {
        SaveManager.DeleteSave();
        GameData.Reset();
        Time.timeScale = 1f;
        SceneManager.LoadScene(1); // Level1 的 build index
    }

    /// <summary>
    /// 继续游戏：读取存档，加载对应关卡。
    /// </summary>
    public void ContinueGame()
    {
        SaveManager.Load();
        Time.timeScale = 1f;

        int levelIndex = GameData.CurrentLevel;
        // 确保关卡索引有效
        if (levelIndex > 0 && levelIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(levelIndex);
        }
        else
        {
            Debug.LogWarning($"[MainMenuController] 存档关卡索引无效: {levelIndex}，从第一关开始。");
            GameData.Reset();
            SceneManager.LoadScene(1);
        }
    }

    /// <summary>
    /// 退出游戏。
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── 保留旧方法名以兼容可能存在的持久化 onClick 引用 ──────────────────

    /// <summary>
    /// 旧版 StartGame 方法，现在等同于 NewGame。
    /// </summary>
    public void StartGame()
    {
        NewGame();
    }
}
