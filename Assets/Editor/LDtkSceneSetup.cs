using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LDtkSceneSetup
{
    [MenuItem("Tools/Create LDtk Test Scene")]
    public static void CreateLDtkTestScene()
    {
        // Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // Save it first so it has a path
        string scenePath = "Assets/Scenes/LDtkLevel.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        
        // --- Camera ---
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        // LDtk level is 1456x456 px at PPU=16 => ~91 x 28.5 world units
        // orthographicSize = half the vertical extent we want to see
        cam.orthographicSize = 15f; // shows ~30 units vertically, good starting point
        cam.backgroundColor = new Color(0.157f, 0.173f, 0.204f, 1f); // dark blue-grey, similar to LDtk bgColor #283046
        cam.clearFlags = CameraClearFlags.SolidColor;
        // Position camera at roughly the center of the level
        // Level worldX=0, worldY=-136 in LDtk pixel coords
        // In Unity: x = (1456/2)/16 = 45.5, y = -(-136 + 456/2)/16 = (136 - 228)/16... 
        // LDtk Y is inverted in Unity. Let's just place at a reasonable starting point.
        // The LDtkToUnity importer handles coordinate conversion. 
        // Level_0 will be at some position - let's center roughly.
        camGO.transform.position = new Vector3(45f, -10f, -10f);
        
        // --- Load and instantiate the LDtk project ---
        string ldtkPath = "Assets/IDTK/Levels.ldtk";
        var ldtkAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ldtkPath);
        if (ldtkAsset != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(ldtkAsset);
            instance.name = "Levels";
            Debug.Log($"[LDtkSceneSetup] Instantiated LDtk project. Root children: {instance.transform.childCount}");
            
            // Auto-center camera on all renderers in the LDtk instance
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                
                camGO.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
                // Set ortho size to fit the level height with some margin
                cam.orthographicSize = bounds.extents.y * 1.1f;
                
                Debug.Log($"[LDtkSceneSetup] Level bounds center: {bounds.center}, size: {bounds.size}");
                Debug.Log($"[LDtkSceneSetup] Camera positioned at {camGO.transform.position}, orthoSize={cam.orthographicSize:F1}");
            }
            else
            {
                Debug.LogWarning("[LDtkSceneSetup] No renderers found. Camera may need manual adjustment.");
            }
            
            // Log hierarchy for debugging
            foreach (Transform child in instance.transform)
            {
                Debug.Log($"[LDtkSceneSetup] Child: '{child.name}' at {child.position}");
                foreach (Transform grandchild in child)
                    Debug.Log($"[LDtkSceneSetup]   - '{grandchild.name}' at {grandchild.position}");
            }
        }
        else
        {
            Debug.LogError($"[LDtkSceneSetup] Could not load LDtk asset at '{ldtkPath}'. Make sure the file exists and imports correctly.");
        }
        
        // Save the scene
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[LDtkSceneSetup] Scene saved to {scenePath}");
    }
}
