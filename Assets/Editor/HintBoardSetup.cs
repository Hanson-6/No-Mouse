using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HintBoardSetup
{
    private const string TexturePausePanel = "Assets/Textures/Buttons/PausePanel.png";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string HintBoardPrefabPath = "Assets/Prefabs/HintBoard.prefab";

    [MenuItem("Tools/Hint Board/1. Build HintBoard Prefab")]
    public static void BuildHintBoardPrefab()
    {
        ConfigureSpriteImport(TexturePausePanel);

        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePausePanel);
        if (panelSprite == null)
        {
            Debug.LogError($"[HintBoardSetup] 找不到提示板背景图片：{TexturePausePanel}");
            return;
        }

        GameObject board = CreateHintBoardObject(panelSprite);

        Directory.CreateDirectory(PrefabFolder);
        PrefabUtility.SaveAsPrefabAsset(board, HintBoardPrefabPath);
        Object.DestroyImmediate(board);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[HintBoardSetup] HintBoard prefab 已生成：{HintBoardPrefabPath}");
    }

    [MenuItem("Tools/Hint Board/2. Place HintBoard")]
    public static void PlaceHintBoard()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[HintBoardSetup] 请先打开一个场景。");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HintBoardPrefabPath);
        if (prefab == null)
        {
            BuildHintBoardPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HintBoardPrefabPath);
        }

        if (prefab == null)
        {
            Debug.LogError("[HintBoardSetup] HintBoard prefab 创建失败。请先检查控制台报错。");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = GetUniqueRootName(scene, "HintBoard");
        PositionAtView(instance);

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[HintBoardSetup] 已放置 HintBoard。可在 Inspector 中修改 Hint Text。");
    }

    private static GameObject CreateHintBoardObject(Sprite panelSprite)
    {
        GameObject root = new GameObject("HintBoard");

        GameObject canvasGO = new GameObject(
            "BoardCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 460f);
        canvasRect.localScale = Vector3.one * 0.01f;
        canvasRect.localPosition = Vector3.zero;

        GameObject panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelGO.GetComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;
        panelImage.preserveAspect = false;

        GameObject textGO = new GameObject("HintText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(panelGO.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.12f, 0.16f);
        textRect.anchorMax = new Vector2(0.88f, 0.84f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text hintText = textGO.GetComponent<Text>();
        hintText.text = "Write your hint here";
        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 46;
        hintText.color = new Color(0.16f, 0.11f, 0.08f, 1f);
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;

        HintBoard board = root.AddComponent<HintBoard>();
        SerializedObject so = new SerializedObject(board);
        so.FindProperty("hintLabel").objectReferenceValue = hintText;
        so.FindProperty("hintText").stringValue = "Write your hint here";
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static void PositionAtView(GameObject go)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y + 1f, 0f);
            return;
        }

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            Vector3 pivot = view.pivot;
            go.transform.position = new Vector3(pivot.x, pivot.y, 0f);
            return;
        }

        go.transform.position = Vector3.zero;
    }

    private static string GetUniqueRootName(Scene scene, string baseName)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith(baseName))
                count++;
        }

        return count == 0 ? baseName : $"{baseName} ({count})";
    }

    private static void ConfigureSpriteImport(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[HintBoardSetup] 找不到图片：{assetPath}");
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
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}
