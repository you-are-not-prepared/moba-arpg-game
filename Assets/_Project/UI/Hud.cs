using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // In-match HUD (UI Toolkit): stats bar, hero panel with Q/W/E/R slots, health bars over units,
    // bounty pop-ups, minimap, targeting hint, respawn timer and end-of-match screen.
    // Inventory, shop and item drag-and-drop live in Hud.Inventory.cs.
    [RequireComponent(typeof(UIDocument))]
    public partial class Hud : MonoBehaviour
    {
        const float FloatingTextLifetime = 1.1f;
        const float FloatingTextRise = 45f;
        const float MinimapBaseSize = 250f;
        const float BottomMargin = 12f;

        static Hud s_Instance;
        public static Hud Instance => s_Instance;

        UIDocument _document;
        VisualElement _root;
        VisualElement _worldLayer;
        VisualElement _heroPanel;
        VisualElement _statsPanel;
        VisualElement _minimapFrame;
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
        bool _hoveredPortrait;
        Label _portraitGlyph;
        VisualElement _portrait;

        GameManager _game;
        HeroController _hero;
        HeroProgression _progression;
        AbilityCaster _abilities;
        Camera _camera;

        readonly Dictionary<Unit, UnitBar> _bars = new();
        readonly Stack<UnitBar> _barPool = new();
        readonly List<Unit> _staleBars = new();
        readonly List<FloatingText> _floating = new();

        float _barScale = 1f;
        float _popupScale = 1f;
        float _heroPanelScale = 1f;

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
            _statsPanel = _root.Q("statsPanel");
            _minimapFrame = _root.Q("minimapFrame");
            _gold = _root.Q<Label>("goldLabel");
            _cs = _root.Q<Label>("csLabel");
            _kda = _root.Q<Label>("kdaLabel");
            _time = _root.Q<Label>("timeLabel");
            _wave = _root.Q<Label>("waveLabel");
            _level = _root.Q<Label>("levelLabel");
            _portrait = _root.Q("portrait");
            _portraitGlyph = _root.Q<Label>("portraitInitial");
            _portrait.RegisterCallback<PointerEnterEvent>(_ => _hoveredPortrait = true);
            _portrait.RegisterCallback<PointerLeaveEvent>(_ => _hoveredPortrait = false);
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
            _root.Q<Button>("continueButton").clicked += () => _game?.ShowMatchSummary();
            Button menuButton = _root.Q<Button>("menuButton");
            menuButton.focusable = false;
            menuButton.clicked += () => OptionsMenu.Instance?.Open();

            for (int i = 0; i < AbilityCaster.SlotCount; i++)
            {
                int slot = i;
                var view = new SlotView(_root.Q($"slot{AbilityCaster.SlotKeys[i]}"));
                view.Frame.RegisterCallback<PointerEnterEvent>(_ => _hoveredSlot = slot);
                view.Frame.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredSlot == slot) _hoveredSlot = -1;
                });
                view.LevelUp.clicked += () => _abilities?.TryLevelUp(slot);
                view.LevelUp.focusable = false;
                _slots[i] = view;
                RegisterSpellSlotDragAndDrop(view, slot);
            }

            _minimap = new Minimap();
            _minimapFrame.Add(_minimap);
            SetUpInventoryAndShop();
            SetUpCheats();
            SetUpScoreboard();
            SetUpAscension();

            _bars.Clear();
            _barPool.Clear();
            _floating.Clear();

            GameSettings.Changed += ApplySettings;
            ApplySettings();
        }

        void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
            GameSettings.Changed -= ApplySettings;
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
            if (_hero.definition != null)
            {
                _portraitGlyph.text = _hero.definition.glyph;
                // Hero colour is data, so it's set here rather than in USS.
                _portraitGlyph.style.color = _hero.definition.color;
            }
            if (_progression != null) _progression.BountyEarned += OnBountyEarned;
            BindInventory();
        }

        void Unbind()
        {
            if (_progression != null) _progression.BountyEarned -= OnBountyEarned;
        }

        // Interface sizes from the options menu. The overall HUD scale multiplies every element.
        void ApplySettings()
        {
            GameSettings s = GameSettings.Current;
            float hud = s.hudScale;
            _heroPanelScale = hud * s.heroPanelScale;
            _heroPanel.style.scale = new Scale(Vector2.one * _heroPanelScale);
            _statsPanel.style.scale = new Scale(Vector2.one * (hud * s.statsBarScale));
            float minimap = MinimapBaseSize * hud * s.minimapScale;
            _minimapFrame.style.width = minimap;
            _minimapFrame.style.height = minimap;
            _barScale = hud * s.healthBarScale;
            _popupScale = hud * s.popupTextScale;
            foreach (UnitBar bar in _bars.Values) bar.SetScale(_barScale);
            foreach (UnitBar bar in _barPool) bar.SetScale(_barScale);
            _tooltip.style.scale = new Scale(Vector2.one * hud);
            _targetingHint.style.scale = new Scale(Vector2.one * hud);
            _respawn.style.scale = new Scale(Vector2.one * hud);
            _shopWindow.style.scale = new Scale(Vector2.one * hud);
            _hudMessage.style.scale = new Scale(Vector2.one * hud);
            _cheatPanel.style.scale = new Scale(Vector2.one * hud);
            _scoreboard.style.scale = new Scale(Vector2.one * hud);
            _ascensionPanel.style.scale = new Scale(Vector2.one * hud);
        }

        // Keeps the tooltip and targeting hint just above the (scaled) hero panel.
        void PositionAboveHeroPanel()
        {
            float panelHeight = _heroPanel.layout.height;
            if (float.IsNaN(panelHeight)) return;
            float top = BottomMargin + panelHeight * _heroPanelScale + 36f * _heroPanelScale;
            _tooltip.style.bottom = top;
            _targetingHint.style.bottom = top;
            _hudMessage.style.bottom = top + 30f * _heroPanelScale;
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
            UpdateInventoryAndShop();
            UpdateCheats();
            UpdateScoreboard();
            UpdateAscension();
            UpdateTooltip();
            UpdateUnitBars();
            UpdateFloatingText();
            UpdateOverlays();
            PositionAboveHeroPanel();
            _minimap.Refresh();
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
            GameInput input = GameInput.Instance;
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].Refresh(_abilities, i, _hero.TargetingSlot == i, GameInput.Display(input.Abilities[i]));
        }

        void UpdateTooltip()
        {
            if (ShowHeroTooltip() || ShowSocketTooltip() || ShowItemTooltip()) return;
            if (_hoveredSlot < 0 || _abilities == null || _dragFrom.Kind != DragKind.None)
            {
                _tooltip.AddToClassList("hidden");
                return;
            }

            int index = _hoveredSlot;
            AbilityCaster.Slot slot = _abilities.GetSlot(index);
            AbilityDefinition a = slot.ability;
            GameInput input = GameInput.Instance;
            string key = GameInput.Display(input.Abilities[index]);
            int maxRank = AbilityCaster.MaxRank(index);
            _tooltip.RemoveFromClassList("hidden");

            string rankLine = $"Rank {slot.rank}/{maxRank}";
            if (_abilities.CanLevelUp(index)) rankLine += $"   ·   {GameInput.Display(input.LevelUpModifier)}+{key} or + to level up";
            else if (slot.rank < maxRank) rankLine += $"   ·   next rank at level {AbilityCaster.RequiredLevel(index, slot.rank + 1)}";

            if (a == null)
            {
                SetText(_tooltipTitle, $"[{key}] Empty spell slot");
                SetText(_tooltipStats, rankLine);
                SetText(_tooltipBody, "Buy a spell in the shop, then drag it here from your inventory.\n" + DescribeMods(index));
                return;
            }

            int shownRank = Mathf.Max(1, slot.rank);
            string effect = a.Describe(shownRank, _abilities.BuildModifiers(index));
            string stats = $"{rankLine}\nCooldown {a.Cooldown(shownRank):0.#}s   ·   {a.ManaCost(shownRank):0} mana";
            if (!string.IsNullOrEmpty(effect)) stats += $"\n{effect}";

            string suffix = _abilities.IsSlotLocked(index) ? (a.IsPassive ? " (passive ultimate)" : " (ultimate)") : "";
            SetText(_tooltipTitle, $"[{key}] {a.displayName}{suffix}");
            SetText(_tooltipStats, stats);
            SetText(_tooltipBody, string.IsNullOrEmpty(a.description) ? DescribeMods(index) : $"{a.description}\n{DescribeMods(index)}");
        }

        bool ShowHeroTooltip()
        {
            HeroDefinition def = _hero != null ? _hero.definition : null;
            if (!_hoveredPortrait || def == null) return false;
            _tooltip.RemoveFromClassList("hidden");
            SetText(_tooltipTitle, string.IsNullOrEmpty(def.title) ? def.displayName : $"{def.displayName}, {def.title}");
            SetText(_tooltipStats, $"Passive: {def.passiveName}");
            string ult = def.ultimate != null ? $"\nUltimate: {def.ultimate.displayName}. {def.ultimate.description}" : "";
            SetText(_tooltipBody, def.passiveDescription + ult + AscensionSummary());
            return true;
        }

        string DescribeMods(int index)
        {
            var parts = new List<string>();
            int empty = 0;
            for (int s = 0; s < _abilities.SocketCount(index); s++)
            {
                ModDefinition mod = _abilities.GetMod(index, s);
                if (mod == null) empty++;
                else parts.Add(_abilities.IsModActive(index, s) ? mod.displayName : $"{mod.displayName} (inactive)");
            }
            string list = parts.Count > 0 ? string.Join(", ", parts) : "none";
            return empty > 0 ? $"Mods: {list}  ({empty} free socket{(empty == 1 ? "" : "s")})" : $"Mods: {list}";
        }

        // ---------- World-space health bars ----------

        void UpdateUnitBars()
        {
            if (_camera == null) return;
            IPanel panel = _worldLayer.panel;
            if (panel == null) return;
            Unit self = _hero.Unit;
            _hero.TryGetComponent(out IsolationPassive isolation);

            foreach (Unit u in Unit.All)
            {
                if (!_bars.TryGetValue(u, out UnitBar bar))
                {
                    if (_barPool.Count > 0)
                    {
                        bar = _barPool.Pop();
                    }
                    else
                    {
                        bar = new UnitBar(_worldLayer);
                        bar.SetScale(_barScale);
                    }
                    bar.Bind(u, u == self, u.team == _game.playerTeam);
                    _bars.Add(u, bar);
                }

                Vector3 world = u.transform.position + Vector3.up * u.barHeight;
                bool visible = u.IsAlive && u.IsVisibleToPlayer && _camera.WorldToViewportPoint(world).z > 0f;
                bar.SetVisible(visible);
                if (!visible) continue;

                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, _camera);
                int level = u.kind == UnitKind.Hero && u.TryGetComponent(out HeroProgression progression) ? progression.Level : 0;
                // An isolation passive marks enemy heroes it would deal bonus damage to.
                bool isolated = isolation != null && u.kind == UnitKind.Hero && u.IsEnemyOf(self) && isolation.IsIsolated(u);
                bar.Update(p, u, level, isolated);
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
            label.style.scale = new Scale(Vector2.one * _popupScale);
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

            GameInput input = GameInput.Instance;
            string confirm = GameInput.Display(input.Select);
            string cancel = $"{GameInput.Display(input.MoveOrAttack)} / Esc to cancel";
            int targeting = _hero.TargetingSlot;
            bool attackMove = _hero.IsAttackMoveTargeting;
            _targetingHint.EnableInClassList("hidden", targeting < 0 && !attackMove);
            if (targeting >= 0 && _abilities != null)
            {
                AbilityDefinition a = _abilities.GetSlot(targeting).ability;
                string what = a.targeting == AbilityTargeting.Unit ? "a target" : "a location";
                SetText(_targetingHint, $"{a.displayName}: {confirm} {what}   ·   {cancel}");
            }
            else if (attackMove)
            {
                SetText(_targetingHint, $"Attack move: {confirm} near an enemy or a location   ·   {cancel}");
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

        static void SetText(TextElement label, string text)
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
            public readonly VisualElement[] Sockets = new VisualElement[AbilityCaster.MaxModSockets];
            readonly VisualElement _root, _icon, _cooldown, _pips;
            readonly Label _cooldownText, _cost, _glyph;
            AbilityDefinition _shownAbility;
            int _shownMaxRank = -1, _shownRank = -1;

            readonly Label _key;

            public SlotView(VisualElement container)
            {
                _root = container.Q("slot");
                Frame = _root.Q("frame");
                LevelUp = _root.Q<Button>("levelUp");
                _icon = _root.Q("icon");
                _cooldown = _root.Q("cooldown");
                _pips = _root.Q("pips");
                _cooldownText = _root.Q<Label>("cooldownText");
                _cost = _root.Q<Label>("cost");
                _key = _root.Q<Label>("key");
                _glyph = _root.Q<Label>("glyph");
                for (int i = 0; i < Sockets.Length; i++) Sockets[i] = _root.Q($"socket{i}");
            }

            public VisualElement DropArea => _root;

            public void SetDropTarget(bool on) => _root.EnableInClassList("ability-slot--drop-target", on);

            public void Refresh(AbilityCaster caster, int index, bool targeting, string keyLabel)
            {
                SetText(_key, keyLabel);
                AbilityCaster.Slot slot = caster.GetSlot(index);
                AbilityDefinition a = slot.ability;

                if (a != _shownAbility)
                {
                    _shownAbility = a;
                    bool hasIcon = a != null && a.icon != null;
                    _icon.style.backgroundImage = hasIcon ? new StyleBackground(a.icon) : new StyleBackground(StyleKeyword.None);
                    _glyph.text = a != null && !hasIcon ? a.glyph : "";
                    // Per-spell colour is data; clearing it falls back to the USS slot colour.
                    Frame.style.backgroundColor = a != null && !hasIcon ? new StyleColor(a.color) : new StyleColor(StyleKeyword.Null);
                }

                int socketCount = caster.SocketCount(index);
                for (int s = 0; s < Sockets.Length; s++)
                {
                    Sockets[s].EnableInClassList("hidden", s >= socketCount);
                    bool filled = caster.GetMod(index, s) != null;
                    bool active = caster.IsModActive(index, s);
                    Sockets[s].EnableInClassList("mod-socket--filled", filled && active);
                    Sockets[s].EnableInClassList("mod-socket--inactive", filled && !active);
                }

                int maxRank = AbilityCaster.MaxRank(index);
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

            public void SetScale(float scale) => _root.style.scale = new Scale(Vector2.one * scale);

            public void SetVisible(bool visible)
            {
                if (visible == _visible) return;
                _visible = visible;
                _root.EnableInClassList("hidden", !visible);
            }

            public void Update(Vector2 panelPos, Unit unit, int level, bool isolated)
            {
                _root.EnableInClassList("unit-bar--isolated", isolated);
                _root.style.left = panelPos.x;
                _root.style.top = panelPos.y;
                _fill.style.width = Length.Percent(100f * unit.Health / unit.maxHealth);
                _root.EnableInClassList("unit-bar--protected", !unit.IsTargetable);
                _root.EnableInClassList("unit-bar--burning", Burning.IsBurning(unit));
                _root.EnableInClassList("unit-bar--stunned", Stun.IsStunned(unit));
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
