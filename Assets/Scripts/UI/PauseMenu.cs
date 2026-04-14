using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// 暂停菜单控制器。
/// 挂载到场景中的 PauseCanvas GameObject 上。
/// 按 Esc 键或点击右上角暂停按钮可暂停游戏；
/// 暂停面板提供"继续游戏"、"重开当前关卡"、"回到最近 checkpoint"、"返回主菜单"四个选项。
///
/// 使用 Time.timeScale = 0 来暂停游戏：
///   - timeScale = 0 时，所有物理运动、动画、协程（WaitForSeconds）都会停止
///   - timeScale = 1 时，一切恢复正常
///   - UI 交互不受 timeScale 影响，所以暂停时仍然可以点击按钮
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("暂停面板（包含四个按钮的面板，默认隐藏）")]
    public GameObject pausePanel;

    [Tooltip("右上角的暂停按钮")]
    public Button pauseButton;

    [Tooltip("继续游戏按钮")]
    public Button resumeButton;

    [Tooltip("重开当前关卡按钮")]
    public Button restartLevelButton;

    [Tooltip("回到最近 checkpoint 按钮")]
    public Button backToCheckpointButton;

    [Tooltip("返回主菜单按钮")]
    public Button menuButton;

    /// <summary>当前是否处于暂停状态</summary>
    private bool isPaused;

    /// <summary>用于键盘导航的菜单按钮列表</summary>
    private Button[] _menuButtons;
    private int _selectedIndex = 0;
    private static readonly Color SelectedButtonTint = new Color(1f, 1f, 1f, 1f);
    private static readonly Color UnselectedButtonTint = new Color(0.84f, 0.84f, 0.84f, 1f);
    private static readonly Vector3 SelectedButtonScale = new Vector3(1.04f, 1.04f, 1f);
    private static readonly Vector3 UnselectedButtonScale = Vector3.one;

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

        if (restartLevelButton == null && pausePanel != null)
        {
            var t = pausePanel.transform.Find("RestartLevelButton");
            if (t != null) restartLevelButton = t.GetComponent<Button>();
        }

        if (backToCheckpointButton == null && pausePanel != null)
        {
            var t = pausePanel.transform.Find("BackToCheckpointButton");
            if (t != null) backToCheckpointButton = t.GetComponent<Button>();
        }

        if (menuButton == null && pausePanel != null)
        {
            var t = pausePanel.transform.Find("MenuButton");
            if (t != null) menuButton = t.GetComponent<Button>();
        }

        if (restartLevelButton == null && pausePanel != null)
        {
            var legacy = pausePanel.transform.Find("SaveQuitButton");
            if (legacy != null) restartLevelButton = legacy.GetComponent<Button>();
        }

        if (menuButton == null && pausePanel != null)
        {
            var legacy = pausePanel.transform.Find("QuitNoSaveButton");
            if (legacy != null) menuButton = legacy.GetComponent<Button>();
        }

        if (menuButton != null && menuButton == backToCheckpointButton)
            backToCheckpointButton = null;

        // 连接按钮事件
        if (pauseButton != null)     pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton != null)    resumeButton.onClick.AddListener(Resume);
        if (restartLevelButton != null)  restartLevelButton.onClick.AddListener(RestartLevel);
        if (backToCheckpointButton != null) backToCheckpointButton.onClick.AddListener(BackToCheckpoint);
        if (menuButton != null) menuButton.onClick.AddListener(ReturnToMenu);

        // 确保暂停面板一开始是隐藏的
        if (pausePanel != null)
            pausePanel.SetActive(false);

        RefreshBackToCheckpointState();
        RebuildMenuButtons();

        isPaused = false;
    }

    private void RefreshBackToCheckpointState()
    {
        if (backToCheckpointButton != null)
            backToCheckpointButton.interactable = SaveManager.HasCheckpointSave();
    }

    private void RebuildMenuButtons()
    {
        var btnList = new List<Button>();
        AddSelectableButton(btnList, menuButton);
        AddSelectableButton(btnList, resumeButton);
        AddSelectableButton(btnList, restartLevelButton);
        AddSelectableButton(btnList, backToCheckpointButton);

        _menuButtons = btnList.ToArray();

        if (_menuButtons.Length == 0)
            _selectedIndex = 0;
        else
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _menuButtons.Length - 1);
    }

    private int GetDefaultSelectionIndex()
    {
        if (_menuButtons == null || _menuButtons.Length == 0)
            return 0;

        if (menuButton == null)
            return 0;

        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] == menuButton)
                return i;
        }

        return 0;
    }

    private static void AddSelectableButton(List<Button> list, Button button)
    {
        if (button == null)
            return;

        if (!button.gameObject.activeInHierarchy)
            return;

        if (!button.interactable)
            return;

        list.Add(button);
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

            SyncSelectionFromEventSystem();
            UpdateSelectionVisuals();
        }
    }

    private void SyncSelectionFromEventSystem()
    {
        if (_menuButtons == null || _menuButtons.Length == 0)
            return;

        EventSystem es = EventSystem.current;
        if (es == null)
            return;

        GameObject selected = es.currentSelectedGameObject;
        if (selected == null)
            return;

        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] != null && _menuButtons[i].gameObject == selected)
            {
                _selectedIndex = i;
                return;
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] != null)
            {
                bool isSelected = i == _selectedIndex;
                _menuButtons[i].transform.localScale = isSelected ? SelectedButtonScale : UnselectedButtonScale;

                Image image = _menuButtons[i].targetGraphic as Image;
                if (image != null)
                    image.color = isSelected ? SelectedButtonTint : UnselectedButtonTint;
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

        RefreshBackToCheckpointState();
        RebuildMenuButtons();
        _selectedIndex = GetDefaultSelectionIndex();
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
                _menuButtons[i].transform.localScale = UnselectedButtonScale;
        }
    }

    /// <summary>
    /// 重开当前关卡。
    /// </summary>
    public void RestartLevel()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameData.ClearCheckpoint(activeScene.buildIndex, activeScene.path);
        SaveManager.ClearCheckpointSnapshot();
        SaveManager.ClearSessionSnapshot();

        if (GameManager.Instance != null)
            GameManager.Instance.ResetRespawnToInitial();

        Time.timeScale = 1f;
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    /// <summary>
    /// 返回最近 checkpoint。
    /// </summary>
    public void BackToCheckpoint()
    {
        Time.timeScale = 1f;
        if (!SaveManager.ContinueFromCheckpointSnapshot())
            Debug.LogWarning("[PauseMenu] BackToCheckpoint 失败：未找到 checkpoint_latest.json。");
    }

    /// <summary>
    /// 保存当前 session 并返回主菜单。
    /// </summary>
    public void ReturnToMenu()
    {
        SaveManager.SaveCurrentSessionLive();
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// 当脚本被销毁时（例如场景切换），确保 timeScale 恢复正常。
    /// </summary>
    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
