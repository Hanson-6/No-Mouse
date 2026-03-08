// ============================================================================
// GestureOverlay.cs
// Debug overlay that visualizes raw hand landmark data on screen.
// Useful during development to verify that MediaPipe tracking is working.
//
// UNITY SETUP:
//   This is optional. Add it to a UI Canvas for debug visualization.
//   It draws landmark points and connections using Unity UI elements.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using GestureRecognition.Core;
using GestureRecognition.Detection;
using GestureRecognition.Service;

namespace GestureRecognition.UI
{
    /// <summary>
    /// Debug overlay that draws hand landmarks on a UI panel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GestureOverlay : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------

        [Header("Visualization")]
        [SerializeField]
        [Tooltip("Color for landmark dots.")]
        private Color _landmarkColor = Color.green;

        [SerializeField]
        [Tooltip("Size of each landmark dot in pixels.")]
        private float _dotSize = 8f;

        [SerializeField]
        [Tooltip("Show connection lines between landmarks.")]
        private bool _showConnections = true;

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private RectTransform _rectTransform;
        private GameObject[] _dots;
        private bool _isInitialized;

        // Connection pairs (MediaPipe hand skeleton)
        private static readonly int[][] Connections = new int[][]
        {
            // Thumb
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 },
            // Index
            new[] { 0, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 8 },
            // Middle
            new[] { 0, 9 }, new[] { 9, 10 }, new[] { 10, 11 }, new[] { 11, 12 },
            // Ring
            new[] { 0, 13 }, new[] { 13, 14 }, new[] { 14, 15 }, new[] { 15, 16 },
            // Pinky
            new[] { 0, 17 }, new[] { 17, 18 }, new[] { 18, 19 }, new[] { 19, 20 },
            // Palm
            new[] { 5, 9 }, new[] { 9, 13 }, new[] { 13, 17 },
        };

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (GestureService.Instance != null &&
                GestureService.Instance.Bridge != null)
            {
                GestureService.Instance.Bridge.OnLandmarksUpdated += HandleLandmarks;
            }
        }

        private void OnDisable()
        {
            if (GestureService.Instance != null &&
                GestureService.Instance.Bridge != null)
            {
                GestureService.Instance.Bridge.OnLandmarksUpdated -= HandleLandmarks;
            }

            HideAllDots();
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        private void HandleLandmarks(HandLandmarkData data)
        {
            if (!data.IsValid)
            {
                HideAllDots();
                return;
            }

            EnsureDots();

            Rect rect = _rectTransform.rect;

            for (int i = 0; i < MediaPipeBridge.LandmarkCount; i++)
            {
                if (i >= _dots.Length || _dots[i] == null)
                {
                    continue;
                }

                Vector3 lm = data.Landmarks[i];

                // Convert normalized [0,1] to local rect position.
                // MediaPipe: (0,0) = top-left; Unity UI: (0,0) = bottom-left
                float x = lm.x * rect.width + rect.xMin;
                float y = (1f - lm.y) * rect.height + rect.yMin;

                _dots[i].SetActive(true);
                _dots[i].GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(x, y);
            }
        }

        private void EnsureDots()
        {
            if (_isInitialized)
            {
                return;
            }

            _dots = new GameObject[MediaPipeBridge.LandmarkCount];

            for (int i = 0; i < MediaPipeBridge.LandmarkCount; i++)
            {
                GameObject dot = new GameObject($"Dot_{i}");
                dot.transform.SetParent(transform, false);

                Image img = dot.AddComponent<Image>();
                img.color = _landmarkColor;

                RectTransform rt = dot.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(_dotSize, _dotSize);

                dot.SetActive(false);
                _dots[i] = dot;
            }

            _isInitialized = true;
        }

        private void HideAllDots()
        {
            if (_dots == null)
            {
                return;
            }

            foreach (GameObject dot in _dots)
            {
                if (dot != null)
                {
                    dot.SetActive(false);
                }
            }
        }
    }
}
