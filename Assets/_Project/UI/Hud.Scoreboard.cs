using System.Collections.Generic;
using Moba.Abilities;
using Moba.Bots;
using Moba.Items;
using Moba.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Scoreboard, shown while the scoreboard key (Tab) is held: every hero in the match with level, K/D/A,
    // creep score, the four spells (with their mod sockets) and the inventory. Gold is deliberately left out.
    public partial class Hud
    {
        VisualElement _scoreboard, _scoreAllyRows, _scoreEnemyRows;
        Label _scoreAllyKills, _scoreEnemyKills, _scoreTime;
        readonly List<ScoreRow> _scoreRows = new();

        void SetUpScoreboard()
        {
            _scoreboard = _root.Q("scoreboard");
            _scoreAllyRows = _root.Q("scoreAllyRows");
            _scoreEnemyRows = _root.Q("scoreEnemyRows");
            _scoreAllyKills = _root.Q<Label>("scoreAllyKills");
            _scoreEnemyKills = _root.Q<Label>("scoreEnemyKills");
            _scoreTime = _root.Q<Label>("scoreTime");
        }

        void UpdateScoreboard()
        {
            bool show = GameInput.Instance.Scoreboard.IsPressed() && !OptionsMenu.IsMenuOpen;
            _scoreboard.EnableInClassList("hidden", !show);
            if (!show) return;

            if (_scoreRows.Count != _game.heroes.Count) BuildScoreRows();
            int allyKills = 0, enemyKills = 0;
            foreach (ScoreRow row in _scoreRows)
            {
                row.Refresh();
                if (row.Hero.Unit.team == _game.playerTeam) allyKills += row.Kills;
                else enemyKills += row.Kills;
            }
            SetText(_scoreAllyKills, allyKills.ToString());
            SetText(_scoreEnemyKills, enemyKills.ToString());
            int seconds = Mathf.FloorToInt(_game.MatchTime);
            SetText(_scoreTime, $"{seconds / 60:00}:{seconds % 60:00}");
        }

        void BuildScoreRows()
        {
            _scoreAllyRows.Clear();
            _scoreEnemyRows.Clear();
            _scoreRows.Clear();
            foreach (HeroController hero in _game.heroes)
            {
                if (hero == null) continue;
                var row = new ScoreRow(hero, PlayerLabel(hero), hero == _game.hero);
                (hero.Unit.team == _game.playerTeam ? _scoreAllyRows : _scoreEnemyRows).Add(row.Root);
                _scoreRows.Add(row);
            }
        }

        string PlayerLabel(HeroController hero)
        {
            if (hero == _game.hero)
                return AccountService.IsLoggedIn ? $"{AccountService.Profile.username} (you)" : "You";
            return hero.GetComponent<BotBrain>() != null ? "Bot" : "Player";
        }

        sealed class ScoreRow
        {
            public readonly HeroController Hero;
            public readonly VisualElement Root;
            readonly HeroProgression _progression;
            readonly AbilityCaster _abilities;
            readonly Inventory _inventory;
            readonly Label _respawn, _level, _kda, _cs;
            readonly SpellCell[] _spells = new SpellCell[AbilityCaster.SlotCount];
            readonly Tile[] _items = new Tile[Inventory.SlotCount];

            public int Kills => _progression != null ? _progression.Kills : 0;

            public ScoreRow(HeroController hero, string player, bool isSelf)
            {
                Hero = hero;
                _progression = hero.GetComponent<HeroProgression>();
                _abilities = hero.Abilities;
                _inventory = hero.GetComponent<Inventory>();

                Root = Element("score-row");
                Root.EnableInClassList("score-row--self", isSelf);

                HeroDefinition def = hero.definition;
                VisualElement heroCell = Element("score-hero");
                VisualElement portrait = Element("score-portrait");
                // Hero colour is data, so it's set here rather than in USS.
                portrait.style.backgroundColor = def != null ? def.color : Color.gray;
                portrait.Add(Text(def != null ? def.glyph : "?", "score-portrait__glyph"));
                _respawn = Text("", "score-portrait__respawn");
                portrait.Add(_respawn);
                heroCell.Add(portrait);
                VisualElement names = Element(null);
                names.Add(Text(def != null ? def.displayName : hero.name, "score-hero__name"));
                names.Add(Text(player, "score-hero__player"));
                heroCell.Add(names);
                Root.Add(heroCell);

                _level = Cell("score-col--level");
                _kda = Cell("score-col--kda");
                _cs = Cell("score-col--cs");

                VisualElement spells = Element("score-spells");
                for (int i = 0; i < _spells.Length; i++)
                {
                    _spells[i] = new SpellCell();
                    spells.Add(_spells[i].Root);
                }
                Root.Add(spells);

                VisualElement items = Element("score-items");
                for (int i = 0; i < _items.Length; i++)
                {
                    _items[i] = new Tile("score-tile--item");
                    items.Add(_items[i].Root);
                }
                Root.Add(items);
            }

            Label Cell(string column)
            {
                Label label = Text("", "score-cell");
                label.AddToClassList(column);
                Root.Add(label);
                return label;
            }

            public void Refresh()
            {
                bool dead = !Hero.Unit.IsAlive;
                Root.EnableInClassList("score-row--dead", dead);
                _respawn.EnableInClassList("hidden", !dead);
                if (dead) SetText(_respawn, Mathf.CeilToInt(Hero.RespawnRemaining).ToString());

                if (_progression != null)
                {
                    SetText(_level, _progression.Level.ToString());
                    SetText(_kda, $"{_progression.Kills} / {_progression.Deaths} / {_progression.Assists}");
                    SetText(_cs, _progression.CreepScore.ToString());
                }

                for (int i = 0; i < _spells.Length; i++) _spells[i].Show(_abilities, i);
                for (int i = 0; i < _items.Length; i++) _items[i].Show(_inventory != null ? _inventory.Get(i) : null);
            }
        }

        // A spell icon with its mod sockets underneath.
        sealed class SpellCell
        {
            public readonly VisualElement Root;
            readonly Tile _tile;
            readonly VisualElement _sockets;
            readonly VisualElement[] _dots = new VisualElement[AbilityCaster.MaxModSockets];

            public SpellCell()
            {
                Root = Element("score-spell");
                _tile = new Tile("score-tile--spell");
                Root.Add(_tile.Root);
                _sockets = Element("score-sockets");
                for (int i = 0; i < _dots.Length; i++)
                {
                    _dots[i] = Element("score-socket");
                    _sockets.Add(_dots[i]);
                }
                Root.Add(_sockets);
            }

            public void Show(AbilityCaster caster, int slot)
            {
                AbilityDefinition ability = caster.GetSlot(slot).ability;
                _tile.Show(ability);
                int sockets = ability != null ? caster.SocketCount(slot) : 0;
                for (int i = 0; i < _dots.Length; i++)
                {
                    _dots[i].EnableInClassList("hidden", i >= sockets);
                    _dots[i].EnableInClassList("score-socket--filled", i < sockets && caster.GetMod(slot, i) != null);
                }
            }
        }

        // A small coloured square with an item's or spell's glyph; dark when empty.
        sealed class Tile
        {
            public readonly VisualElement Root;
            readonly Label _glyph;
            InventoryItem _shown;
            bool _initialized;

            public Tile(string sizeClass)
            {
                Root = Element("score-tile");
                Root.AddToClassList(sizeClass);
                _glyph = Text("", "score-tile__glyph");
                Root.Add(_glyph);
            }

            public void Show(InventoryItem item)
            {
                if (_initialized && item == _shown) return;
                _initialized = true;
                _shown = item;
                _glyph.text = item != null ? item.glyph : "";
                // Per-item colour is data; clearing it falls back to the USS tile colour.
                Root.style.backgroundColor = item != null ? new StyleColor(item.color) : new StyleColor(StyleKeyword.Null);
            }
        }

        static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            if (className != null) element.AddToClassList(className);
            return element;
        }

        static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
