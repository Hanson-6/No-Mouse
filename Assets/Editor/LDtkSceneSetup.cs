using LDtkUnity;
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
        cam.orthographicSize = 10f;
        cam.backgroundColor = new Color(0.157f, 0.173f, 0.204f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        
        // --- Load and instantiate the LDtk project ---
        string ldtkPath = "Assets/IDTK/Levels.ldtk";
        var ldtkAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ldtkPath);
        GameObject ldtkInstance = null;
        if (ldtkAsset != null)
        {
            ldtkInstance = (GameObject)PrefabUtility.InstantiatePrefab(ldtkAsset);
            ldtkInstance.name = "Levels";
            Debug.Log($"[LDtkSceneSetup] Instantiated LDtk project. Root children: {ldtkInstance.transform.childCount}");
            
            // Auto-center camera on all renderers in the LDtk instance
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
        Vector3 spawnPos = new Vector3(16.75f, -4.75f, 0f); // known position + 1 unit above floor
        if (ldtkInstance != null)
        {
            // LDtkComponentEntity is on every imported entity GameObject
            var entities = ldtkInstance.GetComponentsInChildren<LDtkComponentEntity>(true);
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
}
