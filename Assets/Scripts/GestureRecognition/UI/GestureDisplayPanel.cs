// ============================================================================
// GestureDisplayPanel.cs
// Resizable, draggable floating panel that shows gesture recognition results.
// Can display live camera feed, cartoon sprite, or both overlaid.
//
// FEATURES:
//   - Draggable title bar — click and drag to move the panel
//   - Resizable — drag the bottom-right corner to resize
//   - Pure UI — showing/hiding the panel does NOT affect GestureService
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
//   │   ├── TitleText (Text)
//   │   └── CloseButton (Button + Image)
//   │       └── CloseText (Text "X")
//   ├── ContentArea (RectTransform, mask)
//   │   ├── CameraFeed (RawImage)
//   │   └── GestureSprite (Image, preserveAspect)
//   ├── LabelArea (bottom bar)
//   │   ├── GestureLabel (Text)
//   │   └── ConfidenceLabel (Text)
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
            /// <summary>Shows the live camera feed.</summary>
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
        private DisplayMode _displayMode = DisplayMode.CameraFeed;

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
        [SerializeField]
        private Color _titleBarColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        [SerializeField]
        private Color _panelBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        [SerializeField]
        private float _titleBarHeight = 32f;

        [SerializeField]
        private float _labelBarHeight = 28f;

        [Header("UI References (auto-created if null)")]
        [SerializeField]
        private RawImage _cameraImage;

        [SerializeField]
        private Image _gestureImage;

        [SerializeField]
        private Text _gestureLabel;

        [SerializeField]
        private Text _confidenceLabel;

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private GestureType _lastDisplayedGesture = GestureType.None;
        private bool _uiBuilt;

        // Child references created at runtime
        private RectTransform _titleBar;
        private RectTransform _contentArea;
        private RectTransform _labelArea;
        private RectTransform _resizeHandle;
        private Button _closeButton;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

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
        /// Shows the panel (pure UI — does not affect GestureService).
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the panel (pure UI — does not affect GestureService).
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

            BuildUI();
        }

        private void OnEnable()
        {
            GestureEvents.OnGestureUpdated += HandleGestureUpdated;
        }

        private void OnDisable()
        {
            GestureEvents.OnGestureUpdated -= HandleGestureUpdated;
        }

        private void Start()
        {
            if (_gestureConfig == null && GestureService.Instance != null)
            {
                _gestureConfig = GestureService.Instance.Config;
            }

            UpdateDisplayMode();
        }

        // -----------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------

        private void HandleGestureUpdated(GestureResult result)
        {
            // Update camera feed texture
            if (_cameraImage != null &&
                (_displayMode == DisplayMode.CameraFeed ||
                 _displayMode == DisplayMode.CameraWithOverlay))
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

            // Update labels
            if (_gestureLabel != null)
            {
                string displayName = _gestureConfig != null
                    ? _gestureConfig.GetDisplayName(result.Type)
                    : result.Type.ToString();

                _gestureLabel.text = result.IsHandDetected
                    ? displayName
                    : "No Hand";
            }

            if (_confidenceLabel != null)
            {
                _confidenceLabel.text = result.IsHandDetected
                    ? $"{result.Confidence * 100f:F0}%"
                    : "";
            }
        }

        // -----------------------------------------------------------------
        // Display mode switching
        // -----------------------------------------------------------------

        private void UpdateDisplayMode()
        {
            switch (_displayMode)
            {
                case DisplayMode.CameraFeed:
                    if (_cameraImage != null) _cameraImage.gameObject.SetActive(true);
                    if (_gestureImage != null) _gestureImage.gameObject.SetActive(false);
                    break;

                case DisplayMode.CartoonSprite:
                    if (_cameraImage != null) _cameraImage.gameObject.SetActive(false);
                    if (_gestureImage != null) _gestureImage.gameObject.SetActive(true);
                    break;

                case DisplayMode.CameraWithOverlay:
                    if (_cameraImage != null) _cameraImage.gameObject.SetActive(true);
                    if (_gestureImage != null)
                    {
                        _gestureImage.gameObject.SetActive(true);
                        // In overlay mode, shrink the sprite to the corner
                        SetOverlayLayout();
                    }
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

        // -----------------------------------------------------------------
        // UI Construction (builds the full panel hierarchy at runtime)
        // -----------------------------------------------------------------

        private void BuildUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            // ----- Panel background -----
            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            bg.color = _panelBackgroundColor;

            // ----- Title Bar -----
            _titleBar = CreateChild("TitleBar");
            _titleBar.anchorMin = new Vector2(0, 1);
            _titleBar.anchorMax = new Vector2(1, 1);
            _titleBar.pivot = new Vector2(0.5f, 1);
            _titleBar.anchoredPosition = Vector2.zero;
            _titleBar.sizeDelta = new Vector2(0, _titleBarHeight);

            Image titleBg = _titleBar.gameObject.AddComponent<Image>();
            titleBg.color = _titleBarColor;

            // Title bar makes the panel draggable
            _titleBar.gameObject.AddComponent<PanelDragHandler>();

            // Title text
            RectTransform titleTextRt = CreateChild("TitleText", _titleBar);
            titleTextRt.anchorMin = new Vector2(0, 0);
            titleTextRt.anchorMax = new Vector2(1, 1);
            titleTextRt.offsetMin = new Vector2(8, 0);
            titleTextRt.offsetMax = new Vector2(-36, 0);
            Text titleText = titleTextRt.gameObject.AddComponent<Text>();
            titleText.text = "Gesture Recognition";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 14;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;

            // Close button
            RectTransform closeBtnRt = CreateChild("CloseButton", _titleBar);
            closeBtnRt.anchorMin = new Vector2(1, 0);
            closeBtnRt.anchorMax = new Vector2(1, 1);
            closeBtnRt.pivot = new Vector2(1, 0.5f);
            closeBtnRt.anchoredPosition = Vector2.zero;
            closeBtnRt.sizeDelta = new Vector2(_titleBarHeight, 0);

            Image closeBtnImg = closeBtnRt.gameObject.AddComponent<Image>();
            closeBtnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            _closeButton = closeBtnRt.gameObject.AddComponent<Button>();
            _closeButton.targetGraphic = closeBtnImg;
            _closeButton.onClick.AddListener(Hide);

            RectTransform closeTextRt = CreateChild("CloseText", closeBtnRt);
            closeTextRt.anchorMin = Vector2.zero;
            closeTextRt.anchorMax = Vector2.one;
            closeTextRt.offsetMin = Vector2.zero;
            closeTextRt.offsetMax = Vector2.zero;
            Text closeText = closeTextRt.gameObject.AddComponent<Text>();
            closeText.text = "\u00D7"; // multiplication sign as close icon
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.fontSize = 18;
            closeText.color = Color.white;
            closeText.alignment = TextAnchor.MiddleCenter;

            // ----- Content Area (between title bar and label bar) -----
            _contentArea = CreateChild("ContentArea");
            _contentArea.anchorMin = new Vector2(0, 0);
            _contentArea.anchorMax = new Vector2(1, 1);
            _contentArea.offsetMin = new Vector2(0, _labelBarHeight);
            _contentArea.offsetMax = new Vector2(0, -_titleBarHeight);

            // Add mask so content doesn't overflow
            Image contentBg = _contentArea.gameObject.AddComponent<Image>();
            contentBg.color = new Color(0, 0, 0, 0.5f);
            Mask mask = _contentArea.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Camera feed (RawImage) — fills the content area
            if (_cameraImage == null)
            {
                RectTransform camRt = CreateChild("CameraFeed", _contentArea);
                camRt.anchorMin = Vector2.zero;
                camRt.anchorMax = Vector2.one;
                camRt.offsetMin = Vector2.zero;
                camRt.offsetMax = Vector2.zero;
                _cameraImage = camRt.gameObject.AddComponent<RawImage>();
                _cameraImage.color = Color.white;
            }

            // Gesture sprite (Image) — fills the content area, preserves aspect
            if (_gestureImage == null)
            {
                RectTransform spriteRt = CreateChild("GestureSprite", _contentArea);
                spriteRt.anchorMin = Vector2.zero;
                spriteRt.anchorMax = Vector2.one;
                spriteRt.offsetMin = Vector2.zero;
                spriteRt.offsetMax = Vector2.zero;
                _gestureImage = spriteRt.gameObject.AddComponent<Image>();
                _gestureImage.preserveAspect = true;
            }

            // ----- Label Area (bottom bar) -----
            _labelArea = CreateChild("LabelArea");
            _labelArea.anchorMin = new Vector2(0, 0);
            _labelArea.anchorMax = new Vector2(1, 0);
            _labelArea.pivot = new Vector2(0.5f, 0);
            _labelArea.anchoredPosition = Vector2.zero;
            _labelArea.sizeDelta = new Vector2(0, _labelBarHeight);

            Image labelBg = _labelArea.gameObject.AddComponent<Image>();
            labelBg.color = new Color(0, 0, 0, 0.6f);

            // Gesture name label (left side)
            if (_gestureLabel == null)
            {
                RectTransform labelRt = CreateChild("GestureLabel", _labelArea);
                labelRt.anchorMin = new Vector2(0, 0);
                labelRt.anchorMax = new Vector2(0.7f, 1);
                labelRt.offsetMin = new Vector2(8, 0);
                labelRt.offsetMax = new Vector2(0, 0);
                _gestureLabel = labelRt.gameObject.AddComponent<Text>();
                _gestureLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _gestureLabel.fontSize = 14;
                _gestureLabel.color = Color.white;
                _gestureLabel.alignment = TextAnchor.MiddleLeft;
                _gestureLabel.text = "Ready";
            }

            // Confidence label (right side)
            if (_confidenceLabel == null)
            {
                RectTransform confRt = CreateChild("ConfidenceLabel", _labelArea);
                confRt.anchorMin = new Vector2(0.7f, 0);
                confRt.anchorMax = new Vector2(1, 1);
                confRt.offsetMin = new Vector2(0, 0);
                confRt.offsetMax = new Vector2(-8, 0);
                _confidenceLabel = confRt.gameObject.AddComponent<Text>();
                _confidenceLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _confidenceLabel.fontSize = 14;
                _confidenceLabel.color = new Color(0.6f, 1f, 0.6f, 1f);
                _confidenceLabel.alignment = TextAnchor.MiddleRight;
                _confidenceLabel.text = "";
            }

            // ----- Resize Handle (bottom-right corner) -----
            _resizeHandle = CreateChild("ResizeHandle");
            _resizeHandle.anchorMin = new Vector2(1, 0);
            _resizeHandle.anchorMax = new Vector2(1, 0);
            _resizeHandle.pivot = new Vector2(1, 0);
            _resizeHandle.anchoredPosition = Vector2.zero;
            _resizeHandle.sizeDelta = new Vector2(20, 20);

            Image handleImg = _resizeHandle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

            // Add resize text indicator
            RectTransform handleTextRt = CreateChild("HandleText", _resizeHandle);
            handleTextRt.anchorMin = Vector2.zero;
            handleTextRt.anchorMax = Vector2.one;
            handleTextRt.offsetMin = Vector2.zero;
            handleTextRt.offsetMax = Vector2.zero;
            Text handleText = handleTextRt.gameObject.AddComponent<Text>();
            handleText.text = "\u25E2"; // lower-right triangle
            handleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            handleText.fontSize = 14;
            handleText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            handleText.alignment = TextAnchor.MiddleCenter;

            PanelResizeHandler resizer =
                _resizeHandle.gameObject.AddComponent<PanelResizeHandler>();
            resizer.Initialize(this);
        }

        /// <summary>Helper to create a child RectTransform.</summary>
        private RectTransform CreateChild(string name,
            RectTransform parent = null)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(
                parent != null ? parent : transform, false);
            RectTransform rt = child.AddComponent<RectTransform>();
            return rt;
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
