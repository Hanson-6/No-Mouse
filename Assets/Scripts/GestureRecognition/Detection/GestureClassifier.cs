// ============================================================================
// GestureClassifier.cs
// Classifies hand landmark data into discrete GestureType values.
//
// This is the core algorithm file. Each gesture has a classification method
// that analyzes the 21 hand landmarks and returns a confidence score.
//
// TO ADD A NEW GESTURE:
//   1. Add the gesture to GestureType enum
//   2. Write a private Classify_YourGesture() method below
//   3. Register it in the _classifiers list inside the constructor
//   That's it — the system will automatically pick it up.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using GestureRecognition.Core;

namespace GestureRecognition.Detection
{
    /// <summary>
    /// Pure logic class (no MonoBehaviour) that classifies
    /// <see cref="HandLandmarkData"/> into <see cref="GestureType"/>.
    /// <para>
    /// This class is deliberately NOT a MonoBehaviour so it can be
    /// unit-tested in EditMode without a scene.
    /// </para>
    /// </summary>
    public class GestureClassifier
    {
        // -----------------------------------------------------------------
        // Types
        // -----------------------------------------------------------------

        /// <summary>
        /// A single classifier function that takes landmarks and returns
        /// a confidence score in [0, 1]. 0 = definitely not this gesture.
        /// </summary>
        public delegate float ClassifierFunc(Vector3[] landmarks);

        private struct GestureClassifierEntry
        {
            public GestureType Type;
            public ClassifierFunc Classifier;
        }

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private readonly List<GestureClassifierEntry> _classifiers =
            new List<GestureClassifierEntry>();

        private float _confidenceThreshold;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a new classifier with the given confidence threshold.
        /// </summary>
        /// <param name="confidenceThreshold">
        /// Minimum confidence to accept a gesture. Default = 0.6.
        /// </param>
        public GestureClassifier(float confidenceThreshold = 0.6f)
        {
            _confidenceThreshold = confidenceThreshold;

            // Register built-in gesture classifiers.
            // Order does not matter — the highest-confidence one wins.
            RegisterClassifier(GestureType.Fist, ClassifyFist);
            RegisterClassifier(GestureType.OpenPalm, ClassifyOpenPalm);
            RegisterClassifier(GestureType.Push, ClassifyPush);
            RegisterClassifier(GestureType.Lift, ClassifyLift);
            RegisterClassifier(GestureType.Shoot, ClassifyShoot);
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Register a custom gesture classifier at runtime.
        /// This is the extension point for adding new gestures
        /// without modifying this class.
        /// </summary>
        public void RegisterClassifier(GestureType type, ClassifierFunc func)
        {
            _classifiers.Add(new GestureClassifierEntry
            {
                Type = type,
                Classifier = func
            });
        }

        /// <summary>
        /// Classifies the given landmarks into a gesture type.
        /// Returns the type with the highest confidence above threshold.
        /// </summary>
        /// <param name="landmarks">
        /// Array of 21 Vector3 landmarks from MediaPipe.
        /// </param>
        /// <param name="confidence">
        /// Output: the confidence score of the winning gesture.
        /// </param>
        /// <returns>The detected gesture type, or None if below threshold.</returns>
        public GestureType Classify(Vector3[] landmarks, out float confidence)
        {
            confidence = 0f;

            if (landmarks == null || landmarks.Length < MediaPipeBridge.LandmarkCount)
            {
                return GestureType.None;
            }

            GestureType bestType = GestureType.None;
            float bestConfidence = 0f;

            foreach (GestureClassifierEntry entry in _classifiers)
            {
                float score = entry.Classifier(landmarks);
                if (score > bestConfidence)
                {
                    bestConfidence = score;
                    bestType = entry.Type;
                }
            }

            if (bestConfidence >= _confidenceThreshold)
            {
                confidence = bestConfidence;
                return bestType;
            }

            confidence = bestConfidence;
            return GestureType.None;
        }

        /// <summary>Updates the confidence threshold at runtime.</summary>
        public void SetConfidenceThreshold(float threshold)
        {
            _confidenceThreshold = Mathf.Clamp01(threshold);
        }

        // -----------------------------------------------------------------
        // Built-in classifiers
        // -----------------------------------------------------------------
        // Each method analyzes the 21 hand landmarks and returns a
        // confidence score in [0, 1].
        //
        // The landmark indices are defined in MediaPipeBridge:
        //   0=WRIST, 4=THUMB_TIP, 8=INDEX_TIP, 12=MIDDLE_TIP,
        //   16=RING_TIP, 20=PINKY_TIP
        // -----------------------------------------------------------------

        /// <summary>
        /// Fist: all fingertips are close to the palm (wrist).
        /// Measured by checking if each fingertip is closer to the wrist
        /// than its corresponding MCP joint.
        /// </summary>
        public static float ClassifyFist(Vector3[] lm)
        {
            int curledCount = 0;

            // Check each finger: is the tip closer to wrist than the MCP?
            if (IsFingerCurled(lm, MediaPipeBridge.IndexTip, MediaPipeBridge.IndexMcp, MediaPipeBridge.Wrist))
                curledCount++;
            if (IsFingerCurled(lm, MediaPipeBridge.MiddleTip, MediaPipeBridge.MiddleMcp, MediaPipeBridge.Wrist))
                curledCount++;
            if (IsFingerCurled(lm, MediaPipeBridge.RingTip, MediaPipeBridge.RingMcp, MediaPipeBridge.Wrist))
                curledCount++;
            if (IsFingerCurled(lm, MediaPipeBridge.PinkyTip, MediaPipeBridge.PinkyMcp, MediaPipeBridge.Wrist))
                curledCount++;

            // Thumb: tip should be close to index MCP (folded over fist)
            float thumbDist = Vector3.Distance(lm[MediaPipeBridge.ThumbTip], lm[MediaPipeBridge.IndexMcp]);
            bool thumbFolded = thumbDist < 0.08f;

            if (thumbFolded) curledCount++;

            // 5 out of 5 fingers curled = high confidence
            return curledCount / 5f;
        }

        /// <summary>
        /// Open palm: all fingertips are extended (far from wrist).
        /// </summary>
        public static float ClassifyOpenPalm(Vector3[] lm)
        {
            int extendedCount = 0;

            if (IsFingerExtended(lm, MediaPipeBridge.IndexTip, MediaPipeBridge.IndexMcp, MediaPipeBridge.Wrist))
                extendedCount++;
            if (IsFingerExtended(lm, MediaPipeBridge.MiddleTip, MediaPipeBridge.MiddleMcp, MediaPipeBridge.Wrist))
                extendedCount++;
            if (IsFingerExtended(lm, MediaPipeBridge.RingTip, MediaPipeBridge.RingMcp, MediaPipeBridge.Wrist))
                extendedCount++;
            if (IsFingerExtended(lm, MediaPipeBridge.PinkyTip, MediaPipeBridge.PinkyMcp, MediaPipeBridge.Wrist))
                extendedCount++;
            if (IsFingerExtended(lm, MediaPipeBridge.ThumbTip, MediaPipeBridge.ThumbMcp, MediaPipeBridge.Wrist))
                extendedCount++;

            return extendedCount / 5f;
        }

        /// <summary>
        /// Push gesture: open palm with fingers pointing forward.
        /// Detected as open palm where the hand is relatively flat
        /// (fingertips have similar z-depth, indicating forward-facing palm).
        /// </summary>
        public static float ClassifyPush(Vector3[] lm)
        {
            // First check it looks like an open palm
            float palmScore = ClassifyOpenPalm(lm);
            if (palmScore < 0.6f) return 0f;

            // Check if fingers are spread relatively evenly (push posture)
            // and the palm faces forward (z values of tips are similar)
            float avgZ = (lm[MediaPipeBridge.IndexTip].z +
                          lm[MediaPipeBridge.MiddleTip].z +
                          lm[MediaPipeBridge.RingTip].z +
                          lm[MediaPipeBridge.PinkyTip].z) / 4f;

            float zVariance = 0f;
            zVariance += Mathf.Abs(lm[MediaPipeBridge.IndexTip].z - avgZ);
            zVariance += Mathf.Abs(lm[MediaPipeBridge.MiddleTip].z - avgZ);
            zVariance += Mathf.Abs(lm[MediaPipeBridge.RingTip].z - avgZ);
            zVariance += Mathf.Abs(lm[MediaPipeBridge.PinkyTip].z - avgZ);
            zVariance /= 4f;

            // Low z-variance means flat hand = push
            float flatScore = Mathf.Clamp01(1f - zVariance * 20f);

            return palmScore * 0.6f + flatScore * 0.4f;
        }

        /// <summary>
        /// Lift gesture: open hand with wrist below fingertips
        /// (hand pointing upward).
        /// </summary>
        public static float ClassifyLift(Vector3[] lm)
        {
            float palmScore = ClassifyOpenPalm(lm);
            if (palmScore < 0.5f) return 0f;

            // Wrist should be below the middle finger MCP (y-axis)
            // In MediaPipe normalized coords: lower y = higher in image
            // But in our system we'll check wrist.y > fingertip.y
            // meaning wrist is lower on screen than fingertips
            float wristY = lm[MediaPipeBridge.Wrist].y;
            float middleTipY = lm[MediaPipeBridge.MiddleTip].y;

            // In MediaPipe image coords, y increases downward.
            // Wrist below fingers means wrist.y > middleTip.y
            float liftScore = Mathf.Clamp01((wristY - middleTipY) * 5f);

            return palmScore * 0.5f + liftScore * 0.5f;
        }

        /// <summary>
        /// Shoot gesture: index finger extended, other fingers curled
        /// (finger gun). Thumb can be up or curled.
        /// </summary>
        public static float ClassifyShoot(Vector3[] lm)
        {
            // Index must be extended
            bool indexExtended = IsFingerExtended(
                lm, MediaPipeBridge.IndexTip, MediaPipeBridge.IndexMcp, MediaPipeBridge.Wrist);
            if (!indexExtended) return 0f;

            int curledCount = 0;

            // Middle, ring, pinky should be curled
            if (IsFingerCurled(lm, MediaPipeBridge.MiddleTip, MediaPipeBridge.MiddleMcp, MediaPipeBridge.Wrist))
                curledCount++;
            if (IsFingerCurled(lm, MediaPipeBridge.RingTip, MediaPipeBridge.RingMcp, MediaPipeBridge.Wrist))
                curledCount++;
            if (IsFingerCurled(lm, MediaPipeBridge.PinkyTip, MediaPipeBridge.PinkyMcp, MediaPipeBridge.Wrist))
                curledCount++;

            // 3 fingers curled + index extended = shoot
            return (curledCount / 3f) * 0.8f + 0.2f;
        }

        // -----------------------------------------------------------------
        // Helper methods
        // -----------------------------------------------------------------

        /// <summary>
        /// A finger is "curled" if the tip is closer to the wrist
        /// than the MCP joint is.
        /// </summary>
        public static bool IsFingerCurled(
            Vector3[] lm, int tipIdx, int mcpIdx, int wristIdx)
        {
            float tipToWrist = Vector2.Distance(
                new Vector2(lm[tipIdx].x, lm[tipIdx].y),
                new Vector2(lm[wristIdx].x, lm[wristIdx].y));

            float mcpToWrist = Vector2.Distance(
                new Vector2(lm[mcpIdx].x, lm[mcpIdx].y),
                new Vector2(lm[wristIdx].x, lm[wristIdx].y));

            return tipToWrist < mcpToWrist;
        }

        /// <summary>
        /// A finger is "extended" if the tip is farther from the wrist
        /// than the MCP joint is.
        /// </summary>
        public static bool IsFingerExtended(
            Vector3[] lm, int tipIdx, int mcpIdx, int wristIdx)
        {
            return !IsFingerCurled(lm, tipIdx, mcpIdx, wristIdx);
        }
    }
}
