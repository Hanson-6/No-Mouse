// ============================================================================
// GestureEvents.cs
// Central event hub for gesture recognition results.
// Frontend code should subscribe to these events to react to gestures.
// ============================================================================

using System;

namespace GestureRecognition.Core
{
    /// <summary>
    /// Static event hub that broadcasts gesture recognition results.
    /// <para>
    /// <b>Frontend usage example:</b>
    /// <code>
    /// GestureEvents.OnGestureChanged += result => Debug.Log(result.Type);
    /// GestureEvents.OnHandPositionUpdated += pos => transform.position = pos;
    /// </code>
    /// </para>
    /// </summary>
    public static class GestureEvents
    {
        public static bool EnableDiagnostics = true;

        // -----------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired every frame that a gesture is recognized (including None).
        /// Subscribers receive the full <see cref="GestureResult"/>.
        /// </summary>
        public static event Action<GestureResult> OnGestureUpdated;

        /// <summary>
        /// Fired only when the gesture type changes (e.g., None -> Push).
        /// Useful for triggering one-shot game actions.
        /// </summary>
        public static event Action<GestureResult> OnGestureChanged;

        /// <summary>
        /// Fired every frame with the normalized hand position [0,1].
        /// Only fires when a hand is detected.
        /// </summary>
        public static event Action<UnityEngine.Vector2> OnHandPositionUpdated;

        /// <summary>
        /// Fired when hand detection state changes (detected / lost).
        /// </summary>
        public static event Action<bool> OnHandDetectionChanged;

        /// <summary>
        /// Fired when the recognition system starts or stops.
        /// </summary>
        public static event Action<bool> OnRecognitionStateChanged;

        /// <summary>
        /// Fired when runtime camera state changes.
        /// </summary>
        public static event Action<CameraRuntimeState> OnCameraStateChanged;

        /// <summary>
        /// Fired when camera-occlusion state changes.
        /// </summary>
        public static event Action<bool> OnCameraOcclusionChanged;

        // -----------------------------------------------------------------
        // Internal invoke methods (called by GestureService)
        // -----------------------------------------------------------------

        internal static void InvokeGestureUpdated(GestureResult result)
        {
            OnGestureUpdated?.Invoke(result);
        }

        internal static void InvokeGestureChanged(GestureResult result)
        {
            if (EnableDiagnostics)
            {
                UnityEngine.Debug.Log($"[GestureEvents][Diag] GestureChanged type={result.Type} conf={result.Confidence:F2}");
            }
            OnGestureChanged?.Invoke(result);
        }

        internal static void InvokeHandPositionUpdated(UnityEngine.Vector2 position)
        {
            OnHandPositionUpdated?.Invoke(position);
        }

        internal static void InvokeHandDetectionChanged(bool detected)
        {
            if (EnableDiagnostics)
            {
                UnityEngine.Debug.Log($"[GestureEvents][Diag] HandDetected={detected}");
            }
            OnHandDetectionChanged?.Invoke(detected);
        }

        internal static void InvokeRecognitionStateChanged(bool isRunning)
        {
            if (EnableDiagnostics)
            {
                UnityEngine.Debug.Log($"[GestureEvents][Diag] RecognitionRunning={isRunning}");
            }
            OnRecognitionStateChanged?.Invoke(isRunning);
        }

        internal static void InvokeCameraStateChanged(CameraRuntimeState state)
        {
            if (EnableDiagnostics)
            {
                UnityEngine.Debug.Log($"[GestureEvents][Diag] CameraState={state}");
            }
            OnCameraStateChanged?.Invoke(state);
        }

        internal static void InvokeCameraOcclusionChanged(bool occluded)
        {
            if (EnableDiagnostics)
            {
                UnityEngine.Debug.Log($"[GestureEvents][Diag] CameraOccluded={occluded}");
            }
            OnCameraOcclusionChanged?.Invoke(occluded);
        }

        /// <summary>
        /// Removes all subscribers. Called on application quit to prevent leaks.
        /// </summary>
        internal static void ClearAll()
        {
            OnGestureUpdated = null;
            OnGestureChanged = null;
            OnHandPositionUpdated = null;
            OnHandDetectionChanged = null;
            OnRecognitionStateChanged = null;
            OnCameraStateChanged = null;
            OnCameraOcclusionChanged = null;
        }
    }
}
