// ============================================================================
// GestureDisplayPanel.cs
// Resizable, draggable floating panel that shows gesture recognition results.
// Can display live camera feed, cartoon sprite, or both overlaid.
//
// FEATURES:
//   - Draggable title bar �?click and drag to move the panel
//   - Resizable �?drag the bottom-right corner to resize
//   - Pure UI �?showing/hiding the panel does NOT affect GestureService
//   - Content scales proportionally with panel size
//   - Close button in the title bar
//
// FRONTEND USAGE:
//   // Show the panel (pure UI, does not affect recognition):
//   GestureDisplayPanel panel = FindObjectOfType<GestureDisplayPanel>();
//   panel.Show();
//
// UNITY SETUP (creating the Prefab from scratch):
//   See the detailed instructions in the AGENTS.md "Step-by-step" section,
//   or simply use the pre-built Prefab at:
//     Assets/Prefabs/GesturePanel.prefab
//
// PREFAB HIERARCHY:
//   GesturePanel (this script + CanvasGroup + Image background)
//   ├── TitleBar (Image + drag handler)
//   �?  ├── TitleText (Text)
//   �?  └── CloseButton (Button + Image)
//   �?      └── CloseText (Text "X")
//   ├── ContentArea (RectTransform, mask)
//   �?  ├── CameraFeed (RawImage)
//   �?  └── GestureSprite (Image, preserveAspect)
//   ├── LabelArea (bottom bar)
//   �?  ├── GestureLabel (Text)
//   �?  └── ConfidenceLabel (Text)
//   └── ResizeHandle (bottom-right corner drag handle)
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GestureRecognition.Core;
using GestureRecognition.Service;

namespace GestureRecognition.UI
{
    /// <summary>
    /// Resizable, draggable floating panel that displays gesture recognition
    /// results. Supports camera feed, cartoon sprite, or overlay mode.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class GestureDisplayPanel : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Display modes
        // -----------------------------------------------------------------

        public enum DisplayMode
        {
            /// <summary>Shows the live camera feed. (Deprecated visually, maps to CameraWithOverlay)</summary>
            CameraFeed,

            /// <summary>Shows a cartoon sprite for the detected gesture.</summary>
            CartoonSprite,

            /// <summary>Camera feed with cartoon sprite overlaid in corner.</summary>
            CameraWithOverlay
        }

        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------

        [Header("Display Settings")]
        [SerializeField]
        private DisplayMode _displayMode = DisplayMode.CameraWithOverlay;

        [Tooltip("Reference to GestureConfig for sprite lookup. " +
                 "If null, will try to get from GestureService.")]
        [SerializeField]
        private GestureConfig _gestureConfig;

        [Header("Panel Behaviour")]
        [Tooltip("Minimum panel size in pixels.")]
        [SerializeField]
        private Vector2 _minSize = new Vector2(200f, 180f);

        [Tooltip("Maximum panel size in pixels. 0 = unlimited.")]
        [SerializeField]
        private Vector2 _maxSize = new Vector2(800f, 700f);

        [Header("Appearance")]
        [Tooltip("Optional custom frame sprite for the panel background")]
        [SerializeField]
        private Sprite _panelFrameSprite;

        [SerializeField]
        private Color _titleBarColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        [SerializeField]
        private Color _panelBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        [SerializeField]
        private float _titleBarHeight = 32f;

        [SerializeField]
        private float _labelBarHeight = 36f; // Slightly reduced so the camera area gets more vertical space.

        [SerializeField]
        [Range(10, 32)]
        [Tooltip("Base font size for all bottom bar labels.")]
        private int _labelFontSize = 15;

        [SerializeField]
        [Tooltip("Panel height used as the baseline for label font scaling.")]
        private float _labelFontReferenceHeight = 300f;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Minimum scale multiplier applied to bottom bar text.")]
        private float _labelFontMinScale = 0.5f;

        [SerializeField]
        [Range(1f, 5f)]
        [Tooltip("Maximum scale multiplier applied to bottom bar text.")]
        private float _labelFontMaxScale = 3.0f;

        [Header("Input")]
        [SerializeField]
        [Tooltip("Shortcut key to toggle display mode (Camera/Sprite/Overlay)")]
        private KeyCode _toggleModeKey = KeyCode.C;

        [Header("Occlusion Avoidance")]
        [SerializeField]
        [Tooltip("Fade the panel when it covers the player's on-screen position.")]
        private bool _avoidCoveringPlayer = true;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Panel alpha while player is behind it on screen.")]
        private float _coveredPlayerAlpha = 0.2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra overlap detection padding in pixels.")]
        private float _coverDetectionPadding = 20f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How fast alpha transitions between normal and faded states.")]
        private float _alphaTransitionSpeed = 8f;

        [SerializeField]
        [Tooltip("World-space offset from player position used for overlap checks.")]
        private Vector3 _playerProbeOffset = new Vector3(0f, 0.6f, 0f);

        [Header("UI References (auto-created if null)")]
        [SerializeField]
        private RawImage _cameraImage;

        [SerializeField]
        private Image _gestureImage;

        [SerializeField]
        private Image _leftGestureImage;

        [SerializeField]
        private Image _rightGestureImage;

        [SerializeField]
        private Text _floorLabel;

        [SerializeField]
        private Text _scoreLabel;

        [SerializeField]
        private Text _timerLabel;

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private GestureType _lastDisplayedGesture = GestureType.None;
        private bool _uiBuilt;
        private GestureType _lastLeftGesture = GestureType.None;
        private GestureType _lastRightGesture = GestureType.None;

        // Child references created at runtime
        private RectTransform _titleBar;
        private RectTransform _contentArea;
        private RectTransform _labelArea;
        private RectTransform _resizeHandle;
        private Button _closeButton;
        private Font _defaultFont;
        private Sprite _roundedMaskSprite;
        private Transform _playerTransform;
        private Camera _worldCamera;
        private Canvas _rootCanvas;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// TODO: This method is a placeholder for future integration with the game's internal state.
        /// Updates the game statistics displayed in the bottom bar.
        /// (Placeholder interface for future protocol)
        /// </summary>
        public void UpdateGameStats(string floorName, int score, float timer)
        {
            if (_floorLabel != null)
                _floorLabel.gameObject.SetActive(false);

            if (_scoreLabel != null)
                _scoreLabel.gameObject.SetActive(false);

            if (_timerLabel != null)
                _timerLabel.text = FormatTime(timer);
        }

        private static string FormatTime(float timer)
        {
            int hours = Mathf.FloorToInt(timer / 3600f);
            int minutes = Mathf.FloorToInt((timer % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(timer % 60f);
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        /// <summary>Gets or sets the current display mode.</summary>
        public DisplayMode CurrentMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                UpdateDisplayMode();
            }
        }

        /// <summary>
        /// Shows the panel (pure UI �?does not affect GestureService).
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the panel (pure UI �?does not affect GestureService).
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Toggles panel visibility.</summary>
        public void Toggle()
        {
            if (gameObject.activeSelf)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// Sets the panel size in pixels. Content auto-scales.
        /// </summary>
        public void SetSize(float width, float height)
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            width = Mathf.Clamp(width,
                _minSize.x, _maxSize.x > 0 ? _maxSize.x : float.MaxValue);
            height = Mathf.Clamp(height,
                _minSize.y, _maxSize.y > 0 ? _maxSize.y : float.MaxValue);

            _rectTransform.sizeDelta = new Vector2(width, height);
            ApplyBottomBarFontSize();
        }

        /// <summary>Current panel size in pixels.</summary>
        public Vector2 Size => _rectTransform != null
            ? _rectTransform.sizeDelta
            : Vector2.zero;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_gestureConfig == null && GestureService.Instance != null)
            {
                _gestureConfig = GestureService.Instance.Config;
            }

            if (_panelFrameSprite == null)
            {
                _panelFrameSprite = Resources.Load<Sprite>("GestureDisplayPanel/PanelBorder_narrow");
            }

            _rootCanvas = GetComponentInParent<Canvas>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            BuildUI();
        }

        private void OnEnable()
        {
            GestureEvents.OnGestureUpdated += HandleGestureUpdated;
            RefreshOcclusionReferences();
        }

        private void OnDisable()
        {
            GestureEvents.OnGestureUpdated -= HandleGestureUpdated;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_uiBuilt)
                return;

            ApplyBottomBarFontSize();
        }

        private void Start()
        {
            if (_gestureConfig == null && GestureService.Instance != null)
            {
                _gestureConfig = GestureService.Instance.Config;
            }

            // Treat CameraFeed identically to CameraWithOverlay
            if (_displayMode == DisplayMode.CameraFeed)
            {
                _displayMode = DisplayMode.CameraWithOverlay;
            }

            EnsureDualGestureSprites();
            UpdateDisplayMode();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleModeKey))
            {
                // Toggle between the two allowed modes (ignoring CameraFeed for cycle)
                CurrentMode = _displayMode == DisplayMode.CartoonSprite
                    ? DisplayMode.CameraWithOverlay
                    : DisplayMode.CartoonSprite;
            }

            if (_cameraImage != null &&
                (_displayMode == DisplayMode.CameraWithOverlay || _displayMode == DisplayMode.CameraFeed) &&
                GestureService.Instance != null &&
                GestureService.Instance.Camera != null)
            {
                Texture cameraTexture = GestureService.Instance.Camera.CameraTexture;
                if (cameraTexture != null && _cameraImage.texture != cameraTexture)
                {
                    _cameraImage.texture = cameraTexture;
                }
            }

            UpdateOcclusionAvoidance();
        }

        private void RefreshOcclusionReferences()
        {
            if (_playerTransform == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                    _playerTransform = playerObject.transform;
            }

            if (_worldCamera == null || !_worldCamera.isActiveAndEnabled)
            {
                _worldCamera = Camera.main;
                if (_worldCamera == null)
                {
                    Camera[] cameras = FindObjectsOfType<Camera>();
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        if (!cameras[i].isActiveAndEnabled)
                            continue;

                        _worldCamera = cameras[i];
                        break;
                    }
                }
            }

            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();
        }

        private void UpdateOcclusionAvoidance()
        {
            if (_canvasGroup == null)
                return;

            if (!_avoidCoveringPlayer)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, _alphaTransitionSpeed * Time.unscaledDeltaTime);
                return;
            }

            RefreshOcclusionReferences();

            bool isCoveringPlayer = false;
            if (_playerTransform != null && _worldCamera != null && _rectTransform != null)
            {
                Vector3 playerScreen = _worldCamera.WorldToScreenPoint(_playerTransform.position + _playerProbeOffset);
                if (playerScreen.z > 0f)
                {
                    isCoveringPlayer = IsScreenPointInsidePanel(playerScreen);
                }
            }

            float targetAlpha = isCoveringPlayer ? Mathf.Clamp01(_coveredPlayerAlpha) : 1f;
            float alphaStep = Mathf.Max(0f, _alphaTransitionSpeed) * Time.unscaledDeltaTime;
            if (alphaStep <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
            }
            else
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, alphaStep);
            }
        }

        private bool IsScreenPointInsidePanel(Vector2 screenPoint)
        {
            Camera uiCamera = null;
            if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = _rootCanvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, screenPoint, uiCamera, out Vector2 localPoint))
                return false;

            Rect rect = _rectTransform.rect;
            float padding = Mathf.Max(0f, _coverDetectionPadding);
            rect.xMin -= padding;
            rect.xMax += padding;
            rect.yMin -= padding;
            rect.yMax += padding;
            return rect.Contains(localPoint);
        }

        // -----------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------

        private void HandleGestureUpdated(GestureResult result)
        {
            // Update camera feed texture
            if (_cameraImage != null &&
                (_displayMode == DisplayMode.CameraFeed || _displayMode == DisplayMode.CameraWithOverlay))
            {
                if (GestureService.Instance != null &&
                    GestureService.Instance.Camera != null)
                {
                    _cameraImage.texture =
                        GestureService.Instance.Camera.CameraTexture;
                }
            }

            // Update gesture sprite
            if (_gestureImage != null && _gestureConfig != null)
            {
                if (result.Type != _lastDisplayedGesture || !result.IsHandDetected)
                {
                    Sprite sprite = result.IsHandDetected
                        ? _gestureConfig.GetSprite(result.Type)
                        : _gestureConfig.NoneSprite;

                    if (sprite != null)
                    {
                        _gestureImage.sprite = sprite;
                        _gestureImage.enabled = true;
                    }
                    else
                    {
                        _gestureImage.enabled = false;
                    }

                    _lastDisplayedGesture = result.Type;
                }
            }

            UpdateDualHandOverlay();
        }

        private void UpdateDualHandOverlay()
        {
            if (_gestureConfig == null || _leftGestureImage == null || _rightGestureImage == null)
                return;

            if (_displayMode != DisplayMode.CameraFeed && _displayMode != DisplayMode.CameraWithOverlay)
                return;

            GestureService service = GestureService.Instance;
            if (service == null || (!service.HasLeftHandSlot && !service.HasRightHandSlot))
            {
                if (_leftGestureImage.gameObject.activeSelf) _leftGestureImage.gameObject.SetActive(false);
                if (_rightGestureImage.gameObject.activeSelf) _rightGestureImage.gameObject.SetActive(false);
                if (_gestureImage != null && !_gestureImage.gameObject.activeSelf)
                    _gestureImage.gameObject.SetActive(true);
                _lastLeftGesture = GestureType.None;
                _lastRightGesture = GestureType.None;
                return;
            }

            GestureType leftType = service.LeftHandGestureType;
            GestureType rightType = service.RightHandGestureType;
            bool hasLeft = service.HasLeftHandSlot;
            bool hasRight = service.HasRightHandSlot;

            Sprite leftSprite = hasLeft ? ResolveOverlayGestureSprite(leftType) : null;
            Sprite rightSprite = hasRight ? ResolveOverlayGestureSprite(rightType) : null;

            if (leftSprite != null)
            {
                if (leftType != _lastLeftGesture || _leftGestureImage.sprite != leftSprite)
                    _leftGestureImage.sprite = leftSprite;
                if (!_leftGestureImage.gameObject.activeSelf) _leftGestureImage.gameObject.SetActive(true);
            }
            else
            {
                if (_leftGestureImage.gameObject.activeSelf) _leftGestureImage.gameObject.SetActive(false);
            }

            if (rightSprite != null)
            {
                if (rightType != _lastRightGesture || _rightGestureImage.sprite != rightSprite)
                    _rightGestureImage.sprite = rightSprite;
                if (!_rightGestureImage.gameObject.activeSelf) _rightGestureImage.gameObject.SetActive(true);
            }
            else
            {
                if (_rightGestureImage.gameObject.activeSelf) _rightGestureImage.gameObject.SetActive(false);
            }

            if (_gestureImage != null && _gestureImage.gameObject.activeSelf)
                _gestureImage.gameObject.SetActive(false);

            _lastLeftGesture = leftType;
            _lastRightGesture = rightType;
        }

        private Sprite ResolveOverlayGestureSprite(GestureType type)
        {
            if (type == GestureType.None || type == GestureType.Count)
                return null;

            return _gestureConfig.GetSprite(type);
        }

        // -----------------------------------------------------------------
        // Display mode switching
        // -----------------------------------------------------------------

        private void UpdateDisplayMode()
        {
            switch (_displayMode)
            {
                case DisplayMode.CameraFeed:
                case DisplayMode.CameraWithOverlay:
                    if (_cameraImage != null) _cameraImage.gameObject.SetActive(true);
                    if (_gestureImage != null)
                    {
                        _gestureImage.gameObject.SetActive(true);
                        // In overlay mode, shrink the sprite to the corner
                        SetOverlayLayout();
                    }
                    if (_leftGestureImage != null)
                    {
                        _leftGestureImage.gameObject.SetActive(false);
                        SetLeftOverlayLayout();
                    }
                    if (_rightGestureImage != null)
                    {
                        _rightGestureImage.gameObject.SetActive(false);
                        SetRightOverlayLayout();
                    }
                    break;

                case DisplayMode.CartoonSprite:
                    if (_cameraImage != null) _cameraImage.gameObject.SetActive(false);
                    if (_gestureImage != null)
                    {
                        _gestureImage.gameObject.SetActive(true);
                        ResetOverlayLayout();
                    }
                    if (_leftGestureImage != null) _leftGestureImage.gameObject.SetActive(false);
                    if (_rightGestureImage != null) _rightGestureImage.gameObject.SetActive(false);
                    break;
            }
        }

        private void SetOverlayLayout()
        {
            if (_gestureImage == null) return;

            RectTransform rt = _gestureImage.rectTransform;
            // Bottom-right corner, 30% of panel size
            rt.anchorMin = new Vector2(0.65f, 0f);
            rt.anchorMax = new Vector2(1f, 0.4f);
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
        }

        private void SetLeftOverlayLayout()
        {
            if (_leftGestureImage == null) return;

            RectTransform rt = _leftGestureImage.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.35f, 0.4f);
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
        }

        private void SetRightOverlayLayout()
        {
            if (_rightGestureImage == null) return;

            RectTransform rt = _rightGestureImage.rectTransform;
            rt.anchorMin = new Vector2(0.65f, 0f);
            rt.anchorMax = new Vector2(1f, 0.4f);
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
        }

        private void ResetOverlayLayout()
        {
            if (_gestureImage == null) return;

            RectTransform rt = _gestureImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // -----------------------------------------------------------------
        // Procedural Sprite Generator
        // -----------------------------------------------------------------

        /// <summary>
        /// Generates a white rounded 9-slice sprite to use as a mask
        /// </summary>
        private Sprite GenerateRoundedSprite(int width = 64, int height = 64, int radius = 16)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Compute distance to corner centers
                    int cx = x < radius ? radius : (x >= width - radius ? width - radius - 1 : x);
                    int cy = y < radius ? radius : (y >= height - radius ? height - radius - 1 : y);
                    
                    if (cx != x && cy != y)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        // Smooth alphatest (cheap anti-aliasing)
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        pixels[y * width + x] = new Color(1, 1, 1, alpha);
                    }
                    else
                    {
                        pixels[y * width + x] = Color.white;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            
            Vector4 border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        // -----------------------------------------------------------------
        // UI Construction (builds the full panel hierarchy at runtime)
        // -----------------------------------------------------------------

        private struct FrameLayout
        {
            public float HorizontalPadding;
            public float TopPadding;
            public float BottomPadding;
            public float CameraGap;
            public float LabelHorizontalPadding;
            public float LabelBottomPadding;
        }

        private void BuildUI()
        {
            if (_uiBuilt) return;

            ApplyPanelBackground();

            FrameLayout layout = BuildFrameLayout();
            BuildTitleBar(layout);
            BuildContentArea(layout);
            BuildLabelArea(layout);
            BuildResizeHandle();
            ApplyBottomBarFontSize();

            _uiBuilt = true;
        }

        private void ApplyPanelBackground()
        {
            Image bg = GetOrAddComponent<Image>(gameObject);

            if (_panelFrameSprite != null)
            {
                bg.sprite = _panelFrameSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                return;
            }

            bg.sprite = null;
            bg.type = Image.Type.Simple;
            bg.color = _panelBackgroundColor;
        }

        private FrameLayout BuildFrameLayout()
        {
            bool useFrame = _panelFrameSprite != null;

            float horizontalPadding = useFrame ? 0.08f : 0f;
            float topPadding = useFrame ? 0.03f : 0f;
            float bottomPadding = useFrame ? 0.12f : 0f;

            return new FrameLayout
            {
                HorizontalPadding = horizontalPadding,
                TopPadding = topPadding,
                BottomPadding = bottomPadding,
                CameraGap = useFrame ? 0.03f : 0f,
                LabelHorizontalPadding = useFrame ? 0.10f : 0.08f,
                LabelBottomPadding = useFrame ? bottomPadding * 0.6f : bottomPadding
            };
        }

        private void BuildTitleBar(FrameLayout layout)
        {
            _titleBar = GetOrCreateChild("TitleBar");
            _titleBar.anchorMin = new Vector2(layout.HorizontalPadding, 1f - layout.TopPadding);
            _titleBar.anchorMax = new Vector2(1f - layout.HorizontalPadding, 1f - layout.TopPadding);
            _titleBar.pivot = new Vector2(0.5f, 1f);
            _titleBar.anchoredPosition = Vector2.zero;
            _titleBar.sizeDelta = new Vector2(0f, _titleBarHeight);

            Image titleBg = GetOrAddComponent<Image>(_titleBar.gameObject);
            titleBg.color = _panelFrameSprite != null ? Color.clear : _titleBarColor;

            GetOrAddComponent<PanelDragHandler>(_titleBar.gameObject);

            BuildTitleText();
            BuildCloseButton();
        }

        private void BuildTitleText()
        {
            RectTransform titleTextRt = GetOrCreateChild("TitleText", _titleBar);
            titleTextRt.anchorMin = Vector2.zero;
            titleTextRt.anchorMax = Vector2.one;
            titleTextRt.offsetMin = new Vector2(8f, 0f);
            titleTextRt.offsetMax = new Vector2(-36f, 0f);

            Text titleText = GetOrAddComponent<Text>(titleTextRt.gameObject);
            ConfigureText(
                titleText,
                _panelFrameSprite != null ? string.Empty : "Gesture Recognition",
                Color.white,
                TextAnchor.MiddleLeft,
                10,
                30);
        }

        private void BuildCloseButton()
        {
            RectTransform closeBtnRt = GetOrCreateChild("CloseButton", transform as RectTransform);
            closeBtnRt.anchorMin = new Vector2(1f, 1f);
            closeBtnRt.anchorMax = new Vector2(1f, 1f);
            closeBtnRt.pivot = new Vector2(1f, 1f);
            closeBtnRt.anchoredPosition = new Vector2(-6f, -6f);
            closeBtnRt.sizeDelta = new Vector2(_titleBarHeight, _titleBarHeight);

            Image closeBtnImg = GetOrAddComponent<Image>(closeBtnRt.gameObject);
            closeBtnImg.color = _panelFrameSprite != null
                ? Color.clear
                : new Color(0.8f, 0.2f, 0.2f, 0.8f);

            _closeButton = GetOrAddComponent<Button>(closeBtnRt.gameObject);
            _closeButton.targetGraphic = closeBtnImg;
            _closeButton.onClick.RemoveListener(Hide);
            _closeButton.onClick.AddListener(Hide);

            RectTransform closeTextRt = GetOrCreateChild("CloseText", closeBtnRt);
            StretchToParent(closeTextRt);

            Text closeText = GetOrAddComponent<Text>(closeTextRt.gameObject);
            ConfigureText(closeText, "\u00D7", Color.white, TextAnchor.MiddleCenter, 10, 30);
        }

        private void BuildContentArea(FrameLayout layout)
        {
            _contentArea = GetOrCreateChild("ContentArea");
            _contentArea.anchorMin = new Vector2(layout.HorizontalPadding, layout.BottomPadding + layout.CameraGap);
            _contentArea.anchorMax = new Vector2(1f - layout.HorizontalPadding, 1f - layout.TopPadding);
            _contentArea.offsetMin = new Vector2(0f, _labelBarHeight);
            _contentArea.offsetMax = new Vector2(0f, -_titleBarHeight);

            Image contentBg = GetOrAddComponent<Image>(_contentArea.gameObject);
            if (_panelFrameSprite != null)
            {
                contentBg.sprite = GetRoundedMaskSprite();
                contentBg.type = Image.Type.Sliced;
                contentBg.color = new Color(0f, 0f, 0f, 0.01f);
            }
            else
            {
                contentBg.sprite = null;
                contentBg.type = Image.Type.Simple;
                contentBg.color = new Color(0f, 0f, 0f, 0.5f);
            }

            Mask mask = GetOrAddComponent<Mask>(_contentArea.gameObject);
            mask.showMaskGraphic = true;

            EnsureCameraFeed();
            EnsureGestureSprite();
        }

        private void EnsureCameraFeed()
        {
            if (_cameraImage != null)
                return;

            RectTransform camRt = GetOrCreateChild("CameraFeed", _contentArea);
            StretchToParent(camRt);

            _cameraImage = GetOrAddComponent<RawImage>(camRt.gameObject);
            _cameraImage.color = Color.white;
        }

        private void EnsureGestureSprite()
        {
            if (_gestureImage != null)
            {
                EnsureDualGestureSprites();
                return;
            }

            RectTransform spriteRt = GetOrCreateChild("GestureSprite", _contentArea);
            StretchToParent(spriteRt);

            _gestureImage = GetOrAddComponent<Image>(spriteRt.gameObject);
            _gestureImage.preserveAspect = true;

            EnsureDualGestureSprites();
        }

        private void EnsureDualGestureSprites()
        {
            if (_leftGestureImage == null)
            {
                RectTransform leftRt = GetOrCreateChild("LeftGestureSprite", _contentArea);
                _leftGestureImage = GetOrAddComponent<Image>(leftRt.gameObject);
                _leftGestureImage.preserveAspect = true;
            }

            if (_rightGestureImage == null)
            {
                RectTransform rightRt = GetOrCreateChild("RightGestureSprite", _contentArea);
                _rightGestureImage = GetOrAddComponent<Image>(rightRt.gameObject);
                _rightGestureImage.preserveAspect = true;
            }

            SetLeftOverlayLayout();
            SetRightOverlayLayout();

            _leftGestureImage.gameObject.SetActive(false);
            _rightGestureImage.gameObject.SetActive(false);
        }

        private void BuildLabelArea(FrameLayout layout)
        {
            _labelArea = GetOrCreateChild("LabelArea");
            _labelArea.anchorMin = new Vector2(layout.LabelHorizontalPadding, layout.LabelBottomPadding);
            _labelArea.anchorMax = new Vector2(1f - layout.LabelHorizontalPadding, layout.LabelBottomPadding);
            _labelArea.pivot = new Vector2(0.5f, 0f);
            _labelArea.anchoredPosition = Vector2.zero;
            _labelArea.sizeDelta = new Vector2(0f, _labelBarHeight);

            Image labelBg = GetOrAddComponent<Image>(_labelArea.gameObject);
            labelBg.color = _panelFrameSprite != null ? Color.clear : new Color(0f, 0f, 0f, 0.72f);

            // Disable legacy HealthLabel if present
            Transform legacyHealth = _labelArea.Find("HealthLabel");
            if (legacyHealth != null)
                legacyHealth.gameObject.SetActive(false);

            EnsureBottomBarLabel(
                ref _scoreLabel,
                "ScoreLabel",
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 1f),
                new Vector2(6f, -1f),
                new Vector2(0f, 0f),
                new Color(1f, 0.95f, 0.45f, 1f),
                TextAnchor.MiddleLeft,
                "SCORE  0");

            EnsureBottomBarLabel(
                ref _floorLabel,
                "FloorLabel",
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(6f, 0f),
                new Vector2(0f, 1f),
                new Color(0.84f, 0.97f, 1f, 1f),
                TextAnchor.MiddleLeft,
                "STAGE  --");

            EnsureBottomBarLabel(
                ref _timerLabel,
                "TimerLabel",
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Color(1f, 1f, 1f, 0.95f),
                TextAnchor.MiddleCenter,
                "00:00:00");
        }

        private void EnsureBottomBarLabel(
            ref Text label,
            string childName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color,
            TextAnchor alignment,
            string defaultValue)
        {
            RectTransform labelRt;

            if (label == null)
            {
                labelRt = GetOrCreateChild(childName, _labelArea);
                label = GetOrAddComponent<Text>(labelRt.gameObject);
            }
            else
            {
                labelRt = label.rectTransform;
                if (labelRt.parent != _labelArea)
                {
                    labelRt.SetParent(_labelArea, false);
                }
            }

            labelRt.anchorMin = anchorMin;
            labelRt.anchorMax = anchorMax;
            labelRt.offsetMin = offsetMin;
            labelRt.offsetMax = offsetMax;

            string value = string.IsNullOrEmpty(label.text)
                ? defaultValue
                : label.text;
            ConfigureBottomBarText(label, value, color, alignment);
        }

        private void ConfigureBottomBarText(
            Text text,
            string value,
            Color color,
            TextAnchor alignment)
        {
            text.text = value;
            text.font = GetDefaultFont();
            text.resizeTextForBestFit = false;
            text.fontSize = GetScaledLabelFontSize();
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontStyle = FontStyle.Bold;
        }

        private void ApplyBottomBarFontSize()
        {
            int fontSize = GetScaledLabelFontSize();
            ApplyLabelFontSize(_floorLabel, fontSize);
            ApplyLabelFontSize(_scoreLabel, fontSize);
            ApplyLabelFontSize(_timerLabel, Mathf.RoundToInt(fontSize * 1.25f));

            if (_labelArea != null)
            {
                float referenceHeight = Mathf.Max(1f, _labelFontReferenceHeight);
                float panelHeight = Mathf.Max(_minSize.y, _rectTransform != null ? _rectTransform.rect.height : 0f);
                float scale = Mathf.Clamp(panelHeight / referenceHeight, _labelFontMinScale, _labelFontMaxScale);
                _labelArea.sizeDelta = new Vector2(0f, _labelBarHeight * scale);
            }
        }

        private void ApplyLabelFontSize(Text label, int fontSize)
        {
            if (label == null)
                return;

            label.resizeTextForBestFit = false;
            label.fontSize = fontSize;
        }

        private int GetScaledLabelFontSize()
        {
            float referenceHeight = Mathf.Max(1f, _labelFontReferenceHeight);

            float panelHeight = _rectTransform != null
                ? _rectTransform.rect.height
                : 0f;

            if (panelHeight <= 0f && _rectTransform != null)
            {
                panelHeight = _rectTransform.sizeDelta.y;
            }

            if (panelHeight <= 0f)
            {
                panelHeight = _minSize.y;
            }

            float scale = panelHeight / referenceHeight;
            float clampedScale = Mathf.Clamp(scale, _labelFontMinScale, _labelFontMaxScale);

            return Mathf.Max(8, Mathf.RoundToInt(_labelFontSize * clampedScale));
        }

        private void BuildResizeHandle()
        {
            _resizeHandle = GetOrCreateChild("ResizeHandle");
            _resizeHandle.anchorMin = new Vector2(1f, 0f);
            _resizeHandle.anchorMax = new Vector2(1f, 0f);
            _resizeHandle.pivot = new Vector2(1f, 0f);
            _resizeHandle.anchoredPosition = Vector2.zero;
            _resizeHandle.sizeDelta = new Vector2(20f, 20f);

            Image handleImg = GetOrAddComponent<Image>(_resizeHandle.gameObject);
            handleImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

            RectTransform handleTextRt = GetOrCreateChild("HandleText", _resizeHandle);
            StretchToParent(handleTextRt);

            Text handleText = GetOrAddComponent<Text>(handleTextRt.gameObject);
            ConfigureText(
                handleText,
                "\u25E2",
                new Color(0.8f, 0.8f, 0.8f, 0.8f),
                TextAnchor.MiddleCenter,
                10,
                100);

            PanelResizeHandler resizer = GetOrAddComponent<PanelResizeHandler>(_resizeHandle.gameObject);
            resizer.Initialize(this);
        }

        private void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ConfigureText(
            Text text,
            string value,
            Color color,
            TextAnchor alignment,
            int minSize,
            int maxSize)
        {
            text.text = value;
            text.font = GetDefaultFont();
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
            text.color = color;
            text.alignment = alignment;
        }

        private Font GetDefaultFont()
        {
            if (_defaultFont == null)
            {
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _defaultFont;
        }

        private Sprite GetRoundedMaskSprite()
        {
            if (_roundedMaskSprite == null)
            {
                _roundedMaskSprite = GenerateRoundedSprite(128, 128, 32);
            }

            return _roundedMaskSprite;
        }

        private RectTransform GetOrCreateChild(string name, RectTransform parent = null)
        {
            Transform parentTransform = parent != null ? parent : transform;
            Transform existingChild = parentTransform.Find(name);

            if (existingChild != null)
            {
                RectTransform existingRect = existingChild as RectTransform;
                if (existingRect != null)
                {
                    return existingRect;
                }

                Debug.LogWarning($"Child '{name}' exists but is not a RectTransform. Creating a UI replacement child.");
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parentTransform, false);
            return child.GetComponent<RectTransform>();
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        // =================================================================
        // NESTED HELPER: Drag Handler (makes title bar draggable)
        // =================================================================

        /// <summary>
        /// Handles dragging the panel via the title bar.
        /// Attached automatically to the title bar GameObject.
        /// </summary>
        private class PanelDragHandler : MonoBehaviour,
            IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private RectTransform _panelRect;
            private Vector2 _dragOffset;

            private void Awake()
            {
                // The panel is the parent of the title bar
                _panelRect = transform.parent.GetComponent<RectTransform>();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 mousePos);

                _dragOffset = (Vector2)_panelRect.localPosition - mousePos;
            }

            public void OnDrag(PointerEventData eventData)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 mousePos);

                _panelRect.localPosition = mousePos + _dragOffset;
            }

            public void OnEndDrag(PointerEventData eventData) { }
        }

        // =================================================================
        // NESTED HELPER: Resize Handler (bottom-right corner drag-to-resize)
        // =================================================================

        /// <summary>
        /// Handles resizing the panel via the bottom-right corner handle.
        /// Attached automatically to the resize handle GameObject.
        /// </summary>
        private class PanelResizeHandler : MonoBehaviour,
            IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private GestureDisplayPanel _panel;
            private RectTransform _panelRect;
            private Vector2 _startSize;
            private Vector2 _startMousePos;

            public void Initialize(GestureDisplayPanel panel)
            {
                _panel = panel;
            }

            private void Awake()
            {
                // Panel is the grandparent: ResizeHandle -> Panel
                if (_panel == null)
                {
                    _panel = GetComponentInParent<GestureDisplayPanel>();
                }

                _panelRect = _panel.GetComponent<RectTransform>();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                _startSize = _panelRect.sizeDelta;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out _startMousePos);
            }

            public void OnDrag(PointerEventData eventData)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 currentPos);

                Vector2 delta = currentPos - _startMousePos;

                // Increase width rightward, decrease height downward
                // (because Unity UI y-axis points up)
                float newWidth = _startSize.x + delta.x;
                float newHeight = _startSize.y - delta.y;

                _panel.SetSize(newWidth, newHeight);
            }

            public void OnEndDrag(PointerEventData eventData) { }
        }
    }
}
