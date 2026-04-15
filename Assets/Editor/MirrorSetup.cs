using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MirrorSetup
{
    private const string MirrorTexturePath = "Assets/mirror_transparent.png";
    private const string ShadowTexturePath = "Assets/Pixel Adventure 1/Assets/Other/Shadow.png";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MirrorPrefabPath = "Assets/Prefabs/Mirror.prefab";
    private const float TargetMirrorHeight = 3.4f;
    private const float TargetMirrorWallThickness = 0.24f;

    [MenuItem("Tools/Mirror/1. Build Mirror Prefab")]
    public static void BuildMirrorPrefab()
    {
        ConfigureSpriteImport(MirrorTexturePath);
        ConfigureSpriteImport(ShadowTexturePath);

        Sprite mirrorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MirrorTexturePath);
        Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowTexturePath);
        if (mirrorSprite == null)
        {
            Debug.LogError($"[MirrorSetup] Missing mirror sprite: {MirrorTexturePath}");
            return;
        }

        if (shadowSprite == null)
        {
            Debug.LogError($"[MirrorSetup] Missing shadow sprite: {ShadowTexturePath}");
            return;
        }

        GameObject mirror = CreateMirrorObject(mirrorSprite, shadowSprite);

        Directory.CreateDirectory(PrefabFolder);
        PrefabUtility.SaveAsPrefabAsset(mirror, MirrorPrefabPath);
        Object.DestroyImmediate(mirror);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MirrorSetup] Mirror prefab generated: {MirrorPrefabPath}");
    }

    [MenuItem("Tools/Mirror/2. Place Mirror")]
    public static void PlaceMirror()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[MirrorSetup] Open a scene first.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorPrefabPath);
        if (prefab == null)
        {
            BuildMirrorPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorPrefabPath);
        }

        if (prefab == null)
        {
            Debug.LogError("[MirrorSetup] Mirror prefab build failed.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = GetUniqueRootName(scene, "Mirror");
        PositionAtView(instance);

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[MirrorSetup] Mirror placed in current scene.");
    }

    private static GameObject CreateMirrorObject(Sprite mirrorSprite, Sprite shadowSprite)
    {
        GameObject root = new GameObject("Mirror");

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            root.layer = groundLayer;

        SpriteRenderer mirrorRenderer = root.AddComponent<SpriteRenderer>();
        mirrorRenderer.sprite = mirrorSprite;
        mirrorRenderer.sortingOrder = 2;

        Vector2 spriteSize = mirrorSprite.bounds.size;
        float spriteHeight = Mathf.Max(spriteSize.y, 0.01f);
        float uniformScale = TargetMirrorHeight / spriteHeight;
        root.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

        BoxCollider2D mirrorCollider = root.AddComponent<BoxCollider2D>();
        float localColliderWidth = Mathf.Max(0.02f, TargetMirrorWallThickness / uniformScale);
        mirrorCollider.size = new Vector2(localColliderWidth, spriteHeight);

        GameObject leftZone = new GameObject("ZoneLeft");
        leftZone.transform.SetParent(root.transform, false);
        SpriteRenderer leftZoneRenderer = leftZone.AddComponent<SpriteRenderer>();
        leftZoneRenderer.sprite = shadowSprite;

        GameObject rightZone = new GameObject("ZoneRight");
        rightZone.transform.SetParent(root.transform, false);
        SpriteRenderer rightZoneRenderer = rightZone.AddComponent<SpriteRenderer>();
        rightZoneRenderer.sprite = shadowSprite;

        MirrorController controller = root.AddComponent<MirrorController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("mirrorRenderer").objectReferenceValue = mirrorRenderer;
        so.FindProperty("leftZoneRenderer").objectReferenceValue = leftZoneRenderer;
        so.FindProperty("rightZoneRenderer").objectReferenceValue = rightZoneRenderer;
        so.FindProperty("shadowSprite").objectReferenceValue = shadowSprite;
        so.FindProperty("horizontalRange").floatValue = 5f;
        so.FindProperty("verticalRange").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static void PositionAtView(GameObject go)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
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
            Debug.LogWarning($"[MirrorSetup] Missing texture import target: {assetPath}");
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

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            needsReimport = true;
        }

        if (needsReimport)
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}
