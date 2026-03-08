using UnityEngine;
using UnityEditor;
using System.Linq;

public class FixPlatformTiles
{
    // 每个 tile 是 16px，PPU=100，所以 1 tile = 0.16 unit
    const float TILE = 0.16f;

    [MenuItem("Tools/Fix Platform Tiles")]
    public static void Run()
    {
        FixPlatform("Platform1");
        FixPlatform("Platform2");
        FixPlatform("Platform1 (1)");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Platform tiles fixed.");
    }

    static void FixPlatform(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogWarning("Not found: " + goName); return; }

        // 收集并按名字排序所有 tile 子物体
        var tiles = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in go.transform)
            tiles.Add(child);

        // 按 tile_X_Y 的 X 值排序
        tiles.Sort((a, b) =>
        {
            int xa = ParseCol(a.name), xb = ParseCol(b.name);
            int ya = ParseRow(a.name), yb = ParseRow(b.name);
            return (ya != yb) ? ya.CompareTo(yb) : xa.CompareTo(xb);
        });

        if (tiles.Count == 0) { Debug.LogWarning("No tiles in " + goName); return; }

        // 计算列数和行数
        int maxCol = tiles.Max(t => ParseCol(t.name)) + 1;
        int maxRow = tiles.Max(t => ParseRow(t.name)) + 1;

        // 把每个 tile 放到正确的局部坐标（从 (0,0) 开始，列向右，行向上）
        foreach (var tile in tiles)
        {
            int col = ParseCol(tile.name);
            int row = ParseRow(tile.name);
            tile.localPosition = new Vector3(col * TILE, row * TILE, 0);
            EditorUtility.SetDirty(tile.gameObject);
        }

        // 修正父物体的 BoxCollider2D
        var col2d = go.GetComponent<BoxCollider2D>();
        if (col2d != null)
        {
            float w = maxCol * TILE;
            float h = maxRow * TILE;
            col2d.offset = new Vector2(w * 0.5f, h * 0.5f);
            col2d.size   = new Vector2(w, h);
            EditorUtility.SetDirty(go);
        }

        Debug.Log($"Fixed {goName}: {maxCol} cols x {maxRow} rows, collider ({maxCol * TILE} x {maxRow * TILE})");
    }

    static int ParseCol(string name)
    {
        // tile_X_Y
        var parts = name.Split('_');
        return (parts.Length >= 2 && int.TryParse(parts[1], out int v)) ? v : 0;
    }

    static int ParseRow(string name)
    {
        var parts = name.Split('_');
        return (parts.Length >= 3 && int.TryParse(parts[2], out int v)) ? v : 0;
    }
}
