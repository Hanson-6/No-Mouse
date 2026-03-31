// ============================================================================
// GesturePanelManager.cs
// Convenience manager for frontend teammates to show/hide the gesture panel
// with a single line of code. Also handles Prefab instantiation.
//
// FRONTEND USAGE:
//   // Show the floating gesture panel:
//   GesturePanelManager.Instance.ShowPanel();
//
//   // Hide it:
//   GesturePanelManager.Instance.HidePanel();
//
//   // Toggle:
//   GesturePanelManager.Instance.TogglePanel();
//
//   // Change display mode:
//   GesturePanelManager.Instance.SetDisplayMode(
//       GestureDisplayPanel.DisplayMode.CameraFeed);
//
//   // Resize:
//   GesturePanelManager.Instance.SetPanelSize(400, 320);
//
// UNITY SETUP:
//   Option A (Recommended — Prefab):
//     1. Drag the GesturePanel prefab from Assets/Prefabs/ into
//        the "Panel Prefab" slot in the Inspector
//     2. The manager will instantiate it on first ShowPanel() call
//
//   Option B (Scene reference):
//     1. Already have a GestureDisplayPanel in the scene
//     2. Drag it into the "Panel Instance" slot in the Inspector
//     3. The manager will use that existing instance
//
//   Both options: Add GesturePanelManager to any persistent GameObject
//   (or the same one that has GestureService).
// ============================================================================

using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.UI;

namespace GestureRecognition.Service
{
    /// <summary>
    /// Convenience singleton that manages the lifecycle of a
    /// <see cref="GestureDisplayPanel"/> — instantiation, show, hide, resize.
    /// Frontend teammates use this instead of directly manipulating the panel.
    /// </summary>
    public class GesturePanelManager : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------

        private static GesturePanelManager _instance;

        /// <summary>
        /// Returns true if a GesturePanelManager exists in the scene.
        /// Unlike <see cref="Instance"/>, this does NOT call FindObjectOfType
        /// or log errors, making it safe for optional/conditional checks.
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>Global singleton instance.</summary>
        public static GesturePanelManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GesturePanelManager>();
                    if (_instance == null)
                    {
                        Debug.LogError(
                            "[GesturePanelManager] No instance found in scene. " +
                            "Please add one to a GameObject.");
                    }
                }
                return _instance;
            }
        }

        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------

        [Header("Panel Source")]
        [Tooltip("(Optional) Prefab to instantiate. If Panel Instance is " +
                 "already set, this is ignored.")]
        [SerializeField]
        private GestureDisplayPanel _panelPrefab;

        [Tooltip("(Optional) Existing panel instance in the scene. " +
                 "If null, one will be created from the prefab.")]
        [SerializeField]
        private GestureDisplayPanel _panelInstance;

        [Header("Default Settings")]
        [Tooltip("Default panel size when first created (width x height).")]
        [SerializeField]
        private Vector2 _defaultSize = new Vector2(320f, 280f);

        [Tooltip("Default screen position when first created " +
                 "(relative to screen center).")]
        [SerializeField]
        private Vector2 _defaultPosition = new Vector2(0f, 0f);

        [Tooltip("Default display mode.")]
        [SerializeField]
        private GestureDisplayPanel.DisplayMode _defaultDisplayMode =
            GestureDisplayPanel.DisplayMode.CartoonSprite;

        // -----------------------------------------------------------------
        // Public API — Frontend calls these
        // -----------------------------------------------------------------

        /// <summary>
        /// Shows the gesture panel. Creates it if it doesn't exist yet.
        /// Starts gesture recognition automatically.
        /// </summary>
        public void ShowPanel()
        {
            EnsurePanel();

            if (_panelInstance != null)
            {
                _panelInstance.Show();
            }
        }

        /// <summary>
        /// Hides the gesture panel. Stops gesture recognition automatically.
        /// </summary>
        public void HidePanel()
        {
            if (_panelInstance != null)
            {
                _panelInstance.Hide();
            }
        }

        /// <summary>
        /// Toggles the panel visibility.
        /// </summary>
        public void TogglePanel()
        {
            EnsurePanel();

            if (_panelInstance != null)
            {
                _panelInstance.Toggle();
            }
        }

        /// <summary>
        /// Changes the display mode of the panel.
        /// </summary>
        public void SetDisplayMode(GestureDisplayPanel.DisplayMode mode)
        {
            if (_panelInstance != null)
            {
                _panelInstance.CurrentMode = mode;
            }
        }

        /// <summary>
        /// Resizes the panel. Content auto-scales proportionally.
        /// </summary>
        public void SetPanelSize(float width, float height)
        {
            if (_panelInstance != null)
            {
                _panelInstance.SetSize(width, height);
            }
        }

        /// <summary>
        /// Returns the current panel instance (may be null if not yet created).
        /// </summary>
        public GestureDisplayPanel Panel => _panelInstance;

        /// <summary>
        /// Is the panel currently visible?
        /// </summary>
        public bool IsPanelVisible => _panelInstance != null &&
                                       _panelInstance.gameObject.activeSelf;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    "[GesturePanelManager] Duplicate instance destroyed.");
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        /// <summary>
        /// Ensures a panel instance exists. Creates one from the prefab
        /// or from scratch if needed.
        /// </summary>
        private void EnsurePanel()
        {
            if (_panelInstance != null) return;

            // Try to find existing panel in scene
            _panelInstance = FindObjectOfType<GestureDisplayPanel>();
            if (_panelInstance != null) return;

            // Instantiate from prefab
            if (_panelPrefab != null)
            {
                Canvas canvas = EnsureCanvas();
                _panelInstance = Instantiate(_panelPrefab, canvas.transform);
                SetupPanel();
                return;
            }

            // Create from scratch (no prefab available)
            Debug.Log("[GesturePanelManager] No prefab assigned. " +
                      "Creating panel from scratch.");
            CreatePanelFromScratch();
        }

        /// <summary>
        /// Creates a panel entirely in code (fallback when no prefab is set).
        /// </summary>
        private void CreatePanelFromScratch()
        {
            Canvas canvas = EnsureCanvas();

            GameObject panelObj = new GameObject("GesturePanel");
            panelObj.transform.SetParent(canvas.transform, false);

            // Add RectTransform
            RectTransform rt = panelObj.GetComponent<RectTransform>();
            if (rt == null) rt = panelObj.AddComponent<RectTransform>();

            // Add CanvasGroup (required by GestureDisplayPanel)
            panelObj.AddComponent<CanvasGroup>();

            // Add the display panel script (it auto-builds UI in Awake)
            _panelInstance = panelObj.AddComponent<GestureDisplayPanel>();

            SetupPanel();

            // Start hidden — ShowPanel() will activate it
            panelObj.SetActive(false);
        }

        /// <summary>
        /// Configures default size, position, and mode for a new panel.
        /// </summary>
        private void SetupPanel()
        {
            if (_panelInstance == null) return;

            RectTransform rt = _panelInstance.GetComponent<RectTransform>();

            // Anchor to top-left corner — works at any resolution
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);

            // Apply default size; position = small padding from top-left edge
            rt.sizeDelta        = _defaultSize;
            rt.anchoredPosition = new Vector2(10f, -10f);

            // Apply default display mode
            _panelInstance.CurrentMode = _defaultDisplayMode;
        }

        /// <summary>
        /// Finds or creates a Screen Space Overlay canvas.
        /// </summary>
        private Canvas EnsureCanvas()
        {
            // Always create a dedicated canvas — never reuse the game's UI canvases
            // (e.g. WinCanvas) to avoid inheriting their full-screen backgrounds.
            GameObject canvasObj = new GameObject("GestureCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // render on top
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // EventSystem is needed for drag/click
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<
                    UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }
    }
}
