using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 挂载到 SettlementCanvas 上。
/// 控制结算界面的弹出动画、计时显示、烟花特效和按钮逻辑。
/// 所有字段由 SettlementPanelSetup Editor 脚本自动赋值。
/// </summary>
public class SettlementPanel : MonoBehaviour
{
    private const float MainMenuButtonWidth = 380f;
    private const float MainMenuButtonHeight = 100f;

    private static readonly Vector3 SelectedButtonScale = new Vector3(1.04f, 1.04f, 1f);
    private static readonly Vector3 NormalButtonScale = Vector3.one;
    private static readonly Color SelectedButtonTint = new Color(1f, 1f, 1f, 1f);
    private static readonly Color UnselectedButtonTint = new Color(0.84f, 0.84f, 0.84f, 1f);

    [Header("Panel Animation")]
    public RectTransform panelRoot;
    public CanvasGroup   backgroundGroup;

    [Header("Text")]
    public Text timerText;

    [Header("Buttons")]
    public Button restartButton;
    public Button quitButton;

    [Header("Confetti")]
    public ParticleSystem confettiLeft;
    public ParticleSystem confettiRight;

    private Button[] menuButtons;

    // ──────────────────────────────────────────────────────────
    void Start()
    {
        Time.timeScale = 1f;

        if (timerText != null)
            timerText.text = FormatTime(GameData.FinalTime);

        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (quitButton    != null) quitButton.onClick.AddListener(OnQuit);

        ConfigureKeyboardNavigation();
        ApplyMainMenuButtonSizing();
        CacheMenuButtons();
        SelectDefaultButton();

        StartCoroutine(IntroSequence());
    }

    void Update()
    {
        UpdateSelectionVisuals();
    }

    // ──────────────────────────────────────────────────────────
    IEnumerator IntroSequence()
    {
        if (panelRoot      != null) panelRoot.localScale  = Vector3.zero;
        if (backgroundGroup != null) backgroundGroup.alpha = 0f;

        // 1. 背景遮罩淡入 (0.3s)
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
        {
            if (backgroundGroup != null)
                backgroundGroup.alpha = Mathf.Lerp(0f, 0.78f, t);
            yield return null;
        }
        if (backgroundGroup != null) backgroundGroup.alpha = 0.78f;

        // 2. 面板弹出 (0.45s, EaseOutBack)
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
        {
            if (panelRoot != null)
                panelRoot.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t));
            yield return null;
        }
        if (panelRoot != null) panelRoot.localScale = Vector3.one;

        // 3. 烟花
        PositionAndPlayConfetti();
    }

    // 根据摄像机视口动态定位烟花，再播放
    void PositionAndPlayConfetti()
    {
        if (Camera.main == null) return;

        float depth = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 posL = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.3f, Screen.height, depth));
        Vector3 posR = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.7f, Screen.height, depth));
        posL.z = 0f;
        posR.z = 0f;

        if (confettiLeft  != null) { confettiLeft.transform.position  = posL; confettiLeft.Play(); }
        if (confettiRight != null) { confettiRight.transform.position = posR; confettiRight.Play(); }
    }

    // ──────────────────────────────────────────────────────────
    // 按钮回调
    // ──────────────────────────────────────────────────────────

    public void OnRestart()
    {
        GameData.CurrentTimer = 0f;
        SceneManager.LoadScene(GameData.CurrentLevel);
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ConfigureKeyboardNavigation()
    {
        if (restartButton == null || quitButton == null)
            return;

        Navigation restartNav = restartButton.navigation;
        restartNav.mode = Navigation.Mode.Explicit;
        restartNav.selectOnUp = quitButton;
        restartNav.selectOnDown = quitButton;
        restartNav.selectOnLeft = quitButton;
        restartNav.selectOnRight = quitButton;
        restartButton.navigation = restartNav;

        Navigation quitNav = quitButton.navigation;
        quitNav.mode = Navigation.Mode.Explicit;
        quitNav.selectOnUp = restartButton;
        quitNav.selectOnDown = restartButton;
        quitNav.selectOnLeft = restartButton;
        quitNav.selectOnRight = restartButton;
        quitButton.navigation = quitNav;
    }

    void ApplyMainMenuButtonSizing()
    {
        SetButtonSize(restartButton, MainMenuButtonWidth, MainMenuButtonHeight);
        SetButtonSize(quitButton, MainMenuButtonWidth, MainMenuButtonHeight);
    }

    static void SetButtonSize(Button button, float width, float height)
    {
        if (button == null)
            return;

        RectTransform rt = button.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(width, height);

        Image img = button.targetGraphic as Image;
        if (img != null)
            img.preserveAspect = false;
    }

    void CacheMenuButtons()
    {
        if (restartButton != null && quitButton != null)
            menuButtons = new[] { restartButton, quitButton };
        else if (restartButton != null)
            menuButtons = new[] { restartButton };
        else if (quitButton != null)
            menuButtons = new[] { quitButton };
        else
            menuButtons = null;
    }

    void SelectDefaultButton()
    {
        if (EventSystem.current != null && restartButton != null)
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
    }

    void UpdateSelectionVisuals()
    {
        if (menuButtons == null || menuButtons.Length == 0)
            return;

        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            Button btn = menuButtons[i];
            if (btn == null)
                continue;

            bool isSelected = selected == btn.gameObject;
            btn.transform.localScale = isSelected ? SelectedButtonScale : NormalButtonScale;

            Image img = btn.targetGraphic as Image;
            if (img != null)
                img.color = isSelected ? SelectedButtonTint : UnselectedButtonTint;
        }
    }

    // ──────────────────────────────────────────────────────────
    // 工具方法
    // ──────────────────────────────────────────────────────────

    // 弹性缓动：先超过 1 再回弹到 1
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static string FormatTime(float totalSeconds)
    {
        int m = Mathf.FloorToInt(totalSeconds / 60f);
        int s = Mathf.FloorToInt(totalSeconds % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }
}
