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
///  1. Processes StartButton.png, QuitButton.png, GameTile.png:
///       - Enables Read/Write on import so we can read pixel data.
///       - Runs a BFS flood-fill from every edge pixel to transparentise the
///         outer white background while preserving interior white pixels
///         (e.g. the "START" / "QUIT" lettering).
///       - Saves the result as Assets/*_processed.png and imports it as a Sprite.
///  2. Wires up the MainMenu scene:
///       - Replaces TitleText with a logo Image (GameTile_processed.png).
///       - Assigns the processed sprites to StartButton / QuitButton Images.
///       - Attaches MainMenuController to MenuManager.
///       - Wires Button.onClick events to MainMenuController methods.
///       - Configures CanvasScaler for 1920×1080.
///  3. Saves the scene.
/// </summary>
public static class MainMenuSetup
{
    private const string SrcStart    = "Assets/StartButton.png";
    private const string SrcQuit     = "Assets/QuitButton.png";
    private const string SrcLogo     = "Assets/GameTile.png";
    private const string OutStart    = "Assets/StartButton_processed.png";
    private const string OutQuit     = "Assets/QuitButton_processed.png";
    private const string OutLogo     = "Assets/GameTile_processed.png";
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
        // ── 1. Process sprites ──────────────────────────────────────────────
        Sprite startSprite = BuildTransparentSprite(SrcStart, OutStart);
        Sprite quitSprite  = BuildTransparentSprite(SrcQuit,  OutQuit);
        Sprite logoSprite  = BuildTransparentSprite(SrcLogo,  OutLogo);

        if (startSprite == null || quitSprite == null || logoSprite == null)
        {
            Debug.LogError("[MainMenuSetup] One or more source sprites could not be processed. Aborting.");
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

        // ── 5. Replace TitleText with a Logo Image ───────────────────────────
        Transform titleTextTf = canvasGO.transform.Find("TitleText");
        if (titleTextTf != null)
            Object.DestroyImmediate(titleTextTf.gameObject);

        GameObject logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform, false);
        logoGO.transform.SetSiblingIndex(0);                         // behind buttons

        var logoRect = logoGO.AddComponent<RectTransform>();
        logoRect.anchorMin        = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax        = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0f, 200f);           // upper-centre
        logoRect.sizeDelta        = new Vector2(600f, 200f);

        var logoImg = logoGO.AddComponent<Image>();
        logoImg.sprite             = logoSprite;
        logoImg.preserveAspect     = true;
        logoImg.raycastTarget      = false;

        // ── 6. Assign sprites to StartButton / QuitButton ────────────────────
        AssignButtonSprite(canvasGO, "StartButton", startSprite, new Vector2(0f,  30f));
        AssignButtonSprite(canvasGO, "QuitButton",  quitSprite,  new Vector2(0f, -140f));

        // ── 7. Attach MainMenuController ─────────────────────────────────────
        // Remove old MainMenu component if present to avoid duplicate handling.
        var oldMenu = managerGO.GetComponent<MainMenu>();
        if (oldMenu != null) Object.DestroyImmediate(oldMenu);

        var controller = managerGO.GetComponent<MainMenuController>()
                      ?? managerGO.AddComponent<MainMenuController>();

        // ── 8. Wire onClick events ───────────────────────────────────────────
        WireButton(canvasGO, "StartButton", controller, "StartGame");
        WireButton(canvasGO, "QuitButton",  controller, "QuitGame");

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

        Debug.Log("[MainMenuSetup] Main Menu scene set up successfully.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sprite processing helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports <paramref name="srcPath"/> as a readable sprite, removes its outer
    /// white background via BFS flood-fill from all edge pixels, and saves the
    /// result to <paramref name="outPath"/>. Returns the processed Sprite asset.
    /// </summary>
    static Sprite BuildTransparentSprite(string srcPath, string outPath)
    {
        // ── Step A: enable Read/Write so we can sample pixels ────────────────
        var imp = AssetImporter.GetAtPath(srcPath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogError($"[MainMenuSetup] Cannot find importer for {srcPath}");
            return null;
        }

        imp.textureType      = TextureImporterType.Default;   // Default allows isReadable
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

        // ── Step B: BFS flood-fill from edges ────────────────────────────────
        Texture2D processed = RemoveWhiteBackground(srcTex);

        // ── Step C: write PNG to disk ─────────────────────────────────────────
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), outPath),
                           processed.EncodeToPNG());
        Object.DestroyImmediate(processed);

        // ── Step D: reimport the original without Read/Write ─────────────────
        imp.textureType = TextureImporterType.Sprite;
        imp.isReadable  = false;
        AssetDatabase.ImportAsset(srcPath, ImportAssetOptions.ForceUpdate);

        // ── Step E: import processed file as a Sprite ─────────────────────────
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

    /// <summary>
    /// BFS flood-fill from every edge pixel that is "near-white".
    /// Returns a new Texture2D with those pixels made transparent.
    /// Interior white pixels (e.g. button text) are left intact because they
    /// are not reachable from the image border without crossing dark pixels.
    /// </summary>
    static Texture2D RemoveWhiteBackground(Texture2D src, byte threshold = 220)
    {
        int w = src.width;
        int h = src.height;
        Color32[] px = src.GetPixels32();
        bool[] visited = new bool[w * h];
        var queue = new Queue<int>();

        // Seed from all four edges
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

        // BFS
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x   = idx % w;
            int y   = idx / w;
            px[idx].a = 0;   // make transparent

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

    static void AssignButtonSprite(GameObject canvas, string buttonName, Sprite sprite,
                                    Vector2 anchoredPos)
    {
        Transform tf = canvas.transform.Find(buttonName);
        if (tf == null) { Debug.LogWarning($"[MainMenuSetup] '{buttonName}' not found."); return; }

        // Size and position
        var rt = tf.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = new Vector2(450f, 140f);
        }

        // The Image is on the button root itself (not the child Text label)
        var img = tf.GetComponent<Image>();
        if (img == null) { Debug.LogWarning($"[MainMenuSetup] No Image on '{buttonName}'."); return; }

        img.sprite         = sprite;
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;
        img.color          = Color.white;

        // Remove the default Unity button transition colours so our sprite is
        // shown without tinting, and use a subtle scale animation instead.
        var btn = tf.GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor    = Color.white;
            btn.colors = colors;
        }
    }

    static void WireButton(GameObject canvas, string buttonName,
                           MainMenuController controller, string methodName)
    {
        Transform tf = canvas.transform.Find(buttonName);
        if (tf == null) return;

        var btn = tf.GetComponent<Button>();
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();

        // Persistent (serialised) listener so it survives play mode
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            btn.onClick,
            methodName == "StartGame"
                ? (UnityEngine.Events.UnityAction)controller.StartGame
                : (UnityEngine.Events.UnityAction)controller.QuitGame);

        EditorUtility.SetDirty(btn);
    }
}
