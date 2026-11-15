using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ModuleFactory.SimpleUI
{
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        public RectTransform NormalRoot;
        public RectTransform PopupRoot;
        public RectTransform OverlayRoot;
        public UIPanel[] Panels = Array.Empty<UIPanel>();
        private readonly List<UIPanel> registered = new List<UIPanel>();
        private Image mask;
        private readonly List<UIPanel> pendingHidden = new List<UIPanel>();
        private bool initialized;
        private bool shuttingDown;

        // Configure inactive managers before enabling, or use Inspector references.
        private void Start() { Initialize(); }
        public bool Initialize()
        {
            if (initialized) return true;
            if (!isActiveAndEnabled) return false;
            var canvas = NormalRoot != null ? NormalRoot.GetComponentInParent<Canvas>() : null;
            if (canvas == null || PopupRoot == null || OverlayRoot == null ||
                NormalRoot == PopupRoot || PopupRoot == OverlayRoot || NormalRoot == OverlayRoot ||
                NormalRoot.parent != PopupRoot.parent || NormalRoot.parent != OverlayRoot.parent ||
                PopupRoot.GetComponentInParent<Canvas>() != canvas || OverlayRoot.GetComponentInParent<Canvas>() != canvas)
                return Invalid("Layer roots must be distinct siblings under the same Canvas.");
            var seen = new HashSet<UIPanel>();
            foreach (var panel in Panels)
            {
                if (panel == null || !seen.Add(panel) || panel.Owner != null ||
                    panel.transform.parent != Root(panel.Layer) || (panel.Modal && panel.Layer != UILayer.Popup) ||
                    panel.GetComponentsInChildren<Canvas>(true).Length != 0)
                    return Invalid("Invalid panel: check duplicate/owner, direct parent, modal layer or nested Canvas.");
            }
            initialized = true;
            NormalRoot.SetAsLastSibling(); PopupRoot.SetAsLastSibling(); OverlayRoot.SetAsLastSibling();
            var go = new GameObject("Modal Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(PopupRoot, false);
            mask = go.GetComponent<Image>(); mask.color = new Color(0, 0, 0, .55f);
            var rect = mask.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.SetActive(false);
            foreach (var panel in Panels)
            {
                registered.Add(panel); panel.Owner = this; panel.IsOpen = false;
                var group = panel.GetComponent<CanvasGroup>();
                if (panel.Layer == UILayer.Overlay) { group.blocksRaycasts = false; group.interactable = false; }
                panel.gameObject.SetActive(false);
            }
            foreach (var panel in registered.ToArray())
                if (panel != null && panel.InitiallyOpen) Open(panel);
            return true;
        }
        private bool Invalid(string message) { Debug.LogError("Simple UI: " + message, this); return false; }
        private RectTransform Root(UILayer layer)
        {
            switch (layer) { case UILayer.Normal: return NormalRoot; case UILayer.Popup: return PopupRoot;
                case UILayer.Overlay: return OverlayRoot; default: return null; }
        }
        private bool Owns(UIPanel panel) { return panel != null && panel.Owner == this; }
        public bool Open(UIPanel panel)
        {
            if (shuttingDown || !isActiveAndEnabled || !Initialize()) return false;
            if (!Owns(panel)) return Invalid("Open requires a registered panel.");
            panel.transform.SetAsLastSibling();
            if (panel.IsOpen) { RefreshMask(); return true; }
            panel.IsOpen = true; int revision = ++panel.Revision;
            panel.gameObject.SetActive(true); RefreshMask();
            if (panel != null && panel.IsOpen && panel.Revision == revision) Notify(panel, true);
            return panel != null && panel.IsOpen;
        }
        public bool Close(UIPanel panel)
        {
            if (!Owns(panel)) return Invalid("Close requires a registered panel.");
            if (!panel.IsOpen) return false;
            panel.IsOpen = false; int revision = ++panel.Revision;
            panel.gameObject.SetActive(false); RefreshMask();
            if (panel != null && !panel.IsOpen && panel.Revision == revision) Notify(panel, false);
            return true;
        }
        internal void ExternalClose(UIPanel panel)
        {
            if (!Owns(panel) || !panel.IsOpen) return;
            panel.IsOpen = false; ++panel.Revision; RefreshMask();
            if (isActiveAndEnabled) pendingHidden.Add(panel);
        }
        private void Notify(UIPanel panel, bool shown)
        {
            try { if (shown) panel.Shown.Invoke(); else panel.Hidden.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception, panel); }
        }
        public bool Back()
        {
            if (!isActiveAndEnabled || !Initialize()) return false;
            for (int layer = 2; layer >= 0; --layer)
            {
                var root = Root((UILayer)layer);
                for (int i = root.childCount - 1; i >= 0; --i)
                {
                    var panel = root.GetChild(i).GetComponent<UIPanel>();
                    if (!Owns(panel) || !panel.IsOpen) continue;
                    if (panel.CanGoBack) return Close(panel);
                    if (panel.Modal) return false;
                }
            }
            return false;
        }
        public void CloseAll()
        {
            bool previous = shuttingDown; shuttingDown = true;
            try { foreach (var panel in registered.ToArray()) if (panel != null && panel.IsOpen) Close(panel); }
            finally { shuttingDown = previous; }
        }
        private void RefreshMask()
        {
            if (mask == null || PopupRoot == null) return;
            UIPanel top = null;
            foreach (var panel in registered)
                if (panel != null && panel.IsOpen && panel.Modal &&
                    (top == null || panel.transform.GetSiblingIndex() > top.transform.GetSiblingIndex())) top = panel;
            mask.gameObject.SetActive(top != null);
            if (top == null) return;
            if (mask.transform.GetSiblingIndex() != top.transform.GetSiblingIndex() - 1)
            {
                mask.transform.SetAsLastSibling();
                mask.transform.SetSiblingIndex(top.transform.GetSiblingIndex());
            }
        }
        private void LateUpdate()
        {
            var notifications = pendingHidden.ToArray(); pendingHidden.Clear();
            foreach (var panel in notifications) if (panel != null && !panel.IsOpen) Notify(panel, false);
            foreach (var panel in registered.ToArray())
            {
                if (panel == null) { registered.Remove(panel); continue; }
                if (!panel.IsOpen && panel.gameObject.activeSelf)
                { Debug.LogWarning("Simple UI: use Open instead of SetActive(true).", panel); panel.gameObject.SetActive(false); }
            }
            RefreshMask();
        }
        private void OnDisable() { CloseAll(); }
        private void OnDestroy()
        {
            shuttingDown = true; CloseAll();
            foreach (var panel in registered) if (panel != null) panel.Owner = null;
            registered.Clear();
            if (mask != null) Destroy(mask.gameObject);
        }
    }
}
