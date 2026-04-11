using UnityEditor;
using UnityEngine;
using LDtkUnity;

/// <summary>
/// One-time setup: assigns the CollisionTile_Stone LDtkIntGridTile asset to the
/// Levels.ldtk importer for the Collision_2 (Stone) IntGrid value, then reimports.
/// </summary>
public static class LDtkCollisionSetup
{
    private const string LdtkPath = "Assets/IDTK/Levels.ldtk";
    private const string TilePath = "Assets/IDTK/IntGridTiles/CollisionTile_Stone.asset";

    [MenuItem("Tools/Setup LDtk Collision Layer")]
    public static void SetupCollisionLayer()
    {
        // Load the LDtk importer
        var importer = AssetImporter.GetAtPath(LdtkPath);
        if (importer == null)
        {
            Debug.LogError($"[LDtkCollisionSetup] Could not find importer for '{LdtkPath}'");
            return;
        }

        // Load the tile asset
        var tile = AssetDatabase.LoadAssetAtPath<LDtkIntGridTile>(TilePath);
        if (tile == null)
        {
            Debug.LogError($"[LDtkCollisionSetup] Could not load LDtkIntGridTile at '{TilePath}'");
            return;
        }

        // Use SerializedObject to set the _intGridValues array element
        var so = new SerializedObject(importer);
        var intGridValuesProp = so.FindProperty("_intGridValues");

        if (intGridValuesProp == null || !intGridValuesProp.isArray)
        {
            Debug.LogError("[LDtkCollisionSetup] Could not find '_intGridValues' property on importer.");
            return;
        }

        bool found = false;
        for (int i = 0; i < intGridValuesProp.arraySize; i++)
        {
            var element = intGridValuesProp.GetArrayElementAtIndex(i);
            var keyProp = element.FindPropertyRelative("_key");
            if (keyProp != null && keyProp.stringValue == "Collision_2")
            {
                var assetProp = element.FindPropertyRelative("_asset");
                if (assetProp != null)
                {
                    assetProp.objectReferenceValue = tile;
                    found = true;
                    Debug.Log($"[LDtkCollisionSetup] Assigned '{TilePath}' to Collision_2 IntGrid value.");
                }
                break;
            }
        }

        if (!found)
        {
            Debug.LogError("[LDtkCollisionSetup] Could not find 'Collision_2' key in _intGridValues array.");
            so.Dispose();
            return;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        so.Dispose();

        // Save and reimport
        AssetDatabase.WriteImportSettingsIfDirty(LdtkPath);
        AssetDatabase.ImportAsset(LdtkPath, ImportAssetOptions.ForceUpdate);

        Debug.Log("[LDtkCollisionSetup] Done. Levels.ldtk reimported with collision tile assigned.");
        Debug.Log("[LDtkCollisionSetup] Run 'Tools/Create LDtk Test Scene' to regenerate the scene.");
    }
}
