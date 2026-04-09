using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Tools/Setup Level2 from Level1
/// 把 Level1.unity 里的基础对象（GameManager、Player、Camera 等）复制到 Level2.unity。
/// LDtk 生成的地图对象（CaveTilemapGrid、Enemy 等）不复制。
/// </summary>
public static class Level2Setup
{
    // 需要从 Level1 复制过来的对象名称
    private static readonly string[] ObjectsToCopy = new[]
    {
        "GameManager",
        "Player",
        "Main Camera",
        "GestureService",
        "BGM",
        "SpiritHand",
        "DeathZone",
        "WinCanvas",
        "PauseCanvas",
        "EventSystem",
        "Directional Light",
        "RespawnPoint",
    };

    [MenuItem("Tools/Setup Level2 from Level1")]
    public static void SetupLevel2()
    {
        string level2Path = "Assets/Scenes/Level2.unity";
        string level1Path = "Assets/Scenes/Level1.unity";

        // 检查 Level2 是否存在
        if (!System.IO.File.Exists(level2Path))
        {
            Debug.LogError("[Level2Setup] Level2.unity not found. Run 'Tools/Create LDtk Level 2 Scene' first.");
            return;
        }

        // 打开 Level2 为主场景
        var level2Scene = EditorSceneManager.OpenScene(level2Path, OpenSceneMode.Single);

        // Additive 加载 Level1
        var level1Scene = EditorSceneManager.OpenScene(level1Path, OpenSceneMode.Additive);

        // 找到 Level1 里要复制的对象
        var copied = new List<string>();
        var skipped = new List<string>();

        foreach (string objName in ObjectsToCopy)
        {
            // 在 Level2 里检查是否已存在同名对象
            bool alreadyExists = false;
            foreach (var root in level2Scene.GetRootGameObjects())
            {
                if (root.name == objName) { alreadyExists = true; break; }
            }
            if (alreadyExists)
            {
                skipped.Add(objName);
                continue;
            }

            // 在 Level1 里找对象
            GameObject source = null;
            foreach (var root in level1Scene.GetRootGameObjects())
            {
                if (root.name == objName) { source = root; break; }
            }

            if (source == null)
            {
                Debug.LogWarning($"[Level2Setup] '{objName}' not found in Level1.");
                continue;
            }

            // 复制到 Level2
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            SceneManager.MoveGameObjectToScene(copy, level2Scene);
            copied.Add(objName);
        }

        // 关闭 Level1（不保存）
        EditorSceneManager.CloseScene(level1Scene, true);

        // 保存 Level2
        EditorSceneManager.SaveScene(level2Scene, level2Path);

        Debug.Log($"[Level2Setup] Done! Copied: {string.Join(", ", copied)}");
        if (skipped.Count > 0)
            Debug.Log($"[Level2Setup] Skipped (already existed): {string.Join(", ", skipped)}");
    }
}
