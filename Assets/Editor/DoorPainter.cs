using UnityEngine;
using UnityEditor;

// Tools/Door Painter
// ─────────────────────────────────────────────────────────────────────────────
// 1. 在 Project 窗口选中一个 Sprite，它会显示在 "Paint Sprite" 栏
// 2. 左键点格子  → 把当前 Sprite 填进去
//    右键点格子  → 清除该格子
// 3. 点 "Build in Scene" → 在场景里生成预览 GameObject
// 4. 点 "Save as Prefab" → 保存为 Prefab（会弹出保存路径对话框）
// 5. 勾选 "Wrap with SwitchDoor" → 自动加上 SwitchDoor 脚本和 BoxCollider2D
// ─────────────────────────────────────────────────────────────────────────────
public class DoorPainter : EditorWindow
{
    // ── Settings ──────────────────────────────────────────────────────────────
    int   cols          = 2;
    int   rows          = 4;
    float tileWorldSize = 0.32f;   // world units per tile (PPU=8, scale=0.16 → 0.32)
    bool  wrapAsDoor    = true;

    // ── State ─────────────────────────────────────────────────────────────────
    Sprite[,] grid;
    Vector2   scroll;
    float cellPx = 52f;

    // ── Open ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Door Painter")]
    static void Open() => GetWindow<DoorPainter>("Door Painter");

    void OnEnable()
    {
        InitGrid();
        Selection.selectionChanged += Repaint;
    }

    void OnDisable() => Selection.selectionChanged -= Repaint;

    // ── GUI ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        // ── 设置区 ────────────────────────────────────────────────────────────
        GUILayout.Label("Grid Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        cols = EditorGUILayout.IntSlider("Columns", cols, 1, 8);
        rows = EditorGUILayout.IntSlider("Rows",    rows, 1, 12);
        if (EditorGUI.EndChangeCheck()) ResizeGrid();

        tileWorldSize = EditorGUILayout.FloatField("Tile World Size (units)", tileWorldSize);
        cellPx        = EditorGUILayout.Slider("Cell Preview Size", cellPx, 24f, 96f);
        wrapAsDoor    = EditorGUILayout.Toggle("Wrap with SwitchDoor", wrapAsDoor);

        EditorGUILayout.Space(6);

        // ── 当前选中的 Sprite ─────────────────────────────────────────────────
        GUILayout.Label("Paint Sprite  (在 Project 窗口选中 Sprite 后显示)", EditorStyles.boldLabel);
        Sprite activeSpr = GetSelectedSprite();
        if (activeSpr != null)
        {
            Rect previewRect = GUILayoutUtility.GetRect(cellPx, cellPx);
            previewRect.width = cellPx;
            DrawSprite(activeSpr, previewRect);
            EditorGUILayout.LabelField(activeSpr.name, EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("Project 窗口里选中一个 Sprite 就可以开始画了", MessageType.Info);
        }

        EditorGUILayout.Space(6);

        // ── 画格子 ────────────────────────────────────────────────────────────
        GUILayout.Label("左键 = 涂  / 右键 = 清除", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // 从上到下显示（row 大的在上面，更直观）
        for (int r = rows - 1; r >= 0; r--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < cols; c++)
                DrawCell(c, r, activeSpr);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);

        // ── 操作按钮 ──────────────────────────────────────────────────────────
        if (GUILayout.Button("Build in Scene", GUILayout.Height(28)))
            BuildInScene(save: false);

        if (GUILayout.Button("Save as Prefab …", GUILayout.Height(28)))
            BuildInScene(save: true);

        if (GUILayout.Button("Clear All", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("Clear", "清空所有格子？", "确定", "取消"))
                InitGrid();
        }
    }

    // ── 绘制单个格子 ──────────────────────────────────────────────────────────
    void DrawCell(int c, int r, Sprite activeSpr)
    {
        Rect rect = GUILayoutUtility.GetRect(cellPx, cellPx,
            GUILayout.Width(cellPx), GUILayout.Height(cellPx));

        // 背景
        Color bg = grid[c, r] != null
            ? new Color(0.25f, 0.45f, 0.25f)
            : new Color(0.18f, 0.18f, 0.18f);
        EditorGUI.DrawRect(rect, bg);

        // Sprite 预览
        if (grid[c, r] != null)
            DrawSprite(grid[c, r], rect);

        // 边框
        DrawBorder(rect, new Color(0.5f, 0.5f, 0.5f));

        // 鼠标交互
        Event e = Event.current;
        if (rect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
        {
            if (e.button == 0) grid[c, r] = activeSpr;   // 涂
            else               grid[c, r] = null;          // 清除
            e.Use();
            Repaint();
        }
    }

    // ── 生成 / 保存 ───────────────────────────────────────────────────────────
    void BuildInScene(bool save)
    {
        // 清理旧预览
        var old = GameObject.Find("__DoorPainterPreview__");
        if (old != null) DestroyImmediate(old);

        var root = new GameObject("__DoorPainterPreview__");

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (grid[c, r] == null) continue;
                var tile = new GameObject($"Tile_{c}_{r}");
                tile.transform.SetParent(root.transform, worldPositionStays: false);
                tile.transform.localPosition = new Vector3(c * tileWorldSize, r * tileWorldSize, 0f);
                tile.AddComponent<SpriteRenderer>().sprite = grid[c, r];
            }
        }

        // 可选：加 SwitchDoor 脚本 + BoxCollider2D
        if (wrapAsDoor)
        {
            float w = cols * tileWorldSize;
            float h = rows * tileWorldSize;
            var col    = root.AddComponent<BoxCollider2D>();
            col.size   = new Vector2(w, h);
            col.offset = new Vector2(w / 2f - tileWorldSize / 2f,
                                     h / 2f - tileWorldSize / 2f);
            root.AddComponent<SwitchDoor>();
        }

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();

        if (!save) return;

        // 保存 Prefab
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Door Prefab", "CustomDoor", "prefab", "选择保存位置");
        if (string.IsNullOrEmpty(path)) { DestroyImmediate(root); return; }

        root.name = System.IO.Path.GetFileNameWithoutExtension(path);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[DoorPainter] Saved → {path}");
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────────
    static Sprite GetSelectedSprite()
    {
        var obj = Selection.activeObject;
        if (obj is Sprite s) return s;
        if (obj is Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }

    static void DrawSprite(Sprite sprite, Rect position)
    {
        if (sprite == null || sprite.texture == null) return;
        var t  = sprite.texture;
        var tr = sprite.textureRect;
        var uv = new Rect(tr.x / t.width, tr.y / t.height,
                          tr.width / t.width, tr.height / t.height);
        GUI.DrawTextureWithTexCoords(position, t, uv);
    }

    static void DrawBorder(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x,        r.y,        r.width, 1), c);
        EditorGUI.DrawRect(new Rect(r.x,        r.yMax - 1, r.width, 1), c);
        EditorGUI.DrawRect(new Rect(r.x,        r.y,        1, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y,        1, r.height), c);
    }

    void InitGrid()   => grid = new Sprite[cols, rows];

    void ResizeGrid()
    {
        var prev = grid;
        int pc = prev?.GetLength(0) ?? 0;
        int pr = prev?.GetLength(1) ?? 0;
        grid = new Sprite[cols, rows];
        for (int c = 0; c < Mathf.Min(cols, pc); c++)
            for (int r = 0; r < Mathf.Min(rows, pr); r++)
                grid[c, r] = prev[c, r];
    }
}
