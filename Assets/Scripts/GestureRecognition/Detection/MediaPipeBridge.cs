// ============================================================================
// MediaPipeBridge.cs
// Abstraction layer between our gesture system and the MediaPipe Unity Plugin.
//
// This class encapsulates ALL MediaPipe-specific code. If you ever need to
// swap MediaPipe for a different hand tracking backend, only this file
// needs to change.
//
// COMPILATION:
//   When the MediaPipe Unity Plugin is installed, the assembly definition
//   auto-defines MEDIAPIPE_INSTALLED via versionDefines. Code inside
//   #if MEDIAPIPE_INSTALLED blocks uses the real HandLandmarker API.
//   When the plugin is absent, a stub implementation is used so the rest
//   of the project still compiles and mock data works for testing.
//
// UNITY SETUP:
//   Created automatically by GestureService. Do NOT add manually.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if MEDIAPIPE_INSTALLED
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Unity;
#endif

namespace GestureRecognition.Detection
{
    /// <summary>
    /// Raw hand landmark data extracted from MediaPipe.
    /// 21 landmarks per hand, each with x/y/z in normalized [0,1] coords.
    /// </summary>
    public struct HandLandmarkData
    {
        /// <summary>
        /// 21 landmark positions. Index follows the MediaPipe hand landmark model:
        /// 0=WRIST, 4=THUMB_TIP, 8=INDEX_FINGER_TIP, 12=MIDDLE_FINGER_TIP,
        /// 16=RING_FINGER_TIP, 20=PINKY_TIP, etc.
        /// Coordinates are normalized [0,1] relative to the image frame.
        /// </summary>
        public Vector3[] Landmarks;

        /// <summary>Whether this data is valid (landmarks were detected).</summary>
        public bool IsValid;
    }

    /// <summary>
    /// Bridge to MediaPipe Unity Plugin.
    /// <para>
    /// When MediaPipe is not installed, this provides a stub that always
    /// returns "no hand detected", allowing the rest of the system to
    /// compile and run (useful for UI development and testing).
    /// </para>
    /// </summary>
    public class MediaPipeBridge : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------

        /// <summary>Number of landmarks in the MediaPipe hand model.</summary>
        public const int LandmarkCount = 21;

        // Landmark indices (MediaPipe hand model convention)
        public const int Wrist = 0;
        public const int ThumbCmc = 1;
        public const int ThumbMcp = 2;
        public const int ThumbIp = 3;
        public const int ThumbTip = 4;
        public const int IndexMcp = 5;
        public const int IndexPip = 6;
        public const int IndexDip = 7;
        public const int IndexTip = 8;
        public const int MiddleMcp = 9;
        public const int MiddlePip = 10;
        public const int MiddleDip = 11;
        public const int MiddleTip = 12;
        public const int RingMcp = 13;
        public const int RingPip = 14;
        public const int RingDip = 15;
        public const int RingTip = 16;
        public const int PinkyMcp = 17;
        public const int PinkyPip = 18;
        public const int PinkyDip = 19;
        public const int PinkyTip = 20;

        /// <summary>The model file name (located in MediaPipe PackageResources).</summary>
        private const string ModelFileName = "hand_landmarker.bytes";

        // -----------------------------------------------------------------
        // Configuration
        // -----------------------------------------------------------------

        [Header("MediaPipe Settings")]
        [Tooltip("Maximum number of hands to detect.")]
        [SerializeField] private int _numHands = 1;

        [Tooltip("Minimum confidence for hand detection.")]
        [SerializeField] [Range(0.1f, 1f)] private float _minDetectionConfidence = 0.5f;

        [Tooltip("Minimum confidence for hand presence.")]
        [SerializeField] [Range(0.1f, 1f)] private float _minPresenceConfidence = 0.5f;

        [Tooltip("Minimum confidence for hand tracking.")]
        [SerializeField] [Range(0.1f, 1f)] private float _minTrackingConfidence = 0.5f;

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private bool _isInitialized;
        private HandLandmarkData _latestResult;
        private long _frameTimestampMs;

#if MEDIAPIPE_INSTALLED
        private HandLandmarker _handLandmarker;
        private HandLandmarkerResult _mpResult;
        private Texture2D _processingTexture;
#endif

        /// <summary>Whether the bridge has been initialized successfully.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>The most recent hand landmark data.</summary>
        public HandLandmarkData LatestResult => _latestResult;

        /// <summary>
        /// Fired each time new landmark data is available.
        /// </summary>
        public event Action<HandLandmarkData> OnLandmarksUpdated;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes the MediaPipe hand tracking pipeline asynchronously.
        /// This is a coroutine because model asset loading is asynchronous.
        /// <para>
        /// If <see cref="Initialize"/> was already called (stub mode),
        /// this upgrades the bridge to use the real HandLandmarker.
        /// </para>
        /// </summary>
        /// <returns>Coroutine enumerator.</returns>
        public IEnumerator InitializeAsync()
        {
            // Ensure basic data structures are ready
            if (!_isInitialized)
            {
                _latestResult = new HandLandmarkData
                {
                    Landmarks = new Vector3[LandmarkCount],
                    IsValid = false
                };
                _frameTimestampMs = 0;
                _isInitialized = true;
            }

#if MEDIAPIPE_INSTALLED
            // Skip if HandLandmarker is already created
            if (_handLandmarker != null)
            {
                Debug.LogWarning("[MediaPipeBridge] HandLandmarker already created.");
                yield break;
            }

            Debug.Log("[MediaPipeBridge] Initializing MediaPipe HandLandmarker...");

            // Step 1: Enable custom resource resolver
            ResourceUtil.EnableCustomResolver();

            // Step 2: Prepare model asset (async)
            // In Editor: uses LocalResourceManager (reads from PackageResources)
            // In Build: uses StreamingAssetsResourceManager (reads from StreamingAssets)
            IResourceManager resourceManager;

#if UNITY_EDITOR
            resourceManager = new LocalResourceManager();
#else
            resourceManager = new StreamingAssetsResourceManager();
#endif

            Debug.Log($"[MediaPipeBridge] Loading model: {ModelFileName}");
            yield return resourceManager.PrepareAssetAsync(ModelFileName);
            Debug.Log("[MediaPipeBridge] Model loaded successfully.");

            // Step 3: Create HandLandmarker with VIDEO running mode
            // VIDEO mode is synchronous with timestamps — simpler than LIVE_STREAM
            // and avoids cross-thread callback complexity.
            try
            {
                var baseOptions = new BaseOptions(
                    BaseOptions.Delegate.CPU,
                    modelAssetPath: ModelFileName);

                var options = new HandLandmarkerOptions(
                    baseOptions,
                    runningMode: RunningMode.VIDEO,
                    numHands: _numHands,
                    minHandDetectionConfidence: _minDetectionConfidence,
                    minHandPresenceConfidence: _minPresenceConfidence,
                    minTrackingConfidence: _minTrackingConfidence);

                _handLandmarker = HandLandmarker.CreateFromOptions(options);

                // Pre-allocate result buffer to avoid GC
                _mpResult = HandLandmarkerResult.Alloc(_numHands);

                Debug.Log("[MediaPipeBridge] HandLandmarker created successfully (VIDEO mode, CPU).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MediaPipeBridge] Failed to create HandLandmarker: {e.Message}\n{e.StackTrace}");
                Debug.LogWarning("[MediaPipeBridge] Falling back to stub mode.");
                _handLandmarker = null;
            }
#else
            Debug.Log("[MediaPipeBridge] Async init complete (stub mode — " +
                      "install MediaPipe Unity Plugin for real tracking).");
            yield return null;
#endif
        }

        /// <summary>
        /// Synchronous initialization — sets up the bridge in stub mode.
        /// <para>
        /// When MediaPipe is NOT installed, this fully initializes the bridge
        /// synchronously (no model to load). When MediaPipe IS installed, this
        /// only sets up the data structures — you must still call
        /// <see cref="InitializeAsync"/> to load the model and create the
        /// HandLandmarker. This allows tests and mock-data scenarios to work
        /// without awaiting a coroutine.
        /// </para>
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[MediaPipeBridge] Already initialized.");
                return;
            }

            _latestResult = new HandLandmarkData
            {
                Landmarks = new Vector3[LandmarkCount],
                IsValid = false
            };

            _frameTimestampMs = 0;
            _isInitialized = true;

#if MEDIAPIPE_INSTALLED
            Debug.Log("[MediaPipeBridge] Initialized (stub mode — " +
                      "call InitializeAsync() to enable real hand tracking).");
#else
            Debug.Log("[MediaPipeBridge] Initialized (stub mode — " +
                      "install MediaPipe Unity Plugin for real tracking).");
#endif
        }

        /// <summary>
        /// Sends a camera frame to MediaPipe for processing.
        /// Results will be available in <see cref="LatestResult"/> and
        /// broadcast via <see cref="OnLandmarksUpdated"/>.
        /// </summary>
        /// <param name="frameTexture">
        /// The current camera frame as a Texture2D (RGBA32).
        /// </param>
        public void ProcessFrame(Texture2D frameTexture)
        {
            if (!_isInitialized || frameTexture == null)
            {
                return;
            }

#if MEDIAPIPE_INSTALLED
            if (_handLandmarker != null)
            {
                ProcessFrameWithMediaPipe(frameTexture);
                return;
            }
#endif

            // Stub fallback: no hand detected
            _latestResult = new HandLandmarkData
            {
                Landmarks = _latestResult.Landmarks,
                IsValid = false
            };

            OnLandmarksUpdated?.Invoke(_latestResult);
        }

        /// <summary>
        /// Shuts down the MediaPipe pipeline and frees resources.
        /// </summary>
        public void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

#if MEDIAPIPE_INSTALLED
            if (_handLandmarker != null)
            {
                try
                {
                    _handLandmarker.Close();
                    ((IDisposable)_handLandmarker).Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MediaPipeBridge] Error during shutdown: {e.Message}");
                }
                _handLandmarker = null;
            }

            if (_processingTexture != null)
            {
                Destroy(_processingTexture);
                _processingTexture = null;
            }
#endif

            _isInitialized = false;
            _frameTimestampMs = 0;
            Debug.Log("[MediaPipeBridge] Shut down.");
        }

        // -----------------------------------------------------------------
        // Debug / Testing support
        // -----------------------------------------------------------------

        /// <summary>
        /// Injects mock landmark data for testing without a camera.
        /// This allows EditMode tests and the test scene to function
        /// without a real MediaPipe pipeline.
        /// </summary>
        /// <param name="mockData">The mock landmark data to inject.</param>
        public void InjectMockData(HandLandmarkData mockData)
        {
            _latestResult = mockData;
            OnLandmarksUpdated?.Invoke(_latestResult);
        }

        // -----------------------------------------------------------------
        // MediaPipe frame processing
        // -----------------------------------------------------------------

#if MEDIAPIPE_INSTALLED
        private void ProcessFrameWithMediaPipe(Texture2D frameTexture)
        {
            // Increment timestamp monotonically (required by VIDEO mode).
            // We use a simple counter-based approach: each frame = +33ms (~30fps).
            // This avoids issues with Time.time not being monotonic across pauses.
            _frameTimestampMs += 33;

            Mediapipe.Image mpImage = null;
            try
            {
                // Create MediaPipe Image from Texture2D.
                // The Image constructor copies pixel data, so frameTexture can
                // be reused immediately after this call.
                mpImage = new Mediapipe.Image(frameTexture);

                // Detect hand landmarks (synchronous in VIDEO mode)
                bool detected = _handLandmarker.TryDetectForVideo(
                    mpImage,
                    _frameTimestampMs,
                    null,
                    ref _mpResult);

                if (detected && _mpResult.handLandmarks != null &&
                    _mpResult.handLandmarks.Count > 0)
                {
                    // Convert MediaPipe result to our HandLandmarkData
                    ConvertResult(_mpResult, ref _latestResult);
                }
                else
                {
                    _latestResult = new HandLandmarkData
                    {
                        Landmarks = _latestResult.Landmarks ?? new Vector3[LandmarkCount],
                        IsValid = false
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MediaPipeBridge] Frame processing error: {e.Message}");
                _latestResult = new HandLandmarkData
                {
                    Landmarks = _latestResult.Landmarks ?? new Vector3[LandmarkCount],
                    IsValid = false
                };
            }
            finally
            {
                // Always dispose the Image to free native memory
                if (mpImage != null)
                {
                    mpImage.Dispose();
                }
            }

            OnLandmarksUpdated?.Invoke(_latestResult);
        }

        /// <summary>
        /// Converts a MediaPipe HandLandmarkerResult into our HandLandmarkData.
        /// Takes the first detected hand only.
        /// </summary>
        private void ConvertResult(HandLandmarkerResult mpResult, ref HandLandmarkData output)
        {
            if (output.Landmarks == null || output.Landmarks.Length < LandmarkCount)
            {
                output.Landmarks = new Vector3[LandmarkCount];
            }

            // Use the first hand's normalized landmarks.
            // Fully qualified type to avoid ambiguity between
            // Mediapipe.NormalizedLandmark and
            // Mediapipe.Tasks.Components.Containers.NormalizedLandmark.
            Mediapipe.Tasks.Components.Containers.NormalizedLandmarks hand =
                mpResult.handLandmarks[0];
            var landmarks = hand.landmarks;

            int count = Mathf.Min(landmarks.Count, LandmarkCount);
            for (int i = 0; i < count; i++)
            {
                var lm = landmarks[i];
                output.Landmarks[i] = new Vector3(lm.x, lm.y, lm.z);
            }

            output.IsValid = true;
        }
#endif

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
