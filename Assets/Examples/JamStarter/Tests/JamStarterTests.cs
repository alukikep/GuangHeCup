using System.Collections;
using System.Linq;
using ModuleFactory.Audio;
using ModuleFactory.SimpleUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ModuleFactory.GameJam.Tests
{
    public sealed class JamStarterTests
    {
        [UnityTest]
        public IEnumerator RoundCanExpireTargetsPauseScoreAndRestart()
        {
            yield return SceneManager.LoadSceneAsync("JamStarter");
            yield return null;
            var fade = Object.FindObjectOfType<UIScreenFade>();
            var audio = Object.FindObjectOfType<AudioRoot>().Service;
            Assert.That(audio, Is.Not.Null);

            Click("Start round");
            Assert.That(fade.IsFading, Is.True);
            yield return WaitForFade(fade);

            Click("Spawn target");
            var original = FindButton("Target(Clone)");
            Assert.That(original, Is.Not.Null);
            Click("Pause");
            Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(FindButton("Target(Clone)"), Is.SameAs(original));

            Click("Resume");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Time.timeScale = 20f;
            var expiryDeadline = Time.realtimeSinceStartup + 3f;
            while (original.isActiveAndEnabled && Time.realtimeSinceStartup < expiryDeadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(original.isActiveAndEnabled, Is.False);
            Assert.That(Score(), Is.EqualTo("Targets: 0 / 3"));

            for (var i = 1; i <= 3; i++)
            {
                Click("Spawn target");
                Assert.That(FindButton("Target(Clone)"), Is.SameAs(original));
                var rejected = audio.RejectedRequests;
                Click("Target(Clone)");
                Assert.That(original.isActiveAndEnabled, Is.False);
                Assert.That(Score(), Is.EqualTo($"Targets: {i} / 3"));
                Assert.That(audio.RejectedRequests, Is.EqualTo(rejected), audio.LastRejection);
                Assert.That(audio.GetActiveVoices(AudioBus.SFX), Is.GreaterThan(0), audio.LastRejection);
                yield return null;
            }

            Click("Play again");
            yield return WaitForFade(fade);
            Assert.That(Score(), Is.EqualTo("Targets: 0 / 3"));

            Click("Spawn target");
            Click("Pause");
            Click("Main menu");
            yield return WaitForFade(fade);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(original.isActiveAndEnabled, Is.False);
            Assert.That(FindButton("Start round"), Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator UnloadStarter()
        {
            Time.timeScale = 1f;
            var loaded = SceneManager.GetSceneByName("JamStarter");
            if (!loaded.isLoaded) yield break;

            SceneManager.SetActiveScene(SceneManager.CreateScene("After Game Jam Test"));
            yield return SceneManager.UnloadSceneAsync(loaded);
        }

        private static IEnumerator WaitForFade(UIScreenFade fade)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            while ((fade.IsFading || fade.Alpha > 0f) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(fade.IsFading, Is.False);
            Assert.That(fade.Alpha, Is.Zero);
            Assert.That(fade.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        private static string Score() => Object.FindObjectsOfType<Text>()
            .Single(label => label.text.StartsWith("Targets:")).text;

        private static Button FindButton(string name) => Object.FindObjectsOfType<Button>()
            .SingleOrDefault(button => button.name == name);

        private static void Click(string name)
        {
            var button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
