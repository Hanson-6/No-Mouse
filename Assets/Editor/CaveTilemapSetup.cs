using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// STEP 2 / 3 of the cave tilemap migration.
///
///   Tools/Setup Cave Tilemap Prefab
///       Creates Assets/Prefabs/Environment/CaveTilemapGrid.prefab
///       (Grid + Tilemap_Background + Tilemap_Ground + Tilemap_Ground_Coll)
///
///   Tools/Setup Cave Tilemap in Level1
///       Instantiates the prefab in the active scene, sets Grid scale to
///       (0.16, 0.16, 1) so each 1-unit tile occupies 0.16 world units,
///       then scans every old Ground* / Platform* BoxCollider2D in the scene
///       and paints CaveTerrainTiles_64 over the same world-space area.
/// </summary>
public static class CaveTilemapSetup
{
    const string PREFAB_PATH = "Assets/Prefabs/Environment/CaveTilemapGrid.prefab";
    const string TILES_DIR   = "Assets/CaveAssets/Scenes/tiles/";
    const int    LAYER_GROUND = 8;

    // Primary solid fill tile (47 uses in Demo Scene Foreground — confirmed most-used)
    const string GROUND_FILL_TILE = "CaveTerrainTiles_64";

    // Background fill tile (LowerBackground layer counterpart)
    const string BG_FILL_TILE = "CaveBackgroundTiles_0";

    // ─────────────────────────────────────────────────────────────────────────
    // STEP 2 — Create / overwrite the prefab asset
    // ─────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────
    // STEP 3 — Instantiate in Level1, set scale, paint terrain
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Setup Cave Tilemap in Level1")]
    static void SetupInLevel1()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[CaveTilemapSetup] Prefab not found at {PREFAB_PATH}. " +
                           "Run 'Tools/Setup Cave Tilemap Prefab' first.");
            return;
        }

        // Remove any previous instance so re-runs are idempotent
        var existing = GameObject.Find("CaveTilemapGrid");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
            Debug.Log("[CaveTilemapSetup] Removed previous CaveTilemapGrid instance.");
        }

        // Instantiate and apply scale trick:
        // Grid cellSize = (1,1) but localScale = (0.16, 0.16, 1)
        // → each tile occupies 0.16 × 0.16 world units (matches TILE constant in RebuildCaveGround)
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "CaveTilemapGrid";
        instance.transform.position   = Vector3.zero;
        instance.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

        // Locate the three tilemap children
        var tilemaps   = instance.GetComponentsInChildren<Tilemap>();
        Tilemap bgTm   = tilemaps.FirstOrDefault(t => t.gameObject.name == "Tilemap_Background");
        Tilemap groundTm = tilemaps.FirstOrDefault(t => t.gameObject.name == "Tilemap_Ground");
        Tilemap collTm = tilemaps.FirstOrDefault(t => t.gameObject.name == "Tilemap_Ground_Coll");

        if (groundTm == null || collTm == null)
        {
            Debug.LogError("[CaveTilemapSetup] Expected Tilemap children not found in prefab.");
            return;
        }

        // Load tile assets
        TileBase fillTile = AssetDatabase.LoadAssetAtPath<TileBase>($"{TILES_DIR}{GROUND_FILL_TILE}.asset");
        TileBase bgTile   = AssetDatabase.LoadAssetAtPath<TileBase>($"{TILES_DIR}{BG_FILL_TILE}.asset");

        if (fillTile == null)
        {
            Debug.LogError($"[CaveTilemapSetup] Tile not found: {TILES_DIR}{GROUND_FILL_TILE}.asset");
            return;
        }

        // Paint from old terrain collider bounds
        PaintFromOldTerrain(bgTm, groundTm, collTm, fillTile, bgTile);

        // Rebuild composite collider geometry
        var composite = instance.GetComponentInChildren<CompositeCollider2D>();
        if (composite != null)
            composite.GenerateGeometry();

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[CaveTilemapSetup] CaveTilemapGrid added to scene. " +
                  "Save the scene when satisfied. Do NOT delete _OldTerrain_Prefabs yet.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build the in-memory hierarchy (not yet saved as a prefab)
    // ─────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────
    // Scan every old Ground* / Platform* BoxCollider2D in the active scene,
    // convert their world-space bounds to tile coordinates, and fill tiles.
    // ─────────────────────────────────────────────────────────────────────────

    static void PaintFromOldTerrain(Tilemap bgTm, Tilemap groundTm, Tilemap collTm,
                                    TileBase fillTile, TileBase bgTile)
    {
        // Collect all BoxCollider2D on the Ground physics layer whose owner
        // is an old terrain prefab (named Ground* or Platform*)
        var allColliders = Object.FindObjectsOfType<BoxCollider2D>();
        var terrainColliders = new List<BoxCollider2D>();
        foreach (var col in allColliders)
        {
            string n = col.gameObject.name;
            if (col.gameObject.layer != LAYER_GROUND) continue;
            if (n == "Tilemap_Ground_Coll")           continue;  // skip the new layer
            if (!n.StartsWith("Ground") && !n.StartsWith("Platform")) continue;
            if (col.isTrigger)                        continue;  // skip trigger colliders
            terrainColliders.Add(col);
        }

        if (terrainColliders.Count == 0)
        {
            Debug.LogWarning("[CaveTilemapSetup] No old terrain BoxCollider2D found. " +
                             "Make sure Level1 is the active open scene.");
            return;
        }

        int tilesGround = 0;

        foreach (var col in terrainColliders)
        {
            Bounds b = col.bounds;  // world-space bounds, accounts for offset + scale

            // WorldToCell uses the Grid's transform (including scale 0.16, 0.16, 1)
            // so it correctly maps world positions to integer tile coordinates.
            Vector3Int minCell = groundTm.WorldToCell(b.min);
            Vector3Int maxCell = groundTm.WorldToCell(b.max);

            // bounds.max edge may land exactly on a tile boundary; clamp to valid range
            // (WorldToCell floors, so max needs +0 — the loop <=maxCell is inclusive)
            for (int ty = minCell.y; ty <= maxCell.y; ty++)
            {
                for (int tx = minCell.x; tx <= maxCell.x; tx++)
                {
                    var cell = new Vector3Int(tx, ty, 0);
                    groundTm.SetTile(cell, fillTile);
                    collTm.SetTile(cell, fillTile);
                    tilesGround++;
                }
            }

            // Background layer: fill 1-tile padding around each terrain piece
            if (bgTm != null && bgTile != null)
            {
                for (int ty = minCell.y - 1; ty <= maxCell.y + 1; ty++)
                    for (int tx = minCell.x - 1; tx <= maxCell.x + 1; tx++)
                        bgTm.SetTile(new Vector3Int(tx, ty, 0), bgTile);
            }

            Debug.Log($"[CaveTilemapSetup]  {col.gameObject.name} " +
                      $"bounds={b.min:F2}→{b.max:F2}  " +
                      $"tiles=[{minCell.x},{minCell.y}]→[{maxCell.x},{maxCell.y}]");
        }

        Debug.Log($"[CaveTilemapSetup] Painted {tilesGround} ground/coll tiles " +
                  $"from {terrainColliders.Count} old terrain objects.");
    }
}
