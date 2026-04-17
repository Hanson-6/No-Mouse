using UnityEngine;
using UnityEditor;

public class Phase3Setup
{
    const string FIRE_OFF_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Fire/Off.png";
    const string FIRE_ON_PATH  = "Assets/Pixel Adventure 1/Assets/Traps/Fire/On (16x32).png";
    const string FIRE_HIT_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Fire/Hit (16x32).png";
    const string STONE_IDLE_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Spike Head/Idle.png";
    const string SAW_PATH   = "Assets/Pixel Adventure 1/Assets/Traps/Saw/On (38x38).png";
    const string SPIKE_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Spikes/Idle.png";
    const string ENEMY_PATH = "Assets/Pixel Adventure 1/Assets/Main Characters/Mask Dude/Idle (32x32).png";
    const string BROWN_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Platforms/Brown On.png";

    [MenuItem("Tools/Setup Phase3 Objects")]
    public static void Run()
    {
        CreateSpikes("Spikes",  new Vector3(8f,  0.25f, 0));
        CreateSpikes("Spikes2", new Vector3(18f, 0.25f, 0));
        CreateFire("Fire", new Vector3(13f, 0.8f, 0));
        CreateStone("stone", new Vector3(15f, 6f, 0));
        CreateMovingSaw("MovingSaw", new Vector3(11f, 2f, 0), new Vector3(16f, 2f, 0));
        CreateMovingPlatform("MovingPlatform", new Vector3(20f, 3f, 0), new Vector3(25f, 3f, 0));
        CreateEnemy("Enemy", new Vector3(15f, 1f, 0));

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Phase 3 objects created successfully.");
    }

    static Sprite LoadFirst(string path)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in all)
            if (a is Sprite s) return s;
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadNamed(string path, string name)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in all)
            if (a is Sprite s && s.name == name) return s;
        return LoadFirst(path);
    }

    static GameObject GetOrCreate(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) go = new GameObject(goName);
        return go;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    static void CreateSpikes(string goName, Vector3 pos)
    {
        var go = GetOrCreate(goName);
        go.transform.position = pos;

        var sr = EnsureComponent<SpriteRenderer>(go);
        sr.sprite = LoadFirst(SPIKE_PATH);
        sr.sortingOrder = 1;

        var col = EnsureComponent<BoxCollider2D>(go);
        col.isTrigger = true;
        col.size   = new Vector2(0.8f, 0.4f);
        col.offset = new Vector2(0f, -0.1f);

        EnsureComponent<Spike>(go);
        EditorUtility.SetDirty(go);
        Debug.Log("Created: " + goName);
    }

    static void CreateMovingSaw(string goName, Vector3 ptA, Vector3 ptB)
    {
        var go = GetOrCreate(goName);
        go.transform.position = ptA;

        var sr = EnsureComponent<SpriteRenderer>(go);
        sr.sprite = LoadNamed(SAW_PATH, "On (38x38)_0");
        sr.sortingOrder = 1;

        var col = EnsureComponent<CircleCollider2D>(go);
        col.isTrigger = true;
        col.radius = 0.55f;

        var ms = EnsureComponent<MovingSaw>(go);
        ms.pointA = ptA;
        ms.pointB = ptB;
        ms.speed = 3f;
        ms.rotationSpeed = 300f;

        EditorUtility.SetDirty(go);
        Debug.Log("Created: " + goName);
    }

    static void CreateStone(string goName, Vector3 topPos)
    {
        var go = GetOrCreate(goName);
        go.transform.position = topPos;
        go.transform.localScale = new Vector3(2f, 2f, 1f);

        var sr = EnsureComponent<SpriteRenderer>(go);
        sr.sprite = LoadFirst(STONE_IDLE_PATH);
        sr.sortingOrder = 1;

        var col = EnsureComponent<BoxCollider2D>(go);
        col.isTrigger = true;
        col.size = new Vector2(0.38f, 0.38f);
        col.offset = Vector2.zero;

        var stone = EnsureComponent<StoneTrap>(go);
        var so = new SerializedObject(stone);
        so.FindProperty("waitAtTop").floatValue = 2f;
        so.FindProperty("waitOnGround").floatValue = 2f;
        so.FindProperty("riseSpeed").floatValue = 22f;
        so.FindProperty("fallSpeed").floatValue = 22f;
        so.FindProperty("fallbackDropDistance").floatValue = 8f;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            so.FindProperty("groundLayerMask").intValue = 1 << groundLayer;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
        Debug.Log("Created: " + goName);
    }

    static void CreateFire(string goName, Vector3 pos)
    {
        var go = GetOrCreate(goName);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(5f, 5f, 1f);

        var sr = EnsureComponent<SpriteRenderer>(go);
        sr.sprite = LoadFirst(FIRE_OFF_PATH);
        sr.sortingOrder = 1;

        var col = EnsureComponent<BoxCollider2D>(go);
        col.isTrigger = true;
        col.size = new Vector2(0.11f, 0.26f);
        col.offset = Vector2.zero;

        var fire = EnsureComponent<FireTrap>(go);
        var so = new SerializedObject(fire);

        var off = LoadFirst(FIRE_OFF_PATH);
        var on0 = LoadNamed(FIRE_ON_PATH, "On (16x32)_0");
        var on1 = LoadNamed(FIRE_ON_PATH, "On (16x32)_1");
        var on2 = LoadNamed(FIRE_ON_PATH, "On (16x32)_2");

        var hit0 = LoadNamed(FIRE_HIT_PATH, "Hit (16x32)_0");
        var hit1 = LoadNamed(FIRE_HIT_PATH, "Hit (16x32)_1");
        var hit2 = LoadNamed(FIRE_HIT_PATH, "Hit (16x32)_2");
        var hit3 = LoadNamed(FIRE_HIT_PATH, "Hit (16x32)_3");

        so.FindProperty("burstInterval").floatValue = 2f;
        so.FindProperty("warningDuration").floatValue = 0.2f;
        so.FindProperty("hitDuration").floatValue = 0.5f;

        so.FindProperty("offSprite").objectReferenceValue = off;

        var onFrames = so.FindProperty("onFrames");
        onFrames.arraySize = 3;
        onFrames.GetArrayElementAtIndex(0).objectReferenceValue = on0;
        onFrames.GetArrayElementAtIndex(1).objectReferenceValue = on1;
        onFrames.GetArrayElementAtIndex(2).objectReferenceValue = on2;
        so.FindProperty("onFps").floatValue = 12f;

        var hitFrames = so.FindProperty("hitFrames");
        hitFrames.arraySize = 4;
        hitFrames.GetArrayElementAtIndex(0).objectReferenceValue = hit0;
        hitFrames.GetArrayElementAtIndex(1).objectReferenceValue = hit1;
        hitFrames.GetArrayElementAtIndex(2).objectReferenceValue = hit2;
        hitFrames.GetArrayElementAtIndex(3).objectReferenceValue = hit3;
        so.FindProperty("hitFps").floatValue = 12f;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
        Debug.Log("Created: " + goName);
    }

    static void CreateMovingPlatform(string goName, Vector3 ptA, Vector3 ptB)
    {
        var go = GetOrCreate(goName);
        go.transform.position = ptA;
        go.transform.localScale = new Vector3(2f, 0.5f, 1f);

        var sr = EnsureComponent<SpriteRenderer>(go);
        sr.sprite = LoadFirst(BROWN_PATH);
        sr.sortingOrder = 1;

        EnsureComponent<BoxCollider2D>(go);

        var mp = EnsureComponent<MovingPlatform>(go);
        mp.pointA = ptA;
        mp.pointB = ptB;
        mp.speed = 2f;

        EditorUtility.SetDirty(go);
        Debug.Log("Created: " + goName);
    }

    static void CreateEnemy(string goName, Vector3 pos)
    {
        var go = GetOrCreate(goName);
        go.transform.position = pos;

        var sr = EnsureComponent<SpriteRenderer>(go);
        sr.sprite = LoadNamed(ENEMY_PATH, "Idle (32x32)_0");
        sr.sortingOrder = 1;

        var rb = EnsureComponent<Rigidbody2D>(go);
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = EnsureComponent<BoxCollider2D>(go);
        col.size = new Vector2(0.6f, 0.9f);

        EnsureComponent<Enemy>(go);
        EditorUtility.SetDirty(go);
        Debug.Log("Created: " + goName);
    }
}
