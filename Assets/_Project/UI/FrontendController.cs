using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Start screen and champion select. Champion select rolls a random map layout and shows it while you pick;
    // locking in loads that layout's scene with the chosen hero (see MatchSettings / MatchBootstrap).
    [RequireComponent(typeof(UIDocument))]
    public class FrontendController : MonoBehaviour
    {
        public HeroRoster roster;
        public MapLayoutCatalog layouts;

        VisualElement _startScreen, _selectScreen, _heroList;
        Label _mapName, _mapDescription, _status;
        Button _lockButton;
        LanePreview _preview;
        readonly List<(HeroDefinition hero, VisualElement card)> _cards = new();
        HeroDefinition _selected;
        MapLayoutInfo _layout;

        void OnEnable()
        {
            Time.timeScale = 1f;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _startScreen = root.Q("startScreen");
            _selectScreen = root.Q("selectScreen");
            _heroList = root.Q("heroList");
            _mapName = root.Q<Label>("mapName");
            _mapDescription = root.Q<Label>("mapDescription");
            _status = root.Q<Label>("selectStatus");
            _lockButton = root.Q<Button>("lockButton");

            root.Q<Button>("playButton").clicked += ShowChampionSelect;
            root.Q<Button>("quitButton").clicked += Quit;
            root.Q<Button>("backButton").clicked += ShowStart;
            _lockButton.clicked += LockIn;
            root.Query<Button>().ForEach(b => b.focusable = false);

            _preview = new LanePreview();
            root.Q("mapPreview").Add(_preview);
            BuildHeroCards();

            if (MatchSettings.OpenChampionSelect)
            {
                MatchSettings.OpenChampionSelect = false;
                ShowChampionSelect();
            }
            else
            {
                ShowStart();
            }
        }

        void ShowStart()
        {
            _startScreen.RemoveFromClassList("hidden");
            _selectScreen.AddToClassList("hidden");
        }

        void ShowChampionSelect()
        {
            _startScreen.AddToClassList("hidden");
            _selectScreen.RemoveFromClassList("hidden");

            // Every visit to champion select rolls a new map.
            _layout = layouts != null && layouts.layouts.Count > 0
                ? layouts.layouts[Random.Range(0, layouts.layouts.Count)]
                : null;
            _mapName.text = _layout != null ? _layout.displayName : "No map";
            _mapDescription.text = _layout != null ? _layout.description : "";
            _preview.Show(_layout);

            HeroDefinition previous = roster.Find(MatchSettings.SelectedHeroId);
            Select(previous);
        }

        void BuildHeroCards()
        {
            _heroList.Clear();
            _cards.Clear();
            foreach (HeroDefinition hero in roster.heroes)
            {
                if (hero == null) continue;
                VisualElement card = HeroCard(hero);
                card.RegisterCallback<ClickEvent>(_ => Select(hero));
                _heroList.Add(card);
                _cards.Add((hero, card));
            }
        }

        static VisualElement HeroCard(HeroDefinition hero)
        {
            var card = new VisualElement();
            card.AddToClassList("hero-card");

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("hero-card__top");
            var glyph = Text(hero.glyph, "hero-card__glyph");
            // Hero colour is data, so it's set here rather than in USS.
            glyph.style.backgroundColor = hero.color;
            top.Add(glyph);
            var names = new VisualElement { pickingMode = PickingMode.Ignore };
            names.Add(Text(hero.displayName, "hero-card__name"));
            names.Add(Text(hero.title, "hero-card__title"));
            top.Add(names);
            card.Add(top);

            card.Add(Text(hero.StatsLine, "hero-card__stats"));
            if (!string.IsNullOrEmpty(hero.description)) card.Add(Text(hero.description, "hero-card__text"));

            card.Add(Text("PASSIVE", "hero-card__section"));
            card.Add(Text(hero.passiveName, "hero-card__ability"));
            card.Add(Text(hero.passiveDescription, "hero-card__text"));

            if (hero.ultimate != null)
            {
                card.Add(Text("ULTIMATE (R)", "hero-card__section"));
                card.Add(Text(hero.ultimate.displayName, "hero-card__ability"));
                card.Add(Text(hero.ultimate.description, "hero-card__text"));
            }
            return card;
        }

        static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        void Select(HeroDefinition hero)
        {
            _selected = hero;
            foreach ((HeroDefinition h, VisualElement card) in _cards)
                card.EnableInClassList("hero-card--selected", h == hero);
            _lockButton.SetEnabled(hero != null && _layout != null);
            _status.text = hero != null ? $"{hero.displayName} selected" : "Pick a hero";
        }

        void LockIn()
        {
            if (_selected == null || _layout == null) return;
            MatchSettings.SelectedHeroId = _selected.id;
            MatchSettings.LayoutId = _layout.id;
            _status.text = $"Loading {_layout.displayName}…";
            _lockButton.SetEnabled(false);
            SceneManager.LoadScene(_layout.sceneName);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
