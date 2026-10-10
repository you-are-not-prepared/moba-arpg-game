using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Esc / HUD "Options" button. Controls tab: rebind every GameInput action (conflicts are swapped),
    // reset per key or all, attack-move quick cast. Interface tab: HUD element sizes. Pauses the match while open.
    [RequireComponent(typeof(UIDocument))]
    public class OptionsMenu : MonoBehaviour
    {
        public static OptionsMenu Instance { get; private set; }
        public static bool IsMenuOpen => Instance != null && Instance.IsOpen;
        public bool IsOpen { get; private set; }

        sealed class BindingRow
        {
            public GameInput.Entry Entry;
            public Button Key;
            public Button Reset;
        }

        sealed class SliderRow
        {
            public Slider Slider;
            public Label Value;
            public Func<float> Get;
        }

        VisualElement _root;
        Button _tabControls, _tabInterface;
        VisualElement _controlsPage, _interfacePage;
        ScrollView _bindingList, _interfaceList;
        Label _status;
        Toggle _quickCast;
        readonly List<BindingRow> _bindingRows = new();
        readonly List<SliderRow> _sliderRows = new();

        InputActionRebindingExtensions.RebindingOperation _rebind;
        BindingRow _rebindRow;
        string _rebindPreviousPath;
        int _ignoreMenuKeyUntilFrame;

        void OnEnable()
        {
            Instance = this;
            VisualElement docRoot = GetComponent<UIDocument>().rootVisualElement;
            docRoot.pickingMode = PickingMode.Ignore;

            _root = docRoot.Q("optionsRoot");
            _tabControls = _root.Q<Button>("tabControls");
            _tabInterface = _root.Q<Button>("tabInterface");
            _controlsPage = _root.Q("controlsPage");
            _interfacePage = _root.Q("interfacePage");
            _bindingList = _root.Q<ScrollView>("bindingList");
            _interfaceList = _root.Q<ScrollView>("interfaceList");
            _status = _root.Q<Label>("bindingStatus");

            _tabControls.clicked += () => ShowTab(controls: true);
            _tabInterface.clicked += () => ShowTab(controls: false);
            _root.Q<Button>("resetBindings").clicked += ResetAllBindings;
            _root.Q<Button>("resetInterface").clicked += () =>
            {
                GameSettings.ResetInterface();
                RefreshInterface();
            };
            _root.Q<Button>("resumeButton").clicked += Close;

            BuildBindingList();
            BuildInterfaceList();

            // Keyboard focus would turn Enter/Space into button presses (and re-trigger rebinding).
            _root.Query<Button>().ForEach(b => b.focusable = false);

            IsOpen = false;
            _root.AddToClassList("hidden");
        }

        void OnDisable()
        {
            CancelRebind();
            if (IsOpen)
            {
                IsOpen = false;
                GameInput.Instance.Gameplay.Enable();
                if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
            }
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_rebind != null || Time.frameCount <= _ignoreMenuKeyUntilFrame) return;
            if (!GameInput.Instance.Menu.WasPressedThisFrame()) return;

            if (IsOpen)
            {
                Close();
                return;
            }
            // Esc first cancels any targeting mode; only an idle Esc opens the menu.
            HeroController hero = GameManager.Instance != null ? GameManager.Instance.hero : null;
            if (hero != null && hero.CancelTargeting()) return;
            Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _root.RemoveFromClassList("hidden");
            SetStatus("Click a key to rebind it.");
            RefreshBindings();
            RefreshInterface();
            GameInput.Instance.Gameplay.Disable();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPaused(true);
                if (GameManager.Instance.hero != null) GameManager.Instance.hero.CancelTargeting();
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            CancelRebind();
            IsOpen = false;
            _root.AddToClassList("hidden");
            GameInput.Instance.Gameplay.Enable();
            GameInput.Instance.Ui.Enable();
            if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
            PlayerPrefs.Save();
            _ignoreMenuKeyUntilFrame = Time.frameCount + 1;
        }

        void ShowTab(bool controls)
        {
            CancelRebind();
            _tabControls.EnableInClassList("options__tab--active", controls);
            _tabInterface.EnableInClassList("options__tab--active", !controls);
            _controlsPage.EnableInClassList("hidden", !controls);
            _interfacePage.EnableInClassList("hidden", controls);
        }

        // ---------- Controls ----------

        void BuildBindingList()
        {
            _bindingList.Clear();
            _bindingRows.Clear();

            AddSection(_bindingList, "GAMEPLAY");
            _quickCast = new Toggle { focusable = false };
            _quickCast.AddToClassList("setting-row__toggle");
            _quickCast.RegisterValueChangedCallback(evt =>
            {
                GameSettings.Current.attackMoveQuickCast = evt.newValue;
                GameSettings.NotifyChanged();
            });
            VisualElement quickRow = SettingRow("Attack move: quick cast at cursor");
            quickRow.Q<Label>().AddToClassList("setting-row__label--grow");
            quickRow.tooltip = "On: attack move fires the moment its key is pressed.\nOff: press the key, then confirm with a click.";
            quickRow.Add(_quickCast);
            _bindingList.Add(quickRow);

            foreach (GameInput.Category category in Enum.GetValues(typeof(GameInput.Category)))
            {
                AddSection(_bindingList, category.ToString().ToUpperInvariant());
                foreach (GameInput.Entry entry in GameInput.Instance.Entries)
                {
                    if (entry.Category != category) continue;
                    var row = new BindingRow { Entry = entry };

                    var element = new VisualElement();
                    element.AddToClassList("binding-row");
                    var label = new Label(entry.Label);
                    label.AddToClassList("binding-row__label");
                    row.Key = new Button(() => StartRebind(row));
                    row.Key.AddToClassList("binding-row__key");
                    row.Reset = new Button(() => ResetBinding(row)) { text = "Reset" };
                    row.Reset.AddToClassList("binding-row__reset");

                    element.Add(label);
                    element.Add(row.Key);
                    element.Add(row.Reset);
                    _bindingList.Add(element);
                    _bindingRows.Add(row);
                }
            }

            AddSection(_bindingList, "FIXED");
            foreach ((string label, string binding) in GameInput.FixedControls)
            {
                var element = new VisualElement();
                element.AddToClassList("binding-row");
                var name = new Label(label);
                name.AddToClassList("binding-row__label");
                var value = new Label(binding);
                value.AddToClassList("binding-row__fixed");
                element.Add(name);
                element.Add(value);
                _bindingList.Add(element);
            }
        }

        void RefreshBindings()
        {
            _quickCast.SetValueWithoutNotify(GameSettings.Current.attackMoveQuickCast);
            foreach (BindingRow row in _bindingRows)
            {
                bool listening = row == _rebindRow;
                row.Key.text = listening ? "Press a key…" : GameInput.Display(row.Entry.Action);
                row.Key.EnableInClassList("binding-row__key--listening", listening);
                row.Key.EnableInClassList("binding-row__key--overridden", !listening && row.Entry.IsOverridden);
                row.Reset.EnableInClassList("binding-row__reset--inactive", !row.Entry.IsOverridden);
            }
        }

        void StartRebind(BindingRow row)
        {
            CancelRebind();
            _rebindRow = row;
            _rebindPreviousPath = row.Entry.EffectivePath;

            // Actions must be disabled while rebinding; this also keeps Esc from closing the menu.
            GameInput.Instance.Asset.Disable();
            RefreshBindings();
            SetStatus($"Press a key or mouse button for \"{row.Entry.Label}\". Esc cancels.");

            _rebind = row.Entry.Action.PerformInteractiveRebinding(0)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Mouse>/scroll/*")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Pointer>/press")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => FinishRebind(completed: true))
                .OnCancel(_ => FinishRebind(completed: false))
                .Start();
        }

        void FinishRebind(bool completed)
        {
            BindingRow row = _rebindRow;
            _rebind?.Dispose();
            _rebind = null;
            _rebindRow = null;
            _ignoreMenuKeyUntilFrame = Time.frameCount + 1;

            if (row != null)
            {
                if (completed)
                {
                    GameInput.Entry entry = row.Entry;
                    ClearOverrideIfDefault(entry.Action);
                    GameInput.Entry conflict = GameInput.Instance.FindConflict(entry);
                    if (conflict != null)
                    {
                        conflict.Action.ApplyBindingOverride(0, _rebindPreviousPath);
                        ClearOverrideIfDefault(conflict.Action);
                        SetStatus($"{GameInput.Display(entry.Action)} was used by \"{conflict.Label}\". " +
                                  $"Swapped: \"{conflict.Label}\" is now {GameInput.Display(conflict.Action)}.");
                    }
                    else
                    {
                        SetStatus($"\"{entry.Label}\" set to {GameInput.Display(entry.Action)}.");
                    }
                    GameInput.Instance.Save();
                }
                else
                {
                    SetStatus("Rebinding cancelled.");
                }
            }

            GameInput.Instance.Ui.Enable();
            if (!IsOpen) GameInput.Instance.Gameplay.Enable();
            RefreshBindings();
        }

        void CancelRebind()
        {
            if (_rebind != null) _rebind.Cancel(); // OnCancel → FinishRebind cleans up
        }

        void ResetBinding(BindingRow row)
        {
            CancelRebind();
            row.Entry.Action.RemoveBindingOverride(0);

            // Resetting can collide with a key another action was moved to; reset those as well.
            var resetChain = new List<string>();
            GameInput.Entry current = row.Entry;
            for (int i = 0; i < 16; i++)
            {
                GameInput.Entry conflict = GameInput.Instance.FindConflict(current);
                if (conflict == null || !conflict.IsOverridden) break;
                conflict.Action.RemoveBindingOverride(0);
                resetChain.Add(conflict.Label);
                current = conflict;
            }

            GameInput.Entry remaining = GameInput.Instance.FindConflict(row.Entry);
            if (remaining != null)
                SetStatus($"\"{row.Entry.Label}\" reset, but {GameInput.Display(row.Entry.Action)} is also used by \"{remaining.Label}\".");
            else if (resetChain.Count > 0)
                SetStatus($"\"{row.Entry.Label}\" reset (also reset: {string.Join(", ", resetChain)}).");
            else
                SetStatus($"\"{row.Entry.Label}\" reset to {GameInput.Display(row.Entry.Action)}.");

            GameInput.Instance.Save();
            RefreshBindings();
        }

        void ResetAllBindings()
        {
            CancelRebind();
            GameInput.Instance.ResetAll();
            SetStatus("All keys reset to defaults.");
            RefreshBindings();
        }

        static void ClearOverrideIfDefault(InputAction action)
        {
            InputBinding binding = action.bindings[0];
            if (!string.IsNullOrEmpty(binding.overridePath)
                && string.Equals(binding.overridePath, binding.path, StringComparison.OrdinalIgnoreCase))
                action.RemoveBindingOverride(0);
        }

        // ---------- Interface ----------

        void BuildInterfaceList()
        {
            _interfaceList.Clear();
            _sliderRows.Clear();

            AddSection(_interfaceList, "HUD SIZE");
            AddSlider("Overall HUD", () => GameSettings.Current.hudScale, v => GameSettings.Current.hudScale = v);
            AddSlider("Hero panel & abilities", () => GameSettings.Current.heroPanelScale, v => GameSettings.Current.heroPanelScale = v);
            AddSlider("Minimap", () => GameSettings.Current.minimapScale, v => GameSettings.Current.minimapScale = v);
            AddSlider("Stats bar (gold, CS, timer)", () => GameSettings.Current.statsBarScale, v => GameSettings.Current.statsBarScale = v);
            AddSlider("Unit health bars", () => GameSettings.Current.healthBarScale, v => GameSettings.Current.healthBarScale = v);
            AddSlider("Gold / XP pop-ups", () => GameSettings.Current.popupTextScale, v => GameSettings.Current.popupTextScale = v);
        }

        void AddSlider(string label, Func<float> get, Action<float> set)
        {
            VisualElement row = SettingRow(label);
            var slider = new Slider(GameSettings.MinScale, GameSettings.MaxScale) { focusable = false };
            slider.AddToClassList("setting-row__slider");
            var value = new Label();
            value.AddToClassList("setting-row__value");
            var sliderRow = new SliderRow { Slider = slider, Value = value, Get = get };

            slider.RegisterValueChangedCallback(evt =>
            {
                float v = Mathf.Round(evt.newValue * 20f) / 20f; // 5% steps
                set(v);
                value.text = $"{v * 100f:0}%";
                GameSettings.NotifyChanged();
            });

            row.Add(slider);
            row.Add(value);
            _interfaceList.Add(row);
            _sliderRows.Add(sliderRow);
        }

        void RefreshInterface()
        {
            foreach (SliderRow row in _sliderRows)
            {
                float v = row.Get();
                row.Slider.SetValueWithoutNotify(v);
                row.Value.text = $"{v * 100f:0}%";
            }
        }

        // ---------- Helpers ----------

        static VisualElement SettingRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("setting-row");
            var name = new Label(label);
            name.AddToClassList("setting-row__label");
            row.Add(name);
            return row;
        }

        static void AddSection(VisualElement parent, string title)
        {
            var header = new Label(title);
            header.AddToClassList("options__section");
            parent.Add(header);
        }

        void SetStatus(string text) => _status.text = text;
    }
}
