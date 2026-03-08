// ============================================================================
// GestureType.cs
// Defines all recognized gesture types.
// To add a new gesture:
//   1. Add a new enum value here
//   2. Add classification logic in GestureClassifier.cs
//   3. Add a GestureEntry in the GestureConfig ScriptableObject (Inspector)
// ============================================================================

namespace GestureRecognition.Core
{
    /// <summary>
    /// Enum representing all supported gesture types.
    /// <para>
    /// <b>How to extend:</b> Simply add a new value before <see cref="Count"/>.
    /// Then update <see cref="Detection.GestureClassifier"/> and assign a
    /// sprite in the <see cref="GestureConfig"/> asset.
    /// </para>
    /// </summary>
    public enum GestureType
    {
        /// <summary>No gesture detected or hand not visible.</summary>
        None = 0,

        /// <summary>Push gesture — open palm pushing forward.</summary>
        Push = 1,

        /// <summary>Lift gesture — open hand moving upward.</summary>
        Lift = 2,

        /// <summary>Shoot gesture — finger gun / pointing gesture.</summary>
        Shoot = 3,

        /// <summary>Fist gesture — closed hand, used for grabbing.</summary>
        Fist = 4,

        /// <summary>Open palm — all fingers extended.</summary>
        OpenPalm = 5,

        // ---------------------------------------------------------------
        // Add new gestures above this line.
        // ---------------------------------------------------------------

        /// <summary>
        /// Sentinel value equal to the total number of gesture types
        /// (excluding None). Do NOT use as a gesture.
        /// </summary>
        Count
    }
}
