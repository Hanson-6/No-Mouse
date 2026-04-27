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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GestureRecognition.Core;
using GestureRecognition.Detection;
using GestureRecognition.UI;

namespace GestureRecognition.Service
{
    /// <summary>
    /// Facade / singleton that orchestrates the entire gesture recognition
    /// pipeline: Camera -> MediaPipe -> Classifier -> Events.
    /// <para>
    /// 摄像头和 MediaPipe 在首次 StartRecognition 后保持运行，
    /// StopRecognition 只暂停处理循环（不关硬件），
    /// 再次 StartRecognition 时零延迟恢复。
    /// 硬件仅在 OnDestroy（退出应用）时释放。
    /// </para>
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
        private bool _autoStart = true;

        [Tooltip("Automatically create and show the gesture display panel on start.")]
        [SerializeField]
        private bool _autoShowPanel = false;

        [Tooltip("Default display mode for the auto-created panel.")]
        [SerializeField]
        private GestureDisplayPanel.DisplayMode _defaultDisplayMode =
            GestureDisplayPanel.DisplayMode.CameraWithOverlay;

        [Tooltip("Default panel size in pixels.")]
        [SerializeField]
        private Vector2 _defaultPanelSize = new Vector2(320f, 280f);

        [Tooltip("Default panel offset from top-left in pixels (x=left margin, y=distance from top).")]
        [SerializeField]
        private Vector2 _defaultPanelTopLeftOffset = new Vector2(16f, 16f);

        [Header("Camera Settings")]
        [Tooltip("Camera device name. Leave empty to use the default camera.")]
        [SerializeField]
        private string _cameraDeviceName = "";

        [Header("Tracking Settings")]
        [Tooltip("Smoothing factor for hand position. 0 = instant, 0.9 = very smooth.")]
        [SerializeField]
        [Range(0f, 0.95f)]
        private float _positionSmoothing = 0.5f;

        [Header("Dual Hand Debug")]
        [Tooltip("Log dual-hand combo when detected.")]
        [SerializeField] private bool _enableDualHandComboLogs = true;

        [Tooltip("Swap left/right handedness labels if camera setup is not mirrored.")]
        [SerializeField] private bool _swapLeftRightHandedness = false;

        [Tooltip("Minimum handedness confidence required for left/right assignment.")]
        [SerializeField] [Range(0.1f, 1f)] private float _minHandednessConfidence = 0.5f;

        [Header("Camera Diagnostics")]
        [Tooltip("Print concise runtime diagnostic logs for camera state and occlusion events.")]
        [SerializeField]
        private bool _emitDiagnosticLogs = true;

        [Tooltip("Timeout while waiting for webcam startup.")]
        [SerializeField]
        private float _cameraStartupTimeoutSeconds = 5f;

        [Tooltip("Consecutive null/no-update frames before marking camera stream unhealthy.")]
        [SerializeField]
        private int _noFrameThreshold = 20;

        [Tooltip("Seconds without webcam updates before marking camera stream unhealthy.")]
        [SerializeField]
        private float _staleFrameTimeoutSeconds = 1.0f;

        [Tooltip("Recent hand window (seconds) used to improve occlusion confidence.")]
        [SerializeField]
        private float _recentHandWindowSeconds = 1.0f;

        [Header("Occlusion Detection")]
        [Tooltip("Enable camera occlusion detection (e.g., finger covering lens).")]
        [SerializeField]
        private bool _enableOcclusionDetection = true;

        [SerializeField] [Range(2, 8)]
        private int _occlusionSampleStep = 4;

        [SerializeField] [Range(0f, 1f)]
        private float _occlusionDarkThreshold = 0.25f;

        [SerializeField] [Range(0f, 1f)]
        private float _occlusionMinDarkRatio = 0.86f;

        [SerializeField] [Range(0f, 1f)]
        private float _occlusionEnterScore = 0.80f;

        [SerializeField] [Range(0f, 1f)]
        private float _occlusionExitScore = 0.55f;

        [SerializeField]
        private int _occlusionEnterFrames = 12;

        [SerializeField]
        private int _occlusionExitFrames = 8;

        [SerializeField] [Range(0f, 0.25f)]
        private float _occlusionRecentHandBoost = 0.08f;

        [Tooltip("Print periodic occlusion metric values for tuning (verbose).")]
        [SerializeField]
        private bool _emitOcclusionMetricLogs = false;

        [Tooltip("How often to print occlusion metrics (seconds).")]
        [SerializeField]
        private float _occlusionMetricLogInterval = 1.0f;

        [Tooltip("Minimum interval between occlusion state logs (seconds).")]
        [SerializeField]
        private float _occlusionStateLogMinInterval = 0.3f;

        // -----------------------------------------------------------------
        // Runtime components
        // -----------------------------------------------------------------

        private CameraManager _cameraManager;
        private MediaPipeBridge _mediaPipeBridge;
        private GestureClassifier _gestureClassifier;
        private HandTracker _handTracker;

        /// <summary>处理循环是否在跑（对外的"识别中"状态）</summary>
        private bool _isRunning;

        /// <summary>初始化协程是否正在进行，防止重复启动。</summary>
        private bool _isStarting;

        /// <summary>硬件（摄像头+MediaPipe）是否已初始化就绪</summary>
        private bool _hardwareReady;

        // （已移除 _processingCoroutine：不再用协程驱动每帧处理，改用 Update()）

        /// <summary>自动创建的显示面板（如果有）</summary>
        private GestureDisplayPanel _displayPanel;

        private GestureResult _currentResult = GestureResult.Empty;
        private GestureType _previousGestureType = GestureType.None;
        private bool _previousHandDetected;
        private string _lastLoggedDualCombo = string.Empty;
        private bool _hasDualHandPair;
        private bool _hasLeftHandSlot;
        private bool _hasRightHandSlot;
        private GestureType _leftHandGestureType = GestureType.None;
        private GestureType _rightHandGestureType = GestureType.None;
        private float _startTime;
        private float _lastHandSeenRealtime = -999f;
        private int _consecutiveNoFrameCount;
        private float _lastFrameUpdateRealtime = -999f;
        private CameraRuntimeState _cameraState = CameraRuntimeState.Unknown;
        private CameraOcclusionDetector _occlusionDetector;
        private bool _isCameraOccluded;
        private CameraOcclusionMetrics _lastOcclusionMetrics;
        private float _nextOcclusionMetricLogTime;
        private float _lastOcclusionStateLogRealtime = -999f;

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

        /// <summary>Current runtime camera state used for gameplay gating.</summary>
        public CameraRuntimeState CameraState => _cameraState;

        /// <summary>Whether camera lens is currently considered occluded.</summary>
        public bool IsCameraOccluded => _isCameraOccluded;

        /// <summary>Whether both left and right hand slots are currently available.</summary>
        public bool HasDualHandPair => _hasDualHandPair;

        /// <summary>Whether left hand slot currently has a tracked hand.</summary>
        public bool HasLeftHandSlot => _hasLeftHandSlot;

        /// <summary>Whether right hand slot currently has a tracked hand.</summary>
        public bool HasRightHandSlot => _hasRightHandSlot;

        /// <summary>Current classified gesture for the left hand slot.</summary>
        public GestureType LeftHandGestureType => _leftHandGestureType;

        /// <summary>Current classified gesture for the right hand slot.</summary>
        public GestureType RightHandGestureType => _rightHandGestureType;

        /// <summary>Latest occlusion metrics for debugging/calibration.</summary>
        public CameraOcclusionMetrics OcclusionMetrics => _lastOcclusionMetrics;

        // -----------------------------------------------------------------
        // Public API — Frontend calls these
        // -----------------------------------------------------------------

        /// <summary>
        /// Starts the gesture recognition pipeline.
        /// 首次调用时初始化摄像头和 MediaPipe（异步）；
        /// 之后的调用直接恢复处理循环，零延迟。
        /// </summary>
        public void StartRecognition()
        {
            if (_isRunning || _isStarting)
            {
                Debug.LogWarning("[GestureService] Start ignored (already running or starting).");
                return;
            }

            _isStarting = true;
            StartCoroutine(StartRecognitionCoroutine());
        }

        /// <summary>
        /// Stops the recognition processing loop.
        /// 摄像头和 MediaPipe 保持运行（不关硬件），再次 Start 时零延迟。
        /// </summary>
        public void StopRecognition()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _isStarting = false;
            StopAllCoroutines(); // 停掉可能仍在运行的初始化协程

            // 不关闭摄像头和 MediaPipe — 它们继续在后台运行
            _currentResult = GestureResult.Empty;
            _previousGestureType = GestureType.None;
            _lastLoggedDualCombo = string.Empty;
            _lastOcclusionStateLogRealtime = -999f;
            _hasDualHandPair = false;
            _hasLeftHandSlot = false;
            _hasRightHandSlot = false;
            _leftHandGestureType = GestureType.None;
            _rightHandGestureType = GestureType.None;

            GestureEvents.InvokeRecognitionStateChanged(false);
            Debug.Log("[GestureService] Recognition paused (hardware still running).");
        }

        public bool IsCameraReadyForGameplay()
        {
            return _isRunning
                && _cameraState == CameraRuntimeState.Ready
                && !_isCameraOccluded;
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
        /// 这是唯一需要真正重启摄像头的场景。
        /// </summary>
        public void SwitchCamera(string deviceName)
        {
            bool wasRunning = _isRunning;
            if (wasRunning)
            {
                StopRecognition();
            }

            // 切换摄像头时需要真正重启硬件
            if (_cameraManager != null)
            {
                _cameraManager.StopCamera();
            }
            _hardwareReady = false;
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
            GestureEvents.EnableDiagnostics = _emitDiagnosticLogs;

            if (_gestureConfig == null)
            {
                _gestureConfig = Resources.Load<GestureConfig>("GestureConfig");
                if (_gestureConfig == null)
                    Debug.LogWarning("[GestureService] GestureConfig not assigned and Resources/GestureConfig.asset not found.");
            }

            // 跨场景保留：摄像头和 MediaPipe 只初始化一次
            DontDestroyOnLoad(gameObject);

            // Create sub-components on the same GameObject
            _cameraManager = gameObject.AddComponent<CameraManager>();
            _mediaPipeBridge = gameObject.AddComponent<MediaPipeBridge>();
            _gestureClassifier = new GestureClassifier(
                _gestureConfig != null ? _gestureConfig.ConfidenceThreshold : 0.6f);
            _handTracker = new HandTracker(_positionSmoothing);
            _occlusionDetector = new CameraOcclusionDetector(
                _occlusionSampleStep,
                _occlusionDarkThreshold,
                _occlusionMinDarkRatio,
                _occlusionEnterScore,
                _occlusionExitScore,
                _occlusionEnterFrames,
                _occlusionExitFrames,
                _occlusionRecentHandBoost);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 场景加载后回调。
        /// 改用 Update() 驱动处理循环后，场景重载不会影响逐帧处理，
        /// 因此这里只需记录日志即可。
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_isRunning && _hardwareReady)
            {
                Debug.Log($"[GestureService] Scene '{scene.name}' loaded — Update() loop continues automatically.");
            }

            bool hidePanel = scene.name == "MainMenu" || scene.name == "LevelComplete";
            SetDisplayPanelVisibility(!hidePanel, !hidePanel);
        }

        /// <summary>
        /// Controls the floating gesture display panel visibility.
        /// When visible is true and createIfMissing is true, a panel is created if needed.
        /// </summary>
        public void SetDisplayPanelVisibility(bool visible, bool createIfMissing)
        {
            if (visible && createIfMissing && _displayPanel == null)
                EnsureDisplayPanel();

            if (_displayPanel == null)
                return;

            if (visible)
                _displayPanel.Show();
            else
                _displayPanel.Hide();
        }

        private void Start()
        {
            Debug.Log($"[GestureService] Start (autoStart={_autoStart}, autoShowPanel={_autoShowPanel})");

            if (_autoShowPanel)
            {
                EnsureDisplayPanel();
            }

            if (_autoStart)
            {
                StartRecognition();
            }
        }

        private void OnDestroy()
        {
            // ── 重复实例被销毁（场景重载时 Awake 里 Destroy 的那个）──
            // 绝对不能碰硬件、不能清事件，直接返回。
            if (_instance != null && _instance != this)
            {
                Debug.Log("[GestureService] Duplicate instance destroyed — skipping cleanup.");
                return;
            }

            // ── 真正的 singleton 被销毁（退出应用 / 手动 Destroy）──
            _isRunning = false;
            _isStarting = false;
            StopAllCoroutines();

            if (_cameraManager != null)
            {
                _cameraManager.StopCamera();
            }

            if (_mediaPipeBridge != null)
            {
                _mediaPipeBridge.Shutdown();
            }

            GestureEvents.ClearAll();
            _instance = null;

            Debug.Log("[GestureService] Destroyed — hardware released.");
        }

        // -----------------------------------------------------------------
        // Display panel auto-creation
        // -----------------------------------------------------------------

        /// <summary>
        /// 如果场景中不存在 GestureDisplayPanel，自动创建一个 Canvas + Panel。
        /// Panel 使用 DontDestroyOnLoad 跨场景保留。
        /// </summary>
        private void EnsureDisplayPanel()
        {
            // 如果面板已经存在（之前创建过，或手动放在场景里），直接用
            if (_displayPanel != null)
            {
                _displayPanel.Show();
                return;
            }

            // 检查场景里是否已经有面板（可能用户手动添加的）
            _displayPanel = FindObjectOfType<GestureDisplayPanel>(true);
            if (_displayPanel != null)
            {
                _displayPanel.Show();
                return;
            }

            // ── 纯代码创建 Canvas + Panel ──────────────────────────────────

            // 1. 创建 Canvas（UI 根节点）
            GameObject canvasGO = new GameObject("GestureCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // 确保在最上层

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Canvas 跨场景保留
            DontDestroyOnLoad(canvasGO);

            // 2. 创建 Panel（挂 GestureDisplayPanel 组件）
            GameObject panelGO = new GameObject("GesturePanel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            // 左上角，默认使用更小尺寸降低遮挡
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            float leftMargin = Mathf.Max(0f, _defaultPanelTopLeftOffset.x);
            float topOffset = Mathf.Max(0f, _defaultPanelTopLeftOffset.y);
            panelRect.anchoredPosition = new Vector2(leftMargin, -topOffset);
            panelRect.sizeDelta = _defaultPanelSize;

            _displayPanel = panelGO.AddComponent<GestureDisplayPanel>();
            _displayPanel.CurrentMode = _defaultDisplayMode;

            // 3. 确保 EventSystem 存在（UI 交互需要）
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                DontDestroyOnLoad(eventSystemGO);
            }

            Debug.Log("[GestureService] Display panel created and shown.");
        }

        // -----------------------------------------------------------------
        // Internal pipeline
        // -----------------------------------------------------------------

        private IEnumerator StartRecognitionCoroutine()
        {
            GestureEvents.EnableDiagnostics = _emitDiagnosticLogs;

            // ── 首次启动：初始化硬件 ─────────────────────────────────────────
            if (!_hardwareReady)
            {
                Debug.Log("[GestureService] First start — initializing hardware...");
                SetCameraState(CameraRuntimeState.Starting, "start-init");

                if (WebCamTexture.devices.Length == 0)
                {
                    SetCameraState(CameraRuntimeState.NoDevice, "no-webcam-device");
                    Debug.LogError("[GestureService] No webcam detected. Recognition start aborted.");
                    _isStarting = false;
                    yield break;
                }

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
                if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                }

                if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    SetCameraState(CameraRuntimeState.PermissionDenied, "webcam-permission-denied");
                    Debug.LogError("[GestureService] Webcam permission denied. Recognition start aborted.");
                    _isStarting = false;
                    yield break;
                }
#endif

                // 1. Start camera
                string device = string.IsNullOrEmpty(_cameraDeviceName)
                    ? null
                    : _cameraDeviceName;

                yield return _cameraManager.StartCamera(device, _cameraStartupTimeoutSeconds);

                if (!_cameraManager.IsRunning)
                {
                    SetCameraState(CameraRuntimeState.NoFrame, "camera-start-failed");
                    Debug.LogError("[GestureService] Failed to start camera.");
                    _isStarting = false;
                    yield break;
                }

                // 2. Initialize MediaPipe (async — model loading may take time)
                yield return _mediaPipeBridge.InitializeAsync();

                if (!_mediaPipeBridge.IsInitialized)
                {
                    SetCameraState(CameraRuntimeState.BackendUnavailable, "mediapipe-init-failed");
                    Debug.LogError("[GestureService] MediaPipe bridge is not initialized.");
                    _isStarting = false;
                    yield break;
                }

                if (_mediaPipeBridge.IsUsingStubBackend)
                {
                    SetCameraState(CameraRuntimeState.BackendUnavailable, "mediapipe-stub-backend");
                    Debug.LogError("[GestureService] MediaPipe is running in stub backend mode. Recognition start aborted.");
                    _isStarting = false;
                    yield break;
                }

                _hardwareReady = true;
                Debug.Log("[GestureService] Hardware ready (camera + MediaPipe).");
            }
            else
            {
                Debug.Log("[GestureService] Hardware already running — resuming immediately.");
            }

            // ── 恢复处理循环 ─────────────────────────────────────────────────
            _isRunning = true;
            _isStarting = false;
            _startTime = Time.time;
            _previousGestureType = GestureType.None;
            _previousHandDetected = false;
            _lastLoggedDualCombo = string.Empty;
            _hasDualHandPair = false;
            _hasLeftHandSlot = false;
            _hasRightHandSlot = false;
            _leftHandGestureType = GestureType.None;
            _rightHandGestureType = GestureType.None;
            _consecutiveNoFrameCount = 0;
            _lastHandSeenRealtime = Time.realtimeSinceStartup;
            _lastFrameUpdateRealtime = Time.realtimeSinceStartup;
            if (_occlusionDetector != null)
                _occlusionDetector.Reset();
            _isCameraOccluded = false;
            _lastOcclusionMetrics = default(CameraOcclusionMetrics);
            _nextOcclusionMetricLogTime = Time.realtimeSinceStartup + _occlusionMetricLogInterval;
            _lastOcclusionStateLogRealtime = -999f;
            GestureEvents.InvokeCameraOcclusionChanged(false);

            GestureEvents.InvokeRecognitionStateChanged(true);
            SetCameraState(CameraRuntimeState.Ready, "recognition-started");
            Debug.Log("[GestureService] Recognition started.");

            // ── 自动显示面板 ─────────────────────────────────────────────────
            if (_autoShowPanel)
            {
                EnsureDisplayPanel();
            }

            // Update() 会自动开始逐帧调用 ProcessOneFrame()（检查 _isRunning && _hardwareReady）
        }

        /// <summary>
        /// Unity Update() — 每帧调用。
        /// 替代之前的 ProcessingLoopCoroutine 协程方案。
        /// Update() 不会被 SceneManager.LoadScene() 中断（对 DontDestroyOnLoad 对象），
        /// 从根本上解决了场景重载后摄像头画面卡住的问题。
        /// </summary>
        private void Update()
        {
            if (!_isRunning || !_hardwareReady) return;

            try
            {
                ProcessOneFrame();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GestureService] ProcessOneFrame exception (ignored): {ex.Message}");
            }
        }

        private void ProcessOneFrame()
        {
            // Get current camera frame
            Texture2D frame = _cameraManager.GetCurrentFrame();
            bool frameUpdated = _cameraManager.DidUpdateThisFrame;
            float now = Time.realtimeSinceStartup;

            if (frame == null)
            {
                _consecutiveNoFrameCount++;

                bool staleByCount = _consecutiveNoFrameCount >= _noFrameThreshold;
                bool staleByTime =
                    now - _lastFrameUpdateRealtime >= _staleFrameTimeoutSeconds;

                if (staleByCount || staleByTime)
                {
                    CameraRuntimeState degraded = _cameraManager.IsTexturePlaying
                        ? CameraRuntimeState.NoFrame
                        : CameraRuntimeState.StreamStopped;
                    SetCameraState(degraded, "frame-unavailable");
                }

                if (_isCameraOccluded)
                {
                    SetCameraOccluded(false, "stream-unavailable");
                }
                _occlusionDetector?.Reset();

                PublishEmptyResult();
                return;
            }

            if (frameUpdated)
            {
                _consecutiveNoFrameCount = 0;
                _lastFrameUpdateRealtime = now;
                SetCameraState(CameraRuntimeState.Ready, "frame-updated");
            }
            else if (now - _lastFrameUpdateRealtime >= _staleFrameTimeoutSeconds)
            {
                CameraRuntimeState degraded = _cameraManager.IsTexturePlaying
                    ? CameraRuntimeState.NoFrame
                    : CameraRuntimeState.StreamStopped;
                SetCameraState(degraded, "frame-stale");

                if (_isCameraOccluded)
                {
                    SetCameraOccluded(false, "stale-frame");
                }
                _occlusionDetector?.Reset();

                PublishEmptyResult();
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
                _lastHandSeenRealtime = Time.realtimeSinceStartup;
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

            DualHandComboType dualCombo = EvaluateAndLogDualHandCombos(_mediaPipeBridge.LatestMultiResult);
            bool switchComboActive = dualCombo == DualHandComboType.Switch;
            bool invulnerableBodyActive = dualCombo == DualHandComboType.InvulnerableBody;
            if (switchComboActive)
            {
                gestureType = GestureType.Switch;
                confidence = 1f;
            }
            else if (invulnerableBodyActive)
            {
                gestureType = GestureType.InvulnerableBody;
                confidence = 1f;
            }

            // Build result
            float timestamp = Time.time - _startTime;
            _currentResult = new GestureResult(
                gestureType, confidence, handPosition,
                landmarks.IsValid, timestamp);

            if (_enableOcclusionDetection && _occlusionDetector != null)
            {
                bool handSeenRecently =
                    landmarks.IsValid ||
                    (Time.realtimeSinceStartup - _lastHandSeenRealtime) <= _recentHandWindowSeconds;

                bool occluded = _occlusionDetector.Update(
                    _cameraManager.LatestPixelBuffer,
                    _cameraManager.Width,
                    _cameraManager.Height,
                    handSeenRecently,
                    out _lastOcclusionMetrics);

                SetCameraOccluded(occluded, "occlusion-evaluated");

                if (_emitDiagnosticLogs && _emitOcclusionMetricLogs && Time.realtimeSinceStartup >= _nextOcclusionMetricLogTime)
                {
                    Debug.Log(
                        $"[GestureService][Diag] OcclusionMetrics score={_lastOcclusionMetrics.Score:F2} dark={_lastOcclusionMetrics.DarkRatio:F2} var={_lastOcclusionMetrics.LumaVariance:F4} edge={_lastOcclusionMetrics.EdgeDensity:F4}");
                    _nextOcclusionMetricLogTime = Time.realtimeSinceStartup + _occlusionMetricLogInterval;
                }
            }
            else if (_isCameraOccluded)
            {
                SetCameraOccluded(false, "occlusion-disabled");
                _occlusionDetector?.Reset();
            }

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

        private void PublishEmptyResult()
        {
            _currentResult = GestureResult.Empty;
            _lastLoggedDualCombo = string.Empty;
            _hasDualHandPair = false;
            _hasLeftHandSlot = false;
            _hasRightHandSlot = false;
            _leftHandGestureType = GestureType.None;
            _rightHandGestureType = GestureType.None;
            GestureEvents.InvokeGestureUpdated(_currentResult);

            if (_previousGestureType != GestureType.None)
            {
                GestureEvents.InvokeGestureChanged(_currentResult);
                _previousGestureType = GestureType.None;
            }

            if (_previousHandDetected)
            {
                GestureEvents.InvokeHandDetectionChanged(false);
                _previousHandDetected = false;
            }
        }

        private void SetCameraState(CameraRuntimeState state, string reason)
        {
            if (_cameraState == state)
                return;

            _cameraState = state;
            GestureEvents.InvokeCameraStateChanged(state);

            if (_emitDiagnosticLogs)
            {
                Debug.Log($"[GestureService][Diag] CameraState={state} reason={reason}");
            }
        }

        private void SetCameraOccluded(bool occluded, string reason)
        {
            if (_isCameraOccluded == occluded)
                return;

            _isCameraOccluded = occluded;
            GestureEvents.InvokeCameraOcclusionChanged(occluded);

            float now = Time.realtimeSinceStartup;
            if (now - _lastOcclusionStateLogRealtime >= Mathf.Max(0f, _occlusionStateLogMinInterval))
            {
                Debug.Log(occluded ? "[Occlusion] 摄像头被遮住" : "[Occlusion] 摄像头恢复");
                _lastOcclusionStateLogRealtime = now;
            }
        }

        private enum DualHandComboType
        {
            None,
            Switch,
            InvulnerableBody
        }

        private DualHandComboType EvaluateAndLogDualHandCombos(MultiHandLandmarkData multiData)
        {
            if (!TryResolveHandSlots(multiData, out DetectedHandData leftHand, out DetectedHandData rightHand))
            {
                _hasDualHandPair = false;
                _hasLeftHandSlot = false;
                _hasRightHandSlot = false;
                _leftHandGestureType = GestureType.None;
                _rightHandGestureType = GestureType.None;
                _lastLoggedDualCombo = string.Empty;
                return DualHandComboType.None;
            }

            _hasLeftHandSlot = leftHand.IsValid;
            _hasRightHandSlot = rightHand.IsValid;
            _hasDualHandPair = _hasLeftHandSlot && _hasRightHandSlot;

            GestureType leftType = _hasLeftHandSlot ? ClassifyHandGesture(leftHand) : GestureType.None;
            GestureType rightType = _hasRightHandSlot ? ClassifyHandGesture(rightHand) : GestureType.None;
            _leftHandGestureType = leftType;
            _rightHandGestureType = rightType;

            bool switchComboActive = IsSwitchCombo(leftType, rightType);
            bool invulnerableBodyActive = IsInvulnerableBodyCombo(leftType, rightType);

            DualHandComboType comboType = DualHandComboType.None;
            if (switchComboActive)
                comboType = DualHandComboType.Switch;
            else if (invulnerableBodyActive)
                comboType = DualHandComboType.InvulnerableBody;

            if (!_enableDualHandComboLogs)
            {
                _lastLoggedDualCombo = string.Empty;
                return comboType;
            }

            string combo = string.Empty;
            if (switchComboActive)
            {
                combo = leftType == GestureType.Push
                    ? "SWITCH（左 PUSH + 右 FIST）"
                    : "SWITCH（左 FIST + 右 PUSH）";
            }
            else if (invulnerableBodyActive)
            {
                combo = "INVULNERABLE BODY（双 FIST）";
            }

            if (string.IsNullOrEmpty(combo))
            {
                _lastLoggedDualCombo = string.Empty;
                return comboType;
            }

            if (!string.Equals(_lastLoggedDualCombo, combo))
            {
                Debug.Log($"[DualHand] {combo}");
                _lastLoggedDualCombo = combo;
            }

            return comboType;
        }

        private static bool IsSwitchCombo(GestureType leftType, GestureType rightType)
        {
            bool leftPushRightFist = leftType == GestureType.Push && rightType == GestureType.Fist;
            bool leftFistRightPush = leftType == GestureType.Fist && rightType == GestureType.Push;
            return leftPushRightFist || leftFistRightPush;
        }

        private static bool IsInvulnerableBodyCombo(GestureType leftType, GestureType rightType)
        {
            return leftType == GestureType.Fist && rightType == GestureType.Fist;
        }

        private bool TryResolveHandSlots(
            MultiHandLandmarkData multiData,
            out DetectedHandData leftHand,
            out DetectedHandData rightHand)
        {
            leftHand = default;
            rightHand = default;

            if (!multiData.IsValid)
                return false;

            for (int i = 0; i < multiData.Hands.Length; i++)
            {
                DetectedHandData hand = multiData.Hands[i];
                if (!hand.IsValid || hand.Landmarks == null || hand.Landmarks.Length < MediaPipeBridge.LandmarkCount)
                    continue;

                string handedness = NormalizeHandedness(hand.Handedness);
                bool confident = hand.HandednessScore >= _minHandednessConfidence;

                if (confident && handedness == "Left")
                {
                    if (!leftHand.IsValid)
                        leftHand = hand;
                    continue;
                }

                if (confident && handedness == "Right")
                {
                    if (!rightHand.IsValid)
                        rightHand = hand;
                    continue;
                }
            }

            if (leftHand.IsValid && rightHand.IsValid)
                return true;

            // Fallback by image x-ordering.
            // For two hands: smaller x -> left, larger x -> right.
            // For one hand: fill whichever side is currently missing.
            int firstIndex = -1;
            int secondIndex = -1;
            float firstX = 0f;
            float secondX = 0f;

            for (int i = 0; i < multiData.Hands.Length; i++)
            {
                DetectedHandData hand = multiData.Hands[i];
                if (!hand.IsValid || hand.Landmarks == null || hand.Landmarks.Length < MediaPipeBridge.LandmarkCount)
                    continue;

                float x = HandTracker.ComputePalmCenter(hand.Landmarks).x;
                if (firstIndex < 0)
                {
                    firstIndex = i;
                    firstX = x;
                }
                else if (secondIndex < 0)
                {
                    secondIndex = i;
                    secondX = x;
                }
            }

            if (firstIndex < 0)
                return false;

            if (secondIndex < 0)
            {
                // If handedness already assigned one slot, keep it as single-hand state.
                if (leftHand.IsValid || rightHand.IsValid)
                    return true;

                // No handedness info: pick side by image-space x position.
                if (firstX <= 0.5f)
                    leftHand = multiData.Hands[firstIndex];
                else
                    rightHand = multiData.Hands[firstIndex];
                return true;
            }

            if (firstX <= secondX)
            {
                if (!leftHand.IsValid) leftHand = multiData.Hands[firstIndex];
                if (!rightHand.IsValid) rightHand = multiData.Hands[secondIndex];
            }
            else
            {
                if (!leftHand.IsValid) leftHand = multiData.Hands[secondIndex];
                if (!rightHand.IsValid) rightHand = multiData.Hands[firstIndex];
            }

            return leftHand.IsValid || rightHand.IsValid;
        }

        private string NormalizeHandedness(string handedness)
        {
            if (string.IsNullOrEmpty(handedness))
                return string.Empty;

            string normalized = handedness.Trim();
            if (string.Equals(normalized, "Left", System.StringComparison.OrdinalIgnoreCase))
                normalized = "Left";
            else if (string.Equals(normalized, "Right", System.StringComparison.OrdinalIgnoreCase))
                normalized = "Right";
            else
                return string.Empty;

            if (_swapLeftRightHandedness)
                return normalized == "Left" ? "Right" : "Left";

            return normalized;
        }

        private GestureType ClassifyHandGesture(DetectedHandData hand)
        {
            if (!hand.IsValid || hand.Landmarks == null || hand.Landmarks.Length < MediaPipeBridge.LandmarkCount)
                return GestureType.None;

            return _gestureClassifier.Classify(hand.Landmarks, out _);
        }
    }
}
