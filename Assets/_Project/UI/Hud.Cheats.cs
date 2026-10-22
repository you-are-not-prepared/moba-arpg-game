using UnityEngine.UIElements;

namespace Moba.UI
{
    // Cheat menu (right side, under Options) for testing: +1 level, +10000 gold, no mana costs, no cooldowns,
    // and game speed (1x / 2x / 4x / 8x). On a game server that allows cheats the buttons go to the server
    // (game speed stays offline only: it would speed up everyone's match).
    public partial class Hud
    {
        public const int CheatGold = 10000;
        static readonly float[] CheatSpeeds = { 1f, 2f, 4f, 8f };

        VisualElement _cheatPanel;
        Button _cheatHeader, _cheatLevel, _cheatMana, _cheatCooldowns, _cheatSpeed;

        void SetUpCheats()
        {
            _cheatPanel = _root.Q("cheatPanel");
            _cheatHeader = _root.Q<Button>("cheatHeader");
            _cheatLevel = _root.Q<Button>("cheatLevel");
            _cheatMana = _root.Q<Button>("cheatMana");
            _cheatCooldowns = _root.Q<Button>("cheatCooldowns");
            _cheatSpeed = _root.Q<Button>("cheatSpeed");
            Button gold = _root.Q<Button>("cheatGold");

            _cheatHeader.clicked += () =>
            {
                bool collapse = !_cheatPanel.ClassListContains("cheats--collapsed");
                _cheatPanel.EnableInClassList("cheats--collapsed", collapse);
                _cheatHeader.text = collapse ? "Cheats [+]" : "Cheats [-]";
            };
            _cheatLevel.clicked += () =>
            {
                if (SendCheat(Net.CheatAction.Level)) return;
                MarkCheated();
                _progression?.GainLevel();
            };
            gold.clicked += () =>
            {
                if (SendCheat(Net.CheatAction.Gold)) return;
                MarkCheated();
                _progression?.AddGold(CheatGold);
            };
            _cheatMana.clicked += () =>
            {
                if (SendCheat(Net.CheatAction.NoManaCost)) return;
                MarkCheated();
                if (_abilities != null) _abilities.NoManaCost = !_abilities.NoManaCost;
            };
            _cheatCooldowns.clicked += () =>
            {
                if (SendCheat(Net.CheatAction.NoCooldowns)) return;
                MarkCheated();
                if (_abilities != null) _abilities.NoCooldowns = !_abilities.NoCooldowns;
            };
            // Each click steps to the next speed, wrapping back to 1x.
            _cheatSpeed.clicked += () =>
            {
                if (_game == null) return;
                MarkCheated();
                int current = System.Array.IndexOf(CheatSpeeds, _game.GameSpeed);
                _game.SetGameSpeed(CheatSpeeds[(current + 1) % CheatSpeeds.Length]);
            };

            // Keyboard focus would let Space/Enter re-click the last button while playing.
            _cheatPanel.Query<Button>().ForEach(b => b.focusable = false);
        }

        // On a game server the cheat runs there (which marks the match as cheated). True if it was sent.
        bool SendCheat(Net.CheatAction cheat)
        {
            if (_hero == null || _hero.Remote == null) return false;
            _hero.Remote.SendCheat(cheat);
            return true;
        }

        // A match where any cheat was used is recorded as such and left out of the player's stats.
        void MarkCheated()
        {
            if (_game != null) _game.CheatsUsed = true;
        }

        void UpdateCheats()
        {
            // On a game server only if it allows cheats, and without game speed.
            bool remote = Net.NetSession.IsRemoteClient;
            _cheatPanel.EnableInClassList("hidden", remote && !Net.NetworkMatch.CheatsAllowed);
            _cheatSpeed.EnableInClassList("hidden", remote);
            _cheatLevel.SetEnabled(_progression != null && !_progression.IsMaxLevel);
            bool noMana = _abilities != null && _abilities.NoManaCost;
            bool noCooldowns = _abilities != null && _abilities.NoCooldowns;
            float speed = _game != null ? _game.GameSpeed : 1f;
            _cheatMana.EnableInClassList("cheats__toggle--on", noMana);
            _cheatCooldowns.EnableInClassList("cheats__toggle--on", noCooldowns);
            _cheatSpeed.EnableInClassList("cheats__toggle--on", speed > 1f);
            SetText(_cheatMana, noMana ? "No mana cost: ON" : "No mana cost: OFF");
            SetText(_cheatCooldowns, noCooldowns ? "No cooldowns: ON" : "No cooldowns: OFF");
            SetText(_cheatSpeed, $"Game speed: {speed:0.#}x");
        }
    }
}
