using UnityEngine;
using UnityEngine.SceneManagement;

namespace GestureRecognition.Service
{
    public static class GestureServiceBootstrap
    {
        private static bool _sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureServiceExists()
        {
            RegisterSceneHookIfNeeded();

            if (Object.FindObjectOfType<GestureService>() != null)
                return;

            GameObject go = new GameObject("GestureService");
            go.AddComponent<GestureService>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAudioListenerAfterFirstSceneLoad()
        {
            EnsureAudioListenerIfMissing(SceneManager.GetActiveScene());
        }

        private static void RegisterSceneHookIfNeeded()
        {
            if (_sceneHookRegistered)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHookRegistered = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureAudioListenerIfMissing(scene);
        }

        private static void EnsureAudioListenerIfMissing(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            if (Object.FindObjectOfType<AudioListener>() != null)
                return;

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    targetCamera = root.GetComponentInChildren<Camera>(true);
                    if (targetCamera != null)
                        break;
                }
            }

            if (targetCamera != null)
            {
                targetCamera.gameObject.AddComponent<AudioListener>();
                Debug.Log($"[GestureServiceBootstrap] Added missing AudioListener to '{targetCamera.gameObject.name}' in scene '{scene.name}'.");
                return;
            }

            var service = Object.FindObjectOfType<GestureService>();
            if (service != null && service.GetComponent<AudioListener>() == null)
            {
                service.gameObject.AddComponent<AudioListener>();
                Debug.LogWarning($"[GestureServiceBootstrap] No Camera found in scene '{scene.name}'. Added fallback AudioListener to GestureService.");
            }
        }
    }
}
