// ============================================================================
// GestureIntegrationTests.cs
// PlayMode integration tests that verify the full pipeline works:
// GestureService → MediaPipeBridge (mock) → GestureClassifier → Events.
//
// These tests run in PlayMode because they need MonoBehaviour lifecycle,
// coroutines, and the Unity event system.
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GestureRecognition.Core;
using GestureRecognition.Detection;
using GestureRecognition.Service;

namespace GestureRecognition.Tests.PlayMode
{
    [TestFixture]
    public class GestureIntegrationTests
    {
        private GameObject _serviceObject;
        private GestureService _service;

        [SetUp]
        public void SetUp()
        {
            // Create a fresh GestureService for each test
            _serviceObject = new GameObject("TestGestureService");
            _service = _serviceObject.AddComponent<GestureService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_serviceObject != null)
            {
                Object.Destroy(_serviceObject);
            }

            GestureEvents.ClearAll();
        }

        // -----------------------------------------------------------------
        // Tests: Service lifecycle
        // -----------------------------------------------------------------

        [Test]
        public void Service_StartsNotRunning()
        {
            Assert.IsFalse(_service.IsRunning);
        }

        [Test]
        public void Service_HasBridge()
        {
            // After Awake, Bridge should be created
            Assert.IsNotNull(_service.Bridge,
                "GestureService should create a MediaPipeBridge in Awake");
        }

        [Test]
        public void Service_HasCamera()
        {
            Assert.IsNotNull(_service.Camera,
                "GestureService should create a CameraManager in Awake");
        }

        [Test]
        public void Service_CurrentResultIsEmpty()
        {
            GestureResult result = _service.CurrentResult;
            Assert.AreEqual(GestureType.None, result.Type);
            Assert.IsFalse(result.IsHandDetected);
        }

        // -----------------------------------------------------------------
        // Tests: Mock data injection
        // -----------------------------------------------------------------

        [Test]
        public void Bridge_InjectMockData_FiresEvent()
        {
            bool eventFired = false;
            HandLandmarkData receivedData = default;

            _service.Bridge.Initialize();
            _service.Bridge.OnLandmarksUpdated += data =>
            {
                eventFired = true;
                receivedData = data;
            };

            // Create mock fist landmarks
            Vector3[] landmarks = MakeFistLandmarks();
            var mockData = new HandLandmarkData
            {
                Landmarks = landmarks,
                IsValid = true
            };

            _service.Bridge.InjectMockData(mockData);

            Assert.IsTrue(eventFired, "InjectMockData should fire OnLandmarksUpdated");
            Assert.IsTrue(receivedData.IsValid);
            Assert.AreEqual(21, receivedData.Landmarks.Length);
        }

        // -----------------------------------------------------------------
        // Tests: GestureEvents
        // -----------------------------------------------------------------

        [Test]
        public void GestureEvents_OnGestureUpdated_CanSubscribe()
        {
            bool fired = false;
            GestureEvents.OnGestureUpdated += _ => fired = true;

            GestureEvents.InvokeGestureUpdated(GestureResult.Empty);
            Assert.IsTrue(fired);
        }

        [Test]
        public void GestureEvents_OnGestureChanged_CanSubscribe()
        {
            GestureResult received = default;
            GestureEvents.OnGestureChanged += r => received = r;

            var result = new GestureResult(
                GestureType.Fist, 0.9f, Vector2.one, true, 0f);
            GestureEvents.InvokeGestureChanged(result);

            Assert.AreEqual(GestureType.Fist, received.Type);
        }

        [Test]
        public void GestureEvents_OnHandPositionUpdated_CanSubscribe()
        {
            Vector2 received = Vector2.zero;
            GestureEvents.OnHandPositionUpdated += pos => received = pos;

            GestureEvents.InvokeHandPositionUpdated(new Vector2(0.3f, 0.7f));
            Assert.AreEqual(0.3f, received.x, 0.01f);
            Assert.AreEqual(0.7f, received.y, 0.01f);
        }

        [Test]
        public void GestureEvents_OnHandDetectionChanged_CanSubscribe()
        {
            bool? detected = null;
            GestureEvents.OnHandDetectionChanged += d => detected = d;

            GestureEvents.InvokeHandDetectionChanged(true);
            Assert.IsTrue(detected.Value);
        }

        [Test]
        public void GestureEvents_OnRecognitionStateChanged_CanSubscribe()
        {
            bool? running = null;
            GestureEvents.OnRecognitionStateChanged += r => running = r;

            GestureEvents.InvokeRecognitionStateChanged(true);
            Assert.IsTrue(running.Value);
        }

        [Test]
        public void GestureEvents_ClearAll_RemovesSubscribers()
        {
            bool fired = false;
            GestureEvents.OnGestureUpdated += _ => fired = true;

            GestureEvents.ClearAll();
            GestureEvents.InvokeGestureUpdated(GestureResult.Empty);

            Assert.IsFalse(fired, "After ClearAll, events should not fire");
        }

        // -----------------------------------------------------------------
        // Tests: MediaPipeBridge lifecycle
        // -----------------------------------------------------------------

        [Test]
        public void Bridge_Initialize_SetsIsInitialized()
        {
            _service.Bridge.Initialize();
            Assert.IsTrue(_service.Bridge.IsInitialized);
        }

        [Test]
        public void Bridge_Shutdown_ClearsIsInitialized()
        {
            _service.Bridge.Initialize();
            _service.Bridge.Shutdown();
            Assert.IsFalse(_service.Bridge.IsInitialized);
        }

        [Test]
        public void Bridge_DoubleInitialize_LogsWarning()
        {
            _service.Bridge.Initialize();
            LogAssert.Expect(LogType.Warning,
                "[MediaPipeBridge] Already initialized.");
            _service.Bridge.Initialize();
        }

        // -----------------------------------------------------------------
        // Tests: End-to-end (coroutine-based)
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bridge_InjectMock_ThenClassify_ProducesCorrectGesture()
        {
            // Initialize bridge
            _service.Bridge.Initialize();

            // Set up classifier
            var classifier = new GestureClassifier(0.5f);

            // Inject fist landmarks
            Vector3[] fistLm = MakeFistLandmarks();
            var mockData = new HandLandmarkData
            {
                Landmarks = fistLm,
                IsValid = true
            };

            _service.Bridge.InjectMockData(mockData);
            yield return null; // Wait one frame

            // Classify the injected data
            HandLandmarkData latest = _service.Bridge.LatestResult;
            GestureType result = classifier.Classify(
                latest.Landmarks, out float confidence);

            Assert.AreEqual(GestureType.Fist, result,
                $"Expected Fist but got {result} with confidence {confidence:F2}");
            Assert.Greater(confidence, 0.5f);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static Vector3[] MakeFistLandmarks()
        {
            Vector3[] lm = new Vector3[MediaPipeBridge.LandmarkCount];
            for (int i = 0; i < lm.Length; i++)
            {
                lm[i] = new Vector3(0.5f, 0.5f, 0f);
            }

            lm[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.8f, 0f);
            lm[MediaPipeBridge.IndexMcp] = new Vector3(0.45f, 0.6f, 0f);
            lm[MediaPipeBridge.MiddleMcp] = new Vector3(0.5f, 0.55f, 0f);
            lm[MediaPipeBridge.RingMcp] = new Vector3(0.55f, 0.58f, 0f);
            lm[MediaPipeBridge.PinkyMcp] = new Vector3(0.6f, 0.65f, 0f);
            lm[MediaPipeBridge.ThumbMcp] = new Vector3(0.35f, 0.65f, 0f);

            lm[MediaPipeBridge.IndexTip] = new Vector3(0.47f, 0.72f, 0f);
            lm[MediaPipeBridge.MiddleTip] = new Vector3(0.5f, 0.7f, 0f);
            lm[MediaPipeBridge.RingTip] = new Vector3(0.53f, 0.73f, 0f);
            lm[MediaPipeBridge.PinkyTip] = new Vector3(0.57f, 0.76f, 0f);
            lm[MediaPipeBridge.ThumbTip] = new Vector3(0.44f, 0.61f, 0f);

            return lm;
        }
    }
}
