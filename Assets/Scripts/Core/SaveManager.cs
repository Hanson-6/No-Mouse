using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档管理器。
/// 快照写入 Assets/Snapshots/。
/// - checkpoint_latest.json：永久 checkpoint 存档（仅 checkpoint 触发覆盖）
/// - session_live.json：运行期临时存档（进程退出清理）
/// </summary>
public static class SaveManager
{
    private const string KEY_LEVEL = "SavedLevel";
    private const string KEY_LIVES = "SavedLives";
    private const string KEY_SCORE = "SavedScore";
    private const string KEY_EXISTS = "SaveExists";
    private const string KEY_LATEST_SNAPSHOT = "LatestSnapshot";

    private const int SNAPSHOT_VERSION = 1;
    private const string RIGIDBODY_COMPONENT_TYPE = "UnityEngine.Rigidbody2D";
    private const string SNAPSHOT_FOLDER_NAME = "Snapshots";
    private const string CHECKPOINT_FILE_NAME = "checkpoint_latest.json";
    private const string SESSION_FILE_NAME = "session_live.json";
    private const string MAIN_MENU_SCENE_PATH = "Assets/Scenes/MainMenu.unity";

    private static SnapshotFileData pendingSnapshot;
    private static SnapshotRunner runner;
    private static bool isRestoring;
    private static bool isLoadingSnapshotScene;
    private static bool runtimeHooksRegistered;
    private static bool isApplicationQuitting;

    private static string SnapshotDirectoryPath => Path.Combine(Application.dataPath, SNAPSHOT_FOLDER_NAME);
    private static string CheckpointSnapshotPath => Path.Combine(SnapshotDirectoryPath, CHECKPOINT_FILE_NAME);
    private static string SessionSnapshotPath => Path.Combine(SnapshotDirectoryPath, SESSION_FILE_NAME);

    public static void Save()
    {
        SaveSessionSnapshot();
    }

    public static void SaveSessionSnapshot()
    {
        EnsureSnapshotDirectory();

        if (isApplicationQuitting)
            return;

        if (isRestoring)
        {
            Debug.LogWarning("[SaveManager] 当前正在恢复快照，已跳过 session 保存请求。");
            return;
        }

        SnapshotFileData snapshot = TryCaptureSnapshotForSave("session");
        if (snapshot == null)
            return;

        if (WriteSnapshotFile(snapshot, SessionSnapshotPath, "session"))
            CacheLegacySummary(snapshot, SESSION_FILE_NAME);
    }

    public static void SaveCheckpoint()
    {
        EnsureSnapshotDirectory();

        if (isApplicationQuitting)
            return;

        if (isRestoring)
        {
            Debug.LogWarning("[SaveManager] 当前正在恢复快照，已跳过 checkpoint 保存请求。");
            return;
        }

        SnapshotFileData snapshot = TryCaptureSnapshotForSave("checkpoint");
        if (snapshot == null)
            return;

        bool checkpointSaved = WriteSnapshotFile(snapshot, CheckpointSnapshotPath, "checkpoint");
        bool sessionSaved = WriteSnapshotFile(snapshot, SessionSnapshotPath, "session");
        if (checkpointSaved || sessionSaved)
            CacheLegacySummary(snapshot, CHECKPOINT_FILE_NAME);
    }

    public static void Load()
    {
        SnapshotFileData snapshot = LoadContinueSnapshot();
        if (snapshot != null)
        {
            ApplyGameData(snapshot.gameData);
            Debug.Log($"[SaveManager] 已读取快照数据: Scene={snapshot.sceneBuildIndex}, Lives={snapshot.gameData.lives}, Score={snapshot.gameData.score}");
            return;
        }

        GameData.CurrentLevel = PlayerPrefs.GetInt(KEY_LEVEL, 1);
        GameData.Lives = PlayerPrefs.GetInt(KEY_LIVES, 3);
        GameData.Score = PlayerPrefs.GetInt(KEY_SCORE, 0);
        Debug.Log($"[SaveManager] 已读取兼容存档: Level={GameData.CurrentLevel}, Lives={GameData.Lives}, Score={GameData.Score}");
    }

    public static bool ContinueFromLatestSnapshot()
    {
        EnsureSnapshotDirectory();

        if (isLoadingSnapshotScene || pendingSnapshot != null)
        {
            Debug.LogWarning("[SaveManager] Continue 请求被忽略：已有快照加载/恢复在进行中。");
            return true;
        }

        SnapshotFileData snapshot = LoadContinueSnapshot();
        if (snapshot == null)
        {
            Debug.LogWarning("[SaveManager] Continue 失败：未找到快照文件。");
            return false;
        }

        return ContinueFromSnapshot(snapshot);
    }

    public static bool ContinueFromCheckpointSnapshot()
    {
        EnsureSnapshotDirectory();

        if (isLoadingSnapshotScene || pendingSnapshot != null)
        {
            Debug.LogWarning("[SaveManager] BackToCheckpoint 请求被忽略：已有快照加载/恢复在进行中。");
            return true;
        }

        SnapshotFileData snapshot = LoadSnapshotByPath(CheckpointSnapshotPath, "checkpoint");
        if (snapshot == null)
        {
            Debug.LogWarning("[SaveManager] BackToCheckpoint 失败：未找到 checkpoint 快照文件。");
            return false;
        }

        return ContinueFromSnapshot(snapshot);
    }

    public static bool HasCheckpointSave()
    {
        EnsureSnapshotDirectory();
        return HasCheckpointSnapshot();
    }

    public static void ClearSessionSnapshot()
    {
        EnsureSnapshotDirectory();
        DeleteSnapshotFileWithMeta(SessionSnapshotPath);
    }

    public static void ClearCheckpointSnapshot()
    {
        EnsureSnapshotDirectory();
        DeleteSnapshotFileWithMeta(CheckpointSnapshotPath);
        GameData.ClearDarkMode();
    }

    public static void SaveCurrentSessionLive()
    {
        SaveSessionSnapshot();
    }

    public static void SaveAndQuit()
    {
        SaveSessionSnapshot();
    }

    private static bool ContinueFromSnapshot(SnapshotFileData snapshot)
    {
        if (snapshot == null)
            return false;

        string scenePath = snapshot.scenePath;
        int pathBuildIndex = !string.IsNullOrEmpty(scenePath)
            ? SceneUtility.GetBuildIndexByScenePath(scenePath)
            : -1;

        int snapshotBuildIndex = snapshot.sceneBuildIndex;
        bool indexValid = snapshotBuildIndex >= 0 && snapshotBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (pathBuildIndex < 0 && !indexValid)
        {
            Debug.LogWarning($"[SaveManager] Continue 失败：快照场景不可用。scenePath='{scenePath}', sceneBuildIndex={snapshotBuildIndex}, buildCount={SceneManager.sceneCountInBuildSettings}");
            return false;
        }

        ApplyGameData(snapshot.gameData);
        pendingSnapshot = snapshot;
        isLoadingSnapshotScene = true;

        EnsureRunner();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (pathBuildIndex >= 0)
        {
            Debug.Log($"[SaveManager] Continue：按场景路径加载快照 scenePath='{scenePath}'。");
            SceneManager.LoadScene(scenePath);
        }
        else
        {
            Debug.Log($"[SaveManager] Continue：按场景索引加载快照 sceneBuildIndex={snapshotBuildIndex}。");
            SceneManager.LoadScene(snapshotBuildIndex);
        }

        return true;
    }

    private static SnapshotFileData LoadContinueSnapshot()
    {
        SnapshotFileData session = LoadSnapshotByPath(SessionSnapshotPath, "session");
        if (session != null)
            return session;

        return LoadSnapshotByPath(CheckpointSnapshotPath, "checkpoint");
    }

    private static bool HasSessionSnapshot()
    {
        return LoadSnapshotByPath(SessionSnapshotPath, "session") != null;
    }

    private static bool HasCheckpointSnapshot()
    {
        return LoadSnapshotByPath(CheckpointSnapshotPath, "checkpoint") != null;
    }

    public static bool HasSave()
    {
        EnsureSnapshotDirectory();
        return HasSessionSnapshot() || HasCheckpointSnapshot();
    }

    public static void DeleteSave()
    {
        EnsureSnapshotDirectory();

        if (Directory.Exists(SnapshotDirectoryPath))
        {
            foreach (string file in Directory.GetFiles(SnapshotDirectoryPath, "snapshot_*.json", SearchOption.TopDirectoryOnly))
                DeleteSnapshotFileWithMeta(file);
        }

        DeleteSnapshotFileWithMeta(SessionSnapshotPath);
        DeleteSnapshotFileWithMeta(CheckpointSnapshotPath);

        PlayerPrefs.DeleteKey(KEY_LEVEL);
        PlayerPrefs.DeleteKey(KEY_LIVES);
        PlayerPrefs.DeleteKey(KEY_SCORE);
        PlayerPrefs.DeleteKey(KEY_EXISTS);
        PlayerPrefs.DeleteKey(KEY_LATEST_SNAPSHOT);
        PlayerPrefs.Save();

        GameData.ClearCheckpoint();
        GameData.ClearDarkMode();

        pendingSnapshot = null;
        isLoadingSnapshotScene = false;
        Debug.Log("[SaveManager] 存档已删除");
    }

    private static SnapshotFileData TryCaptureSnapshotForSave(string label)
    {
        try
        {
            SnapshotFileData snapshot = CaptureSnapshot();
            if (snapshot == null)
            {
                Debug.LogWarning($"[SaveManager] {label} 快照保存失败：无法捕获场景状态。");
                return null;
            }

            return snapshot;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 保存 {label} 快照失败: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    private static bool WriteSnapshotFile(SnapshotFileData snapshot, string path, string label)
    {
        if (snapshot == null || string.IsNullOrEmpty(path))
            return false;

        try
        {
            Directory.CreateDirectory(SnapshotDirectoryPath);
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(path, json);
            Debug.Log($"[SaveManager] {label} 快照已保存: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 写入 {label} 快照失败: {path} ({e.Message})");
            return false;
        }
    }

    private static SnapshotFileData LoadSnapshotByPath(string path, string label)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SnapshotFileData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 读取 {label} 快照失败: {path} ({e.Message})");
            return null;
        }
    }

    private static void CacheLegacySummary(SnapshotFileData snapshot, string latestFileName)
    {
        if (snapshot == null || snapshot.gameData == null)
            return;

        PlayerPrefs.SetInt(KEY_LEVEL, snapshot.gameData.currentLevel);
        PlayerPrefs.SetInt(KEY_LIVES, snapshot.gameData.lives);
        PlayerPrefs.SetInt(KEY_SCORE, snapshot.gameData.score);
        PlayerPrefs.SetInt(KEY_EXISTS, 1);

        if (!string.IsNullOrEmpty(latestFileName))
            PlayerPrefs.SetString(KEY_LATEST_SNAPSHOT, latestFileName);
        else
            PlayerPrefs.DeleteKey(KEY_LATEST_SNAPSHOT);

        PlayerPrefs.Save();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingSnapshot == null)
        {
            isLoadingSnapshotScene = false;
            return;
        }

        bool byIndex = scene.buildIndex == pendingSnapshot.sceneBuildIndex;
        bool byPath = !string.IsNullOrEmpty(pendingSnapshot.scenePath)
            && string.Equals(scene.path, pendingSnapshot.scenePath, StringComparison.Ordinal);

        if (!byIndex && !byPath)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        EnsureRunner();
        runner.StartCoroutine(RestoreAfterFrame(pendingSnapshot));
    }

    private static IEnumerator RestoreAfterFrame(SnapshotFileData snapshot)
    {
        isRestoring = true;
        yield return null;

        try
        {
            RestoreSnapshot(snapshot);
            pendingSnapshot = null;
        }
        finally
        {
            isRestoring = false;
            isLoadingSnapshotScene = false;
        }
    }

    private static void EnsureRunner()
    {
        if (runner != null)
            return;

        var go = new GameObject("SnapshotRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<SnapshotRunner>();
    }

    private static SnapshotFileData CaptureSnapshot()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;

        var snapshot = new SnapshotFileData
        {
            snapshotVersion = SNAPSHOT_VERSION,
            createdAtUtc = DateTime.UtcNow.ToString("o"),
            sceneBuildIndex = scene.buildIndex,
            scenePath = scene.path,
            gameData = new GameDataSnapshot
            {
                currentLevel = scene.buildIndex,
                scenePath = scene.path,
                lives = GameData.Lives,
                score = GameData.Score,
                hasCheckpoint = GameData.TryGetCheckpoint(scene.buildIndex, scene.path, out Vector3 checkpointPos),
                checkpointX = checkpointPos.x,
                checkpointY = checkpointPos.y,
                checkpointZ = checkpointPos.z,
                darkModeActive = GameData.IsDarkModeActive
            },
            componentStates = new List<ComponentSnapshotEntry>()
        };

        var uniqueEntries = new Dictionary<string, ComponentSnapshotEntry>();

        Rigidbody2D[] rigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D rb = rigidbodies[i];
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) continue;
            if (rb.gameObject.scene.handle != scene.handle) continue;
            if (rb.GetComponent<SnapshotIgnore>() != null) continue;
            if (rb.GetComponent<Bullet>() != null) continue;

            string objectPath = BuildStableObjectKey(rb.transform);
            var state = new RigidbodySnapshotState
            {
                activeSelf = rb.gameObject.activeSelf,
                positionX = rb.transform.position.x,
                positionY = rb.transform.position.y,
                positionZ = rb.transform.position.z,
                rotationZ = rb.transform.eulerAngles.z,
                velocityX = rb.velocity.x,
                velocityY = rb.velocity.y,
                angularVelocity = rb.angularVelocity,
                simulated = rb.simulated,
                constraints = (int)rb.constraints
            };

            string key = objectPath + "|" + RIGIDBODY_COMPONENT_TYPE;
            uniqueEntries[key] = new ComponentSnapshotEntry
            {
                objectPath = objectPath,
                componentType = RIGIDBODY_COMPONENT_TYPE,
                stateJson = JsonUtility.ToJson(state)
            };
        }

        MonoBehaviour[] allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = allBehaviours[i];
            if (behaviour == null) continue;
            if (behaviour.gameObject.scene.handle != scene.handle) continue;
            if (behaviour.GetComponent<SnapshotIgnore>() != null) continue;
            if (behaviour.GetType() == typeof(SnapshotIgnore)) continue;
            if (!(behaviour is ISnapshotSaveable saveable)) continue;

            string stateJson = saveable.CaptureSnapshotState();
            if (string.IsNullOrEmpty(stateJson)) continue;

            string objectPath = BuildStableObjectKey(behaviour.transform);
            string componentType = behaviour.GetType().FullName;
            string key = objectPath + "|" + componentType;

            uniqueEntries[key] = new ComponentSnapshotEntry
            {
                objectPath = objectPath,
                componentType = componentType,
                stateJson = stateJson
            };
        }

        AppendMissingSnapshotSaveables(scene, uniqueEntries);

        foreach (var entry in uniqueEntries.Values)
            snapshot.componentStates.Add(entry);

        snapshot.componentStates.Sort((a, b) =>
        {
            int pathCompare = string.CompareOrdinal(a.objectPath, b.objectPath);
            return pathCompare != 0 ? pathCompare : string.CompareOrdinal(a.componentType, b.componentType);
        });

        return snapshot;
    }

    private static void AppendMissingSnapshotSaveables(Scene scene, Dictionary<string, ComponentSnapshotEntry> entries)
    {
        // Include inactive saveables that may have been deactivated before Save&Quit.
        // We search by known component types because FindObjectsOfType<MonoBehaviour>(true)
        // can miss some disabled script instances depending on load state.
        AddTypedSnapshotEntries<PlayerController>(scene, entries);
        AddTypedSnapshotEntries<Enemy>(scene, entries);
        AddTypedSnapshotEntries<MovingPlatform>(scene, entries);
        AddTypedSnapshotEntries<MovingSaw>(scene, entries);
        AddTypedSnapshotEntries<PushableBox>(scene, entries);
        AddTypedSnapshotEntries<SwitchDoor>(scene, entries);
        AddTypedSnapshotEntries<ButtonController>(scene, entries);
        AddTypedSnapshotEntries<FallingBoulder>(scene, entries);
        AddTypedSnapshotEntries<BreakableDoor>(scene, entries);
        AddTypedSnapshotEntries<Checkpoint>(scene, entries);
        AddTypedSnapshotEntries<DarkCheckpoint>(scene, entries);
        AddTypedSnapshotEntries<DarkVisionController>(scene, entries);
    }

    private static void AddTypedSnapshotEntries<T>(Scene scene, Dictionary<string, ComponentSnapshotEntry> entries)
        where T : MonoBehaviour, ISnapshotSaveable
    {
        T[] components = UnityEngine.Object.FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null) continue;
            if (component.gameObject.scene.handle != scene.handle) continue;
            if (component.GetComponent<SnapshotIgnore>() != null) continue;

            string stateJson = component.CaptureSnapshotState();
            if (string.IsNullOrEmpty(stateJson)) continue;

            string objectPath = BuildStableObjectKey(component.transform);
            string componentType = component.GetType().FullName;
            string key = objectPath + "|" + componentType;

            if (entries.ContainsKey(key))
                continue;

            entries[key] = new ComponentSnapshotEntry
            {
                objectPath = objectPath,
                componentType = componentType,
                stateJson = stateJson
            };
        }
    }

    private static void RestoreSnapshot(SnapshotFileData snapshot)
    {
        if (snapshot == null) return;

        Scene activeScene = SceneManager.GetActiveScene();
        bool byIndex = activeScene.buildIndex == snapshot.sceneBuildIndex;
        bool byPath = !string.IsNullOrEmpty(snapshot.scenePath)
            && string.Equals(activeScene.path, snapshot.scenePath, StringComparison.Ordinal);

        if (!byIndex && !byPath)
        {
            Debug.LogWarning($"[SaveManager] 场景不匹配，跳过恢复: currentIndex={activeScene.buildIndex}, currentPath='{activeScene.path}', saveIndex={snapshot.sceneBuildIndex}, savePath='{snapshot.scenePath}'");
            return;
        }

        Dictionary<string, GameObject> objectLookup = BuildSceneObjectLookup(activeScene);
        int restoredCount = 0;
        int missingObjectCount = 0;
        int missingComponentCount = 0;

        for (int i = 0; i < snapshot.componentStates.Count; i++)
        {
            ComponentSnapshotEntry entry = snapshot.componentStates[i];
            if (entry == null || string.IsNullOrEmpty(entry.objectPath) || string.IsNullOrEmpty(entry.componentType))
                continue;

            if (!objectLookup.TryGetValue(entry.objectPath, out GameObject go) || go == null)
            {
                string fallbackObjectPath = BuildFallbackObjectPath(entry.objectPath);
                if (string.IsNullOrEmpty(fallbackObjectPath)
                    || !objectLookup.TryGetValue(fallbackObjectPath, out go)
                    || go == null)
                {
                    missingObjectCount++;
                    continue;
                }
            }

            if (entry.componentType == RIGIDBODY_COMPONENT_TYPE)
            {
                RestoreRigidbodyState(go, entry.stateJson);
                restoredCount++;
                continue;
            }

            MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
            bool restored = false;
            for (int j = 0; j < behaviours.Length; j++)
            {
                MonoBehaviour behaviour = behaviours[j];
                if (behaviour == null) continue;
                if (!(behaviour is ISnapshotSaveable saveable)) continue;
                if (!string.Equals(behaviour.GetType().FullName, entry.componentType, StringComparison.Ordinal))
                    continue;

                saveable.RestoreSnapshotState(entry.stateJson);
                restoredCount++;
                restored = true;
                break;
            }

            if (!restored)
                missingComponentCount++;
        }

        ApplyGameData(snapshot.gameData);
        Debug.Log($"[SaveManager] 快照恢复完成: Scene={snapshot.sceneBuildIndex}, Entries={snapshot.componentStates.Count}, Restored={restoredCount}, MissingObjects={missingObjectCount}, MissingComponents={missingComponentCount}");
    }

    private static void RestoreRigidbodyState(GameObject go, string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        RigidbodySnapshotState state = JsonUtility.FromJson<RigidbodySnapshotState>(stateJson);
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        go.transform.position = new Vector3(state.positionX, state.positionY, state.positionZ);
        go.transform.rotation = Quaternion.Euler(0f, 0f, state.rotationZ);
        rb.velocity = new Vector2(state.velocityX, state.velocityY);
        rb.angularVelocity = state.angularVelocity;
        rb.simulated = state.simulated;
        rb.constraints = (RigidbodyConstraints2D)state.constraints;
        go.SetActive(state.activeSelf);
    }

    private static Dictionary<string, GameObject> BuildSceneObjectLookup(Scene scene)
    {
        var lookup = new Dictionary<string, GameObject>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null) continue;
            AddToLookupRecursive(root.transform, lookup);
        }

        return lookup;
    }

    private static void AddToLookupRecursive(Transform t, Dictionary<string, GameObject> lookup)
    {
        string primary = BuildStableObjectKey(t);
        lookup[primary] = t.gameObject;

        string secondary = BuildStableObjectKeyWithoutSibling(t);
        if (!string.Equals(primary, secondary, StringComparison.Ordinal))
        {
            if (!lookup.TryGetValue(secondary, out GameObject existing))
                lookup[secondary] = t.gameObject;
            else if (existing != t.gameObject)
                lookup[secondary] = null;
        }

        for (int i = 0; i < t.childCount; i++)
            AddToLookupRecursive(t.GetChild(i), lookup);
    }

    private static string BuildObjectPath(Transform t)
    {
        var parts = new List<string>();

        Transform cursor = t;
        while (cursor != null)
        {
            parts.Add(cursor.name + "[" + cursor.GetSiblingIndex() + "]");
            cursor = cursor.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string BuildStableObjectKey(Transform t)
    {
        if (t != null && t.CompareTag("Player"))
            return "TAG:Player";

        return BuildObjectPath(t);
    }

    private static string BuildObjectPathWithoutSibling(Transform t)
    {
        var parts = new List<string>();

        Transform cursor = t;
        while (cursor != null)
        {
            parts.Add(cursor.name);
            cursor = cursor.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string BuildStableObjectKeyWithoutSibling(Transform t)
    {
        if (t != null && t.CompareTag("Player"))
            return "TAG:Player";

        return BuildObjectPathWithoutSibling(t);
    }

    private static string BuildFallbackObjectPath(string objectPath)
    {
        if (string.IsNullOrEmpty(objectPath))
            return string.Empty;

        if (string.Equals(objectPath, "TAG:Player", StringComparison.Ordinal))
            return objectPath;

        var sb = new StringBuilder(objectPath.Length);
        bool skipping = false;

        for (int i = 0; i < objectPath.Length; i++)
        {
            char c = objectPath[i];
            if (c == '[')
            {
                skipping = true;
                continue;
            }

            if (c == ']')
            {
                skipping = false;
                continue;
            }

            if (!skipping)
                sb.Append(c);
        }

        string fallback = sb.ToString();

        int slash = fallback.IndexOf('/');
        if (slash > 0)
        {
            bool numericPrefix = true;
            for (int i = 0; i < slash; i++)
            {
                if (!char.IsDigit(fallback[i]))
                {
                    numericPrefix = false;
                    break;
                }
            }

            if (numericPrefix && slash + 1 < fallback.Length)
                fallback = fallback.Substring(slash + 1);
        }

        return fallback;
    }

    private static void ApplyGameData(GameDataSnapshot data)
    {
        if (data == null)
            return;

        GameData.CurrentLevel = data.currentLevel;
        GameData.Lives = data.lives;
        GameData.Score = data.score;

        if (data.hasCheckpoint)
        {
            GameData.SetCheckpoint(
                data.currentLevel,
                data.scenePath,
                new Vector3(data.checkpointX, data.checkpointY, data.checkpointZ));
        }
        else
        {
            GameData.ClearCheckpoint(data.currentLevel, data.scenePath);
        }

        if (data.darkModeActive)
            GameData.ActivateDarkMode();
        else
            GameData.ClearDarkMode();
    }

    private static void EnsureSnapshotDirectory()
    {
        Directory.CreateDirectory(SnapshotDirectoryPath);
    }

    private static void RegisterRuntimeHooks()
    {
        if (runtimeHooksRegistered)
            return;

        Application.quitting += OnApplicationQuitting;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        runtimeHooksRegistered = true;
    }

    private static void OnApplicationQuitting()
    {
        isApplicationQuitting = true;
        ClearSessionSnapshot();
        pendingSnapshot = null;
        isLoadingSnapshotScene = false;
    }

    private static void OnActiveSceneChanged(Scene from, Scene to)
    {
        if (isLoadingSnapshotScene || isRestoring || isApplicationQuitting)
            return;

        if (IsMainMenuScene(to))
            return;

        SaveSessionSnapshot();
    }

    private static bool IsMainMenuScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        if (scene.buildIndex == 0)
            return true;

        return string.Equals(scene.path, MAIN_MENU_SCENE_PATH, StringComparison.Ordinal);
    }

    private static void DeleteSnapshotFileWithMeta(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 删除快照失败: {path} ({e.Message})");
        }

        string metaPath = path + ".meta";
        try
        {
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 删除快照元文件失败: {metaPath} ({e.Message})");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void RuntimeBootstrap()
    {
        EnsureSnapshotDirectory();
        RegisterRuntimeHooks();
    }

    [Serializable]
    private class SnapshotFileData
    {
        public int snapshotVersion;
        public string createdAtUtc;
        public int sceneBuildIndex;
        public string scenePath;
        public GameDataSnapshot gameData;
        public List<ComponentSnapshotEntry> componentStates = new List<ComponentSnapshotEntry>();
    }

    [Serializable]
    private class GameDataSnapshot
    {
        public int currentLevel;
        public string scenePath;
        public int lives;
        public int score;
        public bool hasCheckpoint;
        public float checkpointX;
        public float checkpointY;
        public float checkpointZ;
        public bool darkModeActive;
    }

    [Serializable]
    private class ComponentSnapshotEntry
    {
        public string objectPath;
        public string componentType;
        public string stateJson;
    }

    [Serializable]
    private class RigidbodySnapshotState
    {
        public bool activeSelf;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationZ;
        public float velocityX;
        public float velocityY;
        public float angularVelocity;
        public bool simulated;
        public int constraints;
    }

    private sealed class SnapshotRunner : MonoBehaviour { }
}
