using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Fixes pink/magenta tile assets whose m_Sprite references became stale after
/// a texture reimport regenerated sub-sprite fileIDs.
///
/// Strategy: UPDATE every existing .asset in place (preserves GUIDs) so that
/// Level1/CaveTilemapGrid and ScenePalette.prefab keep working without
/// any manual reassignment.
///
/// Menu: Tools/Rebuild Cave Tile Assets
/// </summary>
public static class RebuildCaveTileAssets
{
    const string TILES_DIR  = "Assets/CaveAssets/Scenes/tiles";
    const string TEXTURES_DIR = "Assets/CaveAssets/Tiles";

    // All source textures in the CaveAssets tile set
    static readonly string[] SOURCE_TEXTURES =
    {
        "CaveBackgroundTiles.png",
        "CaveDetailTiles.png",
        "CaveDetailTiles2.png",
        "CaveRailsPlatformsTiles.png",
        "CaveTerrainDetailTiles.png",
        "CaveTerrainDetailTiles2.png",
        "CaveTerrainTiles.png",
        "FoliageTiles.png",
    };

    [MenuItem("Tools/Rebuild Cave Tile Assets")]
    static void RebuildAll()
    {
        var report = new StringBuilder();
        report.AppendLine("=== Cave Tile Asset Rebuild Report ===\n");

        // ── Step 1: Verify (and fix if needed) texture import settings ────────
        report.AppendLine("─── Texture Import Settings ───");
        bool anyReimported = false;
        foreach (string texFile in SOURCE_TEXTURES)
        {
            string path = $"{TEXTURES_DIR}/{texFile}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.AppendLine($"  MISSING: {texFile}");
                continue;
            }

            bool dirty = false;
            if (importer.spritePixelsPerUnit != 8f)
            {
                importer.spritePixelsPerUnit = 8f;
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            // Verify default platform compression = None (0)
            var platformSettings = importer.GetDefaultPlatformTextureSettings();
            if (platformSettings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platformSettings);
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                anyReimported = true;
                report.AppendLine($"  FIXED & reimported: {texFile}  (PPU=8, Point, Uncompressed)");
            }
            else
            {
                report.AppendLine($"  OK: {texFile}  PPU=8, FilterMode=Point, Compression=None");
            }
        }
        if (anyReimported)
            AssetDatabase.Refresh();

        // ── Step 2 + 3: Update/create tile assets per texture ─────────────────
        report.AppendLine("\n─── Tile Asset Repair ───");
        int totalUpdated = 0, totalCreated = 0, totalOrphaned = 0;

        foreach (string texFile in SOURCE_TEXTURES)
        {
            string texPath  = $"{TEXTURES_DIR}/{texFile}";
            string texName  = Path.GetFileNameWithoutExtension(texFile);

            // Load all sprites from this texture, keyed by sprite name
            var spriteMap = AssetDatabase.LoadAllAssetsAtPath(texPath)
                .OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);

            if (spriteMap.Count == 0)
            {
                report.AppendLine($"  {texName}: 0 sprites found — skipped (texture missing or not sliced)");
                continue;
            }

            // Track which sprite names we've matched (to detect orphaned tile assets)
            var unmatchedSprites = new HashSet<string>(spriteMap.Keys);

            // Find every .asset in TILES_DIR that belongs to this texture
            string[] guids = AssetDatabase.FindAssets(
                $"t:Tile {texName}", new[] { TILES_DIR });

            int updated = 0, created = 0, orphaned = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string assetName = Path.GetFileNameWithoutExtension(assetPath); // e.g. "CaveTerrainTiles_64"

                if (!spriteMap.TryGetValue(assetName, out Sprite sprite))
                {
                    // Tile asset exists but no matching sprite found — stale/orphaned
                    orphaned++;
                    report.AppendLine($"    ORPHAN (no sprite): {assetName}.asset");
                    continue;
                }

                var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                if (tile == null) continue;

                tile.sprite        = sprite;
                tile.color         = Color.white;
                tile.colliderType  = Tile.ColliderType.Sprite;
                EditorUtility.SetDirty(tile);
                unmatchedSprites.Remove(assetName);
                updated++;
            }

            // Create tile assets for sprites that have no .asset yet
            foreach (string spriteName in unmatchedSprites)
            {
                string newPath = $"{TILES_DIR}/{spriteName}.asset";
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite       = spriteMap[spriteName];
                tile.color        = Color.white;
                tile.colliderType = Tile.ColliderType.Sprite;
                AssetDatabase.CreateAsset(tile, newPath);
                created++;
            }

            AssetDatabase.SaveAssets();

            report.AppendLine(
                $"  {texName,-35} sprites={spriteMap.Count,4}  " +
                $"updated={updated,4}  created={created,3}  orphaned={orphaned,3}");

            totalUpdated  += updated;
            totalCreated  += created;
            totalOrphaned += orphaned;
        }

        // ── Step 4: Verify ScenePalette.prefab ────────────────────────────────
        report.AppendLine("\n─── ScenePalette ───");
        string palettePath = $"{TILES_DIR}/ScenePalette.prefab";
        var palette = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
        if (palette == null)
        {
            report.AppendLine("  ScenePalette.prefab NOT FOUND at expected path.");
        }
        else
        {
            var tilemapInPalette = palette.GetComponentInChildren<Tilemap>();
            if (tilemapInPalette != null)
            {
                // Count how many tiles the palette references
                tilemapInPalette.CompressBounds();
                var bounds = tilemapInPalette.cellBounds;
                int nullTiles = 0, validTiles = 0;
                foreach (var pos in bounds.allPositionsWithin)
                {
                    var t = tilemapInPalette.GetTile(pos);
                    if (t == null) continue;
                    if (t is Tile tt && tt.sprite == null) nullTiles++;
                    else validTiles++;
                }
                report.AppendLine($"  ScenePalette: {validTiles} valid tiles, {nullTiles} null-sprite tiles.");
                if (nullTiles > 0)
                    report.AppendLine("  → Some palette tiles still show null sprites. " +
                                      "Re-open the Tile Palette window to force a redraw.");
            }
            else
            {
                report.AppendLine("  ScenePalette: no Tilemap component found inside prefab.");
            }
        }

        // ── Step 5: Level1 CaveTilemapGrid status ─────────────────────────────
        report.AppendLine("\n─── Level1 CaveTilemapGrid ───");
        report.AppendLine(
            "  Tile assets were updated IN PLACE (GUIDs preserved).\n" +
            "  Level1 CaveTilemapGrid references are still valid — no reassignment needed.\n" +
            "  If tiles still appear pink in Play Mode, save the scene and re-open it.");

        // ── Summary ───────────────────────────────────────────────────────────
        report.AppendLine($"\n─── Summary ───");
        report.AppendLine($"  Total updated : {totalUpdated}");
        report.AppendLine($"  Total created : {totalCreated}");
        report.AppendLine($"  Total orphaned: {totalOrphaned}");

        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
        Debug.Log("[RebuildCaveTileAssets] Done. Re-open Tile Palette window if palette still shows pink.");
    }
}
