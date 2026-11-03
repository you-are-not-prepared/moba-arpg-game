using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Ascension choice: at levels 6, 11 and 16 cards appear in the middle of the screen and stay until one is clicked
    // (the game keeps running). Level 6 offers one card per path, each the ultimate it gives; the chosen path decides
    // the talents at 11 and 16, which shape that ultimate and the passive.
    public partial class Hud
    {
        VisualElement _ascensionPanel, _ascensionCards;
        Label _ascensionTitle, _ascensionSubtitle;
        HeroAscension _ascension;
        int _shownAscensionTier = -1;
        // On a game server: the tier whose choice was sent and hasn't come back yet (its cards stay hidden).
        int _sentAscensionTier = -1;

        static readonly string[] TierNumerals = { "I", "II", "III" };

        void SetUpAscension()
        {
            _ascensionPanel = _root.Q("ascension");
            _ascensionCards = _root.Q("ascensionCards");
            _ascensionTitle = _root.Q<Label>("ascensionTitle");
            _ascensionSubtitle = _root.Q<Label>("ascensionSubtitle");
        }

        void UpdateAscension()
        {
            if (_ascension == null && _hero != null) _ascension = _hero.GetComponent<HeroAscension>();
            int tier = _ascension != null && !_game.IsOver ? _ascension.PendingTier : -1;
            if (tier != _sentAscensionTier) _sentAscensionTier = -1;
            else tier = -1;
            _ascensionPanel.EnableInClassList("hidden", tier < 0);
            if (tier == _shownAscensionTier) return;
            _shownAscensionTier = tier;
            if (tier >= 0) BuildAscensionCards(tier);
        }

        void BuildAscensionCards(int tier)
        {
            _ascensionCards.Clear();
            SetText(_ascensionTitle, $"ASCENSION {TierNumerals[tier]}");
            SetText(_ascensionSubtitle, tier == 0
                ? $"Level {HeroAscension.Levels[tier]} · choose your ultimate"
                : $"Level {HeroAscension.Levels[tier]} · {_ascension.PathName} path");

            IReadOnlyList<Talent> options = _ascension.Options(tier);
            for (int i = 0; i < options.Count; i++) _ascensionCards.Add(AscensionCard(options[i], tier, i));
        }

        VisualElement AscensionCard(Talent talent, int tier, int option)
        {
            // Path letter: the option itself on the first choice, the chosen path afterwards.
            int path = tier == 0 ? option : _ascension.PathIndex;
            string letter = AscensionTree.Letter(path);
            string code = tier == 0 ? letter : $"{letter}{(tier - 1) * 2 + option + 1}";

            var card = new VisualElement();
            card.AddToClassList("ascension-card");
            card.AddToClassList($"ascension-card--{letter.ToLowerInvariant()}");

            card.Add(CardText($"PATH {letter}", "ascension-card__badge"));
            card.Add(CardText(code, "ascension-card__glyph"));
            card.Add(CardText(talent.name, "ascension-card__name"));
            AbilityDefinition ultimate = tier == 0 ? _ascension.UltimateOf(option) : null;
            if (tier > 0) card.Add(CardText($"{_ascension.PathName} path", "ascension-card__path"));
            else if (ultimate != null)
            {
                DamageType type = _abilities.DamageTypeOf(ultimate);
                card.Add(CardText(type == DamageType.None ? "ULTIMATE" : $"ULTIMATE · {DamageTypes.Name(type).ToUpperInvariant()}",
                    "ascension-card__path"));
            }
            else card.Add(CardText("Your next two choices follow this path", "ascension-card__path"));

            var effects = new VisualElement { pickingMode = PickingMode.Ignore };
            effects.AddToClassList("ascension-card__effects");
            List<string> lines = talent.EffectLines();
            // The ultimate's numbers at rank 1, as this hero casts it.
            if (ultimate != null)
            {
                SpellModifiers m = SpellModifiers.None;
                m.damageType = _abilities.DamageTypeOf(ultimate);
                m.damageMultiplier = _abilities.AffinityMultiplier(m.damageType) * _abilities.spellDamageMultiplier;
                string numbers = ultimate.Describe(1, m);
                if (!string.IsNullOrEmpty(numbers)) lines.Insert(0, numbers);
            }
            foreach (string line in lines) effects.Add(CardText(line, "ascension-card__effect"));
            card.Add(effects);
            if (!string.IsNullOrEmpty(talent.flavor)) card.Add(CardText(talent.flavor, "ascension-card__flavor"));

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            card.Add(spacer);
            card.Add(CardText("Click to ascend", "ascension-card__hint"));

            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (_hero.Remote != null)
                {
                    _hero.Remote.SendAscend(tier, option);
                    _sentAscensionTier = tier;
                }
                else
                {
                    _ascension.Choose(option);
                }
                _shownAscensionTier = -1; // rebuild for the next due choice, if any
            });
            return card;
        }

        static Label CardText(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        // For the portrait tooltip: "Ascension: Storm of Arrows › Quickdraw".
        string AscensionSummary()
        {
            if (_ascension == null || _ascension.Picked.Count == 0) return "";
            var names = new List<string>();
            foreach (Talent t in _ascension.Picked) names.Add(t.name);
            return "\nAscension: " + string.Join(" › ", names);
        }
    }
}
