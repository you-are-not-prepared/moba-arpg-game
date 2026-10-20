using UnityEngine.UIElements;

namespace Moba.UI
{
    // Cheat menu (right side, under Options) for testing: +1 level, +10000 gold, no mana costs, no cooldowns.
    public partial class Hud
    {
        const int CheatGold = 10000;

        VisualElement _cheatPanel;
        Button _cheatHeader, _cheatLevel, _cheatMana, _cheatCooldowns;

        void SetUpCheats()
        {
            _cheatPanel = _root.Q("cheatPanel");
            _cheatHeader = _root.Q<Button>("cheatHeader");
            _cheatLevel = _root.Q<Button>("cheatLevel");
            _cheatMana = _root.Q<Button>("cheatMana");
            _cheatCooldowns = _root.Q<Button>("cheatCooldowns");
            Button gold = _root.Q<Button>("cheatGold");

            _cheatHeader.clicked += () =>
            {
                bool collapse = !_cheatPanel.ClassListContains("cheats--collapsed");
                _cheatPanel.EnableInClassList("cheats--collapsed", collapse);
                _cheatHeader.text = collapse ? "Cheats [+]" : "Cheats [-]";
            };
            _cheatLevel.clicked += () =>
            {
                MarkCheated();
                _progression?.GainLevel();
            };
            gold.clicked += () =>
            {
                MarkCheated();
                _progression?.AddGold(CheatGold);
            };
            _cheatMana.clicked += () =>
            {
                MarkCheated();
                if (_abilities != null) _abilities.NoManaCost = !_abilities.NoManaCost;
            };
            _cheatCooldowns.clicked += () =>
            {
                MarkCheated();
                if (_abilities != null) _abilities.NoCooldowns = !_abilities.NoCooldowns;
            };

            // Keyboard focus would let Space/Enter re-click the last button while playing.
            _cheatPanel.Query<Button>().ForEach(b => b.focusable = false);
        }

        // A match where any cheat was used is recorded as such and left out of the player's stats.
        void MarkCheated()
        {
            if (_game != null) _game.CheatsUsed = true;
        }

        void UpdateCheats()
        {
            _cheatLevel.SetEnabled(_progression != null && !_progression.IsMaxLevel);
            bool noMana = _abilities != null && _abilities.NoManaCost;
            bool noCooldowns = _abilities != null && _abilities.NoCooldowns;
            _cheatMana.EnableInClassList("cheats__toggle--on", noMana);
            _cheatCooldowns.EnableInClassList("cheats__toggle--on", noCooldowns);
            SetText(_cheatMana, noMana ? "No mana cost: ON" : "No mana cost: OFF");
            SetText(_cheatCooldowns, noCooldowns ? "No cooldowns: ON" : "No cooldowns: OFF");
        }
    }
}
