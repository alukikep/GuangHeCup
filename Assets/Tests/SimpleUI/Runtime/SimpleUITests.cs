using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using ModuleFactory.SimpleUI;

public class SimpleUITests
{
    GameObject canvas; UIManager ui; UIPanel a, b, c;
    RectTransform Root(string name) { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(canvas.transform, false); return r; }
    UIPanel Panel(string name, RectTransform root, UILayer layer, bool modal = false)
    { var p = new GameObject(name, typeof(RectTransform), typeof(UIPanel)).GetComponent<UIPanel>(); p.transform.SetParent(root, false); p.Configure(layer, modal); return p; }
    [SetUp] public void Setup()
    {
        canvas = new GameObject("Canvas", typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        ui = canvas.AddComponent<UIManager>(); ui.NormalRoot = Root("Normal"); ui.PopupRoot = Root("Popup"); ui.OverlayRoot = Root("Overlay");
        a = Panel("A", ui.NormalRoot, UILayer.Normal); b = Panel("B", ui.PopupRoot, UILayer.Popup, true); c = Panel("C", ui.PopupRoot, UILayer.Popup, true);
        ui.Panels = new[] { a, b, c };
    }
    [TearDown] public void Cleanup() { Time.timeScale = 1; Object.DestroyImmediate(canvas); }
    [UnityEngine.TestTools.UnityTest] public System.Collections.IEnumerator ModalMaskWinsRaycastOverBackground()
    {
        canvas.AddComponent<GraphicRaycaster>();
        var events = new GameObject("Events", typeof(UnityEngine.EventSystems.EventSystem));
        try
        {
            foreach (var root in new[] { ui.NormalRoot, ui.PopupRoot, ui.OverlayRoot })
            { root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero; }
            var r = (RectTransform)a.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            a.gameObject.AddComponent<Image>(); ui.Open(a); ui.Open(b);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var data = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>());
            data.position = new Vector2(Screen.width/2f, Screen.height/2f);
            canvas.GetComponent<GraphicRaycaster>().Raycast(data,hits);
            Assert.IsNotEmpty(hits); Assert.AreEqual("Modal Blocker",hits[0].gameObject.name);
        }
        finally { Object.DestroyImmediate(events); }
    }
    [Test] public void RepeatedOpenRaisesOnceAndCloseIsIdempotent()
    { int n = 0; a.Shown.AddListener(() => n++); Assert.IsTrue(ui.Open(a)); ui.Open(a); Assert.AreEqual(1,n); Assert.IsTrue(ui.Close(a)); Assert.IsFalse(ui.Close(a)); }
    [Test] public void BackClosesTopThenRestoresMask()
    { ui.Open(a); ui.Open(b); ui.Open(c); Assert.IsTrue(ui.Back()); Assert.IsFalse(c.IsOpen); var mask = ui.PopupRoot.GetComponentInChildren<Image>(); Assert.IsNotNull(mask); Assert.AreEqual(b.transform.GetSiblingIndex()-1,mask.transform.GetSiblingIndex()); ui.Back(); Assert.IsNull(ui.PopupRoot.GetComponentInChildren<Image>()); Assert.IsTrue(a.IsOpen); }
    [Test] public void NonReturnableModalStopsBack()
    { b.Configure(UILayer.Popup, modal: true, canGoBack: false); ui.Open(a); ui.Open(b); Assert.IsFalse(ui.Back()); Assert.IsTrue(a.IsOpen); }
    [Test] public void NonModalWithoutBackAllowsSearch()
    { b.Configure(UILayer.Popup, canGoBack: false); ui.Open(a); ui.Open(b); Assert.IsTrue(ui.Back()); Assert.IsFalse(a.IsOpen); }
    [Test] public void RegisteredPanelRejectsConfigurationChanges()
    {
        Assert.IsTrue(ui.Open(b));
        Assert.Throws<System.InvalidOperationException>(() => b.Configure(UILayer.Normal));
        Assert.AreEqual(UILayer.Popup, b.Layer);
        Assert.IsTrue(b.Modal);
        Assert.IsNotNull(ui.PopupRoot.GetComponentInChildren<Image>());
    }
    [Test] public void ReopenMovesPanelAboveOtherModal()
    { ui.Open(b); ui.Open(c); ui.Open(b); ui.Back(); Assert.IsFalse(b.IsOpen); Assert.IsTrue(c.IsOpen); }
    [Test] public void ExternalDisableClearsStateAndMask()
    { ui.Open(b); b.gameObject.SetActive(false); Assert.IsFalse(b.IsOpen); Assert.IsNull(ui.PopupRoot.GetComponentInChildren<Image>()); }
    [Test] public void ExternalDestroyRestoresLowerModal()
    { ui.Open(b); ui.Open(c); Object.DestroyImmediate(c.gameObject); Assert.IsTrue(b.IsOpen); Assert.IsNotNull(ui.PopupRoot.GetComponentInChildren<Image>()); }
    [Test] public void CallbackCanCloseAndReopen()
    { a.Shown.AddListener(() => ui.Close(a)); Assert.IsFalse(ui.Open(a)); a.Shown.RemoveAllListeners(); a.Hidden.AddListener(() => ui.Open(a)); ui.Open(a); ui.Close(a); Assert.IsTrue(a.IsOpen); }
    [Test] public void CloseAllPreventsCallbackReopening()
    { a.Hidden.AddListener(() => ui.Open(a)); ui.Open(a); ui.CloseAll(); Assert.IsFalse(a.IsOpen); }
    [Test] public void ManagerDisableClosesPanels()
    { ui.Open(b); ui.enabled = false; Assert.IsFalse(b.IsOpen); Assert.IsNull(ui.PopupRoot.GetComponentInChildren<Image>()); }
    [Test] public void PausedGameStillOpensAndCloses()
    { Time.timeScale = 0; Assert.IsTrue(ui.Open(a)); Assert.IsTrue(ui.Back()); }
}
