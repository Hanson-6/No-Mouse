using System.IO;
using UnityEditor;
using UnityEngine;

// Tools/Create Puzzle Prefabs
//   Generates PushableBox, PressureButton, and SwitchDoor prefabs in Assets/Prefabs/.
//   Run once; re-run at any time to regenerate/overwrite them.
//
// Tile-world scale convention used in this project:
//   Cave spritesheet: PPU = 8  →  16 px = 2 local units
//   Tilemap scale: (0.16, 0.16, 1)  →  1 local unit * 0.16 = 0.16 world units
//   Therefore: 1 cave tile  = 16 px / 8 PPU * 0.16 scale = 0.32 world units
public static class PuzzleElementsSetup
{
    const string CaveDetail2 = "Assets/CaveAssets/Spritesheets/Decorations/CaveDetailSprites2.png";
    const float  TileScale   = 0.16f;   // matches CaveTilemapGrid scale

    // ── Entry point ──────────────────────────────────────────────────────────

    [MenuItem("Tools/Create Puzzle Prefabs")]
    static void CreateAll()
    {
        EnsureTag("Box");
        Directory.CreateDirectory("Assets/Prefabs");

        CreateBoxPrefab();
        CreateButtonPrefab();
        CreateDoorPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PuzzleElements] Done — prefabs written to Assets/Prefabs/");
    }

    // ── Tag helper ───────────────────────────────────────────────────────────

    static void EnsureTag(string tag)
    {
        var tm   = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tm.ApplyModifiedProperties();
        Debug.Log($"[PuzzleElements] Added tag '{tag}'");
    }

    // ── Sprite loader ────────────────────────────────────────────────────────

    static Sprite LoadSprite(string texPath, string spriteName)
    {
        foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(texPath))
            if (a is Sprite s && s.name == spriteName) return s;
        Debug.LogWarning($"[PuzzleElements] Sprite '{spriteName}' not found in {texPath}");
        return null;
    }

    // ── PushableBox ──────────────────────────────────────────────────────────
    //
    // Sprite: CaveDetailSprites2_0  (24×16 px)
    // At PPU=8 and TileScale=0.16:  world size = (0.48, 0.32)
    // Collider local size = sprite px / PPU = (24/8, 16/8) = (3, 2)

    static void CreateBoxPrefab()
    {
        var go = new GameObject("PushableBox");
        go.transform.localScale = new Vector3(TileScale, TileScale, 1f);
        try { go.tag = "Box"; }
        catch { Debug.LogWarning("[PuzzleElements] Could not set tag 'Box' — set it manually in the Inspector."); }

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(CaveDetail2, "CaveDetailSprites2_0");

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass         = 5f;
        rb.gravityScale = 3f;
        // "linearDamping" in Unity 6+, "drag" in Unity 2022 and earlier
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 5f;
#else
        rb.drag = 5f;
#endif
        rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Zero-friction material so the box doesn't stick to walls while being pushed
        var pm = new PhysicsMaterial2D("BoxPhysics") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(pm, "Assets/Prefabs/BoxPhysics.physicsMaterial2D");

        var col = go.AddComponent<BoxCollider2D>();
        col.size           = new Vector2(3f, 2f);   // local units
        col.sharedMaterial = pm;

        go.AddComponent<PushableBox>();

        Save(go, "Assets/Prefabs/PushableBox.prefab");
        Object.DestroyImmediate(go);
    }

    // ── PressureButton ───────────────────────────────────────────────────────
    //
    // Two sprites: unpressed = CaveDetailSprites2_1, pressed = CaveDetailSprites2_2
    // Trigger zone: 2×2 local (= 0.32×0.32 world), offset (0, 1) local so it sits
    // just above the ground surface and catches anything resting on the button.

    static void CreateButtonPrefab()
    {
        var go = new GameObject("PressureButton");
        go.transform.localScale = new Vector3(TileScale, TileScale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(CaveDetail2, "CaveDetailSprites2_1"); // default face

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(2f, 2f);   // 1 tile wide/tall in local space
        col.offset    = new Vector2(0f, 1f);   // raised so only things ON TOP activate it

        var btn = go.AddComponent<ButtonController>();

        // Wire default sprites via SerializedObject so the prefab serializes them
        var so           = new SerializedObject(btn);
        var unpressedProp = so.FindProperty("unpressedSprite");
        var pressedProp   = so.FindProperty("pressedSprite");
        unpressedProp.objectReferenceValue = LoadSprite(CaveDetail2, "CaveDetailSprites2_1");
        pressedProp.objectReferenceValue   = LoadSprite(CaveDetail2, "CaveDetailSprites2_2");
        so.ApplyModifiedProperties();

        Save(go, "Assets/Prefabs/PressureButton.prefab");
        Object.DestroyImmediate(go);
    }

    // ── SwitchDoor ───────────────────────────────────────────────────────────
    //
    // Three 16×16 px cave tiles stacked vertically form a door (3 tiles = 0.96 world units).
    // Sprites used: CaveDetailSprites2_11, _12, _13  (swap to taste).
    // openOffset on SwitchDoor is set to 1.0 world unit (slides up one door height).

    static void CreateDoorPrefab()
    {
        var root = new GameObject("SwitchDoor");
        root.transform.localScale = new Vector3(TileScale, TileScale, 1f);

        // 3 stacked sprite tiles (children, so they scale with root)
        string[] spriteNames = { "CaveDetailSprites2_11", "CaveDetailSprites2_12", "CaveDetailSprites2_13" };
        for (int i = 0; i < spriteNames.Length; i++)
        {
            var tile = new GameObject($"DoorTile{i}");
            tile.transform.SetParent(root.transform, worldPositionStays: false);
            tile.transform.localPosition = new Vector3(0f, i * 2f, 0f); // 2 local = 1 tile height
            var sr = tile.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(CaveDetail2, spriteNames[i]);
        }

        // Solid collider covering all 3 tiles
        // Local: size(2, 6), offset(0, 3)  →  world: (0.32, 0.96) centered at y=0.48
        var col    = root.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(2f, 6f);
        col.offset = new Vector2(0f, 3f);

        var door = root.AddComponent<SwitchDoor>();

        // openOffset = 1.0 world unit so the door fully clears the doorway
        var so = new SerializedObject(door);
        so.FindProperty("openOffset").floatValue  = 1f;
        so.FindProperty("slideSpeed").floatValue  = 4f;
        so.ApplyModifiedProperties();

        Save(root, "Assets/Prefabs/SwitchDoor.prefab");
        Object.DestroyImmediate(root);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    static void Save(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Debug.Log($"[PuzzleElements] Saved {path}");
    }
}
