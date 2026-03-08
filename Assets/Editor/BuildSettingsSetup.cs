using UnityEditor;
using UnityEngine;

public class BuildSettingsSetup
{
    [MenuItem("Tools/Setup Build Settings")]
    public static void Setup()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Level1.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/LevelComplete.unity", true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("Build Settings updated: MainMenu(0), Level1(1), LevelComplete(2)");
    }

    [MenuItem("Tools/Load Level1")]
    public static void LoadLevel1()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity");
    }

    [MenuItem("Tools/Load LevelComplete")]
    public static void LoadLevelComplete()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/LevelComplete.unity");
    }
}
