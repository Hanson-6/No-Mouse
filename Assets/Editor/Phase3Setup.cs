using UnityEngine;
using UnityEditor;

public class Phase3Setup
{
    const string SAW_PATH   = "Assets/Pixel Adventure 1/Assets/Traps/Saw/On (38x38).png";
    const string SPIKE_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Spikes/Idle.png";
    const string ENEMY_PATH = "Assets/Pixel Adventure 1/Assets/Main Characters/Mask Dude/Idle (32x32).png";
    const string BROWN_PATH = "Assets/Pixel Adventure 1/Assets/Traps/Platforms/Brown On.png";

    [MenuItem("Tools/Setup Phase3 Objects")]
    public static void Run()
    {
        CreateSpikes("Spikes",  new Vector3(8f,  0.25f, 0));
        CreateSpikes("Spikes2", new Vector3(18f, 0.25f, 0));
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
