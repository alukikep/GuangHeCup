using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ModuleFactory.SceneFlow;

public sealed class TransitionLoadTests
{
    [UnityTearDown]
    public IEnumerator UnloadDemoScene()
    {
        var loaded = SceneManager.GetActiveScene();
        if (loaded.name != "FadeDemoA" && loaded.name != "FadeDemoB")
            yield break;

        SceneManager.SetActiveScene(SceneManager.CreateScene("After Transition Test"));
        yield return SceneManager.UnloadSceneAsync(loaded);
    }

    [UnityTest] public IEnumerator DemoButtonsRunBoundActionsAndRestoreInput()
    {
        string path=null;
        for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++)
        { var candidate=SceneUtility.GetScenePathByBuildIndex(i);if(candidate.EndsWith("/FadeDemoA.unity"))path=candidate; }
        Assert.IsNotNull(path); yield return SceneManager.LoadSceneAsync(path);yield return null;
        var controls=Object.FindObjectOfType<FadeDemoControls>();
        foreach(var caption in new[]{"Blackout and restore","Blackout while paused","Reverse in progress","Cancel and restore"})
        {
            var button=System.Array.Find(Object.FindObjectsOfType<UnityEngine.UI.Button>(),b=>b.name==caption);
            Assert.IsNotNull(button);Assert.AreEqual(1,button.onClick.GetPersistentEventCount());
            var eventData=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            eventData.button=UnityEngine.EventSystems.PointerEventData.InputButton.Left;
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,eventData,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.IsTrue(controls.IsBusy);
            if(caption=="Blackout while paused")Assert.AreEqual(0,Time.timeScale);
            float deadline=Time.realtimeSinceStartup+5;
            while(controls.IsBusy && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(controls.IsBusy);Assert.AreEqual(0,controls.Fade.Alpha);Assert.AreEqual(1,Time.timeScale);
            Assert.IsFalse(controls.Fade.GetComponent<CanvasGroup>().blocksRaycasts);
        }
    }
    [UnityTest]
    public IEnumerator DemoScenesLoadInBothDirectionsAndReleaseInput()
    {
        var a = GetDemoPath("FadeDemoA");
        var b = GetDemoPath("FadeDemoB");
        yield return SceneManager.LoadSceneAsync(a);
        var originalLoader = Object.FindObjectOfType<FadeDemoControls>().Loader;
        Assert.That(originalLoader, Is.Not.Null);

        var previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            ClickLoadButton("FadeDemoB");
            Assert.That(SceneFlowLoader.IsBusy, Is.True);
            Assert.That(originalLoader.TryLoad(b), Is.False);

            var deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().path != b && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(b));
            var destinationLoader = Object.FindObjectOfType<FadeDemoControls>().Loader;
            Assert.That(destinationLoader.TryLoad(a), Is.False, "The original fade still owns the transition.");

            yield return WaitForTransition();
            Assert.That(originalLoader == null, Is.True);
            Assert.That(Object.FindObjectsOfType<SceneFlowLoader>().Length, Is.EqualTo(1));
            AssertClear(destinationLoader);

            ClickLoadButton("FadeDemoA");
            Assert.That(SceneFlowLoader.IsBusy, Is.True);
            yield return WaitForTransition();
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(a));
            Assert.That(destinationLoader == null, Is.True);
            AssertClear(Object.FindObjectOfType<SceneFlowLoader>());
        }
        finally
        {
            Time.timeScale = previousTimeScale;
        }
    }

    [UnityTest]
    public IEnumerator MissingTargetAndDisablingDuringBlackoutRestoreInput()
    {
        var a = GetDemoPath("FadeDemoA");
        var b = GetDemoPath("FadeDemoB");
        yield return SceneManager.LoadSceneAsync(a);
        var loader = Object.FindObjectOfType<SceneFlowLoader>();
        loader.Fade.SetImmediate(0.7f);

        LogAssert.Expect(LogType.Warning, "Scene Flow: target scene 'Missing-Transition-Scene' must be enabled in Build Settings.");
        Assert.That(loader.TryLoad("Missing-Transition-Scene"), Is.False);
        AssertClear(loader);

        Assert.That(loader.TryLoad(b), Is.True);
        Assert.That(loader.Fade.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        loader.enabled = false;
        yield return null;

        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(a));
        AssertClear(loader);
        loader.enabled = true;
        Assert.That(loader.TryLoad(b), Is.True, "A fresh request can acquire the released transition.");
        yield return WaitForTransition();
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(b));
        AssertClear(Object.FindObjectOfType<SceneFlowLoader>());
    }

    [UnityTest]
    public IEnumerator DisablingAtDestinationActivationReleasesTransition()
    {
        var a = GetDemoPath("FadeDemoA");
        var b = GetDemoPath("FadeDemoB");
        yield return SceneManager.LoadSceneAsync(a);
        var loader = Object.FindObjectOfType<SceneFlowLoader>();
        var disabledDuringTransition = false;
        var releasedPointerBlock = false;

        void DisableLoader(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != b)
                return;

            disabledDuringTransition = SceneFlowLoader.IsBusy;
            loader.enabled = false;
            releasedPointerBlock = !loader.Fade.GetComponent<CanvasGroup>().blocksRaycasts;
        }

        SceneManager.sceneLoaded += DisableLoader;
        try
        {
            Assert.That(loader.TryLoad(b), Is.True);
            yield return WaitForTransition();
            Assert.That(disabledDuringTransition, Is.True);
            Assert.That(releasedPointerBlock, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(b));
            Assert.That(loader == null, Is.True);
            AssertClear(Object.FindObjectOfType<SceneFlowLoader>());
        }
        finally
        {
            SceneManager.sceneLoaded -= DisableLoader;
        }
    }

    private static string GetDemoPath(string sceneName)
    {
        for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            if (path.EndsWith("/" + sceneName + ".unity", System.StringComparison.Ordinal))
                return path;
        }

        Assert.Fail($"Enable {sceneName} in Build Settings.");
        return null;
    }

    private static void ClickLoadButton(string sceneName)
    {
        var button = System.Array.Find(Object.FindObjectsOfType<Button>(), candidate => candidate.name == "Load " + sceneName);
        Assert.That(button, Is.Not.Null);
        button.onClick.Invoke();
    }

    private static IEnumerator WaitForTransition()
    {
        var deadline = Time.realtimeSinceStartup + 10;
        while (SceneFlowLoader.IsBusy && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(SceneFlowLoader.IsBusy, Is.False);
        yield return null; // Wait for the old root's deferred destruction.
    }

    private static void AssertClear(SceneFlowLoader loader)
    {
        Assert.That(loader, Is.Not.Null);
        Assert.That(SceneFlowLoader.IsBusy, Is.False);
        Assert.That(loader.Fade.Alpha, Is.Zero);
        Assert.That(loader.Fade.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
    }
}
