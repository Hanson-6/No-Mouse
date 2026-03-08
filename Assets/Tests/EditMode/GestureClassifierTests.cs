// ============================================================================
// GestureClassifierTests.cs
// EditMode unit tests for GestureClassifier pure logic.
//
// These tests verify gesture classification using synthetic landmark data.
// No camera, no MediaPipe, no scene required — pure math tests.
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.Detection;

namespace GestureRecognition.Tests.EditMode
{
    [TestFixture]
    public class GestureClassifierTests
    {
        private GestureClassifier _classifier;

        [SetUp]
        public void SetUp()
        {
            _classifier = new GestureClassifier(confidenceThreshold: 0.5f);
        }

        // -----------------------------------------------------------------
        // Helper: Generate mock landmark arrays
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a 21-landmark array with all positions at (0.5, 0.5, 0).
        /// This is a neutral baseline — modify individual landmarks per test.
        /// </summary>
        private static Vector3[] MakeNeutralLandmarks()
        {
            Vector3[] lm = new Vector3[MediaPipeBridge.LandmarkCount];
            for (int i = 0; i < lm.Length; i++)
            {
                lm[i] = new Vector3(0.5f, 0.5f, 0f);
            }
            return lm;
        }

        /// <summary>
        /// Creates landmarks for a fist pose:
        /// - Wrist at center
        /// - All MCP joints spread out from wrist
        /// - All fingertips curled CLOSER to wrist than their MCPs
        /// - Thumb tip near index MCP
        /// </summary>
        private static Vector3[] MakeFistLandmarks()
        {
            Vector3[] lm = MakeNeutralLandmarks();

            // Wrist at center
            lm[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.8f, 0f);

            // MCP joints spread out above wrist
            lm[MediaPipeBridge.IndexMcp] = new Vector3(0.45f, 0.6f, 0f);
            lm[MediaPipeBridge.MiddleMcp] = new Vector3(0.5f, 0.55f, 0f);
            lm[MediaPipeBridge.RingMcp] = new Vector3(0.55f, 0.58f, 0f);
            lm[MediaPipeBridge.PinkyMcp] = new Vector3(0.6f, 0.65f, 0f);
            lm[MediaPipeBridge.ThumbMcp] = new Vector3(0.35f, 0.65f, 0f);

            // Fingertips curled — closer to wrist than MCPs
            lm[MediaPipeBridge.IndexTip] = new Vector3(0.47f, 0.72f, 0f);
            lm[MediaPipeBridge.MiddleTip] = new Vector3(0.5f, 0.7f, 0f);
            lm[MediaPipeBridge.RingTip] = new Vector3(0.53f, 0.73f, 0f);
            lm[MediaPipeBridge.PinkyTip] = new Vector3(0.57f, 0.76f, 0f);

            // Thumb tip near index MCP (folded over fist)
            lm[MediaPipeBridge.ThumbTip] = new Vector3(0.44f, 0.61f, 0f);

            return lm;
        }

        /// <summary>
        /// Creates landmarks for an open palm:
        /// - Wrist at bottom
        /// - All fingertips far above their MCPs (extended)
        /// </summary>
        private static Vector3[] MakeOpenPalmLandmarks()
        {
            Vector3[] lm = MakeNeutralLandmarks();

            // Wrist at bottom
            lm[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.9f, 0f);

            // MCP joints above wrist
            lm[MediaPipeBridge.IndexMcp] = new Vector3(0.45f, 0.7f, 0f);
            lm[MediaPipeBridge.MiddleMcp] = new Vector3(0.5f, 0.68f, 0f);
            lm[MediaPipeBridge.RingMcp] = new Vector3(0.55f, 0.7f, 0f);
            lm[MediaPipeBridge.PinkyMcp] = new Vector3(0.6f, 0.75f, 0f);
            lm[MediaPipeBridge.ThumbMcp] = new Vector3(0.35f, 0.75f, 0f);

            // Fingertips far above MCPs (extended away from wrist)
            lm[MediaPipeBridge.IndexTip] = new Vector3(0.4f, 0.35f, 0f);
            lm[MediaPipeBridge.MiddleTip] = new Vector3(0.5f, 0.3f, 0f);
            lm[MediaPipeBridge.RingTip] = new Vector3(0.58f, 0.35f, 0f);
            lm[MediaPipeBridge.PinkyTip] = new Vector3(0.65f, 0.4f, 0f);
            lm[MediaPipeBridge.ThumbTip] = new Vector3(0.25f, 0.5f, 0f);

            return lm;
        }

        /// <summary>
        /// Creates landmarks for a shoot (finger gun) pose:
        /// - Index finger extended
        /// - Middle, ring, pinky curled
        /// - Thumb can be up or down
        /// </summary>
        private static Vector3[] MakeShootLandmarks()
        {
            Vector3[] lm = MakeNeutralLandmarks();

            // Wrist at bottom
            lm[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.85f, 0f);

            // MCP joints
            lm[MediaPipeBridge.IndexMcp] = new Vector3(0.45f, 0.65f, 0f);
            lm[MediaPipeBridge.MiddleMcp] = new Vector3(0.5f, 0.63f, 0f);
            lm[MediaPipeBridge.RingMcp] = new Vector3(0.55f, 0.65f, 0f);
            lm[MediaPipeBridge.PinkyMcp] = new Vector3(0.6f, 0.7f, 0f);
            lm[MediaPipeBridge.ThumbMcp] = new Vector3(0.35f, 0.7f, 0f);

            // Index extended (far from wrist)
            lm[MediaPipeBridge.IndexTip] = new Vector3(0.4f, 0.35f, 0f);

            // Middle, ring, pinky curled (closer to wrist than their MCPs)
            lm[MediaPipeBridge.MiddleTip] = new Vector3(0.5f, 0.75f, 0f);
            lm[MediaPipeBridge.RingTip] = new Vector3(0.55f, 0.78f, 0f);
            lm[MediaPipeBridge.PinkyTip] = new Vector3(0.6f, 0.8f, 0f);

            // Thumb up
            lm[MediaPipeBridge.ThumbTip] = new Vector3(0.28f, 0.5f, 0f);

            return lm;
        }

        /// <summary>
        /// Creates landmarks for a lift pose:
        /// - Open palm with wrist clearly below fingertips (y-axis)
        /// - In MediaPipe coords: wrist.y > fingertip.y (y increases downward)
        /// </summary>
        private static Vector3[] MakeLiftLandmarks()
        {
            // Start from open palm
            Vector3[] lm = MakeOpenPalmLandmarks();

            // Ensure wrist is well below middle finger tip
            // (larger y = lower in MediaPipe coords)
            lm[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.95f, 0f);
            lm[MediaPipeBridge.MiddleTip] = new Vector3(0.5f, 0.2f, 0f);

            return lm;
        }

        // -----------------------------------------------------------------
        // Tests: Null / invalid input
        // -----------------------------------------------------------------

        [Test]
        public void Classify_NullLandmarks_ReturnsNone()
        {
            GestureType result = _classifier.Classify(null, out float confidence);
            Assert.AreEqual(GestureType.None, result);
            Assert.AreEqual(0f, confidence);
        }

        [Test]
        public void Classify_EmptyLandmarks_ReturnsNone()
        {
            GestureType result = _classifier.Classify(
                new Vector3[0], out float confidence);
            Assert.AreEqual(GestureType.None, result);
        }

        [Test]
        public void Classify_TooFewLandmarks_ReturnsNone()
        {
            GestureType result = _classifier.Classify(
                new Vector3[10], out float confidence);
            Assert.AreEqual(GestureType.None, result);
        }

        // -----------------------------------------------------------------
        // Tests: Fist detection
        // -----------------------------------------------------------------

        [Test]
        public void Classify_FistPose_ReturnsFist()
        {
            Vector3[] lm = MakeFistLandmarks();
            GestureType result = _classifier.Classify(lm, out float confidence);
            Assert.AreEqual(GestureType.Fist, result,
                $"Expected Fist but got {result} with confidence {confidence:F2}");
            Assert.Greater(confidence, 0.5f);
        }

        [Test]
        public void ClassifyFist_WithFistPose_ReturnsHighConfidence()
        {
            Vector3[] lm = MakeFistLandmarks();
            float score = GestureClassifier.ClassifyFist(lm);
            Assert.Greater(score, 0.7f,
                $"Fist classifier returned {score:F2}, expected > 0.7");
        }

        // -----------------------------------------------------------------
        // Tests: Open palm detection
        // -----------------------------------------------------------------

        [Test]
        public void Classify_OpenPalmPose_ReturnsOpenPalm()
        {
            Vector3[] lm = MakeOpenPalmLandmarks();
            GestureType result = _classifier.Classify(lm, out float confidence);

            // Open palm could also match Push or Lift — we accept any of these
            // since they are sub-gestures of open palm
            bool isOpenHandGesture = result == GestureType.OpenPalm ||
                                     result == GestureType.Push ||
                                     result == GestureType.Lift;

            Assert.IsTrue(isOpenHandGesture,
                $"Expected OpenPalm/Push/Lift but got {result} with confidence {confidence:F2}");
            Assert.Greater(confidence, 0.5f);
        }

        [Test]
        public void ClassifyOpenPalm_WithOpenPalmPose_ReturnsHighConfidence()
        {
            Vector3[] lm = MakeOpenPalmLandmarks();
            float score = GestureClassifier.ClassifyOpenPalm(lm);
            Assert.Greater(score, 0.7f,
                $"OpenPalm classifier returned {score:F2}, expected > 0.7");
        }

        // -----------------------------------------------------------------
        // Tests: Shoot detection
        // -----------------------------------------------------------------

        [Test]
        public void Classify_ShootPose_ReturnsShoot()
        {
            Vector3[] lm = MakeShootLandmarks();
            GestureType result = _classifier.Classify(lm, out float confidence);
            Assert.AreEqual(GestureType.Shoot, result,
                $"Expected Shoot but got {result} with confidence {confidence:F2}");
            Assert.Greater(confidence, 0.5f);
        }

        [Test]
        public void ClassifyShoot_WithShootPose_ReturnsHighConfidence()
        {
            Vector3[] lm = MakeShootLandmarks();
            float score = GestureClassifier.ClassifyShoot(lm);
            Assert.Greater(score, 0.6f,
                $"Shoot classifier returned {score:F2}, expected > 0.6");
        }

        // -----------------------------------------------------------------
        // Tests: Lift detection
        // -----------------------------------------------------------------

        [Test]
        public void ClassifyLift_WithLiftPose_ReturnsHighConfidence()
        {
            Vector3[] lm = MakeLiftLandmarks();
            float score = GestureClassifier.ClassifyLift(lm);
            Assert.Greater(score, 0.5f,
                $"Lift classifier returned {score:F2}, expected > 0.5");
        }

        // -----------------------------------------------------------------
        // Tests: Confidence threshold
        // -----------------------------------------------------------------

        [Test]
        public void Classify_HighThreshold_ReturnsNoneForLowConfidence()
        {
            var strictClassifier = new GestureClassifier(confidenceThreshold: 0.99f);
            Vector3[] lm = MakeFistLandmarks();
            GestureType result = strictClassifier.Classify(lm, out float confidence);
            Assert.AreEqual(GestureType.None, result,
                "With threshold 0.99, most gestures should be rejected");
        }

        [Test]
        public void SetConfidenceThreshold_UpdatesThreshold()
        {
            _classifier.SetConfidenceThreshold(0.99f);
            Vector3[] lm = MakeFistLandmarks();
            GestureType result = _classifier.Classify(lm, out float _);
            Assert.AreEqual(GestureType.None, result);
        }

        // -----------------------------------------------------------------
        // Tests: Custom classifier registration
        // -----------------------------------------------------------------

        [Test]
        public void RegisterClassifier_CustomGesture_CanBeDetected()
        {
            // Register a classifier that always returns 1.0 for Push
            // when index and middle fingers are extended
            _classifier.RegisterClassifier(GestureType.Push, lm => 0.95f);

            Vector3[] landmarks = MakeNeutralLandmarks();
            GestureType result = _classifier.Classify(landmarks, out float confidence);

            Assert.AreEqual(GestureType.Push, result);
            Assert.AreEqual(0.95f, confidence, 0.01f);
        }

        // -----------------------------------------------------------------
        // Tests: Helper methods
        // -----------------------------------------------------------------

        [Test]
        public void IsFingerCurled_TipCloserToWrist_ReturnsTrue()
        {
            Vector3[] lm = MakeNeutralLandmarks();
            // Wrist at (0.5, 0.9)
            lm[0] = new Vector3(0.5f, 0.9f, 0f);
            // MCP at (0.5, 0.6) — distance to wrist = 0.3
            lm[5] = new Vector3(0.5f, 0.6f, 0f);
            // Tip at (0.5, 0.8) — distance to wrist = 0.1 (closer!)
            lm[8] = new Vector3(0.5f, 0.8f, 0f);

            bool curled = GestureClassifier.IsFingerCurled(lm, 8, 5, 0);
            Assert.IsTrue(curled);
        }

        [Test]
        public void IsFingerCurled_TipFartherFromWrist_ReturnsFalse()
        {
            Vector3[] lm = MakeNeutralLandmarks();
            // Wrist at (0.5, 0.9)
            lm[0] = new Vector3(0.5f, 0.9f, 0f);
            // MCP at (0.5, 0.6) — distance to wrist = 0.3
            lm[5] = new Vector3(0.5f, 0.6f, 0f);
            // Tip at (0.5, 0.3) — distance to wrist = 0.6 (farther!)
            lm[8] = new Vector3(0.5f, 0.3f, 0f);

            bool curled = GestureClassifier.IsFingerCurled(lm, 8, 5, 0);
            Assert.IsFalse(curled);
        }

        [Test]
        public void IsFingerExtended_IsOppositeOfCurled()
        {
            Vector3[] lm = MakeNeutralLandmarks();
            lm[0] = new Vector3(0.5f, 0.9f, 0f);
            lm[5] = new Vector3(0.5f, 0.6f, 0f);
            lm[8] = new Vector3(0.5f, 0.3f, 0f);

            bool curled = GestureClassifier.IsFingerCurled(lm, 8, 5, 0);
            bool extended = GestureClassifier.IsFingerExtended(lm, 8, 5, 0);

            Assert.AreNotEqual(curled, extended,
                "Extended and curled should always be opposites");
        }
    }

    // =====================================================================
    // HandTracker tests
    // =====================================================================

    [TestFixture]
    public class HandTrackerTests
    {
        [Test]
        public void Update_ValidData_ReturnsPosition()
        {
            var tracker = new HandTracker(smoothingFactor: 0f);

            Vector3[] landmarks = new Vector3[MediaPipeBridge.LandmarkCount];
            // Set wrist and MCPs to known positions
            landmarks[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.8f, 0f);
            landmarks[MediaPipeBridge.IndexMcp] = new Vector3(0.4f, 0.6f, 0f);
            landmarks[MediaPipeBridge.MiddleMcp] = new Vector3(0.5f, 0.58f, 0f);
            landmarks[MediaPipeBridge.RingMcp] = new Vector3(0.6f, 0.6f, 0f);
            landmarks[MediaPipeBridge.PinkyMcp] = new Vector3(0.65f, 0.65f, 0f);

            var data = new HandLandmarkData
            {
                Landmarks = landmarks,
                IsValid = true
            };

            Vector2 pos = tracker.Update(data);

            // Expected: average of the 5 positions
            float expectedX = (0.5f + 0.4f + 0.5f + 0.6f + 0.65f) / 5f;
            float expectedY = (0.8f + 0.6f + 0.58f + 0.6f + 0.65f) / 5f;

            Assert.AreEqual(expectedX, pos.x, 0.01f);
            Assert.AreEqual(expectedY, pos.y, 0.01f);
            Assert.IsTrue(tracker.HasPosition);
        }

        [Test]
        public void Update_InvalidData_DoesNotUpdatePosition()
        {
            var tracker = new HandTracker();

            var data = new HandLandmarkData
            {
                Landmarks = null,
                IsValid = false
            };

            Vector2 pos = tracker.Update(data);
            Assert.IsFalse(tracker.HasPosition);
        }

        [Test]
        public void Reset_ClearsState()
        {
            var tracker = new HandTracker(smoothingFactor: 0f);

            // Give it valid data first
            Vector3[] landmarks = new Vector3[MediaPipeBridge.LandmarkCount];
            landmarks[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.5f, 0f);
            landmarks[MediaPipeBridge.IndexMcp] = new Vector3(0.5f, 0.5f, 0f);
            landmarks[MediaPipeBridge.MiddleMcp] = new Vector3(0.5f, 0.5f, 0f);
            landmarks[MediaPipeBridge.RingMcp] = new Vector3(0.5f, 0.5f, 0f);
            landmarks[MediaPipeBridge.PinkyMcp] = new Vector3(0.5f, 0.5f, 0f);

            var data = new HandLandmarkData { Landmarks = landmarks, IsValid = true };
            tracker.Update(data);
            Assert.IsTrue(tracker.HasPosition);

            tracker.Reset();
            Assert.IsFalse(tracker.HasPosition);
            Assert.AreEqual(new Vector2(-1f, -1f), tracker.CurrentPosition);
        }

        [Test]
        public void ComputePalmCenter_ReturnsAverageOfFivePoints()
        {
            Vector3[] landmarks = new Vector3[MediaPipeBridge.LandmarkCount];
            landmarks[MediaPipeBridge.Wrist] = new Vector3(0.0f, 0.0f, 0f);
            landmarks[MediaPipeBridge.IndexMcp] = new Vector3(1.0f, 0.0f, 0f);
            landmarks[MediaPipeBridge.MiddleMcp] = new Vector3(0.0f, 1.0f, 0f);
            landmarks[MediaPipeBridge.RingMcp] = new Vector3(1.0f, 1.0f, 0f);
            landmarks[MediaPipeBridge.PinkyMcp] = new Vector3(0.5f, 0.5f, 0f);

            Vector2 center = HandTracker.ComputePalmCenter(landmarks);

            Assert.AreEqual(0.5f, center.x, 0.01f);
            Assert.AreEqual(0.5f, center.y, 0.01f);
        }
    }

    // =====================================================================
    // GestureResult tests
    // =====================================================================

    [TestFixture]
    public class GestureResultTests
    {
        [Test]
        public void Constructor_ClampsConfidence()
        {
            var result = new GestureResult(
                GestureType.Fist, 1.5f, Vector2.zero, true, 0f);
            Assert.AreEqual(1f, result.Confidence);
        }

        [Test]
        public void Constructor_ClampsNegativeConfidence()
        {
            var result = new GestureResult(
                GestureType.Fist, -0.5f, Vector2.zero, true, 0f);
            Assert.AreEqual(0f, result.Confidence);
        }

        [Test]
        public void Empty_ReturnsNoneType()
        {
            GestureResult empty = GestureResult.Empty;
            Assert.AreEqual(GestureType.None, empty.Type);
            Assert.IsFalse(empty.IsHandDetected);
            Assert.AreEqual(new Vector2(-1f, -1f), empty.HandPosition);
        }

        [Test]
        public void ToString_ContainsType()
        {
            var result = new GestureResult(
                GestureType.Push, 0.8f, new Vector2(0.5f, 0.5f), true, 1.23f);
            string str = result.ToString();
            Assert.IsTrue(str.Contains("Push"), $"ToString should contain 'Push': {str}");
        }
    }
}
