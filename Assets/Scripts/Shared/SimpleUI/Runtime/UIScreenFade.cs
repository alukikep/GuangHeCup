using System;
using UnityEngine;
using UnityEngine.UI;

namespace ModuleFactory.SimpleUI
{
    /// <summary>Optional full-screen fade. Does not load scenes or own gameplay input.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public sealed class UIScreenFade : MonoBehaviour
    {
        [SerializeField] private Image screen;
        [SerializeField] private Color color = UnityEngine.Color.black;
        [SerializeField] private int sortingOrder = 1000;
        [SerializeField] private bool blockInput = true;
        private CanvasGroup group;
        private Canvas canvas;
        private FadeRequest current;
        private float alpha, start, target, duration, elapsed;

        public float Alpha => alpha;
        public bool IsFading => current != null;
        public bool BlockInput { get => blockInput; set { blockInput = value; Apply(); } }
        public Color Color { get => color; set { color = value; Apply(); } }
        public int SortingOrder { get => sortingOrder; set { sortingOrder = value; Apply(); } }

        private void Reset() { EnsureView(); Apply(); }
        private void Awake() { EnsureView(); Apply(); }
        private void OnEnable() { EnsureView(); Apply(); }
        private void EnsureView()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (screen != null) return;
            var go = new GameObject("Fade Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            screen = go.GetComponent<Image>();
            var rect = screen.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void Apply()
        {
            if (group == null || canvas == null || screen == null) return;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = isActiveAndEnabled && blockInput && (IsFading || alpha > 0);
            // Color alpha is reserved for fading; otherwise alpha=1 might not obscure the scene.
            screen.color = new Color(color.r, color.g, color.b, 1);
            screen.raycastTarget = true;
        }
        private static void Validate(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
        public FadeRequest FadeOut(float seconds) => FadeTo(1, seconds);
        public FadeRequest FadeIn(float seconds) => FadeTo(0, seconds);
        public FadeRequest FadeTo(float targetAlpha, float seconds)
        {
            Validate(targetAlpha, nameof(targetAlpha)); Validate(seconds, nameof(seconds));
            Cancel();
            var request = new FadeRequest();
            if (!isActiveAndEnabled) { request.Status = FadeStatus.Cancelled; return request; }
            EnsureView();
            target = Mathf.Clamp01(targetAlpha);
            if (seconds <= 0 || target == alpha)
            {
                alpha = target; request.Status = FadeStatus.Completed; Apply(); return request;
            }
            start = alpha; duration = seconds; elapsed = 0; current = request; Apply(); return request;
        }
        public void SetImmediate(float value)
        {
            Validate(value, nameof(value)); Cancel();
            alpha = isActiveAndEnabled ? Mathf.Clamp01(value) : 0;
            Apply();
        }
        public void Cancel()
        {
            if (current != null) { current.Status = FadeStatus.Cancelled; current = null; }
            Apply();
        }
        private void Update()
        {
            if (current != null)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                alpha = Mathf.Lerp(start, target, elapsed / duration);
                if (elapsed >= duration)
                {
                    alpha = target; current.Status = FadeStatus.Completed; current = null;
                }
            }
            Apply();
        }
        private void OnDisable() { Cancel(); alpha = 0; Apply(); }
        private void OnDestroy() { Cancel(); }
    }
}
