using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Moba
{
    // Every game control, built in code so it can be listed and rebound from the options menu.
    // Binding overrides persist in PlayerPrefs.
    public sealed class GameInput
    {
        public enum Category { Abilities, Hero, Camera, Interface, Match }

        public sealed class Entry
        {
            public readonly InputAction Action;
            public readonly string Label;
            public readonly Category Category;
            // Context-only bindings (e.g. Restart on the end screen) may share a key with gameplay controls.
            public readonly bool CheckConflicts;

            public Entry(InputAction action, string label, Category category, bool checkConflicts)
            {
                Action = action;
                Label = label;
                Category = category;
                CheckConflicts = checkConflicts;
            }

            public string EffectivePath => Action.bindings[0].effectivePath;
            public bool IsOverridden => !string.IsNullOrEmpty(Action.bindings[0].overridePath);
        }

        // Controls that aren't rebindable, listed in the menu for reference.
        public static readonly (string label, string binding)[] FixedControls =
        {
            ("Close shop / cancel targeting / options menu", "Esc"),
            ("Zoom camera", "Mouse wheel"),
            ("Pan camera (when unlocked)", "Screen edges"),
            ("Minimap: move camera / move hero", "LMB / RMB on minimap"),
        };

        const string PrefsKey = "moba.input.bindingOverrides";

        static GameInput s_Instance;
        public static GameInput Instance => s_Instance ??= new GameInput();

        public readonly InputActionAsset Asset;
        public readonly InputActionMap Gameplay;
        public readonly InputActionMap Ui;

        public readonly InputAction[] Abilities = new InputAction[4];
        public readonly InputAction LevelUpModifier;
        public readonly InputAction MoveOrAttack;
        public readonly InputAction Select;
        public readonly InputAction AttackMove;
        public readonly InputAction Stop;
        public readonly InputAction CameraLock;
        public readonly InputAction CameraCenter;
        public readonly InputAction PanUp, PanDown, PanLeft, PanRight;
        public readonly InputAction OpenShop;
        public readonly InputAction Scoreboard;
        public readonly InputAction Restart;
        public readonly InputAction Menu;

        readonly List<Entry> _entries = new();
        public IReadOnlyList<Entry> Entries => _entries;

        GameInput()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "GameInput";
            Asset.hideFlags = HideFlags.HideAndDontSave; // survive scene loads / UnloadUnusedAssets
            Gameplay = Asset.AddActionMap("Gameplay");
            Ui = Asset.AddActionMap("UI");

            Abilities[0] = Add("ability1", "Ability 1", Category.Abilities, "<Keyboard>/q");
            Abilities[1] = Add("ability2", "Ability 2", Category.Abilities, "<Keyboard>/w");
            Abilities[2] = Add("ability3", "Ability 3", Category.Abilities, "<Keyboard>/e");
            Abilities[3] = Add("ability4", "Ultimate", Category.Abilities, "<Keyboard>/r");
            LevelUpModifier = Add("levelUpModifier", "Level up ability (hold + ability key)", Category.Abilities, "<Keyboard>/ctrl");

            MoveOrAttack = Add("moveOrAttack", "Move / attack", Category.Hero, "<Mouse>/rightButton");
            Select = Add("select", "Confirm target", Category.Hero, "<Mouse>/leftButton");
            AttackMove = Add("attackMove", "Attack move", Category.Hero, "<Keyboard>/a");
            Stop = Add("stop", "Stop", Category.Hero, "<Keyboard>/s");

            CameraLock = Add("cameraLock", "Toggle camera lock", Category.Camera, "<Keyboard>/y");
            CameraCenter = Add("cameraCenter", "Center camera on hero (hold)", Category.Camera, "<Keyboard>/space");
            PanUp = Add("panUp", "Pan camera up", Category.Camera, "<Keyboard>/upArrow");
            PanDown = Add("panDown", "Pan camera down", Category.Camera, "<Keyboard>/downArrow");
            PanLeft = Add("panLeft", "Pan camera left", Category.Camera, "<Keyboard>/leftArrow");
            PanRight = Add("panRight", "Pan camera right", Category.Camera, "<Keyboard>/rightArrow");

            OpenShop = Add("openShop", "Open / close shop", Category.Interface, "<Keyboard>/b");
            Scoreboard = Add("scoreboard", "Show scoreboard (hold)", Category.Interface, "<Keyboard>/tab");

            Restart = Add("restart", "Restart match (end screen)", Category.Match, "<Keyboard>/r", checkConflicts: false);

            // Esc is also the "cancel" key while rebinding, so it isn't rebindable itself.
            Menu = Ui.AddAction("menu", InputActionType.Button, "<Keyboard>/escape");

            Load();
            Gameplay.Enable();
            Ui.Enable();
        }

        InputAction Add(string id, string label, Category category, string path, bool checkConflicts = true)
        {
            InputAction action = Gameplay.AddAction(id, InputActionType.Button, path);
            action.expectedControlType = "Button";
            _entries.Add(new Entry(action, label, category, checkConflicts));
            return action;
        }

        public static string Display(InputAction action)
        {
            string s = action.GetBindingDisplayString(0);
            return string.IsNullOrEmpty(s) ? "Unbound" : s;
        }

        // Another conflict-checked entry bound to the same control, or null.
        public Entry FindConflict(Entry entry)
        {
            if (!entry.CheckConflicts) return null;
            string path = entry.EffectivePath;
            if (string.IsNullOrEmpty(path)) return null;
            foreach (Entry other in _entries)
            {
                if (other == entry || !other.CheckConflicts) continue;
                if (string.Equals(other.EffectivePath, path, System.StringComparison.OrdinalIgnoreCase)) return other;
            }
            return null;
        }

        // Raised after the bindings were saved (rebinds, resets, or bindings loaded from the account).
        public static event System.Action Saved;

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            Saved?.Invoke();
        }

        // The current binding overrides as JSON (what Save stores; ApplyOverridesJson reads it back).
        public string OverridesJson() => Asset.SaveBindingOverridesAsJson();

        // Replaces all binding overrides with `json` (from SaveBindingOverridesAsJson; empty = defaults), then saves.
        public void ApplyOverridesJson(string json)
        {
            Asset.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(json)) Asset.LoadBindingOverridesFromJson(json);
            Save();
        }

        void Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json)) Asset.LoadBindingOverridesFromJson(json);
        }

        public void ResetAll()
        {
            Asset.RemoveAllBindingOverrides();
            Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Saved = null;
            if (s_Instance == null) return;
            s_Instance.Asset.Disable();
            Object.DestroyImmediate(s_Instance.Asset);
            s_Instance = null;
        }
    }
}
