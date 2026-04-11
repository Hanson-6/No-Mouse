using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档管理器。
/// 开发阶段默认将快照写入 Assets/Snapshots/，每次 Save&Quit 保存一个 JSON 快照，且仅保留最新一个。
/// </summary>
public static class SaveManager
{
    private const string KEY_LEVEL = "SavedLevel";
    private const string KEY_LIVES = "SavedLives";
    private const string KEY_SCORE = "SavedScore";
    private const string KEY_EXISTS = "SaveExists";
    private const string KEY_LATEST_SNAPSHOT = "LatestSnapshot";

    private const int SNAPSHOT_VERSION = 1;
    private const int MAX_SNAPSHOT_FILES = 1;
    private const string RIGIDBODY_COMPONENT_TYPE = "UnityEngine.Rigidbody2D";
    private const string SNAPSHOT_FOLDER_NAME = "Snapshots";
    private const string MANIFEST_FILE_NAME = "manifest.json";

    private static SnapshotFileData pendingSnapshot;
    private static SnapshotRunner runner;
    private static bool isRestoring;
    private static bool isLoadingSnapshotScene;

    private static string SnapshotDirectoryPath => Path.Combine(Application.dataPath, SNAPSHOT_FOLDER_NAME);
    private static string ManifestPath => Path.Combine(SnapshotDirectoryPath, MANIFEST_FILE_NAME);

    public static void Save()
    {
        EnsureSnapshotDirectoryAndPrune();

        if (isRestoring)
        {
            Debug.LogWarning("[SaveManager] 当前正在恢复快照，已跳过保存请求。");
            return;
        }

        try
        {
            var snapshot = CaptureSnapshot();
            if (snapshot == null)
            {
                Debug.LogWarning("[SaveManager] 快照保存失败：无法捕获场景状态。");
                return;
            }

            Directory.CreateDirectory(SnapshotDirectoryPath);

            string fileName = BuildSnapshotFileName();
            string snapshotPath = Path.Combine(SnapshotDirectoryPath, fileName);
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(snapshotPath, json);

            SnapshotManifest manifest = LoadManifest();
            if (manifest.files == null)
                manifest.files = new List<string>();

            manifest.files.RemoveAll(string.IsNullOrWhiteSpace);
            manifest.files.Remove(fileName);
            manifest.files.Add(fileName);
            manifest.latest = fileName;
            PruneOldSnapshots(manifest);
            SaveManifest(manifest);

            PlayerPrefs.SetInt(KEY_LEVEL, snapshot.gameData.currentLevel);
            PlayerPrefs.SetInt(KEY_LIVES, snapshot.gameData.lives);
            PlayerPrefs.SetInt(KEY_SCORE, snapshot.gameData.score);
            PlayerPrefs.SetInt(KEY_EXISTS, 1);
            PlayerPrefs.SetString(KEY_LATEST_SNAPSHOT, fileName);
            PlayerPrefs.Save();

            Debug.Log($"[SaveManager] 快照已保存: {snapshotPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 保存快照失败: {e.Message}\n{e.StackTrace}");
        }
    }

    public static void Load()
    {
        SnapshotFileData snapshot = LoadLatestSnapshot();
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
        EnsureSnapshotDirectoryAndPrune();

        if (isLoadingSnapshotScene || pendingSnapshot != null)
        {
            Debug.LogWarning("[SaveManager] Continue 请求被忽略：已有快照加载/恢复在进行中。");
            return true;
        }

        SnapshotFileData snapshot = LoadLatestSnapshot();
        if (snapshot == null)
        {
            Debug.LogWarning("[SaveManager] Continue 失败：未找到快照文件。");
            return false;
        }

        return ContinueFromSnapshot(snapshot);
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

    public static bool HasSave()
    {
        EnsureSnapshotDirectoryAndPrune();

        SnapshotFileData snapshot = LoadLatestSnapshot();
        if (snapshot != null)
            return true;

        return PlayerPrefs.GetInt(KEY_EXISTS, 0) == 1;
    }

    public static void DeleteSave()
    {
        EnsureSnapshotDirectoryAndPrune();

        if (Directory.Exists(SnapshotDirectoryPath))
        {
            foreach (string file in Directory.GetFiles(SnapshotDirectoryPath, "*.json"))
            {
                DeleteSnapshotFileWithMeta(file);
            }
        }

        PlayerPrefs.DeleteKey(KEY_LEVEL);
        PlayerPrefs.DeleteKey(KEY_LIVES);
        PlayerPrefs.DeleteKey(KEY_SCORE);
        PlayerPrefs.DeleteKey(KEY_EXISTS);
        PlayerPrefs.DeleteKey(KEY_LATEST_SNAPSHOT);
        PlayerPrefs.Save();

        pendingSnapshot = null;
        isLoadingSnapshotScene = false;
        Debug.Log("[SaveManager] 存档已删除");
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
                lives = GameData.Lives,
                score = GameData.Score
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
    }

    private static string BuildSnapshotFileName()
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        return "snapshot_" + stamp + ".json";
    }

    private static void PruneOldSnapshots(SnapshotManifest manifest)
    {
        if (manifest.files == null)
            manifest.files = new List<string>();

        while (manifest.files.Count > MAX_SNAPSHOT_FILES)
        {
            string oldest = manifest.files[0];
            manifest.files.RemoveAt(0);

            string oldPath = Path.Combine(SnapshotDirectoryPath, oldest);
            DeleteSnapshotFileWithMeta(oldPath);
        }

        if (!string.IsNullOrEmpty(manifest.latest) && !manifest.files.Contains(manifest.latest))
            manifest.latest = manifest.files.Count > 0 ? manifest.files[manifest.files.Count - 1] : string.Empty;
    }

    public static void EnsureSnapshotDirectoryAndPrune()
    {
        if (Application.isPlaying && isLoadingSnapshotScene)
            return;

        Directory.CreateDirectory(SnapshotDirectoryPath);

        SnapshotManifest manifest = LoadManifest();
        if (manifest.files == null)
            manifest.files = new List<string>();

        string[] files = Directory.GetFiles(SnapshotDirectoryPath, "snapshot_*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));

        manifest.files.Clear();
        for (int i = 0; i < files.Length; i++)
            manifest.files.Add(Path.GetFileName(files[i]));

        manifest.latest = manifest.files.Count > 0 ? manifest.files[manifest.files.Count - 1] : string.Empty;
        PruneOldSnapshots(manifest);
        SaveManifest(manifest);

        if (!string.IsNullOrEmpty(manifest.latest))
            PlayerPrefs.SetString(KEY_LATEST_SNAPSHOT, manifest.latest);
        else
            PlayerPrefs.DeleteKey(KEY_LATEST_SNAPSHOT);

        PlayerPrefs.Save();
    }

    private static SnapshotManifest LoadManifest()
    {
        if (!File.Exists(ManifestPath))
            return new SnapshotManifest();

        try
        {
            string json = File.ReadAllText(ManifestPath);
            SnapshotManifest manifest = JsonUtility.FromJson<SnapshotManifest>(json);
            if (manifest != null && manifest.files == null)
                manifest.files = new List<string>();
            return manifest ?? new SnapshotManifest();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 读取 manifest 失败: {e.Message}");
            return new SnapshotManifest();
        }
    }

    private static void SaveManifest(SnapshotManifest manifest)
    {
        Directory.CreateDirectory(SnapshotDirectoryPath);
        string json = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(ManifestPath, json);
    }

    private static SnapshotFileData LoadLatestSnapshot()
    {
        EnsureSnapshotDirectoryAndPrune();

        SnapshotManifest manifest = LoadManifest();
        if (manifest.files == null)
            manifest.files = new List<string>();

        string candidate = manifest.latest;

        if (string.IsNullOrEmpty(candidate))
            candidate = PlayerPrefs.GetString(KEY_LATEST_SNAPSHOT, string.Empty);

        if (!string.IsNullOrEmpty(candidate))
        {
            SnapshotFileData exact = LoadSnapshotByFileName(candidate);
            if (exact != null)
                return exact;
        }

        for (int i = manifest.files.Count - 1; i >= 0; i--)
        {
            SnapshotFileData fallback = LoadSnapshotByFileName(manifest.files[i]);
            if (fallback != null)
                return fallback;
        }

        return LoadLatestSnapshotByDirectoryScan();
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

    private static SnapshotFileData LoadLatestSnapshotByDirectoryScan()
    {
        if (!Directory.Exists(SnapshotDirectoryPath))
            return null;

        string[] files = Directory.GetFiles(SnapshotDirectoryPath, "snapshot_*.json", SearchOption.TopDirectoryOnly);
        if (files == null || files.Length == 0)
            return null;

        Array.Sort(files, (a, b) => string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)));

        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            SnapshotFileData data = LoadSnapshotByFileName(name);
            if (data != null)
                return data;
        }

        return null;
    }

    private static SnapshotFileData LoadSnapshotByFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        string path = Path.Combine(SnapshotDirectoryPath, fileName);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SnapshotFileData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 读取快照失败: {path} ({e.Message})");
            return null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void RuntimeBootstrap()
    {
        EnsureSnapshotDirectoryAndPrune();
    }

    [Serializable]
    private class SnapshotManifest
    {
        public List<string> files = new List<string>();
        public string latest;
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
        public int lives;
        public int score;
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
