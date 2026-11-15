using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

namespace ModuleFactory.KeyboardInput
{
    public enum KeyboardRebindResult
    {
        Applied,
        Conflict,
        Cancelled
    }

    /// <summary>Owns an independent copy of the keyboard actions and their session overrides.</summary>
    public sealed class KeyboardActionSet : IDisposable
    {
        public sealed class KeyboardAction
        {
            internal InputAction Source { get; }

            internal KeyboardAction(InputAction source) => Source = source;

            public ReadOnlyArray<InputBinding> Bindings => Source.bindings;
            public string GetBindingDisplayString() => Source.GetBindingDisplayString();
            public string GetBindingDisplayString(int bindingIndex) => Source.GetBindingDisplayString(bindingIndex);
            public TValue ReadValue<TValue>() where TValue : struct => Source.ReadValue<TValue>();

            public Action SubscribePerformed(Action listener)
            {
                if (listener == null)
                    throw new ArgumentNullException(nameof(listener));

                return Subscribe(_ => listener());
            }

            public Action SubscribePerformed<TValue>(Action<TValue> listener) where TValue : struct
            {
                if (listener == null)
                    throw new ArgumentNullException(nameof(listener));

                return Subscribe(context => listener(context.ReadValue<TValue>()));
            }

            private Action Subscribe(Action<InputAction.CallbackContext> listener)
            {
                Source.performed += listener;
                return () => Source.performed -= listener;
            }
        }

        private readonly InputActionAsset actions;
        private readonly InputActionMap gameplay;
        private readonly InputActionMap menu;
        private RebindingOperation rebind;
        private Action finishRebind;
        private bool menuOpen;
        private bool disposed;

        public bool GameplayEnabled => gameplay.enabled;
        public bool MenuEnabled => menu.enabled;
        public bool IsRebinding => rebind != null || finishRebind != null;

        public KeyboardActionSet(InputActionAsset template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            template.FindActionMap("Gameplay", throwIfNotFound: true);
            template.FindActionMap("UI", throwIfNotFound: true);
            actions = UnityEngine.Object.Instantiate(template);
            actions.Disable();
            gameplay = actions.FindActionMap("Gameplay");
            menu = actions.FindActionMap("UI");
            gameplay.Enable();
        }

        public KeyboardAction GetGameplayAction(string name) => GetAction(gameplay, name);
        public KeyboardAction GetMenuAction(string name) => GetAction(menu, name);

        private KeyboardAction GetAction(InputActionMap map, string name)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(KeyboardActionSet));

            return new KeyboardAction(map.FindAction(name, throwIfNotFound: true));
        }

        public void SetMenuOpen(bool open)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(KeyboardActionSet));

            menuOpen = open;
            if (!IsRebinding)
                ApplyMode();
        }

        public bool TryBeginRebind(KeyboardAction keyboardAction, int bindingIndex,
            Action<KeyboardRebindResult> onFinished = null)
        {
            var action = keyboardAction?.Source;
            if (disposed || IsRebinding || action == null || action.actionMap != gameplay ||
                bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            var binding = action.bindings[bindingIndex];
            if (binding.isComposite || !binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                return false;

            gameplay.Disable();
            menu.Disable();
            string newPath = null;
            try
            {
                rebind = action.PerformInteractiveRebinding(bindingIndex)
                    .WithExpectedControlType<KeyControl>()
                    .WithControlsHavingToMatchPath("<Keyboard>/*")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithMatchingEventsBeingSuppressed()
                    .OnMatchWaitForAnother(0)
                    .OnApplyBinding((_, path) => newPath = path)
                    .OnCancel(_ => FinishRebind(KeyboardRebindResult.Cancelled, onFinished))
                    .OnComplete(operation =>
                    {
                        if (HasConflict(action, bindingIndex, operation.selectedControl))
                        {
                            FinishRebind(KeyboardRebindResult.Conflict, onFinished);
                            return;
                        }

                        action.ApplyBindingOverride(bindingIndex, newPath);
                        FinishRebind(KeyboardRebindResult.Applied, onFinished);
                    });
                rebind.Start();
                return true;
            }
            catch
            {
                rebind?.Dispose();
                rebind = null;
                ApplyMode();
                throw;
            }
        }

        public void CancelRebind() => rebind?.Cancel();

        public bool RestoreGameplayDefaults()
        {
            if (disposed || IsRebinding)
                return false;

            gameplay.RemoveAllBindingOverrides();
            return true;
        }

        private bool HasConflict(InputAction action, int bindingIndex, InputControl selected)
        {
            foreach (var other in gameplay.actions)
            {
                var bindings = other.bindings;
                for (var i = 0; i < bindings.Count; i++)
                {
                    if ((other == action && i == bindingIndex) || bindings[i].isComposite)
                        continue;

                    var path = bindings[i].effectivePath;
                    if (!string.IsNullOrEmpty(path) && InputControlPath.Matches(path, selected))
                        return true;
                }
            }

            return false;
        }

        private void FinishRebind(KeyboardRebindResult result, Action<KeyboardRebindResult> onFinished)
        {
            rebind.Dispose();
            rebind = null;
            // Both maps stay off until the captured key event has finished processing.
            finishRebind = () =>
            {
                InputSystem.onAfterUpdate -= finishRebind;
                finishRebind = null;
                ApplyMode();
                onFinished?.Invoke(result);
            };
            InputSystem.onAfterUpdate += finishRebind;
        }

        private void ApplyMode()
        {
            if (menuOpen)
            {
                gameplay.Disable();
                menu.Enable();
            }
            else
            {
                menu.Disable();
                gameplay.Enable();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            rebind?.Dispose();
            rebind = null;
            if (finishRebind != null)
                InputSystem.onAfterUpdate -= finishRebind;
            finishRebind = null;
            actions.Disable();
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(actions);
            else
                UnityEngine.Object.DestroyImmediate(actions);
        }
    }
}
