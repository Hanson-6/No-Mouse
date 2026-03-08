// ============================================================================
// GestureConfig.cs
// ScriptableObject that maps each GestureType to its display sprite.
// Create via Unity menu: Assets > Create > GestureRecognition > GestureConfig
//
// HOW TO USE (Unity Inspector):
//   1. Right-click in Project window > Create > GestureRecognition > GestureConfig
//   2. Select the created asset
//   3. In Inspector, expand "Gesture Entries" and add one entry per gesture
//   4. Drag-and-drop your cartoon sprites into the Sprite field
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GestureRecognition.Core
{
    /// <summary>
    /// Configuration asset that maps each <see cref="GestureType"/>
    /// to a display sprite (cartoon image).
    /// </summary>
    [CreateAssetMenu(
        fileName = "GestureConfig",
        menuName = "GestureRecognition/GestureConfig",
        order = 0)]
    public class GestureConfig : ScriptableObject
    {
        // -----------------------------------------------------------------
        // Serialized data
        // -----------------------------------------------------------------

        [Tooltip("List of gesture-to-sprite mappings. " +
                 "Add one entry for each gesture you want to support.")]
        [SerializeField]
        private List<GestureEntry> _gestureEntries = new List<GestureEntry>();

        [Tooltip("Minimum confidence threshold to accept a gesture. " +
                 "Values below this are treated as GestureType.None.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _confidenceThreshold = 0.6f;

        [Tooltip("Sprite shown when no gesture is detected.")]
        [SerializeField]
        private Sprite _noneSprite;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Minimum confidence threshold.</summary>
        public float ConfidenceThreshold => _confidenceThreshold;

        /// <summary>
        /// Returns the sprite associated with the given gesture type.
        /// Falls back to <see cref="NoneSprite"/> if not found.
        /// </summary>
        public Sprite GetSprite(GestureType type)
        {
            if (type == GestureType.None || type == GestureType.Count)
            {
                return _noneSprite;
            }

            foreach (GestureEntry entry in _gestureEntries)
            {
                if (entry.Type == type)
                {
                    return entry.Sprite != null ? entry.Sprite : _noneSprite;
                }
            }

            return _noneSprite;
        }

        /// <summary>
        /// Returns the display name for a gesture type.
        /// </summary>
        public string GetDisplayName(GestureType type)
        {
            foreach (GestureEntry entry in _gestureEntries)
            {
                if (entry.Type == type && !string.IsNullOrEmpty(entry.DisplayName))
                {
                    return entry.DisplayName;
                }
            }

            return type.ToString();
        }

        /// <summary>Sprite shown when no gesture is detected.</summary>
        public Sprite NoneSprite => _noneSprite;

        /// <summary>Read-only access to all configured gesture entries.</summary>
        public IReadOnlyList<GestureEntry> Entries => _gestureEntries;

        // -----------------------------------------------------------------
        // Nested types
        // -----------------------------------------------------------------

        /// <summary>
        /// Pairs a <see cref="GestureType"/> with its cartoon sprite.
        /// </summary>
        [Serializable]
        public class GestureEntry
        {
            [Tooltip("Which gesture this entry represents.")]
            public GestureType Type;

            [Tooltip("The cartoon sprite displayed for this gesture.")]
            public Sprite Sprite;

            [Tooltip("Optional human-readable name (e.g. for UI labels).")]
            public string DisplayName;
        }
    }
}
