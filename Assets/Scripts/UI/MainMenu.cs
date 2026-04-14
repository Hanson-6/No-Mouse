using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GestureRecognition.Service;

/// <summary>
/// 备用主菜单脚本（简化版）。
/// 项目默认使用 MainMenuController.cs（由 MainMenuSetup 挂载）。
/// 此脚本保留作为兼容备用，逻辑与 MainMenuController 一致。
/// </summary>
public class MainMenu : MonoBehaviour
{
    private const string PreferredFirstLevelScenePath = "Assets/Scenes/Tutoring.unity";
    private const string SecondaryFirstLevelScenePath = "Assets/Scenes/Tutorial.unity";

    [Tooltip("Build index of the first level scene.")]
    public int firstLevelIndex = 1;

    [Tooltip("Require camera readiness before entering gameplay scenes.")]
    [SerializeField] private bool requireCameraReady = true;

    void Start()
    {
        EnsureMenuCanvasScale();

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
        if (requireCameraReady && !CanStartGameplay())
            return;

        SaveManager.DeleteSave();
        GameData.Reset();
        Time.timeScale = 1f;

        int resolvedIndex = ResolveFirstLevelBuildIndex();
        SceneManager.LoadScene(resolvedIndex);
    }

    public void ContinueGame()
    {
        if (requireCameraReady && !CanStartGameplay())
            return;

        Time.timeScale = 1f;

        if (!SaveManager.ContinueFromLatestSnapshot())
            Debug.LogWarning("[MainMenu] Continue 失败：未找到可恢复的 session/checkpoint 存档。");
    }

    public void StartGame()
    {
        NewGame();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private bool CanStartGameplay()
    {
        if (GestureService.Instance == null)
        {
            Debug.LogWarning("[MainMenu][Diag] Start blocked: GestureService missing.");
            return false;
        }

        if (!GestureService.Instance.IsRunning)
            GestureService.Instance.StartRecognition();

        if (!GestureService.Instance.IsCameraReadyForGameplay())
        {
            Debug.LogWarning(
                $"[MainMenu][Diag] Start blocked: state={GestureService.Instance.CameraState} running={GestureService.Instance.IsRunning} occluded={GestureService.Instance.IsCameraOccluded}.");
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
            Debug.Log("[MainMenu][Diag] MenuCanvas scale was zero; restored to (1,1,1).");
        }
    }

    private static int ResolveFirstLevelBuildIndex()
    {
        int preferredIndex = SceneUtility.GetBuildIndexByScenePath(PreferredFirstLevelScenePath);
        if (preferredIndex >= 0)
            return preferredIndex;

        int secondaryIndex = SceneUtility.GetBuildIndexByScenePath(SecondaryFirstLevelScenePath);
        if (secondaryIndex >= 0)
            return secondaryIndex;

        Debug.LogWarning($"[MainMenu] Neither '{PreferredFirstLevelScenePath}' nor '{SecondaryFirstLevelScenePath}' is in Build Settings. Fallback to index 1.");
        return 1;
    }
}
