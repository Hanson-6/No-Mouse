using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// Rebuilds Ground GameObjects in the scene with cave-style sprites and configurable depth.
/// Menu: Tools -> Rebuild Cave Ground
/// </summary>
public class RebuildCaveGround : Editor
{
    const string TERRAIN_PATH = "Assets/Pixel Adventure 1/Assets/Terrain/Terrain Sliced (16x16).png";
    const int COLS = 20;
    const int DEPTH = 30;  // number of tile rows (height of the ground)
    const float TILE = 0.16f;
    const int LAYER_GROUND = 8;

    // Sprite indices in "Terrain Sliced (16x16).png" (12-col sheet)
    // Cave/stone style (same as platforms, no green):
    // Row 0 surface:  0=left  1=middle  2=right
    // Row 1 fill:    12=left 13=middle 14=right
    // Row 2 deep:    24=left 25=middle 26=right

    [MenuItem("Tools/Rebuild Cave Ground")]
    public static void Run()
    {
        var allSprites = AssetDatabase.LoadAllAssetsAtPath(TERRAIN_PATH)
            .OfType<Sprite>().ToArray();

        if (allSprites.Length == 0)
        {
            Debug.LogError("[CaveGround] Cannot find terrain sprites at: " + TERRAIN_PATH);
            return;
        }

        // Find all Ground GameObjects in the active scene
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int rebuilt = 0;
        foreach (var root in roots)
        {
            if (!root.name.StartsWith("Ground")) continue;
            RebuildGround(root, allSprites);
            rebuilt++;
        }

        if (rebuilt == 0)
        {
            Debug.LogWarning("[CaveGround] No 'Ground' objects found in the scene.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[CaveGround] Rebuilt {rebuilt} Ground object(s). Press Ctrl+S to save.");
    }

    static void RebuildGround(GameObject root, Sprite[] allSprites)
    {
        // Remove all existing tile children
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform t in root.transform) children.Add(t.gameObject);
        foreach (var c in children) DestroyImmediate(c);

        // Build new tiles — surface at row 0 (Y=0), depth goes downward (negative Y)
        for (int row = 0; row < DEPTH; row++)
        {
            for (int col = 0; col < COLS; col++)
            {
                int spriteIdx = GetSpriteIndex(col, row);
                Sprite spr = GetSprite(allSprites, spriteIdx);
                if (spr == null) continue;

                var tile = new GameObject($"tile_{col}_{row}");
                tile.transform.SetParent(root.transform, false);
                // row 0 = surface (Y=0), deeper rows go negative Y
                tile.transform.localPosition = new Vector3(col * TILE, -row * TILE, 0f);
                tile.layer = LAYER_GROUND;

                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                sr.sortingOrder = 0;
            }
        }

        // Update BoxCollider2D: surface top at Y=+TILE/2, depth extends downward
        var col2d = root.GetComponent<BoxCollider2D>();
        if (col2d == null) col2d = root.AddComponent<BoxCollider2D>();

        float totalWidth  = COLS * TILE;
        float totalHeight = DEPTH * TILE;
        col2d.size   = new Vector2(totalWidth, totalHeight);
        // offset: center X along tile row, center Y halfway down from surface
        col2d.offset = new Vector2(totalWidth / 2f - TILE / 2f, -(totalHeight / 2f - TILE / 2f));

        root.layer = LAYER_GROUND;
        EditorUtility.SetDirty(root);
        Debug.Log($"[CaveGround] Rebuilt '{root.name}': {COLS}x{DEPTH} tiles, collider {totalWidth:F2}x{totalHeight:F2}");
    }

    /// <summary>
    /// Returns the sprite sheet index for a given tile column and row.
    /// Sheet is 12 columns wide:
    ///   Surface row  (row == DEPTH-1): left=6, middle=7, right=8
    ///   Fill rows                    : left=19, middle=19, right=19 (solid cave fill)
    ///   Deep rows    (row == 0)      : left=31, middle=31, right=31 (darkest fill)
    /// </summary>
    static int GetSpriteIndex(int col, int row)
    {
        bool isLeft  = col == 0;
        bool isRight = col == COLS - 1;
        bool isSurface = row == 0;          // top visible row (Y=0)
        bool isDeep    = row == DEPTH - 1; // deepest row (most negative Y)

        if (isSurface)
        {
            if (isLeft)  return 0;
            if (isRight) return 2;
            return 1;
        }
        if (isDeep)
        {
            if (isLeft)  return 24;
            if (isRight) return 26;
            return 25;
        }
        // Middle fill rows — same stone color as surface
        if (isLeft)  return 0;
        if (isRight) return 2;
        return 1;
    }

    static Sprite GetSprite(Sprite[] sprites, int index)
    {
        string name = $"Terrain (16x16)_{index}";
        var s = sprites.FirstOrDefault(sp => sp.name == name);
        if (s == null) Debug.LogWarning($"[CaveGround] Sprite not found: {name}");
        return s;
    }
}
