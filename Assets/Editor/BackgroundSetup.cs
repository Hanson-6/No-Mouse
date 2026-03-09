using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools/Setup Background
///
/// Creates a seamless parallax mountain background for the active scene and saves it
/// as Assets/Prefabs/Background.prefab for reuse in future scenes.
///
/// What it does:
///   1. Ensures a "Background" sorting layer exists (rendered behind Default).
///   2. Sets the Main Camera background to sky blue (#87CEEB) with SolidColor clear.
///   3. Builds a Background > MountainLayer hierarchy with ParallaxLayer attached.
///   4. Scales the mountain sprite to fill ~45 % of screen height.
///   5. Saves the result as a prefab.
/// </summary>
public static class BackgroundSetup
{
    const string MountainPath = "Assets/Textures/Mountain.png";
    const string PrefabPath   = "Assets/Prefabs/Background.prefab";
    const string BgLayer      = "Background";

    // #87CEEB — a pure, light sky blue
    static readonly Color SkyBlue = new Color(0.529f, 0.808f, 0.922f, 1f);

    [MenuItem("Tools/Setup Background")]
    public static void Setup()
    {
        // ── 1. Sorting layer ───────────────────────────────────────────────
        EnsureSortingLayer(BgLayer);

        // ── 2. Camera ──────────────────────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Background Setup: Camera");
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyBlue;
            EditorUtility.SetDirty(cam);
        }
        else
        {
            Debug.LogWarning("[BackgroundSetup] No Main Camera in scene — sky colour not applied.");
        }

        // ── 3. Mountain sprite ─────────────────────────────────────────────
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MountainPath);
        if (sprite == null)
        {
            Debug.LogError($"[BackgroundSetup] Sprite not found at: {MountainPath}");
            return;
        }

        // ── 4. Replace any existing Background object ──────────────────────
        var existing = GameObject.Find("Background");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        // ── 5. Build hierarchy ─────────────────────────────────────────────
        var root = new GameObject("Background");
        Undo.RegisterCreatedObjectUndo(root, "Create Background");
        root.transform.SetAsFirstSibling();   // visually behind everything in the hierarchy

        var mountainGO = new GameObject("MountainLayer");
        Undo.RegisterCreatedObjectUndo(mountainGO, "Create MountainLayer");
        mountainGO.transform.SetParent(root.transform, false);

        // Scale mountain so it covers ~45 % of screen height (uniform scale)
        float spriteH      = sprite.bounds.size.y;
        float screenHeight = cam != null ? cam.orthographicSize * 2f : 10f;
        float scale        = (screenHeight * 0.45f) / Mathf.Max(spriteH, 0.001f);
        mountainGO.transform.localScale = new Vector3(scale, scale, 1f);

        // Position: bottom of the mountain aligns with the bottom of the camera view
        float camBottomY = cam != null
            ? cam.transform.position.y - cam.orthographicSize
            : -5f;
        mountainGO.transform.position = new Vector3(
            cam != null ? cam.transform.position.x : 0f,   // start centred on camera
            camBottomY + spriteH * scale * 0.5f,            // vertically bottom-aligned
            0f);

        // SpriteRenderer
        var sr              = mountainGO.AddComponent<SpriteRenderer>();
        sr.sprite           = sprite;
        sr.sortingLayerName = BgLayer;
        sr.sortingOrder     = 0;

        // ParallaxLayer — mountain scrolls at 20 % of camera speed
        var parallax             = mountainGO.AddComponent<ParallaxLayer>();
        parallax.parallaxFactor  = 0.2f;
        parallax.autoScrollSpeed = 0f;

        // ── 6. Save prefab ─────────────────────────────────────────────────
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)!);
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            root, PrefabPath, InteractionMode.UserAction);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[BackgroundSetup] Done. Prefab saved to {PrefabPath}. " +
                  "Save the scene (Ctrl+S) to persist.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="layerName"/> to the project's sorting layers at index 0
    /// (rendered behind the built-in Default layer) if it does not already exist.
    /// </summary>
    static void EnsureSortingLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));

        var sortingLayers = tagManager.FindProperty("m_SortingLayers");

        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            var entry = sortingLayers.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("name").stringValue == layerName)
                return;  // already present
        }

        // Insert at index 0 — renders furthest back
        sortingLayers.InsertArrayElementAtIndex(0);
        var newLayer = sortingLayers.GetArrayElementAtIndex(0);
        newLayer.FindPropertyRelative("name").stringValue  = layerName;
        // Use a stable non-zero hash so the ID is deterministic across machines
        newLayer.FindPropertyRelative("uniqueID").intValue = layerName.GetHashCode() | (1 << 30);

        tagManager.ApplyModifiedProperties();
        Debug.Log($"[BackgroundSetup] Sorting layer '{layerName}' added.");
    }
}
