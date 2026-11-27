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
        private static readonly string[] DrawingLayers = MapLayout.Layers.Append("环境摆件").ToArray();
        private Mode mode;
        private MapRoom selected;
        private MapRoom[] roomCache;
        private Scene cachedScene;
        private string roomName = "房间";
        [SerializeField] private int layer = 1;
        [SerializeField] private TileBase tile;
        [SerializeField] private Matrix4x4 brushOrientation = Matrix4x4.identity;
        [SerializeField] private float paletteCellSize = 60;
        [SerializeField] private bool hasSampledTransform;
        [SerializeField] private Matrix4x4 sampledTransform = Matrix4x4.identity;
        private Tilemap previewMap;
        private Vector3Int previewCell;
        private SceneView previewView;
        private TileBase previewTile;
        private TileBase[] palette = System.Array.Empty<TileBase>();
        private (Texture2D Image, string Path)[] backgroundImages;
        private (Texture2D Image, string Path)[] propImages;
        private Sprite propBrush;
        private string propPath;
        private bool placingProp;
        private bool alignPropTop;
        private float propScale = 1;
        private Vector2 paletteScroll;
        private Vector2 windowScroll;
        private bool IsBackgroundLayer => layer == 0;
        private bool IsPropsLayer => layer == MapLayout.Layers.Length;
        private bool IsTileLayer => !IsBackgroundLayer && !IsPropsLayer;
        private string PalettePath => TileFolder + MapLayout.Layers[layer];
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
        private const string EnvironmentFolder = "Assets/Game/Art/Environment";
        private const string PropsContainer = "Environment Props";

        [MenuItem("Tools/GuangHeCup/地图编辑器")]
        private static void Open() => GetWindow<RoomMapWindow>("地图编辑器");

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawScene;
            Undo.undoRedoPerformed += Refresh;
            layer = Mathf.Clamp(layer, 0, DrawingLayers.Length - 1);
            ReloadAssets();
            if (!palette.Contains(tile)) SelectBrush(DefaultTile());
            EditorApplication.projectChanged += ReloadAssets;
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

        private void ReloadAssets()
        {
            backgroundImages = LoadEnvironmentImages("Background");
            propImages = LoadEnvironmentImages("Decorations", "Midground");
            ReloadPalette();
        }

        private static (Texture2D Image, string Path)[] LoadEnvironmentImages(params string[] categories)
        {
            var folders = AssetDatabase.GetSubFolders(EnvironmentFolder)
                .SelectMany(theme => categories.Select(category => theme + "/" + category))
                .Where(AssetDatabase.IsValidFolder)
                .ToArray();
            if (folders.Length == 0) return System.Array.Empty<(Texture2D Image, string Path)>();
            return AssetDatabase.FindAssets("t:Texture2D", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(path => (Image: AssetDatabase.LoadAssetAtPath<Texture2D>(path), Path: path))
                .Where(entry => entry.Image)
                .ToArray();
        }

        private void ReloadPalette()
        {
            palette = IsTileLayer && AssetDatabase.IsValidFolder(PalettePath)
                ? AssetDatabase.FindAssets("t:TileBase", new[] { PalettePath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<TileBase>()
                    .OrderBy(asset => asset.name).ToArray()
                : System.Array.Empty<TileBase>();
            if (!tile && palette.Length > 0) SelectBrush(DefaultTile());
            else Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ReloadAssets;
            EditorApplication.hierarchyChanged -= InvalidateRooms;
            EndGesture();
            SceneView.duringSceneGui -= DrawScene;
            Undo.undoRedoPerformed -= Refresh;
        }

        private void Refresh() { Repaint(); SceneView.RepaintAll(); }
        private TileBase DefaultTile()
        {
            if (!IsTileLayer) return null;
            var builtIn = AssetDatabase.LoadAssetAtPath<TileBase>(PalettePath + "/" + MapLayout.Layers[layer] + ".asset");
            return builtIn ? builtIn : palette.FirstOrDefault();
        }

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
            if (next != mode) { EndGesture(); placingProp = false; mode = next; message = null; Refresh(); }
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
                int nextLayer = EditorGUILayout.Popup("图层", layer, DrawingLayers);
                if (nextLayer != layer)
                {
                    EndGesture();
                    placingProp = false;
                    message = null;
                    layer = nextLayer;
                    paletteScroll = Vector2.zero;
                    ReloadPalette();
                    SelectBrush(DefaultTile());
                }
                if (IsBackgroundLayer)
                    DrawRoomBackground();
                else if (IsPropsLayer)
                    DrawEnvironmentProps();
                else if (mode == Mode.Paint)
                {
                    if (layer == 1)
                        paintSolid = EditorGUILayout.ToggleLeft("同时绘制实体碰撞", paintSolid);
                    DrawPalette();
                }
                if (!selected && IsTileLayer)
                    EditorGUILayout.HelpBox("点击总览中的房间后，在 Scene 内拖动笔刷。", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            HandleBrushShortcut(Event.current);
        }

        private void DrawRoomBackground()
        {
            if (!selected)
            {
                EditorGUILayout.HelpBox("从房间总览选择要设置背景的房间。", MessageType.Info);
                return;
            }

            var background = selected.GetComponent<MapRoomBackground>();
            using var data = background ? new SerializedObject(background) : null;
            var image = data?.FindProperty("image").objectReferenceValue as Texture2D;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("背景图片", EditorStyles.boldLabel);
                if (image && GUILayout.Button("清除背景", GUILayout.ExpandWidth(false)))
                    SetBackgroundImage(selected, null);
            }
            var backgroundPath = DrawImageGallery(backgroundImages, AssetDatabase.GetAssetPath(image),
                "请将图片放入 Environment 下对应主题的 Background 目录。");
            if (backgroundPath != null && backgroundPath != AssetDatabase.GetAssetPath(image))
                SetBackgroundImage(selected, AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundPath));
            if (!image) return;

            var fit = data.FindProperty("fit");
            fit.enumValueIndex = EditorGUILayout.Popup("适配方式", fit.enumValueIndex,
                new[] { "填满房间（裁切）", "完整显示（留边）" });
            var alignment = data.FindProperty("alignment");
            var anchor = alignment.vector2Value;
            EditorGUI.BeginChangeCheck();
            anchor.x = EditorGUILayout.Slider("横向取景（左 → 右）", anchor.x, 0, 1);
            anchor.y = EditorGUILayout.Slider("纵向取景（下 → 上）", anchor.y, 0, 1);
            if (EditorGUI.EndChangeCheck()) alignment.vector2Value = anchor;
            if (data.ApplyModifiedProperties())
            {
                EditorApplication.QueuePlayerLoopUpdate();
                Refresh();
            }
        }

        private void DrawEnvironmentProps()
        {
            if (!selected)
            {
                EditorGUILayout.HelpBox("先从房间总览选择要摆放物件的房间。", MessageType.Info);
                return;
            }

            var activeObject = Selection.activeGameObject;
            var container = selected.transform.Find(PropsContainer);
            if (container && activeObject && activeObject.transform.parent == container &&
                GUILayout.Button("删除选中摆件"))
            {
                Undo.DestroyObjectImmediate(activeObject);
                Refresh();
            }
            if (mode == Mode.Erase)
            {
                EditorGUILayout.LabelField("选中房间内的摆件后，点击「删除选中摆件」。", EditorStyles.miniLabel);
                return;
            }

            var chosen = DrawImageGallery(propImages, propPath,
                "请将摆件 Sprite 放入对应主题的 Decorations 或 Midground 目录。");
            if (chosen != null)
            {
                propPath = chosen;
                propBrush = AssetDatabase.LoadAssetAtPath<Sprite>(chosen);
                placingProp = propBrush;
                message = propBrush ? null : "请先将这张摆件图片导入为单张 Sprite。";
                Refresh();
            }
            if (!propBrush) return;

            EditorGUI.BeginChangeCheck();
            alignPropTop = EditorGUILayout.Popup("对齐点", alignPropTop ? 1 : 0,
                new[] { "底部中心", "顶部中心" }) == 1;
            propScale = EditorGUILayout.Slider("摆放缩放", propScale, .1f, 5);
            if (EditorGUI.EndChangeCheck()) Refresh();
            if (placingProp)
            {
                EditorGUILayout.LabelField("在房间内单击摆放，Esc 取消。", EditorStyles.miniLabel);
                if (GUILayout.Button("取消摆放")) StopPlacingProp();
            }
            else if (GUILayout.Button("再次摆放"))
            {
                placingProp = true;
                Refresh();
            }
        }

        private string DrawImageGallery((Texture2D Image, string Path)[] images, string currentPath, string emptyMessage)
        {
            if (images.Length == 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                return null;
            }

            string chosen = null;
            float available = Mathf.Max(1, position.width - 32);
            int columns = Mathf.Max(1, Mathf.FloorToInt((available + 4) / 140));
            float cardWidth = Mathf.Min(200, (available - (columns - 1) * 4) / columns);
            for (int start = 0; start < images.Length; start += columns)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                for (int index = start; index < Mathf.Min(start + columns, images.Length); index++)
                {
                    var entry = images[index];
                    var card = GUILayoutUtility.GetRect(cardWidth, 92);
                    if (!entry.Image) continue;
                    bool active = currentPath == entry.Path;
                    EditorGUI.DrawRect(card, active ? new Color(.22f, .62f, .65f) : new Color(.22f, .24f, .26f));
                    var thumbnail = new Rect(card.x + 3, card.y + 3, Mathf.Max(0, card.width - 6), 68);
                    EditorGUI.DrawRect(thumbnail, new Color(.11f, .13f, .15f));
                    GUI.DrawTexture(thumbnail, entry.Image, ScaleMode.ScaleToFit);
                    var caption = new Rect(card.x + 3, card.yMax - 20, Mathf.Max(0, card.width - 6), 18);
                    GUI.Label(caption, entry.Image.name, EditorStyles.centeredGreyMiniLabel);
                    var tooltip = entry.Path.Substring(EnvironmentFolder.Length + 1);
                    if (GUI.Button(card, new GUIContent(string.Empty, tooltip), GUIStyle.none))
                        chosen = entry.Path;
                }
                GUILayout.FlexibleSpace();
            }
            return chosen;
        }

        private void SetBackgroundImage(MapRoom room, Texture2D image)
        {
            if (!room) return;
            var background = room.GetComponent<MapRoomBackground>();
            if (!background)
            {
                if (!image) return;
                background = Undo.AddComponent<MapRoomBackground>(room.gameObject);
            }
            using var data = new SerializedObject(background);
            data.FindProperty("image").objectReferenceValue = image;
            data.ApplyModifiedProperties();
            EditorApplication.QueuePlayerLoopUpdate();
            Refresh();
        }

        private void DrawPalette()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Tiles/" + MapLayout.Layers[layer], PalettePath), EditorStyles.miniLabel);
                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(PalettePath);
                using (new EditorGUI.DisabledScope(!folder))
                    if (GUILayout.Button("定位目录", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        EditorUtility.FocusProjectWindow();
                        EditorGUIUtility.PingObject(folder);
                    }
            }
            EditorGUILayout.LabelField("当前笔刷", tile ? tile.name : "无");
            using (new EditorGUI.DisabledScope(!tile || control != 0))
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("↶", "逆时针旋转 90° ["), EditorStyles.toolbarButton))
                    RotateBrush(90);
                if (GUILayout.Button(new GUIContent("↷", "顺时针旋转 90° ]"), EditorStyles.toolbarButton))
                    RotateBrush(-90);
                if (GUILayout.Button(new GUIContent("水平翻转", "快捷键 X"), EditorStyles.toolbarButton))
                    TransformBrush(Matrix4x4.Scale(new Vector3(-1, 1, 1)));
                if (GUILayout.Button(new GUIContent("垂直翻转", "快捷键 Y"), EditorStyles.toolbarButton))
                    TransformBrush(Matrix4x4.Scale(new Vector3(1, -1, 1)));
                if (GUILayout.Button("重置", EditorStyles.toolbarButton)) SelectBrush(tile);
            }
            EditorGUILayout.LabelField("Shift + 单击吸取瓦片及朝向", EditorStyles.miniLabel);
            if (palette.Length == 0)
            {
                EditorGUILayout.HelpBox("此图层暂无瓦片。将 Tile 或 Rule Tile 放入上方目录或其子目录。", MessageType.Info);
                return;
            }
            paletteCellSize = EditorGUILayout.Slider("缩放", paletteCellSize, 36, 100);
            var viewport = GUILayoutUtility.GetRect(1, 210, GUILayout.ExpandWidth(true));
            float contentWidth = Mathf.Max(1, viewport.width - 16);
            float cellSize = Mathf.Min(paletteCellSize, contentWidth);
            int columns = Mathf.Max(1, Mathf.FloorToInt(contentWidth / cellSize));
            int rows = Mathf.CeilToInt((float)palette.Length / columns);
            var content = new Rect(0, 0, contentWidth, Mathf.Max(viewport.height, rows * cellSize));
            using var scroll = new GUI.ScrollViewScope(viewport, paletteScroll, content, false, true);
            paletteScroll = scroll.scrollPosition;
            EditorGUI.DrawRect(content, new Color(.16f, .18f, .20f));
            for (int index = 0; index < palette.Length; index++)
            {
                var entry = palette[index];
                if (!entry) continue;
                var rect = new Rect(index % columns * cellSize, index / columns * cellSize, cellSize - 1, cellSize - 1);
                if (rect.yMax < paletteScroll.y || rect.y > paletteScroll.y + viewport.height) continue;
                bool active = tile == entry;
                EditorGUI.DrawRect(rect, active ? new Color(.22f, .62f, .65f) : new Color(.11f, .13f, .15f));
                var inset = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
                EditorGUI.DrawRect(inset, active ? new Color(.18f, .29f, .32f) : new Color(.22f, .24f, .26f));
                if (GUI.Button(rect, new GUIContent(string.Empty, entry.name), GUIStyle.none) && entry != tile)
                    SelectBrush(entry);
                var thumbnail = AssetPreview.GetAssetPreview(entry);
                if (thumbnail)
                    DrawThumbnail(inset, thumbnail, active ? ThumbnailOrientation() : Matrix4x4.identity);
                else if (AssetPreview.IsLoadingAssetPreview(entry.GetInstanceID())) Repaint();
            }
        }

        private static void DrawThumbnail(Rect rect, Texture texture, Matrix4x4 orientation)
        {
            if (Event.current.type != EventType.Repaint) return;

            var size = new Vector2(texture.width, texture.height);
            size *= Mathf.Min((rect.width - 8) / size.x, (rect.height - 8) / size.y);
            var imageRect = new Rect(rect.center - size * .5f, size);
            var xAxis = new Vector2(orientation.m00, orientation.m10);
            bool mirrored = orientation.m00 * orientation.m11 - orientation.m01 * orientation.m10 < 0;
            float angle = -Mathf.Atan2(xAxis.y, xAxis.x) * Mathf.Rad2Deg;

            // Keep the GUI quad front-facing. Mirroring its UVs gives the same
            // image as reflecting the quad, without reversing its winding.
            var original = GUI.matrix;
            try
            {
                GUIUtility.RotateAroundPivot(angle, rect.center);
                GUI.DrawTextureWithTexCoords(imageRect, texture,
                    mirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1));
            }
            finally
            {
                GUI.matrix = original;
            }
        }

        private void SelectBrush(TileBase brush, Matrix4x4? transform = null)
        {
            tile = brush;
            brushOrientation = Matrix4x4.identity;
            hasSampledTransform = transform.HasValue;
            sampledTransform = transform.GetValueOrDefault(Matrix4x4.identity);
            ClearPreview();
            Refresh();
        }

        private void RotateBrush(float degrees) =>
            TransformBrush(Matrix4x4.Rotate(Quaternion.Euler(0, 0, degrees)));

        private void TransformBrush(Matrix4x4 transform)
        {
            brushOrientation = transform * brushOrientation;
            ClearPreview();
            Refresh();
        }

        private Matrix4x4 ThumbnailOrientation()
        {
            if (!hasSampledTransform) return brushOrientation;
            var orientation = brushOrientation * sampledTransform;
            if (tile is Tile plainTile) orientation *= plainTile.transform.inverse;
            var display = Matrix4x4.identity;
            for (int axis = 0; axis < 2; axis++)
            {
                var direction = new Vector2(orientation[0, axis], orientation[1, axis]).normalized;
                display[0, axis] = direction.x;
                display[1, axis] = direction.y;
            }
            return display;
        }

        private Matrix4x4 BrushTransform(Matrix4x4 tileTransform) =>
            brushOrientation * (hasSampledTransform ? sampledTransform : tileTransform);

        private void HandleBrushShortcut(Event e)
        {
            if (mode != Mode.Paint || !IsTileLayer || !tile || control != 0 || e.type != EventType.KeyDown ||
                EditorGUIUtility.editingTextField || e.alt || e.control || e.command || e.shift) return;
            switch (e.keyCode)
            {
                case KeyCode.LeftBracket: RotateBrush(90); break;
                case KeyCode.RightBracket: RotateBrush(-90); break;
                case KeyCode.X: TransformBrush(Matrix4x4.Scale(new Vector3(-1, 1, 1))); break;
                case KeyCode.Y: TransformBrush(Matrix4x4.Scale(new Vector3(1, -1, 1))); break;
                default: return;
            }
            e.Use();
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
            if (go && go.GetComponentInParent<MapRoom>() is MapRoom room && room != selected)
            {
                EndGesture();
                placingProp = false;
                selected = room;
            }
            Refresh();
        }

        private void EndGesture()
        {
            ClearPreview();
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
            if (previewView == view && (!previewMap || e.type == EventType.MouseLeaveWindow ||
                EditorWindow.mouseOverWindow != view || previewMap.gameObject.scene != scene))
                ClearPreview();
            if (EditorWindow.mouseOverWindow == view) HandleBrushShortcut(e);
            if (control != 0 && (e.type == EventType.Ignore || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)))
            { EndGesture(); view.Repaint(); return; }
            if (e.type == EventType.Repaint)
            {
                foreach (var room in Rooms(scene))
                    Outline(room.Bounds, room == selected ? new Color(.24f,.88f,.82f) : new Color(.55f,.63f,.7f), .025f);
            }
            if (IsPropsLayer && mode != Mode.Create)
            {
                if (mode == Mode.Paint) DrawPropPlacement(view, scene, e);
                return;
            }
            if (mode == Mode.Select || (IsBackgroundLayer && mode != Mode.Create) || (e.alt && control == 0)) return;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance))
            {
                if (previewView == view) ClearPreview();
                return;
            }
            Vector2 point = ray.GetPoint(distance);
            bool insideRoom = selected && selected.gameObject.scene == scene && selected.Bounds.Contains(point);
            if ((e.type == EventType.MouseMove || e.type == EventType.Repaint) && EditorWindow.mouseOverWindow == view)
                UpdatePreview(view, insideRoom && !e.shift && !e.alt ? selected : null, point);
            if (e.type == EventType.MouseMove) view.Repaint();
            if ((mode == Mode.Paint || mode == Mode.Erase) && insideRoom)
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
                        if (sample) SelectBrush(sample, map.GetTransformMatrix(cell));
                        e.Use();
                        Refresh();
                        return;
                    }
                }
            }
            Vector2 corner = (Vector2)Vector2Int.RoundToInt(point / MapRoom.CellSize) * MapRoom.CellSize;
            if (e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == id)
            {
                ClearPreview();
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

        private void DrawPropPlacement(SceneView view, Scene scene, Event e)
        {
            if (!placingProp || !propBrush || !selected || selected.gameObject.scene != scene) return;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                StopPlacingProp();
                e.Use();
                return;
            }
            if (e.alt || EditorWindow.mouseOverWindow != view) return;

            if (e.type == EventType.MouseMove) view.Repaint();
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!new Plane(Vector3.forward, selected.transform.position).Raycast(ray, out float distance)) return;
            Vector2 point = ray.GetPoint(distance);
            if (!selected.Bounds.Contains(point)) return;

            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
            if (e.type == EventType.Repaint)
            {
                var bounds = propBrush.bounds;
                var pivot = PropPosition(point);
                Outline(new Rect(pivot + (Vector2)bounds.min * propScale, (Vector2)bounds.size * propScale),
                    Color.cyan, .18f);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == id)
            {
                var instance = PlaceProp(point);
                StopPlacingProp();
                if (instance) Selection.activeGameObject = instance;
                e.Use();
            }
        }

        private Vector2 PropPosition(Vector2 anchorPoint)
        {
            var bounds = propBrush.bounds;
            var offset = new Vector2(bounds.center.x, alignPropTop ? bounds.max.y : bounds.min.y);
            return anchorPoint - offset * propScale;
        }

        private GameObject PlaceProp(Vector2 point)
        {
            if (!selected || !propBrush || !selected.Bounds.Contains(point)) return null;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("摆放环境摆件");

            var container = selected.transform.Find(PropsContainer);
            if (!container)
            {
                container = new GameObject(PropsContainer).transform;
                container.SetParent(selected.transform, false);
                Undo.RegisterCreatedObjectUndo(container.gameObject, "创建环境摆件组");
            }

            var instance = new GameObject(propBrush.name);
            instance.transform.SetParent(container, false);
            var position = PropPosition(point);
            instance.transform.position = new Vector3(position.x, position.y, selected.transform.position.z);
            instance.transform.localScale = Vector3.one * propScale;
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = propBrush;
            renderer.sortingOrder = propPath.Contains("/Midground/") ? -3 : 1;
            Undo.RegisterCreatedObjectUndo(instance, "摆放环境摆件");
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(selected.gameObject.scene);
            return instance;
        }

        private void StopPlacingProp()
        {
            placingProp = false;
            Refresh();
        }

        private static void Outline(Rect bounds, Color color, float alpha)
        {
            Handles.DrawSolidRectangleWithOutline(new Vector3[] { bounds.min,
                new Vector2(bounds.xMax, bounds.yMin), bounds.max, new Vector2(bounds.xMin, bounds.yMax) },
                new Color(color.r, color.g, color.b, alpha), color);
        }

        private void UpdatePreview(SceneView view, MapRoom room, Vector2 point)
        {
            var map = room && mode == Mode.Paint && control == 0 && tile
                ? room.transform.Find(MapLayout.Layers[layer])?.GetComponent<Tilemap>() : null;
            var cell = map ? map.WorldToCell(point) : default;
            if (map && map == previewMap && cell == previewCell && previewTile == tile) return;
            ClearPreview();
            if (!map || map.GetEditorPreviewTile(cell)) return;
            map.SetEditorPreviewTile(cell, tile);
            map.SetEditorPreviewTransformMatrix(cell, BrushTransform(map.GetEditorPreviewTransformMatrix(cell)));
            var tint = map.GetEditorPreviewColor(cell);
            tint.a *= .65f;
            map.SetEditorPreviewColor(cell, tint);
            previewMap = map;
            previewCell = cell;
            previewView = view;
            previewTile = tile;
        }

        private void ClearPreview()
        {
            if (previewMap && previewMap.GetEditorPreviewTile(previewCell) == previewTile)
                previewMap.SetEditorPreviewTile(previewCell, null);
            if (previewView) previewView.Repaint();
            previewMap = null;
            previewView = null;
            previewTile = null;
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
                if (mode == Mode.Erase) stroke.SetTile(pos, null);
                else SetBrushTile(stroke, pos);
                if (collisionStroke)
                    collisionStroke.SetTile(pos, mode == Mode.Erase ? null : solidTile);
            }
            lastCell = cell;
            hasLastCell = true;
            EditorSceneManager.MarkSceneDirty(stroke.gameObject.scene);
        }

        private void SetBrushTile(Tilemap map, Vector3Int cell)
        {
            // Replacing a rule tile with itself can retain the previous cell transform.
            if (!(tile is Tile) && map.GetTile(cell) == tile) map.SetTile(cell, null);
            map.SetTile(cell, tile);
            var original = map.GetTransformMatrix(cell);
            var transformed = BrushTransform(tile is Tile plainTile ? plainTile.transform : original);
            if (transformed == original) return;
            var flags = map.GetTileFlags(cell);
            map.SetTileFlags(cell, flags & ~TileFlags.LockTransform);
            map.SetTransformMatrix(cell, transformed);
            map.SetTileFlags(cell, flags);
        }
    }
}
