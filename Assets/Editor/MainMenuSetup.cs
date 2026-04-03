using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools/Setup Main Menu Scene
///
/// What this script does:
///  1. Processes button images (removes white background via BFS flood-fill).
///  2. Wires up the MainMenu scene with three buttons:
///       - NewGameButton   → 新游戏（删除存档，从头开始）
///       - ContinueButton  → 继续游戏（读取存档，有存档时才显示）
///       - QuitButton      → 退出游戏
///  3. Replaces TitleText with a logo Image (GameTile_processed.png).
///  4. Attaches MainMenuController to MenuManager.
///  5. Configures CanvasScaler for 1920×1080.
///  6. Saves the scene.
///
/// 如果场景中有旧的 StartButton，会被删除并替换为 NewGameButton。
/// </summary>
public static class MainMenuSetup
{
    // 旧版原始图片路径（用于 BFS 处理）
    private const string SrcStart    = "Assets/Textures/StartButton.png";
    private const string SrcQuit     = "Assets/Textures/QuitButton.png";
    private const string SrcLogo     = "Assets/Textures/GameTile.png";
    private const string OutStart    = "Assets/Textures/StartButton_processed.png";
    private const string OutQuit     = "Assets/Textures/QuitButton_processed.png";
    private const string OutLogo     = "Assets/Textures/GameTile_processed.png";

    // 新版按钮图片路径（Gemini 生成的，已在 Textures 目录下）
    private const string TexNewGame    = "Assets/Textures/NewGameButton.png";
    private const string TexContinue   = "Assets/Textures/ContinueButton.png";
    private const string TexQuitBtn    = "Assets/Textures/QuitButton.png";

    private const string ScenePath   = "Assets/Scenes/MainMenu.unity";

    // ──────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Fix Background Order")]
    public static void FixBackgroundOrder()
    {
        var bg = GameObject.Find("Background");
        if (bg != null)
        {
            bg.transform.SetSiblingIndex(0);
            EditorSceneManager.MarkSceneDirty(bg.scene);
            EditorSceneManager.SaveScene(bg.scene);
            Debug.Log("[MainMenuSetup] Background moved to sibling index 0.");
        }
    }

    [MenuItem("Tools/Setup Main Menu Scene")]
    public static void Run()
    {
        // ── 1. Process logo sprite (BFS background removal) ─────────────────
        Sprite logoSprite  = BuildTransparentSprite(SrcLogo,  OutLogo);

        if (logoSprite == null)
        {
            Debug.LogError("[MainMenuSetup] Logo sprite could not be processed. Aborting.");
            return;
        }

        // ── 1b. Configure new button sprites (Point filter, etc.) ───────────
        ConfigureSpriteImport(TexNewGame);
        ConfigureSpriteImport(TexContinue);
        ConfigureSpriteImport(TexQuitBtn);

        Sprite newGameSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(TexNewGame);
        Sprite continueSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexContinue);
        Sprite quitSprite     = AssetDatabase.LoadAssetAtPath<Sprite>(TexQuitBtn);

        if (newGameSprite == null)
        {
            // 回退：尝试使用旧版 BFS 处理过的 StartButton
            Debug.LogWarning("[MainMenuSetup] NewGameButton.png not found, falling back to StartButton_processed.png");
            newGameSprite = BuildTransparentSprite(SrcStart, OutStart);
        }
        if (quitSprite == null)
        {
            Debug.LogWarning("[MainMenuSetup] QuitButton.png (Textures) not found, falling back to processed version");
            quitSprite = BuildTransparentSprite(SrcQuit, OutQuit);
        }

        if (newGameSprite == null || quitSprite == null)
        {
            Debug.LogError("[MainMenuSetup] Required button sprites missing. Aborting.");
            return;
        }

        // ── 2. Load the scene ───────────────────────────────────────────────
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ── 3. Find / validate root objects ─────────────────────────────────
        GameObject canvasGO  = GameObject.Find("MenuCanvas");
        GameObject managerGO = GameObject.Find("MenuManager");

        if (canvasGO == null)
        {
            Debug.LogError("[MainMenuSetup] 'MenuCanvas' not found in the scene.");
            return;
        }
        if (managerGO == null)
        {
            Debug.LogError("[MainMenuSetup] 'MenuManager' not found in the scene.");
            return;
        }

        // ── 4. Configure CanvasScaler for 1920×1080 ─────────────────────────
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight   = 0.5f;
        }

        // ── 5. Replace TitleText / Logo ──────────────────────────────────────
        // 删除旧的 TitleText 或 Logo
        Transform titleTextTf = canvasGO.transform.Find("TitleText");
        if (titleTextTf != null) Object.DestroyImmediate(titleTextTf.gameObject);
        Transform oldLogoTf = canvasGO.transform.Find("Logo");
        if (oldLogoTf != null) Object.DestroyImmediate(oldLogoTf.gameObject);

        GameObject logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform, false);
        logoGO.transform.SetSiblingIndex(0);

        var logoRect = logoGO.AddComponent<RectTransform>();
        logoRect.anchorMin        = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax        = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0f, 250f);
        logoRect.sizeDelta        = new Vector2(600f, 200f);

        var logoImg = logoGO.AddComponent<Image>();
        logoImg.sprite             = logoSprite;
        logoImg.preserveAspect     = true;
        logoImg.raycastTarget      = false;

        // ── 6. Remove old StartButton, create new buttons ───────────────────
        // 删除旧的 StartButton（如果存在）
        Transform oldStartBtn = canvasGO.transform.Find("StartButton");
        if (oldStartBtn != null) Object.DestroyImmediate(oldStartBtn.gameObject);

        // 删除旧的 NewGameButton / ContinueButton（如果已存在，用于重复运行）
        Transform oldNewGame = canvasGO.transform.Find("NewGameButton");
        if (oldNewGame != null) Object.DestroyImmediate(oldNewGame.gameObject);
        Transform oldContinue = canvasGO.transform.Find("ContinueButton");
        if (oldContinue != null) Object.DestroyImmediate(oldContinue.gameObject);
        Transform oldQuit = canvasGO.transform.Find("QuitButton");
        if (oldQuit != null) Object.DestroyImmediate(oldQuit.gameObject);

        // 三个按钮的垂直布局（屏幕中心偏下）
        float btnWidth  = 450f;
        float btnHeight = 140f;
        float spacing   = 20f;

        // 如果有继续按钮图片，创建三个按钮；否则只创建两个
        bool hasContinue = (continueSprite != null);

        // 按钮组整体垂直偏移（负数 = 往下移）
        float groupOffsetY = -100f;

        float topY, midY, botY;
        if (hasContinue)
        {
            // 三个按钮：新游戏 / 继续游戏 / 退出
            topY = (btnHeight + spacing)  + groupOffsetY;   // 新游戏
            midY = 0f                     + groupOffsetY;   // 继续游戏
            botY = -(btnHeight + spacing) + groupOffsetY;   // 退出
        }
        else
        {
            // 两个按钮：新游戏 / 退出
            topY = (btnHeight + spacing) * 0.5f  + groupOffsetY;
            midY = 0f; // unused
            botY = -(btnHeight + spacing) * 0.5f + groupOffsetY;
        }

        // 新游戏按钮
        CreateMenuButton(canvasGO, "NewGameButton", newGameSprite, btnWidth, btnHeight,
                         new Vector2(0f, topY));

        // 继续游戏按钮
        if (hasContinue)
        {
            CreateMenuButton(canvasGO, "ContinueButton", continueSprite, btnWidth, btnHeight,
                             new Vector2(0f, midY));
        }

        // 退出按钮
        CreateMenuButton(canvasGO, "QuitButton", quitSprite, btnWidth, btnHeight,
                         new Vector2(0f, botY));

        // ── 7. Attach MainMenuController ─────────────────────────────────────
        var oldMenu = managerGO.GetComponent<MainMenu>();
        if (oldMenu != null) Object.DestroyImmediate(oldMenu);

        var controller = managerGO.GetComponent<MainMenuController>()
                      ?? managerGO.AddComponent<MainMenuController>();

        // ── 8. Wire onClick events ───────────────────────────────────────────
        WireButton(canvasGO, "NewGameButton",  controller, nameof(MainMenuController.NewGame));
        if (hasContinue)
            WireButton(canvasGO, "ContinueButton", controller, nameof(MainMenuController.ContinueGame));
        WireButton(canvasGO, "QuitButton",     controller, nameof(MainMenuController.QuitGame));

        // ── 9. Ensure EventSystem exists ─────────────────────────────────────
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── 10. Save ─────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[MainMenuSetup] 主菜单场景设置完成！按钮: NewGame" +
                  (hasContinue ? " + Continue" : "") + " + Quit");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Button creation helper
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 在 Canvas 下创建一个带 Image + Button 的菜单按钮。
    /// </summary>
    static void CreateMenuButton(GameObject canvas, string name, Sprite sprite,
                                  float width, float height, Vector2 anchoredPos)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(canvas.transform, false);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = new Vector2(width, height);

        var img = btnGO.AddComponent<Image>();
        img.sprite         = sprite;
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;
        img.color          = Color.white;
        img.raycastTarget  = true;

        var btn = btnGO.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor    = Color.white;
        btn.colors = colors;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sprite import helper
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 确保图片以正确的 Sprite 设置导入（Point 过滤、无压缩、透明度）。
    /// </summary>
    static void ConfigureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        bool needsReimport = false;

        if (importer.textureType != TextureImporterType.Sprite)
        { importer.textureType = TextureImporterType.Sprite; needsReimport = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        { importer.spriteImportMode = SpriteImportMode.Single; needsReimport = true; }
        if (importer.filterMode != FilterMode.Point)
        { importer.filterMode = FilterMode.Point; needsReimport = true; }
        if (!importer.alphaIsTransparency)
        { importer.alphaIsTransparency = true; needsReimport = true; }
        if (importer.mipmapEnabled)
        { importer.mipmapEnabled = false; needsReimport = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        { importer.textureCompression = TextureImporterCompression.Uncompressed; needsReimport = true; }

        if (needsReimport)
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sprite processing helpers (BFS background removal)
    // ──────────────────────────────────────────────────────────────────────────

    static Sprite BuildTransparentSprite(string srcPath, string outPath)
    {
        var imp = AssetImporter.GetAtPath(srcPath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogError($"[MainMenuSetup] Cannot find importer for {srcPath}");
            return null;
        }

        imp.textureType      = TextureImporterType.Default;
        imp.isReadable       = true;
        imp.filterMode       = FilterMode.Point;
        imp.alphaSource      = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled    = false;
        AssetDatabase.ImportAsset(srcPath, ImportAssetOptions.ForceUpdate);

        var srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
        if (srcTex == null)
        {
            Debug.LogError($"[MainMenuSetup] Failed to load texture: {srcPath}");
            return null;
        }

        Texture2D processed = RemoveWhiteBackground(srcTex);

        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), outPath),
                           processed.EncodeToPNG());
        Object.DestroyImmediate(processed);

        imp.textureType = TextureImporterType.Sprite;
        imp.isReadable  = false;
        AssetDatabase.ImportAsset(srcPath, ImportAssetOptions.ForceUpdate);

        AssetDatabase.Refresh();
        var outImp = AssetImporter.GetAtPath(outPath) as TextureImporter;
        if (outImp == null)
        {
            Debug.LogError($"[MainMenuSetup] Cannot find importer for processed file: {outPath}");
            return null;
        }

        outImp.textureType         = TextureImporterType.Sprite;
        outImp.spriteImportMode    = SpriteImportMode.Single;
        outImp.filterMode          = FilterMode.Point;
        outImp.alphaIsTransparency = true;
        outImp.alphaSource         = TextureImporterAlphaSource.FromInput;
        outImp.isReadable          = false;
        outImp.mipmapEnabled       = false;
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);

        return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
    }

    static Texture2D RemoveWhiteBackground(Texture2D src, byte threshold = 220)
    {
        int w = src.width;
        int h = src.height;
        Color32[] px = src.GetPixels32();
        bool[] visited = new bool[w * h];
        var queue = new Queue<int>();

        for (int x = 0; x < w; x++)
        {
            TryEnqueue(queue, visited, px, x, 0,     w, threshold);
            TryEnqueue(queue, visited, px, x, h - 1, w, threshold);
        }
        for (int y = 1; y < h - 1; y++)
        {
            TryEnqueue(queue, visited, px, 0,     y, w, threshold);
            TryEnqueue(queue, visited, px, w - 1, y, w, threshold);
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x   = idx % w;
            int y   = idx / w;
            px[idx].a = 0;

            if (x > 0)     TryEnqueue(queue, visited, px, x - 1, y,     w, threshold);
            if (x < w - 1) TryEnqueue(queue, visited, px, x + 1, y,     w, threshold);
            if (y > 0)     TryEnqueue(queue, visited, px, x,     y - 1, w, threshold);
            if (y < h - 1) TryEnqueue(queue, visited, px, x,     y + 1, w, threshold);
        }

        var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.SetPixels32(px);
        result.Apply();
        return result;
    }

    static void TryEnqueue(Queue<int> q, bool[] visited, Color32[] px,
                           int x, int y, int w, byte threshold)
    {
        int idx = y * w + x;
        if (visited[idx]) return;
        Color32 c = px[idx];
        if (c.r >= threshold && c.g >= threshold && c.b >= threshold)
        {
            visited[idx] = true;
            q.Enqueue(idx);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scene-wiring helpers
    // ──────────────────────────────────────────────────────────────────────────

    static void WireButton(GameObject canvas, string buttonName,
                           MainMenuController controller, string methodName)
    {
        Transform tf = canvas.transform.Find(buttonName);
        if (tf == null) return;

        var btn = tf.GetComponent<Button>();
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();

        // 根据方法名选择正确的委托
        UnityEngine.Events.UnityAction action = null;
        switch (methodName)
        {
            case nameof(MainMenuController.NewGame):
                action = controller.NewGame;
                break;
            case nameof(MainMenuController.ContinueGame):
                action = controller.ContinueGame;
                break;
            case nameof(MainMenuController.QuitGame):
                action = controller.QuitGame;
                break;
            case "StartGame": // 兼容旧版
                action = controller.StartGame;
                break;
            default:
                Debug.LogWarning($"[MainMenuSetup] Unknown method: {methodName}");
                return;
        }

        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        EditorUtility.SetDirty(btn);
    }
}
