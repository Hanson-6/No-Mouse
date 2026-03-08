// ============================================================================
// GestureService.cs
// The single entry point for the entire gesture recognition system.
// Frontend developers only need to interact with this class.
//
// UNITY SETUP:
//   1. Create an empty GameObject in your scene
//   2. Add this script to it (Add Component > GestureService)
//   3. Assign a GestureConfig asset in the Inspector
//   4. Call GestureService.Instance.StartRecognition() from your game code
//      OR check "Auto Start" in the Inspector
//
// FRONTEND USAGE:
//   // Option A: Use the singleton
//   GestureService.Instance.StartRecognition();
//   GestureService.Instance.StopRecognition();
//
//   // Option B: Subscribe to events
//   GestureEvents.OnGestureChanged += result => { ... };
//   GestureEvents.OnHandPositionUpdated += pos => { ... };
//
//   // Option C: Poll current state
//   GestureResult current = GestureService.Instance.CurrentResult;
// ============================================================================

using System.Collections;
using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.Detection;

namespace GestureRecognition.Service
{
    /// <summary>
    /// Facade / singleton that orchestrates the entire gesture recognition
    /// pipeline: Camera -> MediaPipe -> Classifier -> Events.
    /// </summary>
    public class GestureService : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------

        private static GestureService _instance;

        /// <summary>Global singleton instance.</summary>
        public static GestureService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GestureService>();
                    if (_instance == null)
                    {
                        Debug.LogError("[GestureService] No GestureService found in scene. " +
                                       "Please add one to a GameObject.");
                    }
                }
                return _instance;
            }
        }

        // -----------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------

        [Header("Configuration")]
        [Tooltip("The gesture configuration asset (maps gestures to sprites).")]
        [SerializeField]
        private GestureConfig _gestureConfig;

        [Tooltip("Start recognition automatically when the scene loads.")]
        [SerializeField]
        private bool _autoStart;

        [Header("Camera Settings")]
        [Tooltip("Camera device name. Leave empty to use the default camera.")]
        [SerializeField]
        private string _cameraDeviceName = "";

        [Header("Tracking Settings")]
        [Tooltip("Smoothing factor for hand position. 0 = instant, 0.9 = very smooth.")]
        [SerializeField]
        [Range(0f, 0.95f)]
        private float _positionSmoothing = 0.5f;

        // -----------------------------------------------------------------
        // Runtime components
        // -----------------------------------------------------------------

        private CameraManager _cameraManager;
        private MediaPipeBridge _mediaPipeBridge;
        private GestureClassifier _gestureClassifier;
        private HandTracker _handTracker;

        private bool _isRunning;
        private GestureResult _currentResult = GestureResult.Empty;
        private GestureType _previousGestureType = GestureType.None;
        private bool _previousHandDetected;
        private float _startTime;

        // -----------------------------------------------------------------
        // Public properties
        // -----------------------------------------------------------------

        /// <summary>Is the recognition pipeline currently running?</summary>
        public bool IsRunning => _isRunning;

        /// <summary>The most recent gesture result.</summary>
        public GestureResult CurrentResult => _currentResult;

        /// <summary>The gesture configuration asset.</summary>
        public GestureConfig Config => _gestureConfig;

        /// <summary>The camera manager (for accessing the camera texture).</summary>
        public CameraManager Camera => _cameraManager;

        /// <summary>The MediaPipe bridge (for advanced usage / testing).</summary>
        public MediaPipeBridge Bridge => _mediaPipeBridge;

        // -----------------------------------------------------------------
        // Public API — Frontend calls these
        // -----------------------------------------------------------------

        /// <summary>
        /// Starts the camera and gesture recognition pipeline.
        /// This is a coroutine because camera initialization is async.
        /// </summary>
        public void StartRecognition()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[GestureService] Already running.");
                return;
            }

            StartCoroutine(StartRecognitionCoroutine());
        }

        /// <summary>
        /// Stops recognition and releases camera resources.
        /// </summary>
        public void StopRecognition()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            StopAllCoroutines();

            if (_cameraManager != null)
            {
                _cameraManager.StopCamera();
            }

            if (_mediaPipeBridge != null)
            {
                _mediaPipeBridge.Shutdown();
            }

            _currentResult = GestureResult.Empty;
            _previousGestureType = GestureType.None;

            GestureEvents.InvokeRecognitionStateChanged(false);
            Debug.Log("[GestureService] Recognition stopped.");
        }

        /// <summary>
        /// Returns a list of available camera device names.
        /// </summary>
        public string[] GetAvailableCameras()
        {
            return CameraManager.GetAvailableDevices();
        }

        /// <summary>
        /// Switches to a different camera (stops and restarts).
        /// </summary>
        public void SwitchCamera(string deviceName)
        {
            bool wasRunning = _isRunning;
            if (wasRunning)
            {
                StopRecognition();
            }

            _cameraDeviceName = deviceName;

            if (wasRunning)
            {
                StartRecognition();
            }
        }

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            // Singleton enforcement
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GestureService] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Create sub-components on the same GameObject
            _cameraManager = gameObject.AddComponent<CameraManager>();
            _mediaPipeBridge = gameObject.AddComponent<MediaPipeBridge>();
            _gestureClassifier = new GestureClassifier(
                _gestureConfig != null ? _gestureConfig.ConfidenceThreshold : 0.6f);
            _handTracker = new HandTracker(_positionSmoothing);
        }

        private void Start()
        {
            if (_autoStart)
            {
                StartRecognition();
            }
        }

        private void OnDestroy()
        {
            StopRecognition();
            GestureEvents.ClearAll();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        // -----------------------------------------------------------------
        // Internal pipeline
        // -----------------------------------------------------------------

        private IEnumerator StartRecognitionCoroutine()
        {
            Debug.Log("[GestureService] Starting recognition...");

            // 1. Start camera
            string device = string.IsNullOrEmpty(_cameraDeviceName)
                ? null
                : _cameraDeviceName;

            yield return _cameraManager.StartCamera(device);

            if (!_cameraManager.IsRunning)
            {
                Debug.LogError("[GestureService] Failed to start camera.");
                yield break;
            }

            // 2. Initialize MediaPipe (async — model loading may take time)
            yield return _mediaPipeBridge.InitializeAsync();

            // 3. Mark as running
            _isRunning = true;
            _startTime = Time.time;
            _previousGestureType = GestureType.None;
            _previousHandDetected = false;

            GestureEvents.InvokeRecognitionStateChanged(true);
            Debug.Log("[GestureService] Recognition started.");

            // 3b. Auto-show the display panel so the user can see something
            if (GesturePanelManager.HasInstance &&
                !GesturePanelManager.Instance.IsPanelVisible)
            {
                GesturePanelManager.Instance.ShowPanel();
                Debug.Log("[GestureService] Auto-showed gesture panel.");
            }

            // 4. Main loop
            while (_isRunning)
            {
                ProcessOneFrame();
                yield return null; // wait one frame
            }
        }

        private void ProcessOneFrame()
        {
            // Get current camera frame
            Texture2D frame = _cameraManager.GetCurrentFrame();
            if (frame == null)
            {
                return;
            }

            // Send to MediaPipe
            _mediaPipeBridge.ProcessFrame(frame);

            // Get landmarks
            HandLandmarkData landmarks = _mediaPipeBridge.LatestResult;

            // Track hand position
            Vector2 handPosition;
            if (landmarks.IsValid)
            {
                handPosition = _handTracker.Update(landmarks);
            }
            else
            {
                _handTracker.Reset();
                handPosition = new Vector2(-1f, -1f);
            }

            // Classify gesture
            GestureType gestureType = GestureType.None;
            float confidence = 0f;

            if (landmarks.IsValid)
            {
                gestureType = _gestureClassifier.Classify(
                    landmarks.Landmarks, out confidence);
            }

            // Build result
            float timestamp = Time.time - _startTime;
            _currentResult = new GestureResult(
                gestureType, confidence, handPosition,
                landmarks.IsValid, timestamp);

            // Fire events
            GestureEvents.InvokeGestureUpdated(_currentResult);

            if (gestureType != _previousGestureType)
            {
                GestureEvents.InvokeGestureChanged(_currentResult);
                _previousGestureType = gestureType;
            }

            if (landmarks.IsValid)
            {
                GestureEvents.InvokeHandPositionUpdated(handPosition);
            }

            if (landmarks.IsValid != _previousHandDetected)
            {
                GestureEvents.InvokeHandDetectionChanged(landmarks.IsValid);
                _previousHandDetected = landmarks.IsValid;
            }
        }
    }
}
