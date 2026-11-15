using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ModuleFactory.KeyboardInput.Tests
{
    public sealed class KeyboardActionSetTests : InputTestFixture
    {
        private const string PresetPath = "Assets/Scripts/Shared/KeyboardInput/KeyboardActions.inputactions";
        private Keyboard keyboard;
        private InputActionAsset preset;
        private KeyboardActionSet controls;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            preset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PresetPath);
            controls = new KeyboardActionSet(preset);
        }

        public override void TearDown()
        {
            controls?.Dispose();
            base.TearDown();
        }

        [Test]
        public void MenuAndGameplayAreMutuallyExclusive()
        {
            var move = controls.GetGameplayAction("Move");
            var navigate = controls.GetMenuAction("Navigate");
            SetKeys(Key.W);
            Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.up));

            controls.SetMenuOpen(true);
            SetKeys(Key.W, Key.DownArrow);
            Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
            Assert.That(navigate.ReadValue<Vector2>(), Is.EqualTo(Vector2.down));

            controls.SetMenuOpen(false);
            SetKeys(Key.D);
            Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.right));
            Assert.That(navigate.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RebindAppliesOnlyToSessionAndRestoreReinstatesOriginalKey()
        {
            var interact = controls.GetGameplayAction("Interact");
            var calls = 0;
            interact.SubscribePerformed(() => calls++);
            KeyboardRebindResult? result = null;

            Assert.That(controls.TryBeginRebind(interact, 0, value => result = value), Is.True);
            Assert.That(controls.GameplayEnabled || controls.MenuEnabled, Is.False);
            Assert.That(controls.TryBeginRebind(interact, 0), Is.False);
            Tap(Key.F);
            Assert.That(result, Is.EqualTo(KeyboardRebindResult.Applied));
            Assert.That(controls.GameplayEnabled, Is.True);
            Assert.That(controls.MenuEnabled, Is.False);
            Assert.That(calls, Is.Zero, "The capture key must not run the rebound action.");
            Assert.That(preset.FindAction("Interact").bindings[0].overridePath, Is.Null);

            Tap(Key.E);
            Assert.That(calls, Is.Zero);
            Tap(Key.F);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(controls.RestoreGameplayDefaults(), Is.True);
            Tap(Key.E);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void CompositePartsCanBeReboundAndConflictsKeepExistingBinding()
        {
            var move = controls.GetGameplayAction("Move");
            var interact = controls.GetGameplayAction("Interact");
            KeyboardRebindResult? result = null;
            Assert.That(controls.TryBeginRebind(move, 0), Is.False);
            Assert.That(controls.TryBeginRebind(move, 1, value => result = value), Is.True);
            Tap(Key.I);
            Assert.That(result, Is.EqualTo(KeyboardRebindResult.Applied));
            SetKeys(Key.I);
            Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.up));
            SetKeys();

            Assert.That(controls.TryBeginRebind(interact, 0, value => result = value), Is.True);
            Tap(Key.I);
            Assert.That(result, Is.EqualTo(KeyboardRebindResult.Conflict));
            Assert.That(controls.GameplayEnabled, Is.True);
            Assert.That(controls.MenuEnabled, Is.False);
            Assert.That(interact.Bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
        }

        [Test]
        public void EscapeCancelsWithoutSendingMenuCancel()
        {
            var interact = controls.GetGameplayAction("Interact");
            var menuCancels = 0;
            controls.GetMenuAction("Cancel").SubscribePerformed(() => menuCancels++);
            controls.SetMenuOpen(true);
            KeyboardRebindResult? result = null;

            Assert.That(controls.TryBeginRebind(interact, 0, value => result = value), Is.True);
            Assert.That(controls.MenuEnabled, Is.False);
            Tap(Key.Escape);
            Assert.That(result, Is.EqualTo(KeyboardRebindResult.Cancelled));
            Assert.That(controls.MenuEnabled, Is.True);
            Assert.That(controls.GameplayEnabled, Is.False);
            Assert.That(menuCancels, Is.Zero);
            Assert.That(interact.Bindings[0].overridePath, Is.Null);
            Tap(Key.Escape);
            Assert.That(menuCancels, Is.EqualTo(1));
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
