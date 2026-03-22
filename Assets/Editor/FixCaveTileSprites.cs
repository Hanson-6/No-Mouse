using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;

public class FixCaveTileSprites : EditorWindow
{
    [MenuItem("Tools/Fix Cave Tile Sprites")]
    public static void Run()
    {
        const string tilesDir = "Assets/CaveAssets/Scenes/tiles";
        const string texturesDir = "Assets/CaveAssets/Tiles";

        // Step 1: Force-reimport all cave textures so Library cache is fresh
        Debug.Log("[FixCaveTiles] Force-reimporting cave textures...");
        string[] pngFiles = Directory.GetFiles(texturesDir, "*.png");
        foreach (string f in pngFiles)
            AssetDatabase.ImportAsset(f.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);

        // Step 2: Build sprite lookup: name -> Sprite
        var spriteLookup = new Dictionary<string, Sprite>();
        foreach (string f in pngFiles)
        {
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(f.Replace('\\', '/')))
                if (obj is Sprite s) spriteLookup[s.name] = s;
        }
        Debug.Log($"[FixCaveTiles] Loaded {spriteLookup.Count} sprites.");

        // Step 3: Re-assign sprites in every tile asset
        string[] tileGuids = AssetDatabase.FindAssets("t:Tile", new[] { tilesDir });
        int fixed_ = 0, alreadyOk = 0, failed = 0, nullBefore = 0;

        foreach (string guid in tileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null) { failed++; continue; }

            if (!spriteLookup.TryGetValue(tile.name, out Sprite expected))
            {
                Debug.LogWarning($"[FixCaveTiles] No sprite found for tile '{tile.name}'");
                failed++;
                continue;
            }

            if (tile.sprite == null) nullBefore++;
            if (tile.sprite == expected) { alreadyOk++; continue; }

            SerializedObject so = new SerializedObject(tile);
            so.FindProperty("m_Sprite").objectReferenceValue = expected;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
            fixed_++;
        }

        AssetDatabase.SaveAssets();

        // Step 4: Report palette material
        var palettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tilesDir + "/ScenePalette.prefab");
        if (palettePrefab != null)
        {
            var tr = palettePrefab.GetComponentInChildren<TilemapRenderer>();
            Material mat = tr != null ? tr.sharedMaterial : null;
            Debug.Log("[FixCaveTiles] Palette material: " +
                (mat != null ? mat.name + " / shader=" + mat.shader.name : "NULL (missing!)"));
        }

        Debug.Log($"[FixCaveTiles] Done.  Fixed:{fixed_}  AlreadyOK:{alreadyOk}  NullBefore:{nullBefore}  Failed:{failed}");
    }
}
