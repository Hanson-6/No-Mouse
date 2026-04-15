using LDtkUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools → Setup Tutoring UI   : 一次性创建 HintCanvas + Background（只需运行一次）
/// Tools → Create Hint Trigger : 在场景中新增一个 HintTrigger，可多次运行、自由摆放
/// </summary>
public static class TutoringSetup
{
    private const string TUTORING_SCENE_PATH = "Assets/Scenes/Tutoring.unity";
    private const string TUTORING_LDTK_PATH = "Assets/IDTK/Tutoring.ldtk";
    private const string TEX_MOVING_RULE   = "Assets/moving_rule.png";
    private const string TEX_CAVE_SPRITES2 = "Assets/CaveAssets/Spritesheets/Decorations/CaveDetailSprites2.png";
    private const string TRIGGER_PREFAB_PATH = "Assets/Prefabs/HintTrigger.prefab";
    private const string CHECKPOINT_PREFAB_PATH = "Assets/Prefabs/Checkpoint.prefab";

    // ── 重新加载 Tutoring.ldtk 地形 ──────────────────────────────────────────
    [MenuItem("Tools/Tutoring/0. Reload Tutoring LDtk Terrain")]
    public static void ReloadTutoringTerrain()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[TutoringSetup] 请先打开 Tutoring 场景。");
            return;
        }

        if (!string.Equals(scene.path, TUTORING_SCENE_PATH, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"[TutoringSetup] 当前场景是 '{scene.path}'。请先打开 '{TUTORING_SCENE_PATH}' 再执行 Reload。\n" +
                           "否则会找不到 Tutoring 的 LDtk 根对象。");
            return;
        }

        // 复用 LDtkSceneSetup 里已修好的通用 reload 逻辑：
        //   - 按 prefab source（.ldtk）查找根对象，不依赖名字
        //   - 强制 reimport → 销毁旧实例 → 重新实例化 → GenerateGeometry
        LDtkSceneSetup.ReloadLDtkInCurrentScene(
            forceReimport: true,
            ldtkPathOverride: TUTORING_LDTK_PATH);

        // 同步相机边界 + 左右空气墙 + DeathZone 到当前 LDtk 实际范围，
        // 避免地图扩展后仍沿用旧常量边界造成“空气墙”。
        if (TryGetCurrentMapBounds(scene, out float mapMinX, out float mapMaxX, out float mapMinY, out float mapMaxY))
        {
            ApplyMapBoundsToScene(scene, mapMinX, mapMaxX, mapMinY, mapMaxY);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[TutoringSetup] Reload 后已同步场景边界到 LDtk：X({mapMinX:F2}~{mapMaxX:F2}) Y({mapMinY:F2}~{mapMaxY:F2})。");
        }
        else
        {
            Debug.LogWarning("[TutoringSetup] Reload 完成，但未能从场景中的 LDtk 根对象读取地图边界。" +
                             "请检查 Hierarchy 中是否存在来自 Tutoring.ldtk 的 prefab root。");
        }
    }

    // ── 第一步：只需运行一次，建 UI + 背景 ──────────────────────────────────
    [MenuItem("Tools/Tutoring/1. Setup Tutoring UI (run once)")]
    public static void SetupUI()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[TutoringSetup] 请先打开 Tutoring 场景。");
            return;
        }

        ConfigureSpriteImport(TEX_MOVING_RULE);
        Sprite movingRuleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_MOVING_RULE);
        if (movingRuleSprite == null)
        {
            Debug.LogError($"[TutoringSetup] 找不到图片：{TEX_MOVING_RULE}");
            return;
        }

        // 删除旧 UI（保留触发器位置不变）
        DestroyExisting("HintCanvas");

        // ── Canvas ──────────────────────────────────────────────────────────
        var canvasGO = new GameObject("HintCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── HintPanel root（统一 SetActive）──────────────────────────────────
        var hintRootGO = new GameObject("HintPanel");
        hintRootGO.transform.SetParent(canvasGO.transform, false);
        SetFullStretch(hintRootGO.AddComponent<RectTransform>());

        // 全屏遮罩
        var overlayGO = new GameObject("FullscreenOverlay");
        overlayGO.transform.SetParent(hintRootGO.transform, false);
        SetFullStretch(overlayGO.AddComponent<RectTransform>());
        overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // 内容面板（全屏）
        var panelGO = new GameObject("ContentPanel");
        panelGO.transform.SetParent(hintRootGO.transform, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 深色背景 + 蓝色边框 + 内层深色
        AddLayeredBackground(panelGO);

        // HintImage
        var hintImgGO = new GameObject("HintImage");
        hintImgGO.transform.SetParent(panelGO.transform, false);
        var hintRect = hintImgGO.AddComponent<RectTransform>();
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.one;
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;
        var hintImg = hintImgGO.AddComponent<Image>();
        hintImg.sprite = movingRuleSprite;
        hintImg.preserveAspect = false;

        // 底部提示文字（叠在图片上方，固定高度 36px 贴底部）
        var tipGO = new GameObject("TipText");
        tipGO.transform.SetParent(panelGO.transform, false);
        var tipRect = tipGO.AddComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0f, 0f);
        tipRect.anchorMax = new Vector2(1f, 0f);
        tipRect.pivot = new Vector2(0.5f, 0f);
        tipRect.offsetMin = new Vector2(0f, 12f);
        tipRect.offsetMax = new Vector2(0f, 48f);
        var tipTxt = AddText(tipGO, "Press Q to quit", 22, new Color(1f, 0.2f, 0.2f, 1f));
        tipTxt.alignment = TextAnchor.MiddleCenter;

        hintRootGO.SetActive(false);

        // ── 相机边界 + 左右空气墙 + 死亡区（按当前 Tutoring.ldtk 实际范围生成） ──
        float mapMinX = 159f, mapMaxX = 226f, mapMinY = 31.5f, mapMaxY = 63f; // fallback
        if (!TryGetCurrentMapBounds(scene, out mapMinX, out mapMaxX, out mapMinY, out mapMaxY))
        {
            Debug.LogWarning("[TutoringSetup] 未能从 Tutoring.ldtk 实例读取边界，使用 fallback 常量。" +
                             "建议先运行 Tools/Tutoring/0. Reload Tutoring LDtk Terrain。");
        }

        ApplyMapBoundsToScene(scene, mapMinX, mapMaxX, mapMinY, mapMaxY);
        Debug.Log($"[TutoringSetup] 场景边界已应用：X({mapMinX:F2}~{mapMaxX:F2}) Y({mapMinY:F2}~{mapMaxY:F2})。");

        // ── 同时生成 HintTrigger Prefab 供后续使用 ───────────────────────────
        BuildTriggerPrefab();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TutoringSetup] UI 创建完成。今后用 Tools/Tutoring/2. Place Hint Trigger 放置触发器。");
    }

    // ── 第二步：每次放置新触发器时运行，不影响已有触发器 ────────────────────
    [MenuItem("Tools/Tutoring/2. Place Hint Trigger")]
    public static void PlaceTrigger()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[TutoringSetup] 请先打开 Tutoring 场景。");
            return;
        }

        // 确保 prefab 存在
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TRIGGER_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.Log("[TutoringSetup] HintTrigger prefab 不存在，正在生成…");
            BuildTriggerPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TRIGGER_PREFAB_PATH);
        }

        if (prefab == null)
        {
            Debug.LogError("[TutoringSetup] 无法生成 HintTrigger prefab。");
            return;
        }

        // 实例化并放到相机中央
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var cam = Camera.main;
        instance.transform.position = cam != null
            ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f)
            : Vector3.zero;

        // 自动命名（避免重名）
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.StartsWith("HintTrigger")) count++;
        instance.name = count == 0 ? "HintTrigger" : $"HintTrigger ({count})";

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[TutoringSetup] 已放置 {instance.name}，请在 Scene 视图中移动到目标位置。");
    }

    [MenuItem("Tools/Tutoring/3. Place Checkpoint")]
    public static void PlaceCheckpoint()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[TutoringSetup] 请先打开 Tutoring 场景。");
            return;
        }

        if (!string.Equals(scene.path, TUTORING_SCENE_PATH, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[TutoringSetup] 当前场景是 '{scene.path}'。请先打开 '{TUTORING_SCENE_PATH}' 再放置 Checkpoint。");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHECKPOINT_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[TutoringSetup] 找不到 Checkpoint prefab：{CHECKPOINT_PREFAB_PATH}");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
        {
            Debug.LogError("[TutoringSetup] 放置 Checkpoint 失败。");
            return;
        }

        var cam = Camera.main;
        instance.transform.position = cam != null
            ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f)
            : new Vector3(196f, 47f, 0f);

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.StartsWith("Checkpoint")) count++;
        instance.name = count == 0 ? "Checkpoint" : $"Checkpoint ({count})";

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[TutoringSetup] 已放置 {instance.name}，请在 Scene 视图中移动到目标位置。");
    }

    // ── 生成 HintTrigger.prefab ──────────────────────────────────────────────
    static void BuildTriggerPrefab()
    {
        Sprite unpressedSprite = null, pressedSprite = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(TEX_CAVE_SPRITES2))
        {
            if (asset is Sprite s)
            {
                if (s.name == "CaveDetailSprites2_99")  unpressedSprite = s;
                if (s.name == "CaveDetailSprites2_100") pressedSprite   = s;
            }
        }

        Sprite movingRuleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_MOVING_RULE);

        var go = new GameObject("HintTrigger");
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2f, 1.5f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = unpressedSprite;
        sr.sortingOrder = 5;

        var trigger = go.AddComponent<TutorialHintTrigger>();
        var so = new SerializedObject(trigger);
        so.FindProperty("unpressedSprite").objectReferenceValue = unpressedSprite;
        so.FindProperty("pressedSprite").objectReferenceValue   = pressedSprite != null ? pressedSprite : unpressedSprite;
        so.FindProperty("hintSprite").objectReferenceValue      = movingRuleSprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(go, TRIGGER_PREFAB_PATH);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log($"[TutoringSetup] HintTrigger prefab 已保存到 {TRIGGER_PREFAB_PATH}。");
    }

    // ── 小工具 ───────────────────────────────────────────────────────────────
    static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void AddLayeredBackground(GameObject parent)
    {
        void Layer(string n, Vector2 inset, Color c)
        {
            var g = new GameObject(n);
            g.transform.SetParent(parent.transform, false);
            var r = g.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset.x, inset.x);
            r.offsetMax = new Vector2(-inset.y, -inset.y);
            g.AddComponent<Image>().color = c;
        }
        Layer("Background", Vector2.zero,        new Color(0.1f,  0.1f,  0.15f, 0.92f));
        Layer("Border",     new Vector2(0f, 4f),  new Color(0.4f,  0.7f,  1f,   0.5f));
        Layer("Inner",      new Vector2(8f, 8f),  new Color(0.08f, 0.08f, 0.12f, 1f));
    }

    static Text AddText(GameObject parent, string content, int size, Color color)
    {
        var g = new GameObject("Text");
        g.transform.SetParent(parent.transform, false);
        var r = g.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var t = g.AddComponent<Text>();
        t.text = content; t.fontSize = size; t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    static void ConfigureSpriteImport(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null || imp.textureType == TextureImporterType.Sprite) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    static void DestroyExisting(string objName)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == objName) { Object.DestroyImmediate(root); return; }
    }

    static bool TryGetCurrentMapBounds(Scene scene, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = maxX = minY = maxY = 0f;
        bool hasAny = false;

        foreach (var root in scene.GetRootGameObjects())
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (src == null) continue;

            string srcPath = AssetDatabase.GetAssetPath(src);
            if (!string.Equals(srcPath, TUTORING_LDTK_PATH, System.StringComparison.OrdinalIgnoreCase))
                continue;

            Transform world = root.transform;
            foreach (Transform child in root.transform)
            {
                if (child.GetComponent<LDtkComponentWorld>() != null)
                {
                    world = child;
                    break;
                }
            }

            foreach (Transform level in world)
            {
                var renderers = level.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (!hasAny)
                    {
                        minX = r.bounds.min.x;
                        maxX = r.bounds.max.x;
                        minY = r.bounds.min.y;
                        maxY = r.bounds.max.y;
                        hasAny = true;
                    }
                    else
                    {
                        minX = Mathf.Min(minX, r.bounds.min.x);
                        maxX = Mathf.Max(maxX, r.bounds.max.x);
                        minY = Mathf.Min(minY, r.bounds.min.y);
                        maxY = Mathf.Max(maxY, r.bounds.max.y);
                    }
                }
            }

            // Scene only has one Tutoring LDtk root.
            break;
        }

        return hasAny;
    }

    static void ApplyMapBoundsToScene(Scene scene, float mapMinX, float mapMaxX, float mapMinY, float mapMaxY)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var cf = root.GetComponent<CameraFollow>() ?? root.GetComponentInChildren<CameraFollow>();
            if (cf == null) continue;

            var camComp = root.GetComponent<Camera>() ?? root.GetComponentInChildren<Camera>();
            float orthoSize = camComp != null ? camComp.orthographicSize : 10f;
            float aspect = camComp != null ? camComp.aspect : 16f / 9f;
            float halfW = orthoSize * aspect;
            float halfH = orthoSize;

            var so = new SerializedObject(cf);
            so.FindProperty("offset").vector2Value = new Vector2(5f, 2f);
            so.FindProperty("useBounds").boolValue = true;
            so.FindProperty("minX").floatValue = mapMinX + halfW;
            so.FindProperty("maxX").floatValue = mapMaxX - halfW;

            float mapCenterY = (mapMinY + mapMaxY) * 0.5f;
            so.FindProperty("minY").floatValue = (mapMaxY - mapMinY) > halfH * 2f
                ? mapMinY + halfH : mapCenterY;
            so.FindProperty("maxY").floatValue = (mapMaxY - mapMinY) > halfH * 2f
                ? mapMaxY - halfH : mapCenterY;
            so.ApplyModifiedPropertiesWithoutUndo();
            break;
        }

        // Keep compatibility with existing setup asset names.
        DestroyExisting("WallLeft");
        DestroyExisting("WallRight");

        float wallHeight = mapMaxY - mapMinY + 20f;
        float wallCenterY = (mapMinY + mapMaxY) * 0.5f;

        var wallL = new GameObject("WallLeft");
        var wallLCol = wallL.AddComponent<BoxCollider2D>();
        wallL.transform.position = new Vector3(mapMinX - 1f, wallCenterY, 0f);
        wallLCol.size = new Vector2(2f, wallHeight);
        wallL.layer = LayerMask.NameToLayer("Ground");

        var wallR = new GameObject("WallRight");
        var wallRCol = wallR.AddComponent<BoxCollider2D>();
        wallR.transform.position = new Vector3(mapMaxX + 1f, wallCenterY, 0f);
        wallRCol.size = new Vector2(2f, wallHeight);
        wallR.layer = LayerMask.NameToLayer("Ground");

        DestroyExisting("DeathZone");
        var dzGO = new GameObject("DeathZone");
        dzGO.tag = "Untagged";
        var dzCol = dzGO.AddComponent<BoxCollider2D>();
        dzCol.isTrigger = true;
        dzGO.transform.position = new Vector3((mapMinX + mapMaxX) * 0.5f, mapMinY - 5f, 0f);
        dzCol.size = new Vector2(mapMaxX - mapMinX + 20f, 4f);
        dzGO.AddComponent<DeathZone>();
    }
}
