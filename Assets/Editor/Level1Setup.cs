using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class Level1Setup : EditorWindow
{
    [MenuItem("Tools/Setup Level1 Scene")]
    public static void Setup()
    {
        // --- Player ---
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("Player not found!"); return; }

        // Animator Controller
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PinkMan.controller");
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.runtimeAnimatorController = ctrl;

        // SpriteRenderer - assign idle sprite
        var sr = player.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Pixel Adventure 1/Assets/Main Characters/Pink Man/Idle (32x32).png");
            foreach (var obj in sprites)
                if (obj is Sprite s && s.name == "Idle (32x32)_0") { sr.sprite = s; break; }
            sr.sortingOrder = 1;
        }

        // BoxCollider2D size
        var bc = player.GetComponent<BoxCollider2D>();
        if (bc != null) { bc.size = new Vector2(0.2f, 0.3f); bc.offset = new Vector2(0, 0); }

        // PlayerController references
        var pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            var groundCheck = player.transform.Find("GroundCheck");
            if (groundCheck != null) pc.groundCheck = groundCheck;
            pc.groundLayer = LayerMask.GetMask("Ground");
        }

        // Rigidbody2D
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.gravityScale = 3f;
        }

        // --- Camera ---
        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            var cf = cam.GetComponent<CameraFollow>();
            if (cf != null) cf.target = player.transform;

            var camera = cam.GetComponent<Camera>();
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.backgroundColor = new Color(0.13f, 0.13f, 0.2f);
            }
        }

        // --- GameManager ---
        var gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            var gmComp = gm.GetComponent<GameManager>();
            var respawn = GameObject.Find("RespawnPoint");
            if (gmComp != null && respawn != null) gmComp.respawnPoint = respawn.transform;
        }

        // --- Add Directional Light ---
        var light = new GameObject("Directional Light");
        var dl = light.AddComponent<Light>();
        dl.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(cam);
        EditorUtility.SetDirty(gm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Level1Setup] Scene wiring complete!");
    }
}
