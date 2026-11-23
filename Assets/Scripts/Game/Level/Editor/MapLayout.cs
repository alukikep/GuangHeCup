using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Game.Level.Editor
{
    internal static class MapLayout
    {
        internal static readonly string[] Layers = { "Background", "Ground", "Decoration" };

        internal static MapRoom[] Rooms(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MapRoom>(true)).ToArray();

        internal static MapRoom Conflict(Rect bounds, MapRoom[] rooms) =>
            rooms.FirstOrDefault(room => room.Bounds.Overlaps(bounds));

        internal const string CollisionLayer = "Painted Collision";
        internal const string SolidTilePath = "Assets/Game/Data/Levels/SolidCell.asset";

        internal static Tilemap CollisionMap(MapRoom room, bool create)
        {
            var existing = room.transform.Find(CollisionLayer);
            if (existing) return existing.GetComponent<Tilemap>();
            if (!create) return null;

            var layer = new GameObject(CollisionLayer);
            layer.transform.SetParent(room.transform, false);
            var map = layer.AddComponent<Tilemap>();
            layer.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var composite = layer.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            var collider = layer.AddComponent<TilemapCollider2D>();
            collider.extrusionFactor = .002f;
            collider.usedByComposite = true;
            Undo.RegisterCreatedObjectUndo(layer, "创建实体碰撞层");
            return map;
        }

        internal static MapRoom Create(Scene scene, Rect bounds, string name)
        {
            if (bounds.width <= 0 || bounds.height <= 0 || Conflict(bounds, Rooms(scene))) return null;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("创建房间");
            try
            {
                var root = new GameObject(string.IsNullOrWhiteSpace(name) ? "房间" : name.Trim());
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = bounds.min;
                Undo.RegisterCreatedObjectUndo(root, "创建房间");
                var room = Undo.AddComponent<MapRoom>(root);
                using (var data = new SerializedObject(room))
                {
                    data.FindProperty("dimensions").vector2IntValue = Vector2Int.RoundToInt(bounds.size / MapRoom.CellSize);
                    data.ApplyModifiedProperties();
                }
                var grid = Undo.AddComponent<Grid>(root);
                Undo.RecordObject(grid, "设置网格");
                grid.cellSize = new Vector3(MapRoom.CellSize, MapRoom.CellSize, 0);
                for (int i = 0; i < Layers.Length; i++)
                {
                    var layer = new GameObject(Layers[i]);
                    layer.transform.SetParent(root.transform, false);
                    layer.AddComponent<Tilemap>();
                    layer.AddComponent<TilemapRenderer>().sortingOrder = i - 2;
                    Undo.RegisterCreatedObjectUndo(layer, "创建图层");
                }
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                return room;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }
    }
}
