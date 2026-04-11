using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// 暂停菜单控制器。
/// 挂载到场景中的 PauseCanvas GameObject 上。
/// 按 Esc 键或点击右上角暂停按钮可暂停游戏；
/// 暂停面板提供"继续游戏"、"保存并退出"、"返回主菜单"三个选项。
///
/// 使用 Time.timeScale = 0 来暂停游戏：
///   - timeScale = 0 时，所有物理运动、动画、协程（WaitForSeconds）都会停止
///   - timeScale = 1 时，一切恢复正常
///   - UI 交互不受 timeScale 影响，所以暂停时仍然可以点击按钮
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("暂停面板（包含三个按钮的面板，默认隐藏）")]
    public GameObject pausePanel;

    [Tooltip("右上角的暂停按钮")]
    public Button pauseButton;

    [Tooltip("继续游戏按钮")]
    public Button resumeButton;

    [Tooltip("保存并退出按钮")]
    public Button saveQuitButton;

    [Tooltip("返回主菜单按钮（不保存）")]
    public Button quitNoSaveButton;

    /// <summary>当前是否处于暂停状态</summary>
    private bool isPaused;

    void Start()
    {
        // 自动按名字查找 UI 元素（如果没有在 Inspector 中手动指定）
        if (pausePanel == null)
        {
            var t = transform.Find("PausePanel");
            if (t != null) pausePanel = t.gameObject;
        }

        if (pauseButton == null)
        {
            var t = transform.Find("PauseButton");
            if (t != null) pauseButton = t.GetComponent<Button>();
        }

        if (resumeButton == null && pausePanel != null)
        {
            var t = pausePanel.transform.Find("ResumeButton");
            if (t != null) resumeButton = t.GetComponent<Button>();
        }

        if (saveQuitButton == null && pausePanel != null)
        {
            var t = pausePanel.transform.Find("SaveQuitButton");
            if (t != null) saveQuitButton = t.GetComponent<Button>();
        }

        if (quitNoSaveButton == null && pausePanel != null)
        {
            var t = pausePanel.transform.Find("QuitNoSaveButton");
            if (t != null) quitNoSaveButton = t.GetComponent<Button>();
        }

        // 连接按钮事件
        if (pauseButton != null)     pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton != null)    resumeButton.onClick.AddListener(Resume);
        if (saveQuitButton != null)  saveQuitButton.onClick.AddListener(SaveAndQuit);
        if (quitNoSaveButton != null) quitNoSaveButton.onClick.AddListener(QuitNoSave);

        // 确保暂停面板一开始是隐藏的
        if (pausePanel != null)
            pausePanel.SetActive(false);

        isPaused = false;
    }

    void Update()
    {
        // 监听 Esc 键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    /// <summary>
    /// 切换暂停/恢复状态。
    /// </summary>
    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    /// <summary>
    /// 暂停游戏：显示暂停面板，冻结时间。
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);

        if (resumeButton != null)
            EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
    }

    /// <summary>
    /// 继续游戏：隐藏暂停面板，恢复时间。
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    /// <summary>
    /// 保存进度并退出到主菜单。
    /// </summary>
    public void SaveAndQuit()
    {
        SaveManager.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// 不保存，直接返回主菜单。
    /// </summary>
    public void QuitNoSave()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// 当脚本被销毁时（例如场景切换），确保 timeScale 恢复正常。
    /// 防止从暂停状态切换场景后，新场景仍然是冻结的。
    /// </summary>
    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
