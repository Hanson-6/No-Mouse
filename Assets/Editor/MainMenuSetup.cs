using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuSetup
{
    private const string TexLogo = "Assets/Textures/GameTile.png";
    private const string TexBg = "Assets/Textures/background_new.png";
    private const string TexTutorial = "Assets/Textures/Buttons/TutorialButton.png";
    private const string TexNewGame = "Assets/Textures/Buttons/NewGameButton.png";
    private const string TexContinue = "Assets/Textures/Buttons/ContinueButton.png";
    private const string TexContinueDisabled = "Assets/Textures/Buttons/ContinueButtonDisable.png";
    private const string TexQuit = "Assets/Textures/Buttons/QuitButton.png";

    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    private const float BtnHeight = 100f;
    private const float BtnWidth = 380f;
    private const float BtnGap = 30f;
    private const float LogoToBtnGap = 50f;

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
        ConfigureSpriteImport(TexBg);
        ConfigureSpriteImport(TexLogo);
        ConfigureSpriteImport(TexTutorial);
        ConfigureSpriteImport(TexNewGame);
        ConfigureSpriteImport(TexContinue);
        ConfigureSpriteImport(TexContinueDisabled);
        ConfigureSpriteImport(TexQuit);

        Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexLogo);
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexBg);
        Sprite tutSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexTutorial);
        Sprite newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexNewGame);
        Sprite contSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexContinue);
        Sprite contDisSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexContinueDisabled);
        Sprite quitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexQuit);

        if (logoSprite == null) { Debug.LogError("[MainMenuSetup] Logo missing."); return; }
        if (bgSprite == null) { Debug.LogError("[MainMenuSetup] Background missing."); return; }
        if (tutSprite == null) { Debug.LogError("[MainMenuSetup] TutorialButton missing."); return; }
        if (newSprite == null) { Debug.LogError("[MainMenuSetup] NewGameButton missing."); return; }
        if (contSprite == null) { Debug.LogError("[MainMenuSetup] ContinueButton missing."); return; }
        if (contDisSprite == null) { Debug.LogWarning("[MainMenuSetup] ContinueButtonDisable missing (optional)."); }
        if (quitSprite == null) { Debug.LogError("[MainMenuSetup] QuitButton missing."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject canvasGO = GameObject.Find("MenuCanvas");
        GameObject managerGO = GameObject.Find("MenuManager");

        if (canvasGO == null)
        {
            Debug.LogError("[MainMenuSetup] 'MenuCanvas' not found.");
            return;
        }
        if (managerGO == null)
        {
            Debug.LogError("[MainMenuSetup] 'MenuManager' not found.");
            return;
        }

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.transform.localScale = Vector3.one;

        // ── Clear existing UI ───────────────────────────────────────────────
        DestroyChild(canvasGO.transform, "Background");
        DestroyChild(canvasGO.transform, "Logo");
        DestroyChild(canvasGO.transform, "TutorialButton");
        DestroyChild(canvasGO.transform, "NewGameButton");
        DestroyChild(canvasGO.transform, "ContinueButton");
        DestroyChild(canvasGO.transform, "QuitButton");
        DestroyChild(canvasGO.transform, "TitleText");
        DestroyChild(canvasGO.transform, "StartButton");

        // ── Background (fullscreen, behind all) ─────────────────────────────
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        bgGO.transform.SetSiblingIndex(0);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bgRT.anchoredPosition = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type = Image.Type.Simple;
        bgImg.raycastTarget = false;
        bgImg.preserveAspect = false;

        // ── Logo ────────────────────────────────────────────────────────────
        GameObject logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform, false);
        RectTransform logoRT = logoGO.AddComponent<RectTransform>();
        logoRT.anchorMin = new Vector2(0.5f, 0.5f);
        logoRT.anchorMax = new Vector2(0.5f, 0.5f);
        logoRT.anchoredPosition = new Vector2(0f, 220f);
        logoRT.sizeDelta = new Vector2(800f, 240f);
        Image logoImg = logoGO.AddComponent<Image>();
        logoImg.sprite = logoSprite;
        logoImg.preserveAspect = true;
        logoImg.raycastTarget = false;

        // ── Buttons (4 buttons, equal 30px gaps, 100px height) ───────────────
        // Logo bottom = 220 - 120 = 100
        // Tutorial top = y1 + 50;  gap = 100 - (y1+50) = 45  →  y1 = 5
        float y = 5f;
        CreateMenuButton(canvasGO, "TutorialButton", tutSprite, y); y -= BtnHeight + BtnGap;
        CreateMenuButton(canvasGO, "NewGameButton", newSprite, y); y -= BtnHeight + BtnGap;
        CreateMenuButton(canvasGO, "ContinueButton", contSprite, y); y -= BtnHeight + BtnGap;
        CreateMenuButton(canvasGO, "QuitButton", quitSprite, y);

        // ── Controller ──────────────────────────────────────────────────────
        var oldMenu = managerGO.GetComponent<MainMenu>();
        if (oldMenu != null) Object.DestroyImmediate(oldMenu);

        var ctrl = managerGO.GetComponent<MainMenuController>()
                ?? managerGO.AddComponent<MainMenuController>();

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("tutorialButton").objectReferenceValue =
            canvasGO.transform.Find("TutorialButton")?.GetComponent<Button>();
        so.FindProperty("newGameButton").objectReferenceValue =
            canvasGO.transform.Find("NewGameButton")?.GetComponent<Button>();
        so.FindProperty("continueButton").objectReferenceValue =
            canvasGO.transform.Find("ContinueButton")?.GetComponent<Button>();
        so.FindProperty("quitButton").objectReferenceValue =
            canvasGO.transform.Find("QuitButton")?.GetComponent<Button>();

        if (contDisSprite != null)
            so.FindProperty("continueDisabledSprite").objectReferenceValue = contDisSprite;

        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire onClick ────────────────────────────────────────────────────
        WireButton(canvasGO, "TutorialButton", ctrl, nameof(MainMenuController.Tutorial));
        WireButton(canvasGO, "NewGameButton", ctrl, nameof(MainMenuController.NewGame));
        WireButton(canvasGO, "ContinueButton", ctrl, nameof(MainMenuController.ContinueGame));
        WireButton(canvasGO, "QuitButton", ctrl, nameof(MainMenuController.QuitGame));

        // ── EventSystem ─────────────────────────────────────────────────────
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[MainMenuSetup] Done. Tutorial + NewGame + Continue + Quit. 30px gaps, 100px height.");
    }

    static void CreateMenuButton(GameObject canvas, string name, Sprite sprite, float centerY)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, centerY);
        rt.sizeDelta = new Vector2(BtnWidth, BtnHeight);

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;
    }

    static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    static void ConfigureSpriteImport(string assetPath)
    {
        TextureImporter imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;

        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
        if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
        if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
        if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
        if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }

        if (dirty) AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    static void WireButton(GameObject canvas, string name, MainMenuController ctrl, string method)
    {
        Transform tf = canvas.transform.Find(name);
        if (tf == null) return;
        Button btn = tf.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();

        UnityEngine.Events.UnityAction action = method switch
        {
            nameof(MainMenuController.Tutorial) => ctrl.Tutorial,
            nameof(MainMenuController.NewGame) => ctrl.NewGame,
            nameof(MainMenuController.ContinueGame) => ctrl.ContinueGame,
            nameof(MainMenuController.QuitGame) => ctrl.QuitGame,
            "StartGame" => ctrl.StartGame,
            _ => null
        };

        if (action != null)
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        else
            Debug.LogWarning($"[MainMenuSetup] Unknown method: {method}");
    }
}
