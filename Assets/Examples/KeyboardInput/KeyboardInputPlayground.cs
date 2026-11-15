using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KeyboardAction = ModuleFactory.KeyboardInput.KeyboardActionSet.KeyboardAction;

namespace ModuleFactory.KeyboardInput.Sample
{
    public sealed class KeyboardInputPlayground : MonoBehaviour
    {
        [SerializeField] private InputActionAsset keyboardActions;
        [SerializeField] private RectTransform player;
        [SerializeField] private Image beacon;
        [SerializeField] private Text hud;
        [SerializeField] private GameObject menu;
        [SerializeField] private Text menuRows;
        [SerializeField] private Text menuHint;

        private readonly List<(string Label, KeyboardAction Action, int Binding)> bindings =
            new List<(string, KeyboardAction, int)>();
        private KeyboardActionSet controls;
        private KeyboardAction move;
        private KeyboardAction interact;
        private KeyboardAction pause;
        private bool menuOpen;
        private int selectedRow;
        private int interactionCount;

        private void OnEnable()
        {
            controls = new KeyboardActionSet(keyboardActions);
            move = controls.GetGameplayAction("Move");
            interact = controls.GetGameplayAction("Interact");
            pause = controls.GetGameplayAction("Pause");
            interact.SubscribePerformed(ToggleBeacon);
            pause.SubscribePerformed(OpenMenu);
            controls.GetMenuAction("Navigate").SubscribePerformed<Vector2>(Navigate);
            controls.GetMenuAction("Submit").SubscribePerformed(Submit);
            controls.GetMenuAction("Cancel").SubscribePerformed(CloseMenu);

            bindings.Add(("Interact", interact, 0));
            for (var i = 0; i < move.Bindings.Count; i++)
            {
                var binding = move.Bindings[i];
                if (binding.isPartOfComposite)
                    bindings.Add(($"Move {binding.name}", move, i));
            }
            bindings.Add(("Pause", pause, 0));
            CloseMenu();
        }

        private void OnDisable()
        {
            controls?.Dispose();
            controls = null;
            bindings.Clear();
        }

        private void Update()
        {
            if (menuOpen)
                return;

            var position = player.anchoredPosition + move.ReadValue<Vector2>() * (180 * Time.unscaledDeltaTime);
            position.x = Mathf.Clamp(position.x, -250, 250);
            position.y = Mathf.Clamp(position.y, -105, 105);
            player.anchoredPosition = position;
        }

        private void ToggleBeacon()
        {
            interactionCount++;
            beacon.color = interactionCount % 2 == 0
                ? new Color32(81, 101, 130, 255)
                : new Color32(249, 201, 96, 255);
            RefreshHud();
        }

        private void OpenMenu()
        {
            menuOpen = true;
            selectedRow = 0;
            controls.SetMenuOpen(true);
            menu.SetActive(true);
            RefreshMenu("Up / Down to select   •   Enter to choose   •   Esc to resume");
        }

        private void Navigate(Vector2 direction)
        {
            var vertical = direction.y;
            if (vertical == 0)
                return;

            var rowCount = bindings.Count + 2;
            selectedRow = (selectedRow + (vertical > 0 ? -1 : 1) + rowCount) % rowCount;
            RefreshMenu("Enter to choose   •   Esc to resume");
        }

        private void Submit()
        {
            if (selectedRow < bindings.Count)
            {
                var selected = bindings[selectedRow];
                if (controls.TryBeginRebind(selected.Action, selected.Binding, ShowRebindResult))
                    RefreshMenu($"Press a key for {selected.Label}   •   Esc to cancel");
                return;
            }

            if (selectedRow == bindings.Count)
            {
                controls.RestoreGameplayDefaults();
                RefreshMenu("Default bindings restored.");
                RefreshHud();
            }
            else
            {
                CloseMenu();
            }
        }

        private void ShowRebindResult(KeyboardRebindResult result)
        {
            switch (result)
            {
                case KeyboardRebindResult.Applied:
                    RefreshMenu("Binding updated.");
                    RefreshHud();
                    break;
                case KeyboardRebindResult.Conflict:
                    RefreshMenu("That key already has a gameplay action.");
                    break;
                case KeyboardRebindResult.Cancelled:
                    RefreshMenu("Rebind cancelled.");
                    break;
            }
        }


        private void CloseMenu()
        {
            menuOpen = false;
            controls.SetMenuOpen(false);
            menu.SetActive(false);
            RefreshHud();
        }

        private void RefreshHud()
        {
            hud.text = $"Move: {move.GetBindingDisplayString()}     " +
                $"Interact: {interact.GetBindingDisplayString(0)}     Pause: {pause.GetBindingDisplayString(0)}\n" +
                $"Beacon toggles: {interactionCount}";
        }

        private void RefreshMenu(string hint)
        {
            var rows = new StringBuilder();
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                AppendRow(rows, i, $"{binding.Label}   [{binding.Action.GetBindingDisplayString(binding.Binding)}]");
            }
            AppendRow(rows, bindings.Count, "Restore defaults");
            AppendRow(rows, bindings.Count + 1, "Resume");
            menuRows.text = rows.ToString();
            menuHint.text = hint;
        }

        private void AppendRow(StringBuilder rows, int index, string text)
        {
            if (index == selectedRow)
                rows.Append("<color=#78E4C1>▸  ").Append(text).AppendLine("</color>");
            else
                rows.Append("     ").AppendLine(text);
        }
    }
}
