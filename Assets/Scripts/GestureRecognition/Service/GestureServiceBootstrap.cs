using UnityEngine;

namespace GestureRecognition.Service
{
    public static class GestureServiceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureServiceExists()
        {
            if (Object.FindObjectOfType<GestureService>() != null)
                return;

            GameObject go = new GameObject("GestureService");
            go.AddComponent<GestureService>();
        }
    }
}
