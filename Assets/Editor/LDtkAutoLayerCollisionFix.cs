using LDtkUnity;
using LDtkUnity.Editor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// LDtkUnity post-processor that runs after every Levels.ldtk import.
/// It disables the TilemapCollider2D on any "AutoLayer" tilemap child so
/// that only IntGrid_Ground (layer 8) provides physics collision.
/// Without this, AutoLayer (layer 0) would double-collide with the player.
/// </summary>
public class LDtkAutoLayerCollisionFix : LDtkPostprocessor
{
    protected override void OnPostprocessLevel(GameObject root, LdtkJson projectJson)
    {
        // root is the Level_X GameObject (e.g. Level_0)
        // Walk the hierarchy looking for "AutoLayer" tilemaps
        var colliders = root.GetComponentsInChildren<TilemapCollider2D>(true);
        foreach (var col in colliders)
        {
            if (col.gameObject.name == "AutoLayer")
            {
                col.enabled = false;

                // Also disable the CompositeCollider2D and Rigidbody2D that were
                // added solely to support the (now disabled) TilemapCollider2D.
                var composite = col.gameObject.GetComponent<CompositeCollider2D>();
                if (composite != null) composite.enabled = false;

                var rb = col.gameObject.GetComponent<Rigidbody2D>();
                if (rb != null) rb.simulated = false;

                Debug.Log($"[LDtkAutoLayerCollisionFix] Disabled physics on AutoLayer in '{root.name}'.");
            }
        }
    }
}
