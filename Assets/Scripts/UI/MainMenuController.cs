using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GestureRecognition.Service;

/// <summary>
/// 主菜单控制器。
/// 挂载到 MenuManager GameObject 上。
/// 管理主菜单的四个按钮：新游戏、继续游戏、退出。
///
/// 按钮会在 Awake 中自动按名字查找并连接：
///   - NewGameButton  → 新游戏（删除旧存档，从 Tutoring 开始）
///   - ContinueButton → 继续游戏（读取存档，从存档关卡开始；无存档时隐藏）
///   - QuitButton     → 退出游戏
///
/// 兼容旧版按钮名：如果场景中有 StartButton 而没有 NewGameButton，
/// 则 StartButton 会被当作新游戏按钮使用。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string PreferredFirstLevelScenePath = "Assets/Scenes/Tutoring.unity";
    private const string SecondaryFirstLevelScenePath = "Assets/Scenes/Tutorial.unity";

    [Header("Button References (auto-found if left empty)")]
    public Button newGameButton;
    public Button continueButton;
    public Button quitButton;

    // 保留旧字段名以兼容 Inspector 中可能已有的序列化引用
    [HideInInspector]
    public Button startButton;

    /// <summary>用于键盘导航的菜单按钮列表</summary>
    private Button[] _menuButtons;
    private int _selectedIndex = 0;
    private static readonly Color SelectedButtonTint = Color.white;
    private static readonly Color UnselectedButtonTint = new Color(0.92f, 0.92f, 0.92f, 1f);

    [Header("Camera Gate")]
    [Tooltip("Require camera readiness before entering gameplay scenes.")]
    [SerializeField] private bool requireCameraReady = true;

    [Tooltip("Optional status text to display camera-gate diagnostics.")]
    [SerializeField] private Text cameraStatusText;

    private CameraGateUI _cameraGate;

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

    void Start()
    {
        EnsureMenuCanvasScale();
        EnsureCameraGate();

        // 初始化可以被键盘选择的有效按钮数组
        var btnList = new System.Collections.Generic.List<Button>();
        
        // 只有处于激活状态的按钮才加入键盘导航（例如无存档时不会加入继续游戏）
        // 大厅排版：需与游戏画面内按钮上下排列的顺序严格一致
        if (newGameButton != null && newGameButton.gameObject.activeInHierarchy) btnList.Add(newGameButton);
        if (continueButton != null && continueButton.gameObject.activeInHierarchy) btnList.Add(continueButton);
        if (quitButton != null && quitButton.gameObject.activeInHierarchy) btnList.Add(quitButton);

        _menuButtons = btnList.ToArray();

        // 默认选中第一个
        _selectedIndex = 0;
        if (_menuButtons.Length > 0)
        {
            EventSystem.current?.SetSelectedGameObject(_menuButtons[_selectedIndex].gameObject);
            UpdateSelectionVisuals();
        }
    }

    void Update()
    {
        if (_menuButtons == null || _menuButtons.Length == 0)
            return;

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
            _menuButtons[_selectedIndex].onClick.Invoke();
        }

        SyncSelectionFromEventSystem();
        UpdateSelectionVisuals();
    }

    private void SyncSelectionFromEventSystem()
    {
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
                _menuButtons[i].transform.localScale = Vector3.one;

                Image image = _menuButtons[i].targetGraphic as Image;
                if (image != null)
                    image.color = (i == _selectedIndex) ? SelectedButtonTint : UnselectedButtonTint;
            }
        }
    }

    // ── Public methods ─────────────────────────────────────────────────────

    /// <summary>
    /// 新游戏：删除旧存档，重置 GameData，从 Tutoring 开始。
    /// </summary>
    public void NewGame()
    {
        if (requireCameraReady && !CanStartGameplay())
            return;

        SaveManager.DeleteSave();
        GameData.Reset();
        Time.timeScale = 1f;

        int firstLevelIndex = ResolveFirstLevelBuildIndex();
        SceneManager.LoadScene(firstLevelIndex);
    }

    /// <summary>
    /// 继续游戏：读取存档，加载对应关卡。
    /// </summary>
    public void ContinueGame()
    {
        if (requireCameraReady && !CanStartGameplay())
            return;

        ContinueGameInternal();
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

    private void EnsureCameraGate()
    {
        if (!requireCameraReady)
            return;

        _cameraGate = GetComponent<CameraGateUI>();
        if (_cameraGate == null)
        {
            _cameraGate = gameObject.AddComponent<CameraGateUI>();
            Debug.Log("[MainMenuController][Diag] CameraGateUI attached automatically.");
        }

        _cameraGate.Configure(cameraStatusText, newGameButton, continueButton);
    }

    private bool CanStartGameplay()
    {
        if (GestureService.Instance == null)
        {
            Debug.LogWarning("[MainMenuController][Diag] Start blocked: GestureService missing.");
            return false;
        }

        if (!GestureService.Instance.IsRunning)
            GestureService.Instance.StartRecognition();

        if (!GestureService.Instance.IsCameraReadyForGameplay())
        {
            Debug.LogWarning(
                $"[MainMenuController][Diag] Start blocked: state={GestureService.Instance.CameraState} running={GestureService.Instance.IsRunning} occluded={GestureService.Instance.IsCameraOccluded}.");
            return false;
        }

        return true;
    }

    private static void EnsureMenuCanvasScale()
    {
        GameObject menuCanvas = GameObject.Find("MenuCanvas");
        if (menuCanvas == null)
            return;

        Vector3 scale = menuCanvas.transform.localScale;
        if (Mathf.Abs(scale.x) < 0.001f && Mathf.Abs(scale.y) < 0.001f)
        {
            menuCanvas.transform.localScale = Vector3.one;
            Debug.Log("[MainMenuController][Diag] MenuCanvas scale was zero; restored to (1,1,1).");
        }
    }

    private static void ContinueGameInternal()
    {
        Time.timeScale = 1f;

        if (!SaveManager.ContinueFromLatestSnapshot())
            Debug.LogWarning("[MainMenuController] Continue 失败：未找到可恢复的 session/checkpoint 存档。");
    }

    private static int ResolveFirstLevelBuildIndex()
    {
        int preferredIndex = SceneUtility.GetBuildIndexByScenePath(PreferredFirstLevelScenePath);
        if (preferredIndex >= 0)
            return preferredIndex;

        int secondaryIndex = SceneUtility.GetBuildIndexByScenePath(SecondaryFirstLevelScenePath);
        if (secondaryIndex >= 0)
            return secondaryIndex;

        Debug.LogWarning($"[MainMenuController] Neither '{PreferredFirstLevelScenePath}' nor '{SecondaryFirstLevelScenePath}' is in Build Settings. Fallback to index 1.");
        return 1;
    }
}
