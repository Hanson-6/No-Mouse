using UnityEngine;

public static class DisplaySettingsInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetDefaultDisplayMode()
    {
        // Prefer borderless fullscreen at native resolution when available.
        Resolution current = Screen.currentResolution;
        Screen.SetResolution(current.width, current.height, FullScreenMode.FullScreenWindow, current.refreshRateRatio);
    }
}
