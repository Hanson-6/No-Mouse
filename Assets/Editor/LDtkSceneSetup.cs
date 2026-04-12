using LDtkUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LDtkSceneSetup
{
    private const string AutoReloadPrefKey = "LDtkSceneSetup_AutoReload";
    private const string AutoReloadMenuPath = "Tools/Auto-Reload LDtk Terrain on Save";

    [MenuItem("Tools/Create LDtk Level 1 Scene")]
    public static void CreateLevel1Scene()
    {
        CreateLDtkScene("Assets/Scenes/Level1.unity", "Level_0");
    }

    [MenuItem("Tools/Create LDtk Level 2 Scene")]
    public static void CreateLevel2Scene()
    {
        CreateLDtkScene("Assets/Scenes/Level2.unity", "Level_1");
    }

    [MenuItem("Tools/Create Tutoring Scene")]
    public static void CreateTutoringScene()
    {
        const string scenePath = "Assets/Scenes/Tutoring.unity";
        const string ldtkPath  = "Assets/IDTK/Tutoring.ldtk";

        // 场景已存在 → 只刷新地形，保留所有已放置物体
        if (System.IO.File.Exists(scenePath))
        {
            var existingScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (existingScene.path != scenePath)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            }
            ReloadLDtkInCurrentScene(forceReimport: true, ldtkPathOverride: ldtkPath);
            Debug.Log("[LDtkSceneSetup] Tutoring 场景已存在，只刷新了地形。已放置物体保留不变。");
            return;
        }

        // 场景不存在 → 全新创建
        CreateLDtkScene(scenePath, "Tutoring", ldtkPath);
    }

    [MenuItem("Tools/Force Switch Tutoring to Tutoring.ldtk")]
    public static void ForceSwitchTutoringLdtk()
    {
        const string ldtkPath = "Assets/IDTK/Tutoring.ldtk";

        // Force reimport first
        AssetDatabase.ImportAsset(ldtkPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var ldtkAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ldtkPath);
        if (ldtkAsset == null)
        {
            Debug.LogError($"[LDtkSceneSetup] 无法加载 Tutoring.ldtk，请检查 Console 里的导入错误。");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();

        // Find and destroy any existing "Levels" object
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Levels")
            {
                Object.DestroyImmediate(root);
                Debug.Log("[LDtkSceneSetup] 删除了旧的 Levels 对象。");
                break;
            }
        }

        // Instantiate Tutoring.ldtk
        var newLdtk = (GameObject)PrefabUtility.InstantiatePrefab(ldtkAsset);
        newLdtk.name = "Levels";

        // Show only Tutoring level, hide others
        Transform worldTransform = newLdtk.transform;
        foreach (Transform child in newLdtk.transform)
        {
            if (child.GetComponent<LDtkUnity.LDtkComponentWorld>() != null)
            {
                worldTransform = child;
                break;
            }
        }
        foreach (Transform child in worldTransform)
        {
            child.gameObject.SetActive(child.name == "Tutoring");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[LDtkSceneSetup] 成功切换到 Tutoring.ldtk！请 Ctrl+S 保存场景。");
    }

    [MenuItem("Tools/Create LDtk Test Scene")]
    public static void CreateLDtkTestScene()
    {
        CreateLDtkScene("Assets/Scenes/LDtkLevel.unity", null);
    }

    [MenuItem("Tools/Reload LDtk Terrain (Keep Player & Prefabs)")]
    public static void ReloadLDtkTerrain()
    {
        ReloadLDtkInCurrentScene(forceReimport: true);
    }

    // --- Auto-reload toggle ---
    [MenuItem(AutoReloadMenuPath)]
    public static void ToggleAutoReload()
    {
        bool current = EditorPrefs.GetBool(AutoReloadPrefKey, true);
        EditorPrefs.SetBool(AutoReloadPrefKey, !current);
        Debug.Log($"[LDtkSceneSetup] Auto-reload on LDtk save: {(!current ? "ON" : "OFF")}");
    }

    [MenuItem(AutoReloadMenuPath, true)]
    public static bool ToggleAutoReloadValidate()
    {
        Menu.SetChecked(AutoReloadMenuPath, EditorPrefs.GetBool(AutoReloadPrefKey, true));
        return true;
    }

    public static bool IsAutoReloadEnabled => EditorPrefs.GetBool(AutoReloadPrefKey, true);

    // levelIdentifier: LDtk level name (e.g. "Level_0", "Level_1"). Pass null to show all levels.
    // ldtkPath: optional override for the LDtk project file (defaults to Assets/IDTK/Levels.ldtk)
    private static void CreateLDtkScene(string scenePath, string levelIdentifier, string ldtkPath = "Assets/IDTK/Levels.ldtk")
    {
        // Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Save it first so it has a path
        EditorSceneManager.SaveScene(scene, scenePath);
        
        // --- Camera ---
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.backgroundColor = new Color(0.157f, 0.173f, 0.204f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        
        // --- Load and instantiate the LDtk project ---
        var ldtkAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ldtkPath);
        GameObject ldtkInstance = null;
        Transform targetLevelTransform = null;

        if (ldtkAsset != null)
        {
            ldtkInstance = (GameObject)PrefabUtility.InstantiatePrefab(ldtkAsset);
            ldtkInstance.name = "Levels";
            Debug.Log($"[LDtkSceneSetup] Instantiated LDtk project. Root children: {ldtkInstance.transform.childCount}");

            // LDtk hierarchy: Levels > World > Level_0, Level_1, ...
            // Find the World container (first child with LDtkComponentWorld)
            Transform worldTransform = null;
            foreach (Transform child in ldtkInstance.transform)
            {
                if (child.GetComponent<LDtkComponentWorld>() != null)
                {
                    worldTransform = child;
                    break;
                }
            }

            if (worldTransform == null)
            {
                // Fallback: maybe the levels are direct children of root
                worldTransform = ldtkInstance.transform;
                Debug.LogWarning("[LDtkSceneSetup] No LDtkComponentWorld found; assuming levels are direct children of root.");
            }

            // Show only the specified level, hide others
            if (!string.IsNullOrEmpty(levelIdentifier))
            {
                foreach (Transform child in worldTransform)
                {
                    bool isTarget = child.name == levelIdentifier;
                    child.gameObject.SetActive(isTarget);
                    if (isTarget)
                        targetLevelTransform = child;
                }

                if (targetLevelTransform != null)
                    Debug.Log($"[LDtkSceneSetup] Showing only level: {levelIdentifier}");
                else
                    Debug.LogWarning($"[LDtkSceneSetup] Level '{levelIdentifier}' not found among {worldTransform.childCount} world children.");
            }
            
            // Auto-center camera on all renderers in the active hierarchy
            var renderers = ldtkInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                
                camGO.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
                cam.orthographicSize = bounds.extents.y * 1.1f;
                
                Debug.Log($"[LDtkSceneSetup] Level bounds center: {bounds.center}, size: {bounds.size}");
                Debug.Log($"[LDtkSceneSetup] Camera positioned at {camGO.transform.position}, orthoSize={cam.orthographicSize:F1}");
            }
            else
            {
                Debug.LogWarning("[LDtkSceneSetup] No renderers found. Camera may need manual adjustment.");
            }
        }
        else
        {
            Debug.LogError($"[LDtkSceneSetup] Could not load LDtk asset at '{ldtkPath}'. Make sure the file exists and imports correctly.");
        }

        // --- Determine player spawn position from the PlayerStartPoint entity ---
        // Default fallback if entity is not found
        Vector3 spawnPos = new Vector3(16.75f, -4.75f, 0f);
        if (ldtkInstance != null)
        {
            // Search only within the target level if one is specified, otherwise search the whole prefab
            Transform searchRoot = targetLevelTransform != null ? targetLevelTransform : ldtkInstance.transform;
            var entities = searchRoot.GetComponentsInChildren<LDtkComponentEntity>(true);
            foreach (var entity in entities)
            {
                if (entity.Identifier == "PlayerStartPoint")
                {
                    // Spawn 1 unit above the entity so the player doesn't clip into the floor
                    spawnPos = entity.transform.position + new Vector3(0f, 1f, 0f);
                    Debug.Log($"[LDtkSceneSetup] Found PlayerStartPoint entity at {entity.transform.position}. Spawn: {spawnPos}");
                    break;
                }
            }
        }

        // --- GameManager (required by PlayerController.Respawn) ---
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<GameManager>();
        // Create a RespawnPoint child so GameManager has a valid respawn transform
        var respawnGO = new GameObject("RespawnPoint");
        respawnGO.transform.SetParent(gmGO.transform);
        respawnGO.transform.position = spawnPos;
        // Wire the respawn point reference via SerializedObject so it survives serialization
        var gmSO = new SerializedObject(gmGO.GetComponent<GameManager>());
        gmSO.FindProperty("respawnPoint").objectReferenceValue = respawnGO.transform;
        gmSO.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[LDtkSceneSetup] GameManager created. RespawnPoint at {spawnPos}");

        // --- Player ---
        string playerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab != null)
        {
            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.transform.position = spawnPos;
            playerInstance.transform.localScale = new Vector3(5f, 5f, 1f);
            Debug.Log($"[LDtkSceneSetup] Player instantiated at {spawnPos}, scale=(5,5,1)");

            // Wire CameraFollow if it exists on the camera
            var camFollow = camGO.GetComponent<CameraFollow>();
            if (camFollow == null)
                camFollow = camGO.AddComponent<CameraFollow>();
            var camFollowSO = new SerializedObject(camFollow);
            camFollowSO.FindProperty("target").objectReferenceValue = playerInstance.transform;
            camFollowSO.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError($"[LDtkSceneSetup] Could not load Player prefab at '{playerPrefabPath}'.");
        }
        
        // Save the scene
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[LDtkSceneSetup] Scene saved to {scenePath}");
    }

    /// <summary>
    /// Reloads only the LDtk terrain prefab instance in the current scene,
    /// preserving the Player, Camera, GameManager, and all other manually-placed objects.
    /// This forces collision data (CompositeCollider2D paths) to regenerate from the updated LDtk asset.
    /// </summary>
    /// <param name="forceReimport">If true, force-reimports the LDtk asset first. 
    /// Set to false when called from AssetPostprocessor (asset was already just reimported).</param>
    public static void ReloadLDtkInCurrentScene(bool forceReimport = true, string ldtkPathOverride = null)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[LDtkSceneSetup] No valid scene is loaded.");
            return;
        }

        // Find the existing LDtk instance in the scene (named "Levels")
        GameObject oldLdtk = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Levels")
            {
                oldLdtk = root;
                break;
            }
        }

        if (oldLdtk == null)
        {
            Debug.LogError("[LDtkSceneSetup] Could not find 'Levels' GameObject in the current scene. " +
                           "Make sure the scene was created with 'Create LDtk Level X Scene'.");
            return;
        }

        // --- Record which levels were active/inactive before we destroy the old instance ---
        // LDtk hierarchy: Levels > World > Level_0, Level_1, ...
        Transform oldWorldTransform = null;
        foreach (Transform child in oldLdtk.transform)
        {
            if (child.GetComponent<LDtkComponentWorld>() != null)
            {
                oldWorldTransform = child;
                break;
            }
        }
        if (oldWorldTransform == null)
            oldWorldTransform = oldLdtk.transform;

        // Store active state per level name
        var levelActiveStates = new System.Collections.Generic.Dictionary<string, bool>();
        foreach (Transform child in oldWorldTransform)
        {
            levelActiveStates[child.name] = child.gameObject.activeSelf;
        }

        int siblingIndex = oldLdtk.transform.GetSiblingIndex();

        // Record the prefab asset path from the existing instance
        string ldtkPath = ldtkPathOverride ?? "Assets/IDTK/Levels.ldtk";
        if (ldtkPathOverride == null)
        {
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(oldLdtk);
            if (prefabSource != null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(prefabSource);
                if (!string.IsNullOrEmpty(sourcePath))
                    ldtkPath = sourcePath;
            }
        }

        // --- Force reimport the LDtk asset to ensure Unity picks up latest changes ---
        if (forceReimport)
        {
            AssetDatabase.ImportAsset(ldtkPath, ImportAssetOptions.ForceUpdate);
        }

        // --- Destroy the old instance ---
        Undo.RegisterCompleteObjectUndo(oldLdtk, "Reload LDtk Terrain");
        Object.DestroyImmediate(oldLdtk);
        Debug.Log("[LDtkSceneSetup] Destroyed old 'Levels' instance.");

        // --- Instantiate a fresh copy from the (reimported) LDtk prefab ---
        var ldtkAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ldtkPath);
        if (ldtkAsset == null)
        {
            Debug.LogError($"[LDtkSceneSetup] Could not load LDtk asset at '{ldtkPath}'.");
            return;
        }

        var newLdtk = (GameObject)PrefabUtility.InstantiatePrefab(ldtkAsset);
        newLdtk.name = "Levels";
        newLdtk.transform.SetSiblingIndex(siblingIndex);

        // --- Restore level active/inactive states ---
        Transform newWorldTransform = null;
        foreach (Transform child in newLdtk.transform)
        {
            if (child.GetComponent<LDtkComponentWorld>() != null)
            {
                newWorldTransform = child;
                break;
            }
        }
        if (newWorldTransform == null)
            newWorldTransform = newLdtk.transform;

        foreach (Transform child in newWorldTransform)
        {
            if (levelActiveStates.TryGetValue(child.name, out bool wasActive))
            {
                child.gameObject.SetActive(wasActive);
            }
        }

        // --- Force composite collider regeneration ---
        // Touch all CompositeCollider2D components so Unity rebuilds their geometry
        var composites = newLdtk.GetComponentsInChildren<UnityEngine.CompositeCollider2D>(true);
        foreach (var composite in composites)
        {
            composite.GenerateGeometry();
        }

        Debug.Log($"[LDtkSceneSetup] Re-instantiated 'Levels' from '{ldtkPath}'. " +
                  $"Restored {levelActiveStates.Count} level visibility states. " +
                  $"Regenerated {composites.Length} composite collider(s).");

        // Mark scene dirty so the user can save
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[LDtkSceneSetup] Scene marked dirty. Use Ctrl+S to save. " +
                  "Player, Camera, GameManager, and all other objects are untouched.");
    }
}

/// <summary>
/// Watches for LDtk asset reimports and automatically reloads terrain collisions
/// in the current scene when "Auto-Reload LDtk Terrain on Save" is enabled.
/// </summary>
public class LDtkAutoReloadPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (!LDtkSceneSetup.IsAutoReloadEnabled)
            return;

        bool ldtkChanged = false;
        foreach (var path in importedAssets)
        {
            // Match the LDtk project file (not sub-assets like .ldtkl)
            if (path.EndsWith(".ldtk", System.StringComparison.OrdinalIgnoreCase))
            {
                ldtkChanged = true;
                break;
            }
        }

        if (!ldtkChanged)
            return;

        // Check if the current scene has a "Levels" root object before scheduling
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        bool hasLevels = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Levels")
            {
                hasLevels = true;
                break;
            }
        }

        if (!hasLevels)
            return;

        // Defer the reload to avoid issues with modifying the scene during asset postprocessing
        EditorApplication.delayCall += () =>
        {
            Debug.Log("[LDtkSceneSetup] LDtk asset changed -- auto-reloading terrain collisions...");
            LDtkSceneSetup.ReloadLDtkInCurrentScene(forceReimport: false);
        };
    }
}
