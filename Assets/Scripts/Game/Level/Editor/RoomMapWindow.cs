using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Game.Level.Editor
{
    public sealed class RoomMapWindow : EditorWindow
    {
        private enum Mode { Select, Create, Paint, Erase }
        private Mode mode;
        private MapRoom selected;
        private MapRoom[] roomCache;
        private Scene cachedScene;
        private string roomName = "房间";
        private int layer = 1;
        [SerializeField] private TileBase tile;
        private DefaultAsset paletteFolder;
        private TileBase[] palette = System.Array.Empty<TileBase>();
        private Vector2 paletteScroll;
        private Vector2 windowScroll;
        private string PaletteKey => "Game.Level.RoomPalette." + Application.dataPath;
        private int control;
        private Vector2 anchor;
        private Vector3Int lastCell;
        private Tilemap stroke;
        private Tilemap collisionStroke;
        private TileBase solidTile;
        [SerializeField] private bool paintSolid = true;
        private bool hasLastCell;
        private string message;
        private Scene gestureScene;
        private SceneView gestureView;
        internal const string TileFolder = "Assets/Game/Art/Tiles/";

        [MenuItem("Tools/GuangHeCup/地图编辑器")]
        private static void Open() => GetWindow<RoomMapWindow>("地图编辑器");

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawScene;
            Undo.undoRedoPerformed += Refresh;
            if (!tile) tile = DefaultTile();
            string path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(PaletteKey, ""));
            paletteFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(
                AssetDatabase.IsValidFolder(path) ? path : TileFolder.TrimEnd('/'));
            ReloadPalette();
            EditorApplication.projectChanged += ReloadPalette;
            EditorApplication.hierarchyChanged += InvalidateRooms;
        }

        private void InvalidateRooms()
        {
            roomCache = null;
            Refresh();
        }

        private MapRoom[] Rooms(Scene scene)
        {
            if (roomCache == null || cachedScene != scene || roomCache.Any(room => !room))
            {
                cachedScene = scene;
                roomCache = MapLayout.Rooms(scene);
            }
            return roomCache;
        }

        private void ReloadPalette()
        {
            string path = AssetDatabase.GetAssetPath(paletteFolder);
            palette = AssetDatabase.IsValidFolder(path)
                ? AssetDatabase.FindAssets("t:TileBase", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<TileBase>()
                    .OrderBy(asset => asset.name).ToArray()
                : System.Array.Empty<TileBase>();
            Repaint();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ReloadPalette;
            EditorApplication.hierarchyChanged -= InvalidateRooms;
            EndGesture();
            SceneView.duringSceneGui -= DrawScene;
            Undo.undoRedoPerformed -= Refresh;
        }

        private void Refresh() { Repaint(); SceneView.RepaintAll(); }
        private TileBase DefaultTile() => AssetDatabase.LoadAssetAtPath<TileBase>(
            TileFolder + (layer == 2 ? "Decoration" : "Ground") + ".asset");

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorGUILayout.HelpBox("请在普通场景的编辑模式使用。", MessageType.Info);
                return;
            }
            var scene = SceneManager.GetActiveScene();
            var rooms = Rooms(scene);
            if (selected && selected.gameObject.scene != scene) selected = null;
            using var scroll = new EditorGUILayout.ScrollViewScope(windowScroll);
            windowScroll = scroll.scrollPosition;
            var next = (Mode)GUILayout.Toolbar((int)mode, new[] { "选择", "建房", "绘制", "擦除" });
            if (next != mode) { EndGesture(); mode = next; message = null; Refresh(); }
            if (mode == Mode.Create)
            {
                roomName = EditorGUILayout.TextField("新房间名称", roomName);
                EditorGUILayout.HelpBox("在 Scene 拖出矩形，松开创建。每格 0.5 世界单位。", MessageType.Info);
            }
            if (rooms.Length > 0) Overview(rooms);
            if (selected)
            {
                EditorGUILayout.LabelField(selected.name, $"{selected.Dimensions.x} × {selected.Dimensions.y} 格");
                if (GUILayout.Button("定位房间")) FocusRoom(selected);
            }
            if (mode == Mode.Paint || mode == Mode.Erase)
            {
                int nextLayer = EditorGUILayout.Popup("图层", layer, MapLayout.Layers);
                if (nextLayer != layer) layer = nextLayer;
                if (mode == Mode.Paint)
                {
                    if (layer == 1)
                        paintSolid = EditorGUILayout.ToggleLeft("同时绘制实体碰撞", paintSolid);
                    DrawPalette();
                }
                if (!selected) EditorGUILayout.HelpBox("点击总览中的房间后，在 Scene 内拖动笔刷。", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        private void DrawPalette()
        {
            var folder = (DefaultAsset)EditorGUILayout.ObjectField("调色板目录", paletteFolder, typeof(DefaultAsset), false);
            if (folder != paletteFolder && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
            {
                paletteFolder = folder;
                EditorPrefs.SetString(PaletteKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(folder)));
                ReloadPalette();
            }
            tile = (TileBase)EditorGUILayout.ObjectField("当前笔刷", tile, typeof(TileBase), false);
            EditorGUILayout.LabelField("Shift + 单击吸取当前层瓦片", EditorStyles.miniLabel);
            if (palette.Length == 0)
            {
                EditorGUILayout.HelpBox("选择包含 Tile 或 Rule Tile 的目录，可包含子目录。", MessageType.Info);
                return;
            }
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 42) / 74));
            using var scroll = new EditorGUILayout.ScrollViewScope(paletteScroll, GUILayout.MaxHeight(186));
            paletteScroll = scroll.scrollPosition;
            for (int row = 0; row < palette.Length; row += columns)
            {
                using var line = new EditorGUILayout.HorizontalScope();
                for (int j = row; j < Mathf.Min(row + columns, palette.Length); j++)
                {
                    var entry = palette[j];
                    if (!entry) continue;
                    var rect = GUILayoutUtility.GetRect(68, 56, GUILayout.Width(68));
                    if (GUI.Button(rect, new GUIContent("", entry.name))) tile = entry;
                    var preview = AssetPreview.GetAssetPreview(entry);
                    if (preview) GUI.DrawTexture(new Rect(rect.center.x - 15, rect.y + 3, 30, 30), preview, ScaleMode.ScaleToFit);
                    else if (AssetPreview.IsLoadingAssetPreview(entry.GetInstanceID())) Repaint();
                    GUI.Label(new Rect(rect.x + 3, rect.yMax - 22, rect.width - 6, 18), entry.name, EditorStyles.centeredGreyMiniLabel);
                    if (tile == entry) EditorGUI.DrawRect(new Rect(rect.x + 2, rect.yMax - 3, rect.width - 4, 2), Color.cyan);
                }
            }
        }

        private static void FocusRoom(MapRoom room)
        {
            SceneView.lastActiveSceneView?.Frame(new Bounds(room.Bounds.center, room.Bounds.size), false);
        }

        private void Overview(MapRoom[] rooms)
        {
            var area = GUILayoutUtility.GetRect(100, 145, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(.13f, .15f, .18f));
            var min = new Vector2(rooms.Min(r => r.Bounds.xMin), rooms.Min(r => r.Bounds.yMin));
            var max = new Vector2(rooms.Max(r => r.Bounds.xMax), rooms.Max(r => r.Bounds.yMax));
            float scale = Mathf.Min((area.width - 12) / (max.x - min.x), (area.height - 12) / (max.y - min.y));
            var origin = area.center - (max - min) * scale / 2;
            foreach (var room in rooms)
            {
                var b = room.Bounds;
                var rect = new Rect(origin.x + (b.xMin - min.x) * scale, origin.y + (max.y - b.yMax) * scale,
                    b.width * scale, b.height * scale);
                EditorGUI.DrawRect(rect, room == selected ? new Color(.06f,.43f,.46f) : Color.gray);
                if (rect.width > 40) GUI.Label(rect, room.name, EditorStyles.whiteMiniLabel);
                if (GUI.Button(rect, new GUIContent("", room.name), GUIStyle.none))
                {
                    selected = room;
                    Selection.activeGameObject = room.gameObject;
                    FocusRoom(room);
                    Refresh();
                }
            }
        }

        private void OnSelectionChange()
        {
            var go = Selection.activeGameObject;
            if (go && go.GetComponentInParent<MapRoom>() is MapRoom room) selected = room;
            Refresh();
        }

        private void EndGesture()
        {
            if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            control = 0;
            if (collisionStroke)
                collisionStroke.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            collisionStroke = null;
            stroke = null;
            gestureView = null;
            hasLastCell = false;
        }

        private void DrawScene(SceneView view)
        {
            var scene = SceneManager.GetActiveScene();
            if (control != 0 && gestureView != view) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null ||
                (control != 0 && gestureScene != scene)) { EndGesture(); return; }
            var e = Event.current;
            if (control != 0 && (e.type == EventType.Ignore || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)))
            { EndGesture(); view.Repaint(); return; }
            if (e.type == EventType.Repaint)
            {
                foreach (var room in Rooms(scene))
                    Outline(room.Bounds, room == selected ? new Color(.24f,.88f,.82f) : new Color(.55f,.63f,.7f), .025f);
            }
            if (mode == Mode.Select || (e.alt && control == 0)) return;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector2 point = ray.GetPoint(distance);
            if (e.type == EventType.MouseMove) view.Repaint();
            if ((mode == Mode.Paint || mode == Mode.Erase) && selected && selected.gameObject.scene == scene &&
                selected.Bounds.Contains(point))
            {
                var grid = selected.GetComponent<Grid>();
                if (grid)
                {
                    var cell = grid.WorldToCell(point);
                    if (e.type == EventType.Repaint)
                        Outline(new Rect(grid.CellToWorld(cell), Vector2.one * MapRoom.CellSize),
                            mode == Mode.Paint ? Color.cyan : new Color(1,.38f,.32f), .26f);
                    if (mode == Mode.Paint && e.shift && e.type == EventType.MouseDown && e.button == 0 &&
                        HandleUtility.nearestControl == id)
                    {
                        var map = selected.transform.Find(MapLayout.Layers[layer])?.GetComponent<Tilemap>();
                        var sample = map ? map.GetTile(cell) : null;
                        if (sample) tile = sample;
                        e.Use();
                        Refresh();
                        return;
                    }
                }
            }
            Vector2 corner = (Vector2)Vector2Int.RoundToInt(point / MapRoom.CellSize) * MapRoom.CellSize;
            if (e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == id)
            {
                if (mode != Mode.Create)
                {
                    if (!selected || selected.gameObject.scene != scene || !selected.Bounds.Contains(point)) return;
                    stroke = selected.transform.Find(MapLayout.Layers[layer])?.GetComponent<Tilemap>();
                    if (!stroke || (mode == Mode.Paint && !tile)) return;
                    bool addSolid = layer == 1 && mode == Mode.Paint && paintSolid;
                    if (addSolid && !solidTile)
                    {
                        solidTile = AssetDatabase.LoadAssetAtPath<TileBase>(MapLayout.SolidTilePath);
                        if (!solidTile)
                        {
                            message = "实体瓦片资源缺失：" + MapLayout.SolidTilePath;
                            stroke = null;
                            Repaint();
                            return;
                        }
                    }
                    Undo.IncrementCurrentGroup();
                    if (addSolid || (layer == 1 && mode == Mode.Erase))
                        collisionStroke = MapLayout.CollisionMap(selected, addSolid);
                    if (collisionStroke) Undo.RegisterCompleteObjectUndo(collisionStroke, "编辑实体碰撞");
                    Undo.RegisterCompleteObjectUndo(stroke, "编辑房间瓦片");
                }
                anchor = corner;
                control = id;
                gestureView = view;
                gestureScene = scene;
                GUIUtility.hotControl = id;
                message = null;
            }
            if (control == 0) return;
            var bounds = new Rect(Vector2.Min(anchor, corner), Vector2.Max(anchor, corner) - Vector2.Min(anchor, corner));
            if (mode == Mode.Create && e.type == EventType.Repaint)
            {
                var conflict = MapLayout.Conflict(bounds, Rooms(scene));
                var color = conflict ? Color.red : Color.cyan;
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { bounds.min, new Vector2(bounds.xMax,bounds.yMin),
                    bounds.max, new Vector2(bounds.xMin,bounds.yMax) }, new Color(color.r,color.g,color.b,.18f), color);
                var size = Vector2Int.RoundToInt(bounds.size / MapRoom.CellSize);
                Handles.Label(bounds.max, conflict ? $"与 {conflict.name} 重叠" : $"{size.x} × {size.y} 格");
            }
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag && e.type != EventType.MouseUp) return;
            if (e.button != 0) return;
            if (stroke) Paint(point);
            if (e.type == EventType.MouseUp)
            {
                if (mode == Mode.Create)
                {
                    var room = MapLayout.Create(scene, bounds, roomName);
                    if (room) { roomCache = null; selected = room; Selection.activeGameObject = room.gameObject; }
                    else message = "需要至少一格宽高，且不能与已有房间重叠。";
                }
                EndGesture();
            }
            e.Use();
            Refresh();
        }

        private static void Outline(Rect bounds, Color color, float alpha)
        {
            Handles.DrawSolidRectangleWithOutline(new Vector3[] { bounds.min,
                new Vector2(bounds.xMax, bounds.yMin), bounds.max, new Vector2(bounds.xMin, bounds.yMax) },
                new Color(color.r, color.g, color.b, alpha), color);
        }

        private void Paint(Vector2 point)
        {
            if (!selected.Bounds.Contains(point)) { hasLastCell = false; return; }
            var cell = stroke.WorldToCell(point);
            var start = hasLastCell ? lastCell : cell;
            int steps = Mathf.Max(Mathf.Abs(cell.x - start.x), Mathf.Abs(cell.y - start.y));
            for (int i = 0; i <= steps; i++)
            {
                var pos = Vector3Int.RoundToInt(Vector3.Lerp(start, cell, steps == 0 ? 0 : (float)i / steps));
                stroke.SetTile(pos, mode == Mode.Erase ? null : tile);
                if (collisionStroke)
                    collisionStroke.SetTile(pos, mode == Mode.Erase ? null : solidTile);
            }
            lastCell = cell;
            hasLastCell = true;
            EditorSceneManager.MarkSceneDirty(stroke.gameObject.scene);
        }
    }
}
