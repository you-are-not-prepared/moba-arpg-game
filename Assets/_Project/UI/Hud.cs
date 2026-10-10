using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // In-match HUD (UI Toolkit): stats bar, hero panel with Q/W/E/R slots, health bars over units,
    // bounty pop-ups, minimap, targeting hint, respawn timer and end-of-match screen.
    [RequireComponent(typeof(UIDocument))]
    public class Hud : MonoBehaviour
    {
        const float FloatingTextLifetime = 1.1f;
        const float FloatingTextRise = 45f;

        static Hud s_Instance;

        UIDocument _document;
        VisualElement _root;
        VisualElement _worldLayer;
        VisualElement _heroPanel;
        Label _gold, _cs, _kda, _time, _wave;
        Label _level, _skillPoints;
        ResourceBar _health, _mana;
        VisualElement _xpFill;
        Label _targetingHint, _respawn;
        VisualElement _tooltip;
        Label _tooltipTitle, _tooltipStats, _tooltipBody;
        VisualElement _gameOver;
        Label _gameOverTitle;
        Minimap _minimap;
        readonly SlotView[] _slots = new SlotView[AbilityCaster.SlotCount];
        int _hoveredSlot = -1;

        GameManager _game;
        HeroController _hero;
        HeroProgression _progression;
        AbilityCaster _abilities;
        Camera _camera;

        readonly Dictionary<Unit, UnitBar> _bars = new();
        readonly Stack<UnitBar> _barPool = new();
        readonly List<Unit> _staleBars = new();
        readonly List<FloatingText> _floating = new();

        // True when the screen position (Input System coordinates, origin bottom-left) is over HUD chrome,
        // so world clicks there should be ignored.
        public static bool IsPointerOverHud(Vector2 screenPosition)
        {
            if (s_Instance == null || s_Instance._root == null) return false;
            IPanel panel = s_Instance._root.panel;
            if (panel == null) return false;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            VisualElement picked = panel.Pick(panelPos);
            return picked != null && picked != panel.visualTree && picked != s_Instance._document.rootVisualElement
                   && picked != s_Instance._root;
        }

        void OnEnable()
        {
            s_Instance = this;
            _document = GetComponent<UIDocument>();
            VisualElement docRoot = _document.rootVisualElement;
            docRoot.pickingMode = PickingMode.Ignore;

            _root = docRoot.Q("hudRoot");
            _worldLayer = _root.Q("worldLayer");
            _heroPanel = _root.Q("heroPanel");
            _gold = _root.Q<Label>("goldLabel");
            _cs = _root.Q<Label>("csLabel");
            _kda = _root.Q<Label>("kdaLabel");
            _time = _root.Q<Label>("timeLabel");
            _wave = _root.Q<Label>("waveLabel");
            _level = _root.Q<Label>("levelLabel");
            _skillPoints = _root.Q<Label>("skillPointsLabel");
            _health = new ResourceBar(_root.Q("healthBar"));
            _mana = new ResourceBar(_root.Q("manaBar"));
            _xpFill = _root.Q("xpBar").Q("fill");
            _targetingHint = _root.Q<Label>("targetingHint");
            _respawn = _root.Q<Label>("respawnLabel");
            _tooltip = _root.Q("tooltip");
            _tooltipTitle = _root.Q<Label>("tooltipTitle");
            _tooltipStats = _root.Q<Label>("tooltipStats");
            _tooltipBody = _root.Q<Label>("tooltipBody");
            _gameOver = _root.Q("gameOverOverlay");
            _gameOverTitle = _root.Q<Label>("gameOverTitle");
            _root.Q<Button>("restartButton").clicked += () => _game?.Restart();

            for (int i = 0; i < AbilityCaster.SlotCount; i++)
            {
                int slot = i;
                var view = new SlotView(_root.Q($"slot{AbilityCaster.SlotKeys[i]}"), AbilityCaster.SlotKeys[i]);
                view.Frame.RegisterCallback<ClickEvent>(_ => _hero?.BeginCast(slot));
                view.Frame.RegisterCallback<PointerEnterEvent>(_ => _hoveredSlot = slot);
                view.Frame.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredSlot == slot) _hoveredSlot = -1;
                });
                view.LevelUp.clicked += () => _abilities?.TryLevelUp(slot);
                _slots[i] = view;
            }

            _minimap = new Minimap();
            _root.Q("minimapFrame").Add(_minimap);

            _bars.Clear();
            _barPool.Clear();
            _floating.Clear();
        }

        void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
            Unbind();
        }

        void Start() => Bind();

        void Bind()
        {
            _game = GameManager.Instance;
            _hero = _game != null ? _game.hero : null;
            if (_hero == null) return;
            _progression = _hero.GetComponent<HeroProgression>();
            _abilities = _hero.Abilities;
            if (_progression != null) _progression.BountyEarned += OnBountyEarned;
        }

        void Unbind()
        {
            if (_progression != null) _progression.BountyEarned -= OnBountyEarned;
        }

        void LateUpdate()
        {
            if (_root == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_game == null || _hero == null) Bind();
            if (_game == null || _hero == null) return;

            UpdateStats();
            UpdateHeroPanel();
            UpdateSlots();
            UpdateTooltip();
            UpdateUnitBars();
            UpdateFloatingText();
            UpdateOverlays();
            _minimap.MarkDirtyRepaint();
        }

        // ---------- Top bar ----------

        void UpdateStats()
        {
            if (_progression != null)
            {
                SetText(_gold, $"{_progression.Gold}g");
                SetText(_cs, $"CS {_progression.CreepScore}");
                SetText(_kda, $"{_progression.Kills} / {_progression.Deaths}");
            }
            int seconds = Mathf.FloorToInt(_game.MatchTime);
            SetText(_time, $"{seconds / 60:00}:{seconds % 60:00}");
            if (_game.playerSpawner != null)
                SetText(_wave, $"Next wave {Mathf.CeilToInt(_game.playerSpawner.NextWaveIn)}s");
        }

        // ---------- Hero panel ----------

        void UpdateHeroPanel()
        {
            Unit unit = _hero.Unit;
            _heroPanel.EnableInClassList("hero-panel--dead", !unit.IsAlive);
            _health.Set(unit.Health, unit.maxHealth);
            if (_abilities != null) _mana.Set(_abilities.Mana, _abilities.maxMana);

            if (_progression != null)
            {
                SetText(_level, _progression.Level.ToString());
                float xpPct = _progression.IsMaxLevel ? 100f : 100f * _progression.Xp / _progression.XpToNextLevel;
                _xpFill.style.width = Length.Percent(xpPct);
            }

            int points = _abilities != null ? _abilities.SkillPoints : 0;
            _skillPoints.EnableInClassList("hidden", points <= 0);
            if (points > 0) SetText(_skillPoints, $"+{points}");
        }

        void UpdateSlots()
        {
            if (_abilities == null) return;
            for (int i = 0; i < _slots.Length; i++) _slots[i].Refresh(_abilities, i, _hero.TargetingSlot == i);
        }

        void UpdateTooltip()
        {
            if (_hoveredSlot < 0 || _abilities == null)
            {
                _tooltip.AddToClassList("hidden");
                return;
            }

            AbilityCaster.Slot slot = _abilities.GetSlot(_hoveredSlot);
            AbilityDefinition a = slot.ability;
            string key = AbilityCaster.SlotKeys[_hoveredSlot];
            _tooltip.RemoveFromClassList("hidden");

            if (a == null)
            {
                SetText(_tooltipTitle, $"[{key}] Empty slot");
                SetText(_tooltipStats, "No ability assigned.");
                SetText(_tooltipBody, "");
                return;
            }

            int shownRank = Mathf.Max(1, slot.rank);
            string stats = $"Rank {slot.rank}/{a.maxRank}   Cooldown {a.Cooldown(shownRank):0.#}s   Cost {a.ManaCost(shownRank):0} mana";
            if (a.range > 0f && a.targeting != AbilityTargeting.Instant) stats += $"   Range {a.range:0.#}";
            if (_abilities.CanLevelUp(_hoveredSlot)) stats += $"\nCtrl+{key} or + to level up";
            else if (slot.rank < a.maxRank) stats += $"\nNext rank at level {a.RequiredLevel(slot.rank + 1)}";

            SetText(_tooltipTitle, $"[{key}] {a.displayName}");
            SetText(_tooltipStats, stats);
            SetText(_tooltipBody, a.description ?? "");
        }

        // ---------- World-space health bars ----------

        void UpdateUnitBars()
        {
            if (_camera == null) return;
            IPanel panel = _worldLayer.panel;
            if (panel == null) return;
            Unit self = _hero.Unit;

            foreach (Unit u in Unit.All)
            {
                if (!_bars.TryGetValue(u, out UnitBar bar))
                {
                    bar = _barPool.Count > 0 ? _barPool.Pop() : new UnitBar(_worldLayer);
                    bar.Bind(u, u == self, u.team == _game.playerTeam);
                    _bars.Add(u, bar);
                }

                Vector3 world = u.transform.position + Vector3.up * u.barHeight;
                bool visible = u.IsAlive && _camera.WorldToViewportPoint(world).z > 0f;
                bar.SetVisible(visible);
                if (!visible) continue;

                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, _camera);
                bar.Update(p, u, u == self && _progression != null ? _progression.Level : 0);
            }

            // Release bars whose units were destroyed or disabled.
            _staleBars.Clear();
            foreach (KeyValuePair<Unit, UnitBar> pair in _bars)
                if (pair.Key == null || !pair.Key.isActiveAndEnabled) _staleBars.Add(pair.Key);
            foreach (Unit u in _staleBars)
            {
                UnitBar bar = _bars[u];
                _bars.Remove(u);
                bar.SetVisible(false);
                _barPool.Push(bar);
            }
        }

        // ---------- Bounty pop-ups ----------

        void OnBountyEarned(int gold, float xp, Vector3 worldPos)
        {
            if (gold > 0) SpawnFloatingText($"+{gold}g", worldPos + Vector3.up * 1.5f, xp: false);
            if (xp > 0f) SpawnFloatingText($"+{xp:0} xp", worldPos + Vector3.up * 0.6f, xp: true);
        }

        void SpawnFloatingText(string text, Vector3 worldPos, bool xp)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("floating-text");
            label.EnableInClassList("floating-text--xp", xp);
            _worldLayer.Add(label);
            _floating.Add(new FloatingText { Label = label, World = worldPos });
        }

        void UpdateFloatingText()
        {
            IPanel panel = _worldLayer.panel;
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                FloatingText f = _floating[i];
                f.Age += Time.unscaledDeltaTime;
                if (f.Age >= FloatingTextLifetime || panel == null || _camera == null)
                {
                    f.Label.RemoveFromHierarchy();
                    _floating.RemoveAt(i);
                    continue;
                }
                float t = f.Age / FloatingTextLifetime;
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, f.World, _camera);
                f.Label.style.left = p.x;
                f.Label.style.top = p.y - t * FloatingTextRise;
                f.Label.style.opacity = 1f - t * t;
                _floating[i] = f;
            }
        }

        // ---------- Overlays ----------

        void UpdateOverlays()
        {
            bool dead = !_hero.Unit.IsAlive;
            _respawn.EnableInClassList("hidden", !dead || _game.IsOver);
            if (dead) SetText(_respawn, $"Respawning in {Mathf.CeilToInt(_hero.RespawnRemaining)}");

            int targeting = _hero.TargetingSlot;
            _targetingHint.EnableInClassList("hidden", targeting < 0);
            if (targeting >= 0 && _abilities != null)
            {
                AbilityDefinition a = _abilities.GetSlot(targeting).ability;
                string what = a.targeting == AbilityTargeting.Unit ? "a target" : "a location";
                SetText(_targetingHint, $"{a.displayName}: left-click {what}   ·   right-click / Esc to cancel");
            }

            _gameOver.EnableInClassList("hidden", !_game.IsOver);
            if (_game.IsOver)
            {
                bool won = _game.Winner == _game.playerTeam;
                SetText(_gameOverTitle, won ? "VICTORY" : "DEFEAT");
                _gameOver.EnableInClassList("game-over--victory", won);
                _gameOver.EnableInClassList("game-over--defeat", !won);
            }
        }

        static void SetText(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        // ---------- Views ----------

        sealed class ResourceBar
        {
            readonly VisualElement _fill;
            readonly Label _text;
            int _shownCurrent = -1, _shownMax = -1;

            public ResourceBar(VisualElement root)
            {
                _fill = root.Q("fill");
                _text = root.Q<Label>("text");
            }

            public void Set(float current, float max)
            {
                int c = Mathf.CeilToInt(current), m = Mathf.RoundToInt(max);
                _fill.style.width = Length.Percent(max > 0f ? 100f * current / max : 0f);
                if (c == _shownCurrent && m == _shownMax) return;
                _shownCurrent = c;
                _shownMax = m;
                _text.text = $"{c} / {m}";
            }
        }

        sealed class SlotView
        {
            public readonly VisualElement Frame;
            public readonly Button LevelUp;
            readonly VisualElement _root, _icon, _cooldown, _pips;
            readonly Label _cooldownText, _cost;
            AbilityDefinition _shownAbility;
            int _shownMaxRank = -1, _shownRank = -1;

            public SlotView(VisualElement container, string key)
            {
                _root = container.Q("slot");
                Frame = _root.Q("frame");
                LevelUp = _root.Q<Button>("levelUp");
                _icon = _root.Q("icon");
                _cooldown = _root.Q("cooldown");
                _pips = _root.Q("pips");
                _cooldownText = _root.Q<Label>("cooldownText");
                _cost = _root.Q<Label>("cost");
                _root.Q<Label>("key").text = key;
            }

            public void Refresh(AbilityCaster caster, int index, bool targeting)
            {
                AbilityCaster.Slot slot = caster.GetSlot(index);
                AbilityDefinition a = slot.ability;

                if (a != _shownAbility)
                {
                    _shownAbility = a;
                    _icon.style.backgroundImage = a != null && a.icon != null ? new StyleBackground(a.icon) : new StyleBackground(StyleKeyword.None);
                }

                int maxRank = a != null ? a.maxRank : 0;
                if (maxRank != _shownMaxRank || slot.rank != _shownRank) RebuildPips(maxRank, slot.rank);

                CastBlock block = caster.GetCastBlock(index);
                _root.EnableInClassList("ability-slot--empty", a == null);
                _root.EnableInClassList("ability-slot--unlearned", a != null && slot.rank == 0);
                _root.EnableInClassList("ability-slot--no-mana", block == CastBlock.NotEnoughMana);
                _root.EnableInClassList("ability-slot--ready", block == CastBlock.None);
                _root.EnableInClassList("ability-slot--targeting", targeting);
                _root.EnableInClassList("ability-slot--can-level", caster.CanLevelUp(index));

                if (slot.cooldownRemaining > 0f && slot.cooldownTotal > 0f)
                {
                    _cooldown.style.height = Length.Percent(100f * slot.cooldownRemaining / slot.cooldownTotal);
                    SetText(_cooldownText, slot.cooldownRemaining >= 1f
                        ? Mathf.CeilToInt(slot.cooldownRemaining).ToString()
                        : slot.cooldownRemaining.ToString("0.0"));
                }
                else
                {
                    _cooldown.style.height = 0f;
                    SetText(_cooldownText, "");
                }

                float cost = a != null && slot.rank > 0 ? a.ManaCost(slot.rank) : 0f;
                SetText(_cost, cost > 0f ? cost.ToString("0") : "");
            }

            void RebuildPips(int maxRank, int rank)
            {
                _shownMaxRank = maxRank;
                _shownRank = rank;
                _pips.Clear();
                for (int i = 0; i < maxRank; i++)
                {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                    pip.AddToClassList("pip");
                    pip.EnableInClassList("pip--filled", i < rank);
                    _pips.Add(pip);
                }
            }
        }

        sealed class UnitBar
        {
            readonly VisualElement _root;
            readonly VisualElement _fill;
            readonly Label _level;
            bool _visible = true;
            int _shownLevel = -1;

            public UnitBar(VisualElement layer)
            {
                _root = new VisualElement { pickingMode = PickingMode.Ignore };
                _root.AddToClassList("unit-bar");
                _fill = new VisualElement { pickingMode = PickingMode.Ignore };
                _fill.AddToClassList("unit-bar__fill");
                _root.Add(_fill);
                _level = new Label { pickingMode = PickingMode.Ignore };
                _level.AddToClassList("unit-bar__level");
                _root.Add(_level);
                layer.Add(_root);
            }

            public void Bind(Unit unit, bool isSelf, bool isAlly)
            {
                _root.EnableInClassList("unit-bar--hero", unit.kind == UnitKind.Hero);
                _root.EnableInClassList("unit-bar--tower", unit.kind == UnitKind.Tower);
                _root.EnableInClassList("unit-bar--core", unit.kind == UnitKind.Core);
                _root.EnableInClassList("unit-bar--self", isSelf);
                _root.EnableInClassList("unit-bar--ally", isAlly && !isSelf);
                _level.EnableInClassList("hidden", unit.kind != UnitKind.Hero);
                _shownLevel = -1;
            }

            public void SetVisible(bool visible)
            {
                if (visible == _visible) return;
                _visible = visible;
                _root.EnableInClassList("hidden", !visible);
            }

            public void Update(Vector2 panelPos, Unit unit, int level)
            {
                _root.style.left = panelPos.x;
                _root.style.top = panelPos.y;
                _fill.style.width = Length.Percent(100f * unit.Health / unit.maxHealth);
                _root.EnableInClassList("unit-bar--protected", !unit.IsTargetable);
                if (level > 0 && level != _shownLevel)
                {
                    _shownLevel = level;
                    _level.text = level.ToString();
                }
            }
        }

        struct FloatingText
        {
            public Label Label;
            public Vector3 World;
            public float Age;
        }
    }
}
