using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools → Setup Pause Menu
///
/// 在当前打开的场景中创建暂停菜单 UI：
///   - PauseCanvas (Screen Space - Overlay, 最高 sortOrder)
///     ├── PauseButton  (右上角暂停按钮图标)
///     └── PausePanel   (暂停面板，默认隐藏)
///         ├── PanelBackground (半透明背景图)
///         ├── MenuButton      (返回主菜单)
///         ├── ResumeButton    (继续游戏)
///         ├── RestartLevelButton   (重开当前关卡)
///         └── BackToCheckpointButton(回到最近 checkpoint)
///   - 自动挂载 PauseMenu.cs 脚本到 PauseCanvas
///   - 自动将所有图片设置为 Point 过滤（像素风格清晰）
///   - 保存场景
///
/// 如果场景中已存在 PauseCanvas，会先删除再重建。
/// </summary>
public static class PauseMenuSetup
{
    // 图片资源路径（Assets/Textures/Buttons/ 下的文件）
    private const string TEX_PAUSE_BTN    = "Assets/Textures/Buttons/PauseButton.png";
    private const string TEX_RESUME_BTN   = "Assets/Textures/Buttons/ResumeButton.png";
    private const string TEX_RESTART_BTN = "Assets/Textures/Buttons/RestartLevelButton.png";
    private const string TEX_BACK_TO_CHECKPOINT_BTN = "Assets/Textures/Buttons/BackToCheckpointButton.png";
    private const string TEX_MENU_BTN = "Assets/Textures/Buttons/MenuButton.png";
    private const string TEX_MENU_BTN_FALLBACK = "Assets/Textures/Buttons/QuitNoSaveButton.png";
    private const string TEX_PANEL_BG     = "Assets/Textures/Buttons/PausePanel.png";

    [MenuItem("Tools/Setup Pause Menu")]
    public static void Run()
    {
        // ── 0. 确保图片导入设置正确 ─────────────────────────────────────
        ConfigureSpriteImport(TEX_PAUSE_BTN);
        ConfigureSpriteImport(TEX_RESUME_BTN);
        ConfigureSpriteImport(TEX_RESTART_BTN);
        ConfigureSpriteImport(TEX_BACK_TO_CHECKPOINT_BTN);
        ConfigureSpriteImport(TEX_MENU_BTN);
        ConfigureSpriteImport(TEX_MENU_BTN_FALLBACK);
        ConfigureSpriteImport(TEX_PANEL_BG);

        // ── 1. 加载所有 Sprite ──────────────────────────────────────────
        Sprite pauseSprite    = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_PAUSE_BTN);
        Sprite resumeSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_RESUME_BTN);
        Sprite restartSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_RESTART_BTN);
        Sprite backToCheckpointSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_BACK_TO_CHECKPOINT_BTN);
        Sprite menuSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_MENU_BTN);
        if (menuSprite == null)
            menuSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_MENU_BTN_FALLBACK);
        Sprite panelBgSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_PANEL_BG);

        if (pauseSprite == null || resumeSprite == null ||
            restartSprite == null || backToCheckpointSprite == null || menuSprite == null)
        {
            Debug.LogError("[PauseMenuSetup] 找不到一个或多个按钮图片，请确认 Assets/Textures/Buttons/ 下有：" +
                           "PauseButton.png, ResumeButton.png, RestartLevelButton.png, BackToCheckpointButton.png, MenuButton.png(或 QuitNoSaveButton.png)");
            return;
        }

        // ── 2. 如果已有 PauseCanvas，先删除 ─────────────────────────────
        var existing = GameObject.Find("PauseCanvas");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
            Debug.Log("[PauseMenuSetup] 已删除旧的 PauseCanvas，将重新创建。");
        }

        // ── 3. 创建 PauseCanvas ─────────────────────────────────────────
        GameObject canvasGO = new GameObject("PauseCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 确保在最上层显示

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 4. 创建暂停按钮（右上角） ──────────────────────────────────
        GameObject pauseBtnGO = CreateImageButton(canvasGO.transform, "PauseButton", pauseSprite);
        RectTransform pauseBtnRect = pauseBtnGO.GetComponent<RectTransform>();
        // 锚点设在右上角
        pauseBtnRect.anchorMin        = new Vector2(1f, 1f);
        pauseBtnRect.anchorMax        = new Vector2(1f, 1f);
        pauseBtnRect.pivot            = new Vector2(1f, 1f);
        pauseBtnRect.anchoredPosition = new Vector2(-30f, -30f); // 距离右上角 30px
        pauseBtnRect.sizeDelta        = GetButtonSize(pauseSprite, 80f, 64f, 180f);

        // ── 5. 创建暂停面板 ─────────────────────────────────────────────
        GameObject panelGO = new GameObject("PausePanel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        // 面板居中显示
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.pivot            = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta        = new Vector2(500f, 450f);

        // 面板背景图片
        Image panelImage = panelGO.AddComponent<Image>();
        if (panelBgSprite != null)
        {
            panelImage.sprite = panelBgSprite;
            panelImage.type   = Image.Type.Sliced;
            panelImage.color  = Color.white;
        }
        else
        {
            // 如果没有面板背景图片，使用半透明深色
            panelImage.color = new Color(0.15f, 0.1f, 0.2f, 0.92f);
        }

        // ── 6. 面板上的半透明全屏遮罩（点击遮罩区域不会穿透到游戏） ────
        // 在 PausePanel 之前插入一个全屏遮罩
        GameObject dimGO = new GameObject("DimOverlay");
        dimGO.transform.SetParent(canvasGO.transform, false);
        dimGO.transform.SetSiblingIndex(panelGO.transform.GetSiblingIndex()); // 在 Panel 之前
        RectTransform dimRect = dimGO.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;
        Image dimImage = dimGO.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.5f); // 半透明黑色
        dimImage.raycastTarget = true; // 拦截点击，防止穿透到游戏

        // 将 DimOverlay 设为 PausePanel 的子对象（这样它随 PausePanel 一起隐藏/显示）
        // 实际上更好的做法是让 DimOverlay 也是 PausePanel 的一部分
        // 但为了层级正确（遮罩在面板背后），我们把遮罩作为面板的第一个子对象
        dimGO.transform.SetParent(panelGO.transform, false);
        dimGO.transform.SetAsFirstSibling();
        // 让遮罩撑满整个屏幕（超出面板范围）
        dimRect.anchorMin = new Vector2(-2f, -2f);
        dimRect.anchorMax = new Vector2(3f, 3f);
        dimRect.sizeDelta = Vector2.zero;
        dimRect.anchoredPosition = Vector2.zero;

        // ── 7. 创建面板上的四个按钮 ────────────────────────────────────
        float buttonHeight = 100f;
        float spacing      = 18f;
        float stepY        = buttonHeight + spacing;

        Vector2 resumeSize   = GetButtonSize(resumeSprite, buttonHeight, 280f, 620f);
        Vector2 restartSize = GetButtonSize(restartSprite, buttonHeight, 280f, 620f);
        Vector2 checkpointSize     = GetButtonSize(backToCheckpointSprite, buttonHeight, 280f, 620f);
        Vector2 menuSize = GetButtonSize(menuSprite, buttonHeight, 280f, 620f);

        float maxButtonWidth = Mathf.Max(Mathf.Max(resumeSize.x, restartSize.x), Mathf.Max(checkpointSize.x, menuSize.x));
        float panelWidth = Mathf.Max(560f, maxButtonWidth + 140f);
        float panelHeight = Mathf.Max(520f, buttonHeight * 4f + spacing * 3f + 140f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        float yTop = stepY * 1.5f;
        float yUpperMid = stepY * 0.5f;
        float yLowerMid = -stepY * 0.5f;
        float yBottom = -stepY * 1.5f;

        // 返回主菜单按钮（置于最上方）
        GameObject menuBtnGO = CreateImageButton(panelGO.transform, "MenuButton", menuSprite);
        SetupPanelButton(menuBtnGO, menuSize, new Vector2(0f, yTop));

        // 继续游戏按钮
        GameObject resumeBtnGO = CreateImageButton(panelGO.transform, "ResumeButton", resumeSprite);
        SetupPanelButton(resumeBtnGO, resumeSize, new Vector2(0f, yUpperMid));

        // 重开关卡按钮
        GameObject restartBtnGO = CreateImageButton(panelGO.transform, "RestartLevelButton", restartSprite);
        SetupPanelButton(restartBtnGO, restartSize, new Vector2(0f, yLowerMid));

        // 回到 checkpoint 按钮
        GameObject backToCheckpointBtnGO = CreateImageButton(panelGO.transform, "BackToCheckpointButton", backToCheckpointSprite);
        SetupPanelButton(backToCheckpointBtnGO, checkpointSize, new Vector2(0f, yBottom));

        // ── 8. 面板默认隐藏 ─────────────────────────────────────────────
        panelGO.SetActive(false);

        // ── 9. 挂载 PauseMenu 脚本 ─────────────────────────────────────
        PauseMenu pauseMenu = canvasGO.AddComponent<PauseMenu>();
        pauseMenu.pausePanel     = panelGO;
        pauseMenu.pauseButton    = pauseBtnGO.GetComponent<Button>();
        pauseMenu.resumeButton   = resumeBtnGO.GetComponent<Button>();
        pauseMenu.restartLevelButton = restartBtnGO.GetComponent<Button>();
        pauseMenu.backToCheckpointButton = backToCheckpointBtnGO.GetComponent<Button>();
        pauseMenu.menuButton = menuBtnGO.GetComponent<Button>();

        // ── 10. 确保场景中有 EventSystem ─────────────────────────────────
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[PauseMenuSetup] 已创建 EventSystem。");
        }

        // ── 11. 标记脏并保存 ─────────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.SetDirty(canvasGO);

        Debug.Log("[PauseMenuSetup] 暂停菜单创建完成！PauseCanvas 已添加到当前场景。");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 确保图片以正确的 Sprite 设置导入（Point 过滤、无压缩、透明度）。
    /// </summary>
    static void ConfigureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[PauseMenuSetup] 找不到图片: {assetPath}");
            return;
        }

        bool needsReimport = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            needsReimport = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            needsReimport = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            needsReimport = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            needsReimport = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            needsReimport = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            needsReimport = true;
        }

        if (needsReimport)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[PauseMenuSetup] 已修正图片导入设置: {assetPath}");
        }
    }

    /// <summary>
    /// 创建一个带有 Image + Button 组件的 UI 按钮 GameObject。
    /// </summary>
    static GameObject CreateImageButton(Transform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        go.AddComponent<RectTransform>();

        Image img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget  = true;

        Button btn = go.AddComponent<Button>();
        // 设置按钮悬停/按下效果（轻微变暗）
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor    = Color.white;
        btn.colors = colors;

        return go;
    }

    /// <summary>
    /// 设置面板内按钮的位置和大小。
    /// </summary>
    static void SetupPanelButton(GameObject btnGO, Vector2 size, Vector2 anchoredPos)
    {
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
    }

    static Vector2 GetButtonSize(Sprite sprite, float targetHeight, float minWidth, float maxWidth)
    {
        float aspect = 1f;
        if (sprite != null && sprite.rect.height > 0f)
            aspect = sprite.rect.width / sprite.rect.height;

        float width = Mathf.Clamp(targetHeight * aspect, minWidth, maxWidth);
        return new Vector2(width, targetHeight);
    }
}
