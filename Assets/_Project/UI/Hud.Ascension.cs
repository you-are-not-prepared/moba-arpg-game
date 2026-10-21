using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Ascension choice: at levels 6, 11 and 18 two talent cards appear in the middle of the screen and stay
    // until one is clicked (the game keeps running). The first pick (A or B) decides the later cards.
    public partial class Hud
    {
        VisualElement _ascensionPanel, _ascensionCards;
        Label _ascensionTitle, _ascensionSubtitle;
        HeroAscension _ascension;
        int _shownAscensionTier = -1;

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
                ? $"Level {HeroAscension.Levels[tier]} · choose your path"
                : $"Level {HeroAscension.Levels[tier]} · {_ascension.PathName} path");

            (Talent first, Talent second) = _ascension.Options(tier);
            _ascensionCards.Add(AscensionCard(first, tier, 0));
            _ascensionCards.Add(AscensionCard(second, tier, 1));
        }

        VisualElement AscensionCard(Talent talent, int tier, int option)
        {
            // Path letter: the option itself on the first choice, the chosen path afterwards.
            int path = tier == 0 ? option : _ascension.PathIndex;
            string letter = path == 0 ? "A" : "B";
            string code = tier == 0 ? letter : $"{letter}{(tier - 1) * 2 + option + 1}";

            var card = new VisualElement();
            card.AddToClassList("ascension-card");
            card.AddToClassList(path == 0 ? "ascension-card--a" : "ascension-card--b");

            card.Add(CardText($"PATH {letter}", "ascension-card__badge"));
            card.Add(CardText(code, "ascension-card__glyph"));
            card.Add(CardText(talent.name, "ascension-card__name"));
            if (tier > 0) card.Add(CardText($"{_ascension.PathName} path", "ascension-card__path"));
            else card.Add(CardText("Your next two choices follow this path", "ascension-card__path"));

            var effects = new VisualElement { pickingMode = PickingMode.Ignore };
            effects.AddToClassList("ascension-card__effects");
            List<string> lines = talent.EffectLines();
            foreach (string line in lines) effects.Add(CardText(line, "ascension-card__effect"));
            card.Add(effects);
            if (!string.IsNullOrEmpty(talent.flavor)) card.Add(CardText(talent.flavor, "ascension-card__flavor"));

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            card.Add(spacer);
            card.Add(CardText("Click to ascend", "ascension-card__hint"));

            card.RegisterCallback<ClickEvent>(_ =>
            {
                _ascension.Choose(option);
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
