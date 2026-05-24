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
            new EditorBuildSettingsScene("Assets/Scenes/Level2.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Tutoring.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/LevelComplete.unity", true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("Build Settings updated: MainMenu(0), Level2(1), Tutoring(2), LevelComplete(3)");
    }

    [MenuItem("Tools/Load Tutoring")]
    public static void LoadTutoring()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Tutoring.unity");
    }

    [MenuItem("Tools/Load LevelComplete")]
    public static void LoadLevelComplete()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/LevelComplete.unity");
    }
}
