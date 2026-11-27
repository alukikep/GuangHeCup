using UnityEngine;

namespace Game.Level
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MapRoom))]
    public sealed class MapRoomBackground : MonoBehaviour
    {
        private enum FitMode { Cover, Contain }

        [SerializeField] private Texture2D image;
        [SerializeField] private FitMode fit;
        [SerializeField] private Vector2 alignment = new Vector2(.5f, .5f);

        private MapRoom room;
        private SpriteRenderer background;
        private Texture2D renderedImage;
        private Sprite renderedSprite;
        private Vector2Int renderedDimensions;
        private Vector2 renderedAlignment;
        private FitMode renderedFit;

        private void OnEnable()
        {
            room = GetComponent<MapRoom>();
            Update();
        }

        private void Update()
        {
            if (!image)
            {
                Clear();
                return;
            }

            var dimensions = room.Dimensions;
            if (dimensions.x <= 0 || dimensions.y <= 0)
            {
                Clear();
                return;
            }

            if (background && renderedImage == image && renderedDimensions == dimensions &&
                renderedAlignment == alignment && renderedFit == fit) return;

            if (!background)
            {
                var display = new GameObject("Room Background") { hideFlags = HideFlags.HideAndDontSave };
                display.transform.SetParent(transform, false);
                background = display.AddComponent<SpriteRenderer>();
                background.sortingOrder = -10;
            }

            var roomSize = (Vector2)dimensions * MapRoom.CellSize;
            var imageSize = new Vector2(image.width, image.height);
            var anchor = new Vector2(Mathf.Clamp01(alignment.x), Mathf.Clamp01(alignment.y));
            var crop = new Rect(Vector2.zero, imageSize);
            var scale = new Vector2(roomSize.x / imageSize.x, roomSize.y / imageSize.y);
            float unitsPerPixel;
            Vector2 offset;
            if (fit == FitMode.Cover)
            {
                unitsPerPixel = Mathf.Max(scale.x, scale.y);
                crop.size = Vector2.Min(imageSize, roomSize / unitsPerPixel);
                crop.position = Vector2.Scale(imageSize - crop.size, anchor);
                offset = Vector2.zero;
            }
            else
            {
                unitsPerPixel = Mathf.Min(scale.x, scale.y);
                offset = Vector2.Scale(roomSize - imageSize * unitsPerPixel, anchor);
            }

            var sprite = Sprite.Create(image, crop, Vector2.zero, 1 / unitsPerPixel, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            background.transform.localPosition = offset;
            background.sprite = sprite;
            Release(renderedSprite);
            renderedSprite = sprite;
            renderedImage = image;
            renderedDimensions = dimensions;
            renderedAlignment = alignment;
            renderedFit = fit;
        }

        private void OnDisable() => Clear();

        private void Clear()
        {
            if (background)
            {
                background.enabled = false;
                Release(background.gameObject);
                background = null;
            }
            Release(renderedSprite);
            renderedSprite = null;
            renderedImage = null;
        }

        private void Release(Object instance)
        {
            if (!instance) return;
            if (Application.IsPlaying(gameObject)) Destroy(instance);
            else DestroyImmediate(instance);
        }
    }
}
