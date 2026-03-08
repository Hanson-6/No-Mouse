// ============================================================================
// GenerateGestureSprites.cs
// Editor utility that generates placeholder colored sprites for each gesture.
//
// HOW TO USE:
//   In Unity Editor menu: Tools > Gesture Recognition > Generate Placeholder Sprites
//   This creates simple 64x64 colored PNG files in Assets/Resources/GestureSprites/
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using GestureRecognition.Core;

namespace GestureRecognition.Editor
{
    public static class GenerateGestureSprites
    {
        private static readonly string OutputFolder = "Assets/Resources/GestureSprites";

        [MenuItem("Tools/Gesture Recognition/Generate Placeholder Sprites")]
        public static void Generate()
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }

            // Color per gesture type
            CreateSprite("None", new Color(0.3f, 0.3f, 0.3f, 1f), "?");
            CreateSprite("Push", new Color(0.2f, 0.6f, 1f, 1f), "P");
            CreateSprite("Lift", new Color(0.2f, 0.8f, 0.4f, 1f), "L");
            CreateSprite("Shoot", new Color(1f, 0.3f, 0.2f, 1f), "S");
            CreateSprite("Fist", new Color(0.9f, 0.6f, 0.1f, 1f), "F");
            CreateSprite("OpenPalm", new Color(0.8f, 0.4f, 0.9f, 1f), "O");

            AssetDatabase.Refresh();
            Debug.Log($"[GenerateGestureSprites] Created placeholder sprites in {OutputFolder}");
        }

        private static void CreateSprite(string name, Color bgColor, string letter)
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            // Fill background
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bgColor;
            }

            // Draw a simple border (2px darker)
            Color borderColor = bgColor * 0.6f;
            borderColor.a = 1f;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (x < 2 || x >= size - 2 || y < 2 || y >= size - 2)
                    {
                        pixels[y * size + x] = borderColor;
                    }
                }
            }

            // Draw a centered circle (radius ~20px) with lighter color
            Color circleColor = Color.Lerp(bgColor, Color.white, 0.4f);
            int cx = size / 2;
            int cy = size / 2;
            int radius = 20;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (dist < radius)
                    {
                        pixels[y * size + x] = circleColor;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            string path = $"{OutputFolder}/Gesture_{name}.png";
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            Object.DestroyImmediate(tex);

            Debug.Log($"  Created: {path}");
        }

        [MenuItem("Tools/Gesture Recognition/Create GestureConfig Asset")]
        public static void CreateGestureConfigAsset()
        {
            string folder = "Assets/Resources";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string assetPath = $"{folder}/DefaultGestureConfig.asset";

            if (File.Exists(assetPath))
            {
                Debug.LogWarning($"[GenerateGestureSprites] Asset already exists: {assetPath}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GestureConfig>(assetPath);
                return;
            }

            GestureConfig config = ScriptableObject.CreateInstance<GestureConfig>();
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            Debug.Log($"[GenerateGestureSprites] Created GestureConfig asset: {assetPath}\n" +
                      "Now open it in the Inspector and add gesture entries.");
        }
    }
}
#endif
