using LDtkUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools → Setup Tutoring UI   : 一次性创建 HintCanvas + Background（只需运行一次）
/// Tools → Create Hint Trigger : 在场景中新增一个 HintTrigger，可多次运行、自由摆放
/// </summary>
public static class TutoringSetup
{
    private const string TEX_MOVING_RULE   = "Assets/moving_rule.png";
    private const string TEX_CAVE_SPRITES2 = "Assets/CaveAssets/Spritesheets/Decorations/CaveDetailSprites2.png";
    private const string TRIGGER_PREFAB_PATH = "Assets/Prefabs/HintTrigger.prefab";

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

        // 内容面板（右侧）
        var panelGO = new GameObject("ContentPanel");
        panelGO.transform.SetParent(hintRootGO.transform, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.55f, 0.15f);
        panelRect.anchorMax = new Vector2(0.95f, 0.85f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        // 深色背景 + 蓝色边框 + 内层深色
        AddLayeredBackground(panelGO);

        // HintImage
        var hintImgGO = new GameObject("HintImage");
        hintImgGO.transform.SetParent(panelGO.transform, false);
        var hintRect = hintImgGO.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0.1f);
        hintRect.anchorMax = Vector2.one;
        hintRect.offsetMin = new Vector2(16f, 8f);
        hintRect.offsetMax = new Vector2(-16f, -50f);
        var hintImg = hintImgGO.AddComponent<Image>();
        hintImg.sprite = movingRuleSprite;
        hintImg.preserveAspect = true;

        // 关闭按钮
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(panelGO.transform, false);
        var cbRect = closeBtnGO.AddComponent<RectTransform>();
        cbRect.anchorMin = cbRect.anchorMax = cbRect.pivot = Vector2.one;
        cbRect.sizeDelta = new Vector2(60f, 60f);
        cbRect.anchoredPosition = new Vector2(-8f, -8f);
        closeBtnGO.AddComponent<Image>().color = new Color(0.75f, 0.15f, 0.15f, 1f);
        closeBtnGO.AddComponent<Button>();
        var closeTxt = AddText(closeBtnGO, "×", 36, Color.white);
        closeTxt.alignment = TextAnchor.MiddleCenter;

        // 底部提示文字
        var tipGO = new GameObject("TipText");
        tipGO.transform.SetParent(panelGO.transform, false);
        var tipRect = tipGO.AddComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0f, 0f);
        tipRect.anchorMax = new Vector2(1f, 0.1f);
        tipRect.offsetMin = new Vector2(16f, 8f);
        tipRect.offsetMax = new Vector2(-16f, -4f);
        var tipTxt = AddText(tipGO, "按任意键关闭", 18, new Color(0.6f, 0.6f, 0.6f, 1f));
        tipTxt.alignment = TextAnchor.MiddleCenter;

        hintRootGO.SetActive(false);

        // ── LDtk 地形（如果场景里没有就补上）────────────────────────────────
        bool hasLevels = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Levels") { hasLevels = true; break; }

        if (!hasLevels)
        {
            const string ldtkPath = "Assets/IDTK/Tutoring.ldtk";
            var ldtkAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ldtkPath);
            if (ldtkAsset != null)
            {
                var ldtkInst = (GameObject)PrefabUtility.InstantiatePrefab(ldtkAsset);
                ldtkInst.name = "Levels";
                // 只显示 Tutoring 关卡
                foreach (Transform child in ldtkInst.transform)
                {
                    var world = child.GetComponent<LDtkUnity.LDtkComponentWorld>();
                    if (world != null)
                    {
                        foreach (Transform level in child)
                            level.gameObject.SetActive(level.name == "Tutoring");
                        break;
                    }
                }
                Debug.Log("[TutoringSetup] LDtk 地形已加入场景。");
            }
            else
            {
                Debug.LogWarning($"[TutoringSetup] 找不到 {ldtkPath}，请确认文件存在。");
            }
        }

        // ── 背景 ─────────────────────────────────────────────────────────────
        DestroyExisting("Background");
        var bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Background.prefab");
        if (bgPrefab != null)
        {
            var bgInst = (GameObject)PrefabUtility.InstantiatePrefab(bgPrefab);
            bgInst.transform.SetAsFirstSibling();
        }
        else
        {
            Debug.LogWarning("[TutoringSetup] 找不到 Background.prefab，请先在其他场景运行 Tools/Setup Background。");
        }

        // ── 相机偏移 + 边界 ───────────────────────────────────────────────────
        // Tutoring 关卡 Unity 世界坐标范围（从 LDtk 读取，PPU=16）
        // worldX=2544, pxWid=1072 → x: 159 ~ 226
        // worldY=-1008, pxHei=504 → y: 63 ~ 31.5
        const float mapMinX = 159f, mapMaxX = 226f;
        const float mapMinY = 31.5f, mapMaxY = 63f;

        foreach (var root in scene.GetRootGameObjects())
        {
            var cf = root.GetComponent<CameraFollow>() ?? root.GetComponentInChildren<CameraFollow>();
            if (cf == null) continue;

            var camComp = root.GetComponent<Camera>() ?? root.GetComponentInChildren<Camera>();
            float orthoSize = camComp != null ? camComp.orthographicSize : 10f;
            float aspect    = 16f / 9f; // 目标分辨率 1920×1080
            float halfW = orthoSize * aspect;
            float halfH = orthoSize;

            var so = new SerializedObject(cf);
            so.FindProperty("offset").vector2Value = new Vector2(5f, 2f);
            so.FindProperty("useBounds").boolValue = true;
            // 相机中心不能超出地图范围（留出半屏空间）
            so.FindProperty("minX").floatValue = mapMinX + halfW;
            so.FindProperty("maxX").floatValue = mapMaxX - halfW;
            // Y 方向：若地图高度 < 相机高度则锁定 Y 中央
            float mapCenterY = (mapMinY + mapMaxY) * 0.5f;
            so.FindProperty("minY").floatValue = (mapMaxY - mapMinY) > halfH * 2f
                ? mapMinY + halfH : mapCenterY;
            so.FindProperty("maxY").floatValue = (mapMaxY - mapMinY) > halfH * 2f
                ? mapMaxY - halfH : mapCenterY;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[TutoringSetup] 相机边界设置：X({mapMinX + halfW:F1}~{mapMaxX - halfW:F1}) Y({mapMinY + halfH:F1}~{mapMaxY - halfH:F1})");
            break;
        }

        // ── 死亡区（地图底部下方）────────────────────────────────────────────
        DestroyExisting("DeathZone");
        var dzGO = new GameObject("DeathZone");
        dzGO.tag = "Untagged";
        // 宽度覆盖整个地图，位置在底部下方 5 units
        var dzCol = dzGO.AddComponent<BoxCollider2D>();
        dzCol.isTrigger = true;
        dzGO.transform.position = new Vector3((mapMinX + mapMaxX) * 0.5f, mapMinY - 5f, 0f);
        dzCol.size = new Vector2(mapMaxX - mapMinX + 20f, 4f);
        dzGO.AddComponent<DeathZone>();
        Debug.Log($"[TutoringSetup] DeathZone 已创建，位置 y={mapMinY - 5f}，宽度 {mapMaxX - mapMinX + 20f}。");

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
}
