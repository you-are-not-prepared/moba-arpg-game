using System.Collections.Generic;
using Moba.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Start screen, mode choice (PvE / PvP) and champion select.
    // PvE: champion select rolls a random map and shows every slot of both teams; the bots pick random heroes one
    // after another (never a hero their own team already has). Locking in loads the map with the lineup (see
    // MatchSettings / MatchBootstrap). PvP: the same screen shows the game server's queue and champion select
    // (FrontendController.Lobby).
    [RequireComponent(typeof(UIDocument))]
    public partial class FrontendController : MonoBehaviour
    {
        const float FirstBotPickDelay = 0.6f;
        const float MinBotPickGap = 0.5f;
        const float MaxBotPickGap = 1.2f;

        public HeroRoster roster;
        public MapLayoutCatalog layouts;
        [Tooltip("For item and spell icons in match details.")]
        public Items.ItemCatalog items;

        sealed class Seat
        {
            public Team Team;
            public bool IsLocal;
            public bool IsBot;
            public int BotNumber;
            // PvP: another player's seat (-1 otherwise), their name, and whether their pick is locked in.
            public long ClientId = -1;
            public string Name;
            public bool Locked;
            public HeroDefinition Hero;
            public float PickAt;
            public VisualElement Root;
            public Label Glyph, Who, HeroName;
        }

        VisualElement _loginScreen, _loginForm, _registerForm;
        Label _loginHeading, _loginMessage, _accountLabel;
        Button _loginSubmit, _loginSwitch, _playOffline, _accountButton;
        TextField _loginName, _loginPassword, _registerEmail, _registerUsername, _registerPassword, _registerConfirm;
        bool _registerMode, _busy;
        VisualElement _startScreen, _selectScreen, _startMenu, _modeMenu;
        VisualElement _heroGrid, _heroDetail, _allyList, _enemyList;
        Label _mapName, _mapDescription, _status, _modeLabel;
        Button _lockButton;
        LanePreview _preview;
        readonly List<(HeroDefinition hero, VisualElement tile, Label note)> _tiles = new();
        readonly List<Seat> _seats = new();
        HeroDefinition _selected, _hovered, _shownDetail;
        bool _detailDirty = true;
        MapLayoutInfo _layout;

        Seat LocalSeat => _seats.Find(s => s.IsLocal);

        void OnEnable()
        {
            Time.timeScale = 1f;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _startScreen = root.Q("startScreen");
            _selectScreen = root.Q("selectScreen");
            _startMenu = root.Q("startMenu");
            _modeMenu = root.Q("modeMenu");
            _heroGrid = root.Q("heroGrid");
            _heroDetail = root.Q("heroDetail");
            _allyList = root.Q("allyList");
            _enemyList = root.Q("enemyList");
            _mapName = root.Q<Label>("mapName");
            _mapDescription = root.Q<Label>("mapDescription");
            _status = root.Q<Label>("selectStatus");
            _modeLabel = root.Q<Label>("modeLabel");
            _lockButton = root.Q<Button>("lockButton");

            root.Q<Button>("findMatchButton").clicked += ShowModeMenu;
            root.Q<Button>("quitButton").clicked += Quit;
            root.Q<Button>("modeBackButton").clicked += ShowStart;
            root.Q("pveCard").RegisterCallback<ClickEvent>(_ => StartChampionSelect(MatchMode.PvE));
            root.Q("pvpCard").RegisterCallback<ClickEvent>(_ => StartPvP());
            root.Q<Button>("backButton").clicked += () =>
            {
                if (MatchSettings.Mode == MatchMode.PvP) LeaveLobby();
                ShowModeMenu();
            };
            _lockButton.clicked += LockIn;
            root.Query<Button>().ForEach(b => b.focusable = false);

            _preview = new LanePreview();
            root.Q("mapPreview").Add(_preview);
            BuildHeroTiles();
            SetUpLogin(root);
            SetUpHistory(root);
            SetUpItemTooltip(root);
            SetUpPostMatch(root);
            SetUpLobby(root);
            SetUpUpdater(root);
            Net.GameClient.ConnectFailed += OnConnectFailed;
            // Dropped from a game server mid-match: say why on the start screen.
            var startMessage = root.Q<Label>("startMessage");
            startMessage.text = Net.GameClient.LastError ?? "";
            startMessage.EnableInClassList("hidden", Net.GameClient.LastError == null);
            Net.GameClient.LastError = null;

            if (PostMatch.ShowOnMenu && PostMatch.Last != null)
            {
                PostMatch.ShowOnMenu = false;
                ShowPostMatch(PostMatch.Last);
            }
            else if (AccountService.IsLoggedIn || AccountService.PlayingOffline)
            {
                ShowStart();
            }
            else if (AccountService.HasSavedSession)
            {
                _ = ResumeSession();
            }
            else
            {
                ShowLogin();
            }
        }

        void OnDisable()
        {
            AccountService.Changed -= RefreshAccountBar;
            Net.GameClient.ConnectFailed -= OnConnectFailed;
            TearDownLobby();
        }

        // ---------- Login / register ----------

        void SetUpLogin(VisualElement root)
        {
            _loginScreen = root.Q("loginScreen");
            _loginForm = root.Q("loginForm");
            _registerForm = root.Q("registerForm");
            _loginHeading = root.Q<Label>("loginHeading");
            _loginMessage = root.Q<Label>("loginMessage");
            _loginSubmit = root.Q<Button>("loginSubmit");
            _loginSwitch = root.Q<Button>("loginSwitch");
            _playOffline = root.Q<Button>("playOfflineButton");
            _loginName = root.Q<TextField>("loginName");
            _loginPassword = root.Q<TextField>("loginPassword");
            _registerEmail = root.Q<TextField>("registerEmail");
            _registerUsername = root.Q<TextField>("registerUsername");
            _registerPassword = root.Q<TextField>("registerPassword");
            _registerConfirm = root.Q<TextField>("registerConfirm");
            _accountLabel = root.Q<Label>("accountLabel");
            _accountButton = root.Q<Button>("accountButton");

            _loginSubmit.clicked += () => _ = Submit();
            _loginSwitch.clicked += () => SetRegisterMode(!_registerMode);
            _playOffline.clicked += PlayOffline;
            _accountButton.clicked += () => _ = OnAccountButton();
            // Enter submits from any field.
            root.Q("loginCard").RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter)) return;
                e.StopPropagation();
                _ = Submit();
            }, TrickleDown.TrickleDown);

            AccountService.Changed += RefreshAccountBar;
            RefreshAccountBar();
        }

        void ShowLogin()
        {
            ShowOnly(_loginScreen);
            SetRegisterMode(_registerMode);
            ShowMessage(null);
            (_registerMode ? _registerEmail : _loginName).Focus();
        }

        void SetRegisterMode(bool register)
        {
            _registerMode = register;
            _loginForm.EnableInClassList("hidden", register);
            _registerForm.EnableInClassList("hidden", !register);
            _loginHeading.text = register ? "Create an account" : "Log in";
            _loginSubmit.text = register ? "Create account" : "Log in";
            _loginSwitch.text = register ? "Already have an account? Log in" : "No account yet? Create one";
            ShowMessage(null);
        }

        async System.Threading.Tasks.Task Submit()
        {
            if (_busy) return;
            string error = _registerMode ? CheckRegisterFields() : CheckLoginFields();
            if (error != null)
            {
                ShowMessage(error);
                return;
            }

            SetBusy(true, _registerMode ? "Creating your account…" : "Logging in…");
            error = _registerMode
                ? await AccountService.Register(_registerEmail.value.Trim(), _registerUsername.value.Trim(), _registerPassword.value)
                : await AccountService.Login(_loginName.value.Trim(), _loginPassword.value);
            if (this == null) return; // the scene changed while waiting
            SetBusy(false, null);

            if (error != null)
            {
                ShowMessage(error);
                return;
            }
            _loginPassword.value = _registerPassword.value = _registerConfirm.value = "";
            ShowStart();
        }

        string CheckLoginFields()
        {
            if (string.IsNullOrWhiteSpace(_loginName.value)) return "Enter your email or username.";
            if (string.IsNullOrEmpty(_loginPassword.value)) return "Enter your password.";
            return null;
        }

        // Quick checks before asking the server (which checks everything again).
        string CheckRegisterFields()
        {
            if (string.IsNullOrWhiteSpace(_registerEmail.value) || !_registerEmail.value.Contains('@'))
                return "Enter a valid email address.";
            string username = _registerUsername.value.Trim();
            if (username.Length < 3) return "Usernames need at least 3 characters.";
            if (_registerPassword.value.Length < 8) return "Passwords need at least 8 characters.";
            if (_registerPassword.value != _registerConfirm.value) return "The passwords don't match.";
            return null;
        }

        // A session remembered from an earlier run: check it with the server before showing the menu.
        async System.Threading.Tasks.Task ResumeSession()
        {
            ShowLogin();
            SetBusy(true, "Signing in…");
            string error = await AccountService.Resume();
            if (this == null) return;
            SetBusy(false, null);
            if (error == null) ShowStart();
            else ShowMessage(error);
        }

        void PlayOffline()
        {
            if (_busy) return;
            AccountService.PlayingOffline = true;
            RefreshAccountBar();
            ShowStart();
        }

        async System.Threading.Tasks.Task OnAccountButton()
        {
            if (AccountService.IsLoggedIn) await AccountService.Logout();
            if (this == null) return;
            AccountService.PlayingOffline = false;
            SetRegisterMode(false);
            ShowLogin();
        }

        void RefreshAccountBar()
        {
            if (_accountLabel == null) return;
            bool loggedIn = AccountService.IsLoggedIn;
            _accountLabel.text = loggedIn ? $"Logged in as <b>{AccountService.Profile.username}</b>" : "Playing offline";
            _accountButton.text = loggedIn ? "Log out" : "Log in";
            // Match history lives on the account.
            _historyButton?.EnableInClassList("hidden", !loggedIn);
        }

        void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _loginSubmit.SetEnabled(!busy);
            _loginSwitch.SetEnabled(!busy);
            _playOffline.SetEnabled(!busy);
            if (busy) ShowMessage(status, info: true);
        }

        void ShowMessage(string message, bool info = false)
        {
            _loginMessage.text = message ?? "";
            _loginMessage.EnableInClassList("hidden", string.IsNullOrEmpty(message));
            _loginMessage.EnableInClassList("login__message--info", info);
        }

        void ShowOnly(VisualElement screen)
        {
            _loginScreen.EnableInClassList("hidden", screen != _loginScreen);
            _startScreen.EnableInClassList("hidden", screen != _startScreen);
            _selectScreen.EnableInClassList("hidden", screen != _selectScreen);
            _historyScreen.EnableInClassList("hidden", screen != _historyScreen);
            _postMatchScreen.EnableInClassList("hidden", screen != _postMatchScreen);
            HideItemTooltip(_itemTooltipAnchor);
            RefreshUpdateBar(screen);
        }

        void Update()
        {
            if (_selectScreen.ClassListContains("hidden")) return;

            // PvE: bots lock in one after another. PvP: the server's queue and champion select.
            if (MatchSettings.Mode == MatchMode.PvE)
            {
                foreach (Seat seat in _seats)
                    if (seat.IsBot && seat.Hero == null && Time.unscaledTime >= seat.PickAt) PickForBot(seat);
            }
            else
            {
                UpdateLobbyStatus();
            }

            RefreshSeats();
            RefreshTiles();
            if (_detailDirty || _shownDetail != (_hovered ?? _selected)) RefreshDetail();
        }

        // ---------- Screens ----------

        void ShowStart()
        {
            ShowOnly(_startScreen);
            _startMenu.RemoveFromClassList("hidden");
            _modeMenu.AddToClassList("hidden");
            RefreshAccountBar();
        }

        void ShowModeMenu()
        {
            ShowOnly(_startScreen);
            _startMenu.AddToClassList("hidden");
            _modeMenu.RemoveFromClassList("hidden");
        }

        void StartChampionSelect(MatchMode mode)
        {
            MatchSettings.Mode = mode;
            ShowOnly(_selectScreen);
            _modeLabel.text = $"PvE · {MatchSettings.TeamSize}v{MatchSettings.TeamSize} against bots";

            // Every visit rolls a new map.
            _layout = layouts != null && layouts.layouts.Count > 0
                ? layouts.layouts[Random.Range(0, layouts.layouts.Count)]
                : null;
            _mapName.text = _layout != null ? _layout.displayName : "No map";
            _mapDescription.text = _layout != null ? _layout.description : "";
            _preview.Show(_layout);

            BuildSeats(mode);
            _hovered = null;
            Select(roster.Find(MatchSettings.SelectedHeroId));
        }

        // ---------- Seats (both teams) ----------

        void BuildSeats(MatchMode mode)
        {
            _seats.Clear();
            _allyList.Clear();
            _enemyList.Clear();
            int botNumber = 0;
            var bots = new List<Seat>();

            for (int t = 0; t < 2; t++)
            {
                Team team = t == 0 ? Team.Blue : Team.Red;
                for (int i = 0; i < MatchSettings.TeamSize; i++)
                {
                    bool local = team == Team.Blue && i == 0;
                    var seat = new Seat
                    {
                        Team = team,
                        IsLocal = local,
                        IsBot = !local && mode == MatchMode.PvE,
                    };
                    if (seat.IsBot)
                    {
                        seat.BotNumber = ++botNumber;
                        bots.Add(seat);
                    }
                    CreateSeatView(seat, ally: team == Team.Blue);
                    (team == Team.Blue ? _allyList : _enemyList).Add(seat.Root);
                    _seats.Add(seat);
                }
            }

            // Bots pick in a random order, a moment apart.
            for (int i = bots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bots[i], bots[j]) = (bots[j], bots[i]);
            }
            float at = Time.unscaledTime + FirstBotPickDelay;
            foreach (Seat bot in bots)
            {
                bot.PickAt = at;
                at += Random.Range(MinBotPickGap, MaxBotPickGap);
            }
        }

        void CreateSeatView(Seat seat, bool ally)
        {
            seat.Root = new VisualElement();
            seat.Root.AddToClassList("team-slot");
            seat.Root.AddToClassList(ally ? "team-slot--ally" : "team-slot--enemy");
            seat.Root.EnableInClassList("team-slot--local", seat.IsLocal);
            seat.Root.EnableInClassList("team-slot--open", !seat.IsLocal && !seat.IsBot && seat.ClientId < 0);

            seat.Glyph = Text("", "team-slot__glyph");
            seat.Root.Add(seat.Glyph);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            seat.Who = Text(seat.IsLocal ? "YOU" : seat.IsBot ? $"BOT {seat.BotNumber}"
                : seat.ClientId >= 0 ? (seat.Name ?? "PLAYER").ToUpperInvariant() : "OPEN SLOT", "team-slot__who");
            seat.HeroName = Text("", "team-slot__hero");
            texts.Add(seat.Who);
            texts.Add(seat.HeroName);
            seat.Root.Add(texts);
        }

        void RefreshSeats()
        {
            foreach (Seat seat in _seats)
            {
                HeroDefinition hero = seat.IsLocal ? _selected : seat.Hero;
                bool picking = seat.IsLocal || seat.IsBot || seat.ClientId >= 0;
                string name = hero != null ? hero.displayName
                    : picking ? "Picking…"
                    : "Waiting for a player";
                if (seat.HeroName.text != name) seat.HeroName.text = name;
                seat.HeroName.EnableInClassList("team-slot__hero--pending", hero == null);
                string glyph = hero != null ? hero.glyph : picking ? "?" : "";
                if (seat.Glyph.text != glyph) seat.Glyph.text = glyph;
                // Hero colour is data, so it's set here rather than in USS.
                seat.Glyph.style.backgroundColor = hero != null ? new StyleColor(hero.color) : new StyleColor(StyleKeyword.Null);
            }
        }

        // A random hero nobody on the bot's team has (the local player's current pick counts for the blue team).
        void PickForBot(Seat bot)
        {
            var options = new List<HeroDefinition>();
            foreach (HeroDefinition hero in roster.heroes)
                if (hero != null && !TakenByTeam(hero, bot.Team, bot)) options.Add(hero);
            if (options.Count == 0)
                foreach (HeroDefinition hero in roster.heroes)
                    if (hero != null) options.Add(hero);
            bot.Hero = options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
        }

        bool TakenByTeam(HeroDefinition hero, Team team, Seat except)
        {
            foreach (Seat seat in _seats)
            {
                if (seat == except || seat.Team != team) continue;
                if ((seat.IsLocal ? _selected : seat.Hero) == hero) return true;
            }
            return false;
        }

        // ---------- Hero tiles + details ----------

        void BuildHeroTiles()
        {
            _heroGrid.Clear();
            _tiles.Clear();
            foreach (HeroDefinition hero in roster.heroes)
            {
                if (hero == null) continue;
                var tile = new VisualElement();
                tile.AddToClassList("hero-tile");
                Label glyph = Text(hero.glyph, "hero-tile__glyph");
                glyph.style.backgroundColor = hero.color;
                tile.Add(glyph);
                tile.Add(Text(hero.displayName, "hero-tile__name"));
                Label note = Text(hero.title, "hero-tile__note");
                tile.Add(note);

                tile.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!TakenByAlly(hero)) Select(hero);
                });
                tile.RegisterCallback<PointerEnterEvent>(_ => _hovered = hero);
                tile.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hovered == hero) _hovered = null;
                });
                _heroGrid.Add(tile);
                _tiles.Add((hero, tile, note));
            }
        }

        // Taken by someone else on the local player's team, so the player can't pick it.
        bool TakenByAlly(HeroDefinition hero) => TakenByTeam(hero, LocalSeat?.Team ?? Team.Blue, LocalSeat);

        void RefreshTiles()
        {
            foreach ((HeroDefinition hero, VisualElement tile, Label note) in _tiles)
            {
                bool taken = TakenByAlly(hero);
                tile.EnableInClassList("hero-tile--selected", hero == _selected);
                tile.EnableInClassList("hero-tile--taken", taken);
                string text = taken ? "Taken by a teammate" : hero.title;
                if (note.text != text) note.text = text;
            }
        }

        // Shows the hovered hero, otherwise the selected one.
        void RefreshDetail()
        {
            _detailDirty = false;
            HeroDefinition hero = _hovered ?? _selected;
            _shownDetail = hero;
            _heroDetail.Clear();
            if (hero == null)
            {
                _heroDetail.Add(Text("Pick a hero to see their abilities.", "hero-detail__empty"));
                return;
            }

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("hero-detail__top");
            Label glyph = Text(hero.glyph, "hero-detail__glyph");
            glyph.style.backgroundColor = hero.color;
            top.Add(glyph);
            var names = new VisualElement { pickingMode = PickingMode.Ignore };
            names.Add(Text(hero.displayName, "hero-detail__name"));
            names.Add(Text(hero.title, "hero-detail__title"));
            top.Add(names);
            _heroDetail.Add(top);

            _heroDetail.Add(Text(hero.StatsLine, "hero-detail__stats"));
            if (!string.IsNullOrEmpty(hero.description)) _heroDetail.Add(Text(hero.description, "hero-detail__text"));

            var abilities = new VisualElement { pickingMode = PickingMode.Ignore };
            abilities.AddToClassList("hero-detail__abilities");
            abilities.Add(AbilityBlock("PASSIVE", hero.passiveName, hero.passiveDescription));
            if (hero.ultimate != null)
                abilities.Add(AbilityBlock("ULTIMATE (R)", hero.ultimate.displayName, hero.ultimate.description));
            _heroDetail.Add(abilities);
        }

        static VisualElement AbilityBlock(string section, string name, string text)
        {
            var block = new VisualElement { pickingMode = PickingMode.Ignore };
            block.AddToClassList("hero-detail__ability");
            block.Add(Text(section, "hero-detail__section"));
            block.Add(Text(name, "hero-detail__ability-name"));
            block.Add(Text(text, "hero-detail__text"));
            return block;
        }

        static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        // ---------- Pick + lock in ----------

        void Select(HeroDefinition hero)
        {
            if (hero != null && TakenByAlly(hero)) hero = null;
            // Once locked in on the game server, the pick stays.
            if (MatchSettings.Mode == MatchMode.PvP && LocalSeat is { Locked: true }) return;
            _selected = hero;
            _detailDirty = true;
            _lockButton.SetEnabled(CanLockIn);
            _status.text = hero != null ? $"{hero.displayName} selected" : "Pick a hero";
        }

        void LockIn()
        {
            if (MatchSettings.Mode == MatchMode.PvP)
            {
                SendLockIn();
                return;
            }
            if (_selected == null || _layout == null) return;

            // Bots still choosing pick right away.
            foreach (Seat seat in _seats)
                if (seat.IsBot && seat.Hero == null) PickForBot(seat);

            MatchSettings.SelectedHeroId = _selected.id;
            MatchSettings.LayoutId = _layout.id;
            MatchSettings.Lineup.Clear();
            foreach (Seat seat in _seats)
            {
                HeroDefinition hero = seat.IsLocal ? _selected : seat.Hero;
                if (hero == null) continue; // open PvP slots
                MatchSettings.Lineup.Add(new LobbySlot
                {
                    team = seat.Team,
                    heroId = hero.id,
                    isBot = seat.IsBot,
                    isLocal = seat.IsLocal,
                });
            }

            RefreshSeats();
            _lockButton.SetEnabled(false);
            _status.text = $"Loading {_layout.displayName}…";
            SceneManager.LoadScene(_layout.sceneName);
        }

        void OnConnectFailed(string message)
        {
            if (this == null) return;
            _lobbyMessage = message;
            _status.text = message;
            _startNowButton.AddToClassList("hidden");
            _lockButton.SetEnabled(false);
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
