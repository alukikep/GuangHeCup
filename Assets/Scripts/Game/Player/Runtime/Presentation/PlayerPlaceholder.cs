using Game.Player.Movement;
using UnityEngine;

namespace Game.Player.Presentation
{
    /// <summary>
    /// 没有美术资源时的占位：运行时生成一张带描边的长方形贴图，
    /// 按 PPU 换算成 0.5 x 1.5 世界单位，底边中心对齐，朝向靠 flipX。
    /// 一旦 SpriteRenderer 上有真实 Sprite，就不再生成。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(PlayerMotor))]
    public sealed class PlayerPlaceholder : MonoBehaviour
    {
        [SerializeField] private Vector2Int pixels = new Vector2Int(16, 24);
        [SerializeField, Min(1)] private int pixelsPerUnit = 32;
        [SerializeField] private Color bodyColor = new Color(0.85f, 0.62f, 0.35f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.23f, 0.16f, 0.12f, 1f);
        [SerializeField, Min(0)] private int outlineThickness = 2;
        [SerializeField] private int sortingOrder = 10;

        private SpriteRenderer spriteRenderer;
        private PlayerMotor motor;
        private Sprite generatedSprite;
        private Texture2D generatedTexture;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            motor = GetComponent<PlayerMotor>();
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private void OnEnable()
        {
            if (motor != null) motor.FacingChanged += OnFacingChanged;
        }

        private void Start()
        {
            if (spriteRenderer.sprite == null) ApplyGeneratedSprite();

            if (motor != null) OnFacingChanged(motor.Facing);
        }

        private void OnDisable()
        {
            if (motor != null) motor.FacingChanged -= OnFacingChanged;
        }

        private void OnDestroy()
        {
            if (generatedSprite != null) Destroy(generatedSprite);
            if (generatedTexture != null) Destroy(generatedTexture);
        }

        private void ApplyGeneratedSprite()
        {
            int width = Mathf.Max(1, pixels.x);
            int height = Mathf.Max(1, pixels.y);

            generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PlayerPlaceholderTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            int outline = Mathf.Clamp(outlineThickness, 0, Mathf.Min(width, height) / 2);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < outline || y < outline || x >= width - outline || y >= height - outline;
                    generatedTexture.SetPixel(x, y, edge ? outlineColor : bodyColor);
                }
            }

            generatedTexture.Apply();

            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0f),
                Mathf.Max(1, pixelsPerUnit));
            generatedSprite.name = "PlayerPlaceholder";

            spriteRenderer.sprite = generatedSprite;
        }

        private void OnFacingChanged(int facing)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
        }
    }
}
