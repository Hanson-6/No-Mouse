// ============================================================================
// CameraManager.cs
// Manages WebCamTexture lifecycle: start, stop, select camera device.
// This component handles the camera independently from MediaPipe so that
// the same camera feed can be displayed in the UI AND sent for processing.
//
// UNITY SETUP:
//   This script is used internally by GestureService. You do NOT need to
//   manually add it to a GameObject — GestureService creates it at runtime.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;

namespace GestureRecognition.Detection
{
    /// <summary>
    /// Manages a <see cref="WebCamTexture"/> instance.
    /// Provides the current camera frame as a <see cref="Texture"/> for UI
    /// display and as pixel data for MediaPipe processing.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Configuration
        // -----------------------------------------------------------------

        [SerializeField] private int _requestedWidth = 640;
        [SerializeField] private int _requestedHeight = 480;
        [SerializeField] private int _requestedFps = 30;

        // -----------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------

        private WebCamTexture _webCamTexture;
        private Texture2D _cpuTexture;
        private Color32[] _pixelBuffer;
        private bool _isRunning;

        // -----------------------------------------------------------------
        // Public properties
        // -----------------------------------------------------------------

        /// <summary>Is the camera currently capturing?</summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// The live camera texture. Can be assigned to a RawImage.texture
        /// for real-time display.
        /// </summary>
        public Texture CameraTexture => _webCamTexture;

        /// <summary>Actual width of the camera feed.</summary>
        public int Width => _webCamTexture != null ? _webCamTexture.width : 0;

        /// <summary>Actual height of the camera feed.</summary>
        public int Height => _webCamTexture != null ? _webCamTexture.height : 0;

        /// <summary>Whether the underlying WebCamTexture is currently playing.</summary>
        public bool IsTexturePlaying => _webCamTexture != null && _webCamTexture.isPlaying;

        /// <summary>Whether this frame was updated by webcam hardware.</summary>
        public bool DidUpdateThisFrame => _webCamTexture != null && _webCamTexture.didUpdateThisFrame;

        /// <summary>Latest CPU-side pixel buffer captured from webcam.</summary>
        public Color32[] LatestPixelBuffer => _pixelBuffer;

        /// <summary>Fired once the camera is ready (width > 16).</summary>
        public event Action OnCameraReady;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Starts the camera. Optionally specify a device name
        /// (pass null to use the first available camera).
        /// </summary>
        public IEnumerator StartCamera(string deviceName = null, float startupTimeoutSeconds = 5f)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[CameraManager] Camera is already running.");
                yield break;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[CameraManager] No webcam devices found.");
                yield break;
            }

            string selectedDevice = deviceName;
            if (string.IsNullOrEmpty(selectedDevice))
            {
                selectedDevice = devices[0].name;
            }

            Debug.Log($"[CameraManager] Starting camera: {selectedDevice} " +
                      $"({_requestedWidth}x{_requestedHeight} @ {_requestedFps}fps)");

            _webCamTexture = new WebCamTexture(
                selectedDevice,
                _requestedWidth,
                _requestedHeight,
                _requestedFps);

            _webCamTexture.Play();

            // Wait until the camera actually delivers frames.
            // Unity's WebCamTexture reports width=16 until ready.
            float startedAt = Time.realtimeSinceStartup;
            while (_webCamTexture != null && _webCamTexture.width <= 16)
            {
                if (Time.realtimeSinceStartup - startedAt > startupTimeoutSeconds)
                {
                    Debug.LogError("[CameraManager] Camera startup timed out.");
                    _webCamTexture.Stop();
                    Destroy(_webCamTexture);
                    _webCamTexture = null;
                    yield break;
                }

                yield return null;
            }

            if (_webCamTexture == null || _webCamTexture.width <= 16)
            {
                Debug.LogError("[CameraManager] Camera startup failed before ready.");
                yield break;
            }

            // Allocate CPU-side buffers for pixel readback
            _cpuTexture = new Texture2D(
                _webCamTexture.width,
                _webCamTexture.height,
                TextureFormat.RGBA32,
                false);

            _pixelBuffer = new Color32[_webCamTexture.width * _webCamTexture.height];

            _isRunning = true;

            Debug.Log($"[CameraManager] Camera ready: " +
                      $"{_webCamTexture.width}x{_webCamTexture.height}");

            OnCameraReady?.Invoke();
        }

        /// <summary>Stops the camera and releases resources.</summary>
        public void StopCamera()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;

            if (_webCamTexture != null)
            {
                _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }

            if (_cpuTexture != null)
            {
                Destroy(_cpuTexture);
                _cpuTexture = null;
            }

            _pixelBuffer = null;

            Debug.Log("[CameraManager] Camera stopped.");
        }

        /// <summary>
        /// Copies the current camera frame into a CPU-side Texture2D
        /// and returns the raw texture data as a NativeArray.
        /// Call this once per frame before sending data to MediaPipe.
        /// </summary>
        /// <returns>
        /// A Texture2D containing the current frame, or null if not ready.
        /// </returns>
        public Texture2D GetCurrentFrame()
        {
            if (!_isRunning || _webCamTexture == null || _cpuTexture == null)
            {
                return null;
            }

            // Scene reload (SceneManager.LoadScene) can cause WebCamTexture
            // to stop playing even though the GameObject survives via
            // DontDestroyOnLoad.  If that happens, restart it immediately
            // so the camera feed doesn't freeze on the last frame.
            if (!_webCamTexture.isPlaying)
            {
                Debug.LogWarning("[CameraManager] WebCamTexture stopped unexpectedly — restarting.");
                _webCamTexture.Play();
                // The first frame after restart may still be stale;
                // return null this once so callers skip the stale data.
                return null;
            }

            _cpuTexture.SetPixels32(_webCamTexture.GetPixels32(_pixelBuffer));
            return _cpuTexture;
        }

        /// <summary>
        /// Returns a list of available camera device names.
        /// </summary>
        public static string[] GetAvailableDevices()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            string[] names = new string[devices.Length];
            for (int i = 0; i < devices.Length; i++)
            {
                names[i] = devices[i].name;
            }
            return names;
        }

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void OnDestroy()
        {
            StopCamera();
        }
    }
}
