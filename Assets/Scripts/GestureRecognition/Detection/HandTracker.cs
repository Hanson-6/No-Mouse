// ============================================================================
// HandTracker.cs
// Extracts hand position (center of palm) from landmark data.
// Also provides smoothing to reduce jitter in the tracked position.
//
// This is a pure logic class (no MonoBehaviour) for easy testing.
// ============================================================================

using UnityEngine;

namespace GestureRecognition.Detection
{
    /// <summary>
    /// Extracts and smooths the hand center position from
    /// <see cref="HandLandmarkData"/>.
    /// </summary>
    public class HandTracker
    {
        // -----------------------------------------------------------------
        // Configuration
        // -----------------------------------------------------------------

        private float _smoothingFactor;

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private Vector2 _currentPosition = new Vector2(-1f, -1f);
        private bool _hasPosition;

        // -----------------------------------------------------------------
        // Public properties
        // -----------------------------------------------------------------

        /// <summary>
        /// The current smoothed hand position in normalized [0,1] coords.
        /// Returns (-1, -1) if no hand has been tracked yet.
        /// </summary>
        public Vector2 CurrentPosition => _currentPosition;

        /// <summary>Whether a valid position has been computed.</summary>
        public bool HasPosition => _hasPosition;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a new hand tracker.
        /// </summary>
        /// <param name="smoothingFactor">
        /// Lerp factor for position smoothing. 0 = no smoothing (instant),
        /// 0.9 = very smooth but laggy. Default = 0.5.
        /// </param>
        public HandTracker(float smoothingFactor = 0.5f)
        {
            _smoothingFactor = Mathf.Clamp01(smoothingFactor);
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Updates the tracked position from new landmark data.
        /// </summary>
        /// <param name="data">The latest hand landmark data.</param>
        /// <returns>The updated hand position.</returns>
        public Vector2 Update(HandLandmarkData data)
        {
            if (!data.IsValid || data.Landmarks == null ||
                data.Landmarks.Length < MediaPipeBridge.LandmarkCount)
            {
                _hasPosition = false;
                return _currentPosition;
            }

            // Compute hand center as average of wrist and middle finger MCP.
            // This gives a stable "palm center" position.
            Vector2 rawPosition = ComputePalmCenter(data.Landmarks);

            if (!_hasPosition)
            {
                // First valid position — no smoothing
                _currentPosition = rawPosition;
                _hasPosition = true;
            }
            else
            {
                // Apply exponential smoothing
                _currentPosition = Vector2.Lerp(
                    rawPosition, _currentPosition, _smoothingFactor);
            }

            return _currentPosition;
        }

        /// <summary>
        /// Resets the tracker state (e.g. when hand is lost).
        /// </summary>
        public void Reset()
        {
            _currentPosition = new Vector2(-1f, -1f);
            _hasPosition = false;
        }

        /// <summary>
        /// Adjusts the smoothing factor at runtime.
        /// </summary>
        public void SetSmoothingFactor(float factor)
        {
            _smoothingFactor = Mathf.Clamp01(factor);
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        /// <summary>
        /// Computes the palm center from landmarks.
        /// Uses average of WRIST(0), INDEX_MCP(5), MIDDLE_MCP(9),
        /// RING_MCP(13), PINKY_MCP(17) for a stable center.
        /// </summary>
        public static Vector2 ComputePalmCenter(Vector3[] landmarks)
        {
            Vector2 center = Vector2.zero;

            center += new Vector2(landmarks[MediaPipeBridge.Wrist].x,
                                  landmarks[MediaPipeBridge.Wrist].y);
            center += new Vector2(landmarks[MediaPipeBridge.IndexMcp].x,
                                  landmarks[MediaPipeBridge.IndexMcp].y);
            center += new Vector2(landmarks[MediaPipeBridge.MiddleMcp].x,
                                  landmarks[MediaPipeBridge.MiddleMcp].y);
            center += new Vector2(landmarks[MediaPipeBridge.RingMcp].x,
                                  landmarks[MediaPipeBridge.RingMcp].y);
            center += new Vector2(landmarks[MediaPipeBridge.PinkyMcp].x,
                                  landmarks[MediaPipeBridge.PinkyMcp].y);

            return center / 5f;
        }
    }
}
