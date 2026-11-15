#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ModuleFactory.KeyboardInput.Sample.Tests
{
    public sealed class KeyboardInputFlowTests : InputTestFixture
    {
        private Keyboard keyboard;

        [UnityTest]
        public IEnumerator KeyboardCanMovePauseRebindAndRestoreDefaults()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Examples/KeyboardInput/KeyboardInputPlayground.unity", new LoadSceneParameters(LoadSceneMode.Single));

            var player = GameObject.Find("Player Marker").GetComponent<RectTransform>();
            var hud = GameObject.Find("Keyboard HUD").GetComponent<Text>();
            var initialPosition = player.anchoredPosition;
            SetKeys(Key.D);
            yield return null;
            yield return null;
            SetKeys();
            Assert.That(player.anchoredPosition.x, Is.GreaterThan(initialPosition.x));
            Tap(Key.E);
            StringAssert.Contains("Beacon toggles: 1", hud.text);

            Tap(Key.Escape);
            Assert.That(GameObject.Find("Controls Menu"), Is.Not.Null);
            var pausedPosition = player.anchoredPosition;
            SetKeys(Key.D, Key.E);
            yield return null;
            yield return null;
            SetKeys();
            Assert.That(player.anchoredPosition, Is.EqualTo(pausedPosition));
            StringAssert.Contains("Beacon toggles: 1", hud.text);

            Tap(Key.Enter);
            var hint = GameObject.Find("Menu Hint").GetComponent<Text>();
            StringAssert.Contains("Press a key for Interact", hint.text);
            Tap(Key.F);
            StringAssert.Contains("Binding updated", hint.text);
            Tap(Key.Escape);
            Assert.That(GameObject.Find("Controls Menu"), Is.Null);
            Tap(Key.E);
            StringAssert.Contains("Beacon toggles: 1", hud.text);
            Tap(Key.F);
            StringAssert.Contains("Beacon toggles: 2", hud.text);

            Tap(Key.Escape);
            Tap(Key.UpArrow);
            Tap(Key.UpArrow);
            Tap(Key.Enter);
            StringAssert.Contains("Default bindings restored", hint.text);
            Tap(Key.Escape);
            Tap(Key.E);
            StringAssert.Contains("Beacon toggles: 3", hud.text);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var loaded = SceneManager.GetActiveScene();
            if (loaded.name == "KeyboardInputPlayground")
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("After Keyboard Input Test"));
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
        }

        private void Tap(Key key)
        {
            SetKeys(key);
            SetKeys();
        }

        private void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }
    }
}
#endif
