using UnityEngine;
using UnityEditor;

public class TerrainSpriteSetup
{
    const string TERRAIN_PATH = "Assets/Pixel Adventure 1/Assets/Terrain/Terrain Sliced (16x16).png";
    const int COLS = 17; // 17 columns per row

    [MenuItem("Tools/Setup Terrain Sprites")]
    public static void Run()
    {
        // Load all terrain sprites
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(TERRAIN_PATH);
        var sprites = new System.Collections.Generic.Dictionary<string, Sprite>();
        foreach (var a in allAssets)
            if (a is Sprite s) sprites[s.name] = s;

        // Helper: get sprite by row,col
        Sprite Get(int row, int col)
        {
            string name = "Terrain (16x16)_" + (row * COLS + col);
            return sprites.TryGetValue(name, out var sp) ? sp : null;
        }

        // Apply to Ground (20 cols x 2 rows)
        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            ApplyTileRow(ground, 20, 0, Get, true);  // top surface row
            ApplyTileRow(ground, 20, 1, Get, false); // underground row
        }

        // Apply to Platform1 (6 cols x 1 row)
        var p1 = GameObject.Find("Platform1");
        if (p1 != null) ApplyTileRow(p1, 6, 0, Get, true);

        // Apply to Platform2 (6 cols x 1 row)
        var p2 = GameObject.Find("Platform2");
        if (p2 != null) ApplyTileRow(p2, 6, 0, Get, true);

        // Apply to MovingPlatform (single sprite, use middle surface tile)
        var mp = GameObject.Find("MovingPlatform");
        if (mp != null)
        {
            var sr = mp.GetComponent<SpriteRenderer>() ?? mp.AddComponent<SpriteRenderer>();
            sr.sprite = Get(0, 1);
            EditorUtility.SetDirty(mp);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Terrain sprites applied to all platforms.");
    }

    static void ApplyTileRow(GameObject parent, int totalCols, int yRow,
        System.Func<int, int, Sprite> get, bool isSurface)
    {
        for (int x = 0; x < totalCols; x++)
        {
            string tileName = "tile_" + x + "_" + yRow;
            var tileGO = parent.transform.Find(tileName)?.gameObject;
            if (tileGO == null) continue;

            var sr = tileGO.GetComponent<SpriteRenderer>() ?? tileGO.AddComponent<SpriteRenderer>();

            Sprite sp;
            if (isSurface)
            {
                // Surface row: left cap / middle / right cap (row 0 of tileset)
                if (x == 0)               sp = get(0, 0);   // left cap with grass
                else if (x == totalCols - 1) sp = get(0, 16); // right cap with grass
                else                         sp = get(0, 1);  // middle with grass
            }
            else
            {
                // Underground row (row 1 of tileset)
                if (x == 0)               sp = get(1, 0);   // underground left
                else if (x == totalCols - 1) sp = get(1, 16); // underground right
                else                         sp = get(1, 1);  // underground middle
            }

            if (sp != null)
            {
                sr.sprite = sp;
                EditorUtility.SetDirty(tileGO);
            }
        }
    }
}
