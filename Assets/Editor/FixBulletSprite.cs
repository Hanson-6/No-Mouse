using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public static class FixBulletSprite
{
    [MenuItem("Tools/Fix Bullet Sprite")]
    public static void Fix()
    {
        // 1. 把 BulletCircle.png 重新设置为 Sprite 类型
        string texPath = "Assets/Textures/BulletCircle.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (importer == null)
        {
            Debug.LogError("[FixBullet] 找不到 BulletCircle.png");
            return;
        }
        importer.textureType          = TextureImporterType.Sprite;
        importer.spriteImportMode     = SpriteImportMode.Single;
        importer.spritePivot          = new Vector2(0.5f, 0.5f);
        importer.spritePixelsPerUnit  = 100f;
        importer.alphaIsTransparency  = true;
        importer.filterMode           = FilterMode.Point;
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

        // 2. 读取 Sprite
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (sprite == null)
        {
            Debug.LogError("[FixBullet] 导入后仍无法加载 Sprite");
            return;
        }

        // 3. 获取 Player 的 SortingLayer 作为参考
        string sortingLayer = "Default";
        int sortingOrder = 10;
        var playerGO = GameObject.Find("Player");
        if (playerGO != null)
        {
            var psr = playerGO.GetComponent<SpriteRenderer>();
            if (psr != null)
            {
                sortingLayer = psr.sortingLayerName;
                sortingOrder = psr.sortingOrder + 1;
            }
        }

        // 4. 赋值到 Bullet Prefab
        string prefabPath = "Assets/Prefabs/Player/Bullet.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[FixBullet] 找不到 Bullet.prefab");
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = scope.prefabContentsRoot;
            var sr = root.GetComponent<SpriteRenderer>();
            sr.sprite           = sprite;
            sr.color            = new Color(1f, 0.15f, 0.15f, 1f);
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder     = sortingOrder;

            // 确保大小合适
            root.transform.localScale = Vector3.one * 0.25f;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixBullet] 完成！Sprite={sprite.name} Layer={sortingLayer} Order={sortingOrder}");
    }
}
