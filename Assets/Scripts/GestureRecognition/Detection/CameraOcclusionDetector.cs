using UnityEngine;

namespace GestureRecognition.Detection
{
    public struct CameraOcclusionMetrics
    {
        public float Score;
        public float DarkRatio;
        public float LumaVariance;
        public float EdgeDensity;
    }

    /// <summary>
    /// Detects whether a webcam is likely occluded (e.g. covered by a finger)
    /// by combining darkness, texture variance, and edge density with hysteresis.
    /// </summary>
    public class CameraOcclusionDetector
    {
        private readonly int _sampleStep;
        private readonly float _darkThreshold;
        private readonly float _minDarkRatio;
        private readonly float _enterScore;
        private readonly float _exitScore;
        private readonly int _enterFrames;
        private readonly int _exitFrames;
        private readonly float _recentHandBoost;

        private bool _isOccluded;
        private int _enterCounter;
        private int _exitCounter;

        public bool IsOccluded => _isOccluded;

        public CameraOcclusionDetector(
            int sampleStep,
            float darkThreshold,
            float minDarkRatio,
            float enterScore,
            float exitScore,
            int enterFrames,
            int exitFrames,
            float recentHandBoost)
        {
            _sampleStep = Mathf.Max(2, sampleStep);
            _darkThreshold = Mathf.Clamp01(darkThreshold);
            _minDarkRatio = Mathf.Clamp01(minDarkRatio);
            _enterScore = Mathf.Clamp01(enterScore);
            _exitScore = Mathf.Clamp01(exitScore);
            _enterFrames = Mathf.Max(1, enterFrames);
            _exitFrames = Mathf.Max(1, exitFrames);
            _recentHandBoost = Mathf.Max(0f, recentHandBoost);
        }

        public void Reset()
        {
            _isOccluded = false;
            _enterCounter = 0;
            _exitCounter = 0;
        }

        public bool Update(
            Color32[] pixels,
            int width,
            int height,
            bool handSeenRecently,
            out CameraOcclusionMetrics metrics)
        {
            metrics = new CameraOcclusionMetrics
            {
                Score = 0f,
                DarkRatio = 0f,
                LumaVariance = 0f,
                EdgeDensity = 0f,
            };

            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
            {
                _enterCounter = 0;
                return _isOccluded;
            }

            int count = 0;
            int darkCount = 0;
            float lumaSum = 0f;
            float lumaSqSum = 0f;
            float edgeSum = 0f;
            int edgeCount = 0;

            int step = _sampleStep;
            for (int y = step; y < height; y += step)
            {
                int row = y * width;
                int prevRow = (y - step) * width;

                for (int x = step; x < width; x += step)
                {
                    int index = row + x;
                    float luma = GetLuma(pixels[index]);
                    lumaSum += luma;
                    lumaSqSum += luma * luma;
                    count++;

                    if (luma < _darkThreshold)
                        darkCount++;

                    float lumaLeft = GetLuma(pixels[row + (x - step)]);
                    float lumaUp = GetLuma(pixels[prevRow + x]);
                    edgeSum += Mathf.Abs(luma - lumaLeft) + Mathf.Abs(luma - lumaUp);
                    edgeCount += 2;
                }
            }

            if (count < 64 || edgeCount < 64)
            {
                _enterCounter = 0;
                return _isOccluded;
            }

            float mean = lumaSum / count;
            float variance = Mathf.Max(0f, (lumaSqSum / count) - (mean * mean));
            float darkRatio = (float)darkCount / count;
            float edgeDensity = edgeSum / edgeCount;

            float darkScore = Mathf.InverseLerp(0.55f, 0.96f, darkRatio);
            float varianceScore = 1f - Mathf.InverseLerp(0.006f, 0.05f, variance);
            float edgeScore = 1f - Mathf.InverseLerp(0.015f, 0.11f, edgeDensity);

            float score = 0.50f * darkScore + 0.30f * varianceScore + 0.20f * edgeScore;
            if (handSeenRecently)
                score = Mathf.Clamp01(score + _recentHandBoost);

            bool enterCandidate = score >= _enterScore && darkRatio >= _minDarkRatio;
            bool exitCandidate = score <= _exitScore || darkRatio < (_minDarkRatio - 0.08f);

            if (!_isOccluded)
            {
                if (enterCandidate)
                {
                    _enterCounter++;
                    if (_enterCounter >= _enterFrames)
                    {
                        _isOccluded = true;
                        _enterCounter = 0;
                        _exitCounter = 0;
                    }
                }
                else
                {
                    _enterCounter = Mathf.Max(0, _enterCounter - 1);
                }
            }
            else
            {
                if (exitCandidate)
                {
                    _exitCounter++;
                    if (_exitCounter >= _exitFrames)
                    {
                        _isOccluded = false;
                        _enterCounter = 0;
                        _exitCounter = 0;
                    }
                }
                else
                {
                    _exitCounter = Mathf.Max(0, _exitCounter - 1);
                }
            }

            metrics = new CameraOcclusionMetrics
            {
                Score = score,
                DarkRatio = darkRatio,
                LumaVariance = variance,
                EdgeDensity = edgeDensity,
            };

            return _isOccluded;
        }

        private static float GetLuma(Color32 color)
        {
            return (0.299f * color.r + 0.587f * color.g + 0.114f * color.b) / 255f;
        }
    }
}
