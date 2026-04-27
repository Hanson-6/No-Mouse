using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tools → Setup Settlement Panel
///
/// 自动在 LevelComplete 场景中构建结算 UI：
///   SettlementCanvas  (Canvas + CanvasScaler + GraphicRaycaster + SettlementPanel)
///   ├── DimOverlay    (全屏黑色遮罩，CanvasGroup 控制淡入透明度)
///   └── PanelRoot     (SettlementPanel.png 面板背景，弹出动画起始 scale=0)
///       ├── BannerImage     (CelebrationBanner.png 庆典横幅，可选)
///       ├── TitleText        "CONGRATULATIONS"
///       ├── SubtitleText     "GAME OVER"
///       ├── Separator1
///       ├── TimerLabel       "TIME"
///       ├── TimerValue       "00:00"   ← SettlementPanel.timerText
///       ├── Separator2
///       ├── RestartButton    RestartLevelButton.png
///       └── QuitButton       QuitButton.png
///   ConfettiLeft  (ParticleSystem，SparkParticle.png 粒子贴图，运行时自动定位)
///   ConfettiRight (ParticleSystem，SparkParticle.png 粒子贴图，运行时自动定位)
///
/// 若已存在 SettlementCanvas，先删除再重建。
/// </summary>
public static class SettlementPanelSetup
{
    private const string SCENE_PATH      = "Assets/Scenes/LevelComplete.unity";
    private const string TEX_PANEL       = "Assets/Textures/Buttons/SettlementPanel.png";
    private const string TEX_BANNER      = "Assets/Textures/Buttons/CelebrationBanner.png";
    private const string TEX_RESTART     = "Assets/Textures/Buttons/RestartLevelButton.png";
    private const string TEX_QUIT        = "Assets/Textures/Buttons/QuitButton.png";
    private const string TEX_SPARK       = "Assets/Textures/SparkParticle.png";

    // 面板参考尺寸（最终会按贴图比例微调）
    private const float PANEL_W = 680f;
    private const float PANEL_H = 800f;

    [MenuItem("Tools/Setup Settlement Panel")]
    public static void Run()
    {
        // ── 0. 打开 LevelComplete 场景 ───────────────────────────────
        if (EditorSceneManager.GetActiveScene().path != SCENE_PATH)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(SCENE_PATH);
        }

        // ── 1. 修正图片导入设置 ──────────────────────────────────────
        ConfigureSpriteImport(TEX_PANEL);
        ConfigureSpriteImport(TEX_BANNER);
        ConfigureSpriteImport(TEX_RESTART);
        ConfigureSpriteImport(TEX_QUIT);
        ConfigureSpriteImport(TEX_SPARK);

        // ── 2. 加载 Sprite ──────────────────────────────────────────
        Sprite panelSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_PANEL);
        Sprite bannerSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_BANNER);
        Sprite restartSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_RESTART);
        Sprite quitSprite    = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_QUIT);
        Sprite sparkSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_SPARK);

        if (panelSprite == null || restartSprite == null || quitSprite == null)
        {
            Debug.LogError("[SettlementPanelSetup] 找不到必要图片，请确认：" +
                           "SettlementPanel.png, RestartLevelButton.png, QuitButton.png");
            return;
        }

        // ── 3. 清理旧对象 ───────────────────────────────────────────
        DestroyIfExists("SettlementCanvas");
        DestroyIfExists("ConfettiLeft");
        DestroyIfExists("ConfettiRight");

        // ── 4. 创建 Canvas ──────────────────────────────────────────
        var canvasGO = new GameObject("SettlementCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.transform.localScale = Vector3.one;

        var scaler                   = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode           = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution   = new Vector2(1920f, 1080f);
        scaler.screenMatchMode       = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight    = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 5. DimOverlay（全屏黑色遮罩） ───────────────────────────
        var dimGO  = new GameObject("DimOverlay");
        dimGO.transform.SetParent(canvasGO.transform, false);
        var dimRT  = dimGO.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color         = Color.black;
        dimImg.raycastTarget = true;
        var dimCG  = dimGO.AddComponent<CanvasGroup>();
        dimCG.alpha          = 0f; // 运行时淡入

        // ── 6. PanelRoot（面板背景，弹出动画对象） ──────────────────
        var panelGO = new GameObject("PanelRoot");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        Vector2 panelSize        = GetButtonSize(panelSprite, PANEL_H, PANEL_W - 60f, PANEL_W + 80f);
        panelRT.sizeDelta        = panelSize;
        panelRT.localScale       = Vector3.one; // 编辑器可见，运行时由 SettlementPanel 动画隐藏

        var panelImg = panelGO.AddComponent<Image>();
        panelImg.sprite         = panelSprite;
        panelImg.type           = Image.Type.Simple;
        panelImg.preserveAspect = true;
        panelImg.color          = Color.white;

        // ── 7. 面板内容（从顶部向下排列） ───────────────────────────
        // 所有子元素使用 anchorMin=(0.5,1) anchorMax=(0.5,1) pivot=(0.5,1)
        // anchoredPosition.y 为距面板顶边的偏移（负数 = 向下）

        float contentW = Mathf.Max(420f, panelSize.x - 160f);
        float y = -18f;

        // 庆典横幅（可选装饰，有图则显示）
        if (bannerSprite != null)
        {
            var bannerGO = new GameObject("BannerImage");
            bannerGO.transform.SetParent(panelGO.transform, false);
            var bannerRT = bannerGO.AddComponent<RectTransform>();
            bannerRT.anchorMin        = new Vector2(0.5f, 1f);
            bannerRT.anchorMax        = new Vector2(0.5f, 1f);
            bannerRT.pivot            = new Vector2(0.5f, 1f);
            bannerRT.anchoredPosition = new Vector2(0f, y);
            Vector2 bannerSize = GetButtonSize(bannerSprite, 90f, 420f, contentW + 40f);
            bannerRT.sizeDelta        = bannerSize;
            var bannerImg = bannerGO.AddComponent<Image>();
            bannerImg.sprite        = bannerSprite;
            bannerImg.preserveAspect = true;
            bannerImg.raycastTarget = false;
            y -= bannerSize.y + 28f;
        }

        // 标题 "CONGRATULATIONS"
        AddText(panelGO.transform, "TitleText", "CONGRATULATIONS",
                ref y, contentW, 64f, 48, FontStyle.Bold, Color.white);

        y -= 5f;

        // 副标题 "GAME OVER"
        AddText(panelGO.transform, "SubtitleText", "GAME OVER",
                ref y, contentW, 40f, 26, FontStyle.Bold, new Color(1f, 0.85f, 0.35f));

        y -= 16f;

        // 分隔线 1
        AddSeparator(panelGO.transform, "Separator1", ref y, contentW + 24f);

        y -= 12f;

        // 计时标签 "TIME"
        AddText(panelGO.transform, "TimerLabel", "TIME",
                ref y, 220f, 34f, 22, FontStyle.Bold, new Color(0.9f, 0.7f, 0.3f));

        y -= 2f;

        // 计时数值 "00:00" ← SettlementPanel.timerText 将引用这个
        var timerValueGO = new GameObject("TimerValue");
        timerValueGO.transform.SetParent(panelGO.transform, false);
        var timerRT      = timerValueGO.AddComponent<RectTransform>();
        timerRT.anchorMin        = new Vector2(0.5f, 1f);
        timerRT.anchorMax        = new Vector2(0.5f, 1f);
        timerRT.pivot            = new Vector2(0.5f, 1f);
        timerRT.anchoredPosition = new Vector2(0f, y);
        timerRT.sizeDelta        = new Vector2(360f, 80f);
        var timerTxt = timerValueGO.AddComponent<Text>();
        timerTxt.text                = "00:00";
        timerTxt.fontSize            = 58;
        timerTxt.fontStyle           = FontStyle.Bold;
        timerTxt.alignment           = TextAnchor.MiddleCenter;
        timerTxt.color               = Color.white;
        timerTxt.resizeTextForBestFit = false;
        // 可在 Inspector 中将字体替换为像素字体（如 Press Start 2P）
        y -= 80f + 18f;

        // 分隔线 2
        AddSeparator(panelGO.transform, "Separator2", ref y, contentW + 24f);

        y -= 24f;

        // 重玩按钮（RestartLevelButton.png）
        Vector2 restartSize = new Vector2(380f, 100f);
        var restartGO = CreateImageButton(panelGO.transform, "RestartButton", restartSprite);
        SetupTopAnchoredRT(restartGO, restartSize, new Vector2(0f, y));
        y -= restartSize.y + 18f;

        // 退出按钮（QuitButton.png）
        Vector2 quitSize = new Vector2(380f, 100f);
        var quitGO = CreateImageButton(panelGO.transform, "QuitButton", quitSprite);
        SetupTopAnchoredRT(quitGO, quitSize, new Vector2(0f, y));

        // ── 8. 烟花粒子系统（场景根节点，运行时定位） ───────────────
        var confettiLeft  = CreateConfettiSystem("ConfettiLeft",  sparkSprite);
        var confettiRight = CreateConfettiSystem("ConfettiRight", sparkSprite);

        // ── 9. 确保 EventSystem 存在 ────────────────────────────────
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // ── 10. 挂载 SettlementPanel 并赋值所有引用 ─────────────────
        var sp            = canvasGO.AddComponent<SettlementPanel>();
        sp.panelRoot      = panelRT;
        sp.backgroundGroup = dimCG;
        sp.timerText      = timerTxt;
        sp.restartButton  = restartGO.GetComponent<Button>();
        sp.quitButton     = quitGO.GetComponent<Button>();
        sp.confettiLeft   = confettiLeft;
        sp.confettiRight  = confettiRight;

        // 关闭遗留按钮（保留旧 Canvas 的背景图）
        SetActiveIfExists("NextLevelButton", false);
        SetActiveIfExists("MainMenuButton", false);

        // ── 11. 标记并保存场景 ────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.SetDirty(canvasGO);

        Debug.Log("[SettlementPanelSetup] 结算面板已创建！在 LevelComplete 场景中运行即可看到效果。");
    }

    // ═══════════════════════════════════════════════════════════
    // UI 辅助方法
    // ═══════════════════════════════════════════════════════════

    static void AddText(Transform parent, string name, string content,
        ref float y, float width, float height,
        int fontSize, FontStyle style, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(width, height);
        var txt = go.AddComponent<Text>();
        txt.text                = content;
        txt.fontSize            = fontSize;
        txt.fontStyle           = style;
        txt.alignment           = TextAnchor.MiddleCenter;
        txt.color               = color;
        txt.resizeTextForBestFit = false;
        y -= height + 8f;
    }

    static void AddSeparator(Transform parent, string name, ref float y, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(width, 2f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.8f, 0.5f, 0.2f); // 橙色，与 PausePanel 边框一致
        y -= 2f + 8f;
    }

    static void SetupTopAnchoredRT(GameObject go, Vector2 size, Vector2 pos)
    {
        var rt           = go.GetComponent<RectTransform>();
        rt.anchorMin     = new Vector2(0.5f, 1f);
        rt.anchorMax     = new Vector2(0.5f, 1f);
        rt.pivot         = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta     = size;
    }

    static GameObject CreateImageButton(Transform parent, string name, Sprite sprite)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        var img           = go.AddComponent<Image>();
        img.sprite        = sprite;
        img.type          = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = true;

        var btn                 = go.AddComponent<Button>();
        var colors              = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.68f);
        colors.pressedColor     = new Color(0.72f, 0.72f, 0.72f);
        colors.selectedColor    = Color.white;
        btn.colors              = colors;

        return go;
    }

    static Vector2 GetButtonSize(Sprite sprite, float targetH, float minW, float maxW)
    {
        float aspect = (sprite != null && sprite.rect.height > 0f)
            ? sprite.rect.width / sprite.rect.height : 1f;
        return new Vector2(Mathf.Clamp(targetH * aspect, minW, maxW), targetH);
    }

    // ═══════════════════════════════════════════════════════════
    // 烟花粒子系统
    // ═══════════════════════════════════════════════════════════

    static ParticleSystem CreateConfettiSystem(string name, Sprite sparkSprite)
    {
        var go = new GameObject(name);
        go.transform.position = Vector3.zero;

        var ps       = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        if (sparkSprite != null)
        {
            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetTexture("_MainTex", sparkSprite.texture);
            renderer.sharedMaterial = mat;
        }
        else
        {
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        }

        var main = ps.main;
        main.duration         = 3.5f;
        main.loop             = false;
        main.startLifetime    = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSpeed       = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize        = new ParticleSystem.MinMaxCurve(0.06f, 0.24f);
        main.startRotation    = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier  = 0.55f;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.maxParticles     = 300;
        main.playOnAwake      = false;

        // 烟花色彩：金 / 红 / 洋红 / 青 / 翠绿
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.00f, 0.84f, 0.00f), 0.00f), // Gold
                new GradientColorKey(new Color(1.00f, 0.20f, 0.20f), 0.22f), // Red
                new GradientColorKey(new Color(1.00f, 0.30f, 0.75f), 0.44f), // Magenta
                new GradientColorKey(new Color(0.00f, 0.90f, 1.00f), 0.66f), // Cyan
                new GradientColorKey(new Color(0.20f, 1.00f, 0.30f), 0.88f), // Emerald
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0.0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1.0f),
            }
        );
        var minMaxColor = new ParticleSystem.MinMaxGradient(grad);
        minMaxColor.mode = ParticleSystemGradientMode.RandomColor;
        main.startColor = minMaxColor;

        // 多层次迸发：主爆 + 延迟二爆，模拟烟花升空后绽放
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 100),
            new ParticleSystem.Burst(0.25f, 60),
            new ParticleSystem.Burst(0.50f, 40),
        });

        // 水平扩散（烟花扇形）
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(6f, 0.1f, 0.1f);

        // 粒子飞行中旋转
        var rotOL   = ps.rotationOverLifetime;
        rotOL.enabled = true;
        rotOL.z     = new ParticleSystem.MinMaxCurve(-360f, 360f);

        // 粒子大小渐变：起始大→逐渐缩小，模拟烟花尾迹
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size    = new ParticleSystem.MinMaxCurve(1.2f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.3f, 0.6f),
            new Keyframe(1f, 0f)
        ));

        return ps;
    }

    // ═══════════════════════════════════════════════════════════
    // 资源辅助方法
    // ═══════════════════════════════════════════════════════════

    static void DestroyIfExists(string goName)
    {
        var go = GameObject.Find(goName);
        if (go != null)
        {
            Object.DestroyImmediate(go);
            Debug.Log($"[SettlementPanelSetup] 已删除旧的 {goName}。");
        }
    }

    static void SetActiveIfExists(string goName, bool active)
    {
        var go = GameObject.Find(goName);
        if (go != null)
            go.SetActive(active);
    }

    static void ConfigureSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[SettlementPanelSetup] 找不到图片: {path}"); return; }

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
            { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single)
            { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (importer.filterMode != FilterMode.Point)
            { importer.filterMode = FilterMode.Point; dirty = true; }
        if (!importer.alphaIsTransparency)
            { importer.alphaIsTransparency = true; dirty = true; }
        if (importer.mipmapEnabled)
            { importer.mipmapEnabled = false; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }

        if (dirty) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
