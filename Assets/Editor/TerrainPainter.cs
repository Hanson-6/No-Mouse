using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Tools → Terrain Painter
/// 在 Scene 视图里左键画地形、右键擦除，自动写入 CaveTilemapGrid/Tilemap_Ground_Coll。
/// </summary>
public class TerrainPainterWindow : EditorWindow
{
    // ── 状态 ─────────────────────────────────────────────────────────────
    private bool isPainting = false;
    private bool eraseMode  = false;

    private Tilemap targetTilemap;
    private TileBase selectedTile;

    private List<TileBase> availableTiles = new List<TileBase>();
    private string[]       tileNames;
    private int            tileIndex = 0;

    private Vector2 scrollPos;

    // ── 打开窗口 ──────────────────────────────────────────────────────────
    [MenuItem("Tools/Terrain Painter")]
    public static void Open()
    {
        var w = GetWindow<TerrainPainterWindow>("Terrain Painter");
        w.minSize = new Vector2(260, 340);
        w.LoadTiles();
        w.FindTilemap();
    }

    // ── 加载所有 CaveTerrainTiles ─────────────────────────────────────────
    private void LoadTiles()
    {
        availableTiles.Clear();
        var guids = AssetDatabase.FindAssets("CaveTerrainTiles t:TileBase",
                        new[] { "Assets/CaveAssets/Scenes/tiles" });
        foreach (var g in guids)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (tile != null) availableTiles.Add(tile);
        }
        availableTiles.Sort((a, b) => string.Compare(a.name, b.name));
        tileNames = new string[availableTiles.Count];
        for (int i = 0; i < availableTiles.Count; i++)
            tileNames[i] = availableTiles[i].name;

        if (availableTiles.Count > 0)
            selectedTile = availableTiles[0];
    }

    // ── 在场景里找 Tilemap_Ground_Coll ────────────────────────────────────
    private void FindTilemap()
    {
        targetTilemap = null;
        var grids = FindObjectsOfType<Grid>();
        foreach (var g in grids)
        {
            if (!g.gameObject.name.Contains("CaveTilemapGrid")) continue;
            foreach (Transform child in g.transform)
            {
                if (child.name == "Tilemap_Ground_Coll")
                {
                    targetTilemap = child.GetComponent<Tilemap>();
                    return;
                }
            }
        }
    }

    // ── GUI ──────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        GUILayout.Label("Terrain Painter", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // 目标 Tilemap
        EditorGUI.BeginChangeCheck();
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Target Tilemap", targetTilemap, typeof(Tilemap), true);
        if (EditorGUI.EndChangeCheck() && targetTilemap == null)
            FindTilemap();

        if (targetTilemap == null)
        {
            EditorGUILayout.HelpBox("场景里找不到 Tilemap_Ground_Coll，请先运行 Tools/Setup Cave Tilemap Prefab 再打开此窗口。", MessageType.Warning);
            if (GUILayout.Button("重新搜索")) FindTilemap();
            return;
        }

        EditorGUILayout.Space(6);

        // 开启/关闭 绘制模式
        Color prev = GUI.color;
        GUI.color = isPainting ? new Color(0.5f, 1f, 0.5f) : Color.white;
        if (GUILayout.Button(isPainting ? "■ 停止绘制 (S)" : "▶ 开始绘制 (S)", GUILayout.Height(32)))
            TogglePainting();
        GUI.color = prev;

        EditorGUILayout.Space(4);

        // 擦除模式
        eraseMode = EditorGUILayout.Toggle("擦除模式 (右键也可擦)", eraseMode);

        EditorGUILayout.Space(8);
        GUILayout.Label("选择瓦片：", EditorStyles.boldLabel);

        if (availableTiles.Count == 0)
        {
            EditorGUILayout.HelpBox("Assets/CaveAssets/Scenes/tiles 下没有 CaveTerrainTiles。", MessageType.Info);
            if (GUILayout.Button("重新加载")) LoadTiles();
            return;
        }

        // 瓦片列表
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(160));
        int newIndex = GUILayout.SelectionGrid(tileIndex, tileNames, 1);
        EditorGUILayout.EndScrollView();

        if (newIndex != tileIndex)
        {
            tileIndex    = newIndex;
            selectedTile = availableTiles[tileIndex];
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "开始绘制后：\n左键拖动 = 画地形\n右键拖动 = 擦除地形\nS 键切换开关", MessageType.Info);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("清空当前 Tilemap"))
        {
            if (EditorUtility.DisplayDialog("确认", "清空所有地形瓦片？", "确认", "取消"))
            {
                Undo.RecordObject(targetTilemap, "Clear Tilemap");
                targetTilemap.ClearAllTiles();
            }
        }
    }

    // ── 切换绘制状态 ──────────────────────────────────────────────────────
    private void TogglePainting()
    {
        isPainting = !isPainting;
        if (isPainting)
            SceneView.duringSceneGui += OnSceneGUI;
        else
            SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    // ── Scene 视图交互 ────────────────────────────────────────────────────
    private int _controlID;

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPainting || targetTilemap == null) return;

        Event e = Event.current;

        // Layout 事件：注册控件 ID，阻止选择工具抢占鼠标
        if (e.type == EventType.Layout)
        {
            _controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(_controlID);
            return;
        }

        // S 键切换
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S)
        {
            TogglePainting();
            e.Use();
            Repaint();
            return;
        }

        // 绘制光标提示
        EditorGUIUtility.AddCursorRect(new Rect(0, 0, sceneView.position.width, sceneView.position.height),
            eraseMode ? MouseCursor.ArrowMinus : MouseCursor.ArrowPlus);

        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            GUIUtility.hotControl = _controlID;
            e.Use();
        }

        if (e.type == EventType.MouseUp)
        {
            GUIUtility.hotControl = 0;
            e.Use();
            return;
        }

        bool leftDown  = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0;
        bool rightDown = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 1;

        if (!leftDown && !rightDown) return;

        // 世界坐标 → tilemap 格子
        Vector3 worldPos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        worldPos.z = 0f;

        Vector3Int cell = targetTilemap.WorldToCell(worldPos);

        bool doErase = eraseMode || rightDown;
        TileBase tileToSet = doErase ? null : selectedTile;

        Undo.RegisterCompleteObjectUndo(targetTilemap, doErase ? "Erase Tile" : "Paint Tile");
        targetTilemap.SetTile(cell, tileToSet);
        targetTilemap.RefreshTile(cell);

        EditorUtility.SetDirty(targetTilemap);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetTilemap.gameObject.scene);
        sceneView.Repaint();
        e.Use();
    }

    // ── 关闭时清理 ────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        if (isPainting)
            SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnDisable()
    {
        if (isPainting)
            SceneView.duringSceneGui -= OnSceneGUI;
    }
}
