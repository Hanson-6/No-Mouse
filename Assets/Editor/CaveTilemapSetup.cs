using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Creates Assets/Prefabs/Environment/CaveTilemapGrid.prefab
/// (Grid + Tilemap_Background + Tilemap_Ground + Tilemap_Ground_Coll)
/// </summary>
public static class CaveTilemapSetup
{
    const string PREFAB_PATH = "Assets/Prefabs/Environment/CaveTilemapGrid.prefab";
    const int    LAYER_GROUND = 8;

    [MenuItem("Tools/Setup Cave Tilemap Prefab")]
    static void CreatePrefab()
    {
        var go = BuildGridHierarchy();

        bool success;
        PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH, out success);
        Object.DestroyImmediate(go);

        if (success)
            Debug.Log($"[CaveTilemapSetup] Prefab saved → {PREFAB_PATH}");
        else
            Debug.LogError("[CaveTilemapSetup] SaveAsPrefabAsset failed.");
    }

    static GameObject BuildGridHierarchy()
    {
        var root = new GameObject("CaveTilemapGrid");
        var grid = root.AddComponent<Grid>();
        grid.cellSize   = new Vector3(1f, 1f, 0f);
        grid.cellLayout = GridLayout.CellLayout.Rectangle;

        // ── Tilemap_Background (visual only, rendered behind ground) ──────────
        var bgGO = new GameObject("Tilemap_Background");
        bgGO.transform.SetParent(root.transform, false);
        bgGO.AddComponent<Tilemap>();
        var bgRenderer = bgGO.AddComponent<TilemapRenderer>();
        bgRenderer.sortingOrder = -1;   // behind Tilemap_Ground

        // ── Tilemap_Ground (visual, SortingOrder = 0) ─────────────────────────
        var groundGO = new GameObject("Tilemap_Ground");
        groundGO.transform.SetParent(root.transform, false);
        groundGO.AddComponent<Tilemap>();
        var groundRenderer = groundGO.AddComponent<TilemapRenderer>();
        groundRenderer.sortingOrder = 0;

        // ── Tilemap_Ground_Coll (collision only — NO TilemapRenderer) ─────────
        var collGO = new GameObject("Tilemap_Ground_Coll");
        collGO.transform.SetParent(root.transform, false);
        collGO.layer = LAYER_GROUND;    // Layer 8 = "Ground" (PlayerController ground detection)
        collGO.AddComponent<Tilemap>();
        // Deliberately no TilemapRenderer

        // TilemapCollider2D → feeds into CompositeCollider2D
        var tc = collGO.AddComponent<TilemapCollider2D>();
        tc.usedByComposite = true;

        // CompositeCollider2D auto-adds Rigidbody2D; set both
        var composite = collGO.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        var rb = collGO.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Static;

        return root;
    }
}
