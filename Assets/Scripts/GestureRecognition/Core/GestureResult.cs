// ============================================================================
// GestureResult.cs
// Immutable data object that represents one frame of gesture recognition output.
// This is the primary data structure passed to the frontend via events.
// ============================================================================

using UnityEngine;

namespace GestureRecognition.Core
{
    /// <summary>
    /// Represents the result of a single frame of gesture recognition.
    /// Passed to subscribers of <see cref="GestureEvents"/>.
    /// </summary>
    public readonly struct GestureResult
    {
        /// <summary>The detected gesture type.</summary>
        public GestureType Type { get; }

        /// <summary>
        /// Confidence score in the range [0, 1].
        /// 0 means no confidence; 1 means maximum confidence.
        /// </summary>
        public float Confidence { get; }

        /// <summary>
        /// Normalized hand center position in the range [0, 1] for both axes.
        /// (0, 0) = bottom-left of camera frame; (1, 1) = top-right.
        /// Returns (-1, -1) when no hand is detected.
        /// </summary>
        public Vector2 HandPosition { get; }

        /// <summary>
        /// Whether a hand is currently detected in the camera frame.
        /// </summary>
        public bool IsHandDetected { get; }

        /// <summary>
        /// Timestamp in seconds since recognition started.
        /// </summary>
        public float Timestamp { get; }

        public GestureResult(
            GestureType type,
            float confidence,
            Vector2 handPosition,
            bool isHandDetected,
            float timestamp)
        {
            Type = type;
            Confidence = Mathf.Clamp01(confidence);
            HandPosition = handPosition;
            IsHandDetected = isHandDetected;
            Timestamp = timestamp;
        }

        /// <summary>A result representing "no detection".</summary>
        public static GestureResult Empty => new GestureResult(
            GestureType.None, 0f, new Vector2(-1f, -1f), false, 0f);

        public override string ToString()
        {
            return $"[Gesture] Type={Type}, Confidence={Confidence:F2}, " +
                   $"HandPos={HandPosition}, HandDetected={IsHandDetected}, " +
                   $"Time={Timestamp:F3}s";
        }
    }
}
