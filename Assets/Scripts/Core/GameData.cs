using System;
using UnityEngine;

/// <summary>
/// Static container for persisting player data between scenes/levels.
/// </summary>
public static class GameData
{
    public static int CurrentLevel = 1;
    public static int Lives = 3;
    public static int Score = 0;

    private static bool darkModeActive;

    private static bool hasCheckpoint;
    private static int checkpointLevel = -1;
    private static string checkpointScenePath = string.Empty;
    private static Vector3 checkpointPosition = Vector3.zero;

    public static bool IsDarkModeActive => darkModeActive;

    public static void ActivateDarkMode()
    {
        darkModeActive = true;
    }

    public static void ClearDarkMode()
    {
        darkModeActive = false;
    }

    public static void SetCheckpoint(int levelIndex, Vector3 worldPosition)
    {
        SetCheckpoint(levelIndex, string.Empty, worldPosition);
    }

    public static void SetCheckpoint(int levelIndex, string scenePath, Vector3 worldPosition)
    {
        hasCheckpoint = true;
        checkpointLevel = levelIndex;
        checkpointScenePath = scenePath ?? string.Empty;
        checkpointPosition = worldPosition;
    }

    public static bool TryGetCheckpoint(int levelIndex, out Vector3 worldPosition)
    {
        return TryGetCheckpoint(levelIndex, string.Empty, out worldPosition);
    }

    public static bool TryGetCheckpoint(int levelIndex, string scenePath, out Vector3 worldPosition)
    {
        if (!hasCheckpoint)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        bool hasScenePathQuery = !string.IsNullOrEmpty(scenePath);
        bool hasStoredScenePath = !string.IsNullOrEmpty(checkpointScenePath);

        if (hasScenePathQuery && hasStoredScenePath)
        {
            if (string.Equals(scenePath, checkpointScenePath, StringComparison.Ordinal))
            {
                worldPosition = checkpointPosition;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        if (checkpointLevel == levelIndex)
        {
            worldPosition = checkpointPosition;
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    public static void ClearCheckpoint(int levelIndex)
    {
        ClearCheckpoint(levelIndex, string.Empty);
    }

    public static void ClearCheckpoint(int levelIndex, string scenePath)
    {
        if (!hasCheckpoint)
            return;

        bool byScenePath = !string.IsNullOrEmpty(scenePath)
            && !string.IsNullOrEmpty(checkpointScenePath)
            && string.Equals(scenePath, checkpointScenePath, StringComparison.Ordinal);

        bool byLevel = checkpointLevel == levelIndex;

        if (byScenePath || byLevel)
            ClearCheckpoint();
    }

    public static void ClearCheckpoint()
    {
        hasCheckpoint = false;
        checkpointLevel = -1;
        checkpointScenePath = string.Empty;
        checkpointPosition = Vector3.zero;
    }

    public static void Reset()
    {
        CurrentLevel = 1;
        Lives = 3;
        Score = 0;
        ClearCheckpoint();
        ClearDarkMode();
    }
}
