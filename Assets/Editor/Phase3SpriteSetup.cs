using UnityEngine;
using UnityEditor;

public class Phase3SpriteSetup
{
    [MenuItem("Tools/Setup Phase3 Sprites")]
    public static void Run()
    {
        // MovingSaw sprite
        SetSprite("MovingSaw", "Assets/Pixel Adventure 1/Assets/Traps/Saw/On (38x38).png", "On (38x38)_0");

        // Enemy sprite
        SetSprite("Enemy", "Assets/Pixel Adventure 1/Assets/Main Characters/Mask Dude/Idle (32x32).png", "Idle (32x32)_0");

        // Spikes sprite (Single mode, use full texture as sprite)
        SetSpriteByPath("Spikes", "Assets/Pixel Adventure 1/Assets/Traps/Spikes/Idle.png");
        SetSpriteByPath("Spikes2", "Assets/Pixel Adventure 1/Assets/Traps/Spikes/Idle.png");

        Debug.Log("Phase 3 sprites applied.");
    }

    static void SetSprite(string goName, string texPath, string spriteName)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogWarning("GameObject not found: " + goName); return; }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(texPath);
        foreach (var a in allAssets)
        {
            if (a is Sprite s && s.name == spriteName)
            {
                sr.sprite = s;
                EditorUtility.SetDirty(go);
                Debug.Log("Set sprite on " + goName + ": " + spriteName);
                return;
            }
        }
        Debug.LogWarning("Sprite not found: " + spriteName + " in " + texPath);
    }

    static void SetSpriteByPath(string goName, string texPath)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogWarning("GameObject not found: " + goName); return; }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (sprite == null)
        {
            // Try loading from sub-assets
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(texPath);
            foreach (var a in allAssets)
                if (a is Sprite s) { sprite = s; break; }
        }

        if (sprite != null)
        {
            sr.sprite = sprite;
            EditorUtility.SetDirty(go);
            Debug.Log("Set sprite on " + goName + ": " + texPath);
        }
        else
        {
            Debug.LogWarning("Sprite not found at: " + texPath);
        }
    }
}
