using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using ModuleFactory.SimpleUI;

public sealed class ScreenFadeTests
{
    GameObject root; UIScreenFade fade;
    [SetUp] public void Setup() { root = new GameObject("Fade Test"); fade = root.AddComponent<UIScreenFade>(); }
    [TearDown] public void Cleanup() { Time.timeScale=1; UnityEngine.Object.DestroyImmediate(root); }
    [Test] public void ImmediateClampsAndCancels()
    {
        var r=fade.FadeOut(1);fade.SetImmediate(2);Assert.AreEqual(FadeStatus.Cancelled,r.Status);Assert.AreEqual(1,fade.Alpha);
        Assert.IsTrue(root.GetComponent<CanvasGroup>().blocksRaycasts);fade.SetImmediate(-1);Assert.AreEqual(0,fade.Alpha);Assert.IsFalse(root.GetComponent<CanvasGroup>().blocksRaycasts);
    }
    [UnityTest] public IEnumerator PausedFadeCompletesBothDirections()
    {
        Time.timeScale=0;var r=fade.FadeOut(.05f);yield return r;
        Assert.AreEqual(FadeStatus.Completed,r.Status);Assert.AreEqual(1,fade.Alpha);Assert.IsFalse(fade.IsFading);
        yield return new WaitForSecondsRealtime(.03f);Assert.AreEqual(1,fade.Alpha);
        r=fade.FadeIn(.05f);yield return r;Assert.AreEqual(FadeStatus.Completed,r.Status);Assert.AreEqual(0,fade.Alpha);
    }
    [UnityTest] public IEnumerator ReplacementStartsFromCurrentAndOldStaysCancelled()
    {
        var old=fade.FadeOut(1);yield return null;float before=fade.Alpha;
        var next=fade.FadeIn(.05f);Assert.AreEqual(before,fade.Alpha);Assert.AreEqual(FadeStatus.Cancelled,old.Status);
        yield return next;Assert.AreEqual(FadeStatus.Completed,next.Status);Assert.AreEqual(0,fade.Alpha);Assert.AreEqual(FadeStatus.Cancelled,old.Status);
    }
    [UnityTest] public IEnumerator CancelKeepsCurrentOpacityAndReleasesWait()
    {
        var r=fade.FadeOut(1);yield return null;float before=fade.Alpha;fade.Cancel();yield return r;
        Assert.AreEqual(FadeStatus.Cancelled,r.Status);Assert.AreEqual(before,fade.Alpha);Assert.IsFalse(fade.IsFading);
        Assert.AreEqual(before>0,root.GetComponent<CanvasGroup>().blocksRaycasts);
    }
    [Test] public void DisableCancelsClearsAndReenableDoesNotResume()
    {
        fade.SetImmediate(.5f);var r=fade.FadeOut(1);fade.enabled=false;
        Assert.AreEqual(FadeStatus.Cancelled,r.Status);Assert.AreEqual(0,fade.Alpha);Assert.IsFalse(root.GetComponent<CanvasGroup>().blocksRaycasts);
        Assert.AreEqual(FadeStatus.Cancelled,fade.FadeOut(1).Status);fade.enabled=true;Assert.AreEqual(0,fade.Alpha);Assert.IsFalse(fade.IsFading);
    }
    [Test] public void DestroyTerminatesPendingRequest()
    { var r=fade.FadeOut(1);UnityEngine.Object.DestroyImmediate(root);Assert.AreEqual(FadeStatus.Cancelled,r.Status); }
    [Test] public void InvalidNumbersDoNotReplaceValidRequest()
    {
        var r=fade.FadeOut(1);
        Assert.Throws<ArgumentOutOfRangeException>(()=>fade.FadeTo(float.NaN,1));Assert.Throws<ArgumentOutOfRangeException>(()=>fade.FadeTo(0,float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(()=>fade.SetImmediate(float.NegativeInfinity));Assert.AreEqual(FadeStatus.Pending,r.Status);
    }
    [Test] public void ZeroNegativeDurationAndSameTargetCompleteImmediately()
    { Assert.AreEqual(FadeStatus.Completed,fade.FadeOut(0).Status);Assert.AreEqual(1,fade.Alpha);Assert.AreEqual(FadeStatus.Completed,fade.FadeIn(-1).Status);Assert.AreEqual(0,fade.Alpha);Assert.AreEqual(FadeStatus.Completed,fade.FadeIn(1).Status); }
    [UnityTest] public IEnumerator RaycasterSeesFadeOnlyWhenBlocking()
    {
        var events=new GameObject("Events",typeof(EventSystem));
        var back=new GameObject("Back Canvas",typeof(Canvas),typeof(GraphicRaycaster));
        try
        {
            back.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var image=new GameObject("Background",typeof(RectTransform),typeof(Image));image.transform.SetParent(back.transform,false);
            var rect=(RectTransform)image.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            yield return null;Canvas.ForceUpdateCanvases();
            var data=new PointerEventData(events.GetComponent<EventSystem>()){position=new Vector2(Screen.width/2f,Screen.height/2f)};
            var hits=new List<RaycastResult>();
            var ray=root.GetComponent<GraphicRaycaster>();ray.Raycast(data,hits);Assert.IsEmpty(hits);
            var r=fade.FadeOut(10);ray.Raycast(data,hits);Assert.IsNotEmpty(hits);Assert.AreEqual("Fade Screen",hits[0].gameObject.name);
            fade.SetImmediate(1);hits.Clear();ray.Raycast(data,hits);Assert.IsNotEmpty(hits);
            fade.BlockInput=false;hits.Clear();ray.Raycast(data,hits);Assert.IsEmpty(hits);
            fade.BlockInput=true;fade.SetImmediate(0);hits.Clear();events.GetComponent<EventSystem>().RaycastAll(data,hits);
            Assert.IsNotEmpty(hits);Assert.AreEqual("Background",hits[0].gameObject.name);
        }
        finally { UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(back); }
    }
}
