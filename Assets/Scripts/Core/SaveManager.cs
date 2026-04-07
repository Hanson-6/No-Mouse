using UnityEngine;

/// <summary>
/// 静态存档管理器。
/// 使用 PlayerPrefs 保存/读取 GameData 中的关卡进度、生命数和分数。
/// 无需挂载到 GameObject 上，直接调用静态方法即可。
/// </summary>
public static class SaveManager
{
    private const string KEY_LEVEL  = "SavedLevel";
    private const string KEY_LIVES  = "SavedLives";
    private const string KEY_SCORE  = "SavedScore";
    private const string KEY_EXISTS = "SaveExists";

    /// <summary>
    /// 将当前 GameData 的数据保存到 PlayerPrefs。
    /// </summary>
    public static void Save()
    {
        PlayerPrefs.SetInt(KEY_LEVEL, GameData.CurrentLevel);
        PlayerPrefs.SetInt(KEY_LIVES, GameData.Lives);
        PlayerPrefs.SetInt(KEY_SCORE, GameData.Score);
        PlayerPrefs.SetInt(KEY_EXISTS, 1);
        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] 存档已保存: Level={GameData.CurrentLevel}, Lives={GameData.Lives}, Score={GameData.Score}");
    }

    /// <summary>
    /// 从 PlayerPrefs 读取存档数据，写入 GameData。
    /// </summary>
    public static void Load()
    {
        GameData.CurrentLevel = PlayerPrefs.GetInt(KEY_LEVEL, 1);
        GameData.Lives        = PlayerPrefs.GetInt(KEY_LIVES, 3);
        GameData.Score        = PlayerPrefs.GetInt(KEY_SCORE, 0);
        Debug.Log($"[SaveManager] 存档已读取: Level={GameData.CurrentLevel}, Lives={GameData.Lives}, Score={GameData.Score}");
    }

    /// <summary>
    /// 是否存在有效存档。
    /// </summary>
    public static bool HasSave()
    {
        return PlayerPrefs.GetInt(KEY_EXISTS, 0) == 1;
    }

    /// <summary>
    /// 删除存档（新游戏时调用）。
    /// </summary>
    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(KEY_LEVEL);
        PlayerPrefs.DeleteKey(KEY_LIVES);
        PlayerPrefs.DeleteKey(KEY_SCORE);
        PlayerPrefs.DeleteKey(KEY_EXISTS);
        PlayerPrefs.Save();
        Debug.Log("[SaveManager] 存档已删除");
    }
}
