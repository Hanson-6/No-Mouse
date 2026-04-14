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

    /// <summary>用于键盘导航的菜单按钮列表</summary>
    private Button[] _menuButtons;
    private int _selectedIndex = 0;

    private const string DiagPrefix = "[PauseMenu][Diag]";

    void Start()
    {
        EnsureCanvasScale();

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

        // 初始化可以被键盘选择的按钮数组（按照下移顺序）
        var btnList = new System.Collections.Generic.List<Button>();
        if (resumeButton != null) btnList.Add(resumeButton);
        if (saveQuitButton != null) btnList.Add(saveQuitButton);
        if (quitNoSaveButton != null) btnList.Add(quitNoSaveButton);
        _menuButtons = btnList.ToArray();

        isPaused = false;
    }

    private void EnsureCanvasScale()
    {
        Transform root = transform;
        if (root == null)
            return;

        Vector3 scale = root.localScale;
        if (Mathf.Abs(scale.x) < 0.001f && Mathf.Abs(scale.y) < 0.001f)
        {
            root.localScale = Vector3.one;
            Debug.Log($"{DiagPrefix} Root canvas scale was zero; restored to (1,1,1).");
        }
    }

    void Update()
    {
        // 监听 Esc 键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        // 如果在暂停状态下且按钮已正确初始化，则允许使用键盘导航
        if (isPaused && _menuButtons.Length > 0)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = _menuButtons.Length - 1;
                EventSystem.current?.SetSelectedGameObject(_menuButtons[_selectedIndex].gameObject);
                UpdateSelectionVisuals();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuButtons.Length;
                EventSystem.current?.SetSelectedGameObject(_menuButtons[_selectedIndex].gameObject);
                UpdateSelectionVisuals();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // 回车触发当前选中按钮的点击事件
                _menuButtons[_selectedIndex].onClick.Invoke();
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] != null)
            {
                // 如果是当前选中的按钮，放大一点作为提示，否则恢复原状
                if (i == _selectedIndex)
                {
                    _menuButtons[i].transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                    // 改变颜色也可以在这里加，但放大通常已经足够明显
                }
                else
                {
                    _menuButtons[i].transform.localScale = Vector3.one;
                }
            }
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

        _selectedIndex = 0;
        if (_menuButtons.Length > 0)
        {
            EventSystem.current?.SetSelectedGameObject(_menuButtons[_selectedIndex].gameObject);
            UpdateSelectionVisuals();
        }
    }

    /// <summary>
    /// 继续游戏：隐藏暂停面板，恢复时间。
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);

        // 恢复所有按钮大小
        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] != null)
                _menuButtons[i].transform.localScale = Vector3.one;
        }
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
