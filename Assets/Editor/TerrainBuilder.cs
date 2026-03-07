using UnityEngine;
using UnityEditor;

public class TerrainBuilder : EditorWindow
{
    [MenuItem("Tools/Build Terrain Visuals")]
    public static void BuildTerrain()
    {
        string terrainPath = "Assets/Pixel Adventure 1/Assets/Terrain/Terrain Sliced (16x16).png";

        // Load all terrain sprites
        var allSprites = AssetDatabase.LoadAllAssetsAtPath(terrainPath);
        Sprite topLeft = null, topMid = null, topRight = null;
        Sprite midLeft = null, midMid = null, midRight = null;

        foreach (var obj in allSprites)
        {
            if (obj is Sprite s)
            {
                // Pixel Adventure 1 terrain layout (6 cols x N rows):
                // Row0: _0=topLeft, _1=topMid, _2=topRight, _3..._5=misc
                // Row1: _6=midLeft, _7=midMid, _8=midRight, ...
                switch (s.name)
                {
                    case "Terrain (16x16)_0": topLeft  = s; break;
                    case "Terrain (16x16)_1": topMid   = s; break;
                    case "Terrain (16x16)_2": topRight = s; break;
                    case "Terrain (16x16)_6": midLeft  = s; break;
                    case "Terrain (16x16)_7": midMid   = s; break;
                    case "Terrain (16x16)_8": midRight = s; break;
                }
            }
        }

        if (topMid == null)
        {
            Debug.LogError("[TerrainBuilder] Could not find terrain sprites. Check sprite names.");
            return;
        }

        // Rebuild Ground (20 wide, 2 rows tall)
        RebuildPlatform("Ground",   new Vector2(0, 0),   20, 2, topLeft, topMid, topRight, midLeft, midMid, midRight);
        // Rebuild Platform1
        RebuildPlatform("Platform1", new Vector2(5, 3),   6, 1, topLeft, topMid, topRight, midLeft, midMid, midRight);
        // Rebuild Platform2
        RebuildPlatform("Platform2", new Vector2(10, 5),  6, 1, topLeft, topMid, topRight, midLeft, midMid, midRight);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("[TerrainBuilder] Done!");
    }

    static void RebuildPlatform(string name, Vector2 worldPos, int tilesWide, int tilesHigh,
        Sprite topLeft, Sprite topMid, Sprite topRight,
        Sprite midLeft, Sprite midMid, Sprite midRight)
    {
        const float tileSize = 0.16f; // 16px at 100 PPU

        // Find or create the parent GO
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            // Remove old children
            while (existing.transform.childCount > 0)
                Object.DestroyImmediate(existing.transform.GetChild(0).gameObject);

            // Reset scale
            existing.transform.localScale = Vector3.one;
            existing.transform.position = new Vector3(worldPos.x, worldPos.y, 0);
        }
        else
        {
            existing = new GameObject(name);
            existing.transform.position = new Vector3(worldPos.x, worldPos.y, 0);
            existing.layer = LayerMask.NameToLayer("Ground");
        }

        float totalWidth  = tilesWide * tileSize;
        float totalHeight = tilesHigh * tileSize;

        // Ensure BoxCollider2D exists and is sized correctly
        var bc = existing.GetComponent<BoxCollider2D>();
        if (bc == null) bc = existing.AddComponent<BoxCollider2D>();
        bc.size   = new Vector2(totalWidth, totalHeight);
        bc.offset = new Vector2(totalWidth / 2f - tileSize / 2f, totalHeight / 2f - tileSize / 2f);

        // Place tiles
        for (int row = 0; row < tilesHigh; row++)
        {
            for (int col = 0; col < tilesWide; col++)
            {
                Sprite sprite;
                int displayRow = tilesHigh - 1 - row; // top row = highest displayRow

                if (displayRow == tilesHigh - 1) // top row
                {
                    if (col == 0)              sprite = topLeft;
                    else if (col == tilesWide - 1) sprite = topRight;
                    else                       sprite = topMid;
                }
                else // middle/bottom rows
                {
                    if (col == 0)              sprite = midLeft;
                    else if (col == tilesWide - 1) sprite = midRight;
                    else                       sprite = midMid;
                }

                var tile = new GameObject($"tile_{col}_{row}");
                tile.transform.SetParent(existing.transform);
                tile.transform.localPosition = new Vector3(col * tileSize, row * tileSize, 0);
                tile.layer = existing.layer;

                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 0;
                // pixel-perfect: no filtering
                
            }
        }

        Debug.Log($"[TerrainBuilder] Built '{name}' ({tilesWide}x{tilesHigh} tiles)");
    }
}
