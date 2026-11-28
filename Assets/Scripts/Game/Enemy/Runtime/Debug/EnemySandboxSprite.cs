using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 灰盒用占位精灵。没有美术资源时在运行时生成一张带描边的贴图。
    /// 只用于测试 Prefab，不参与任何战斗判定。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemySandboxSprite : MonoBehaviour
    {
        [SerializeField] private Vector2Int m_pixels = new Vector2Int(8, 8);
        [SerializeField, Min(1)] private int m_pixelsPerUnit = 32;
        [SerializeField] private Color m_bodyColor = Color.white;
        [SerializeField] private Color m_outlineColor = Color.black;
        [SerializeField, Min(0)] private int m_outlineThickness = 1;
        [SerializeField] private int m_sortingOrder = 20;

        private Sprite m_sprite;
        private Texture2D m_texture;

        private void Awake()
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingOrder = m_sortingOrder;
            if (spriteRenderer.sprite == null)
            {
                Generate(spriteRenderer);
            }
        }

        private void OnDestroy()
        {
            if (m_sprite != null)
            {
                Destroy(m_sprite);
            }

            if (m_texture != null)
            {
                Destroy(m_texture);
            }
        }

        private void Generate(SpriteRenderer spriteRenderer)
        {
            int width = Mathf.Max(1, m_pixels.x);
            int height = Mathf.Max(1, m_pixels.y);

            m_texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "EnemySandboxTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            int outline = Mathf.Clamp(m_outlineThickness, 0, Mathf.Min(width, height) / 2);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < outline || y < outline || x >= width - outline || y >= height - outline;
                    m_texture.SetPixel(x, y, edge ? m_outlineColor : m_bodyColor);
                }
            }

            m_texture.Apply();

            m_sprite = Sprite.Create(
                m_texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1, m_pixelsPerUnit));
            m_sprite.name = "EnemySandboxSprite";

            spriteRenderer.sprite = m_sprite;
        }
    }
}
