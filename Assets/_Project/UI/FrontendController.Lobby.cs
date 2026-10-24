using System.Linq;
using Moba.Net;
using Moba.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // PvP: finding a match on the game server. The champion select screen shows the server's queue ("Searching…")
    // and then its champion select (GameClient.Lobby): every seat of both teams with the other players' and bots'
    // locked-in picks, the map, and the time left. Lock in sends the pick to the server; the server loads the map
    // when everyone has locked in, the timer runs out, or a player presses Start now.
    public partial class FrontendController
    {
        Button _startNowButton;
        string _lobbyPhase;
        // When the server's current timer ends, in unscaled time.
        float _lobbyDeadline;
        // Who sits where, to rebuild the seat views only when that changes.
        string _seatKey;
        string _lobbyMessage;

        void SetUpLobby(VisualElement root)
        {
            _startNowButton = root.Q<Button>("startNowButton");
            _startNowButton.clicked += GameClient.SendStartNow;
            GameClient.LobbyChanged += OnLobby;
        }

        void TearDownLobby() => GameClient.LobbyChanged -= OnLobby;

        void StartPvP()
        {
            MatchSettings.Mode = MatchMode.PvP;
            ShowLobbyScreen();
            string name = AccountService.IsLoggedIn ? AccountService.Profile.username : "Player";
            GameClient.FindMatch(name);
        }

        void ShowLobbyScreen()
        {
            ShowOnly(_selectScreen);
            _modeLabel.text = $"PvP · game server at {NetSession.ServerAddress}";
            _layout = null;
            _mapName.text = "Finding a match…";
            _mapDescription.text = "The game server picks the map for its match.";
            _preview.Show(null);
            _seats.Clear();
            _allyList.Clear();
            _enemyList.Clear();
            _seatKey = null;
            _lobbyPhase = null;
            _lobbyMessage = $"Connecting to the game server at {NetSession.ServerAddress}…";
            _hovered = null;
            _startNowButton.AddToClassList("hidden");
            Select(null);
        }

        // Back from the PvP screen: leave the queue or champion select.
        void LeaveLobby()
        {
            GameClient.Leave();
            _lobbyPhase = null;
            _startNowButton.AddToClassList("hidden");
        }

        void OnLobby(NetLobby.State state)
        {
            if (this == null) return;
            // A built client started with -join gets here without going through the menu.
            if (_selectScreen.ClassListContains("hidden") || MatchSettings.Mode != MatchMode.PvP)
            {
                MatchSettings.Mode = MatchMode.PvP;
                ShowLobbyScreen();
            }
            _lobbyPhase = state.phase;
            _lobbyDeadline = Time.unscaledTime + state.timeLeft;
            _lobbyMessage = null;

            MapLayoutInfo layout = layouts != null ? layouts.layouts.Find(l => l.id == state.map) : null;
            if (layout != null && layout != _layout)
            {
                _layout = layout;
                _mapName.text = layout.displayName;
                _mapDescription.text = $"{layout.description} Picked by the game server.";
                _preview.Show(layout);
            }

            SyncSeats(state);
            NetLobby.Seat mine = GameClient.LocalSeat(state);
            _startNowButton.EnableInClassList("hidden", state.phase != NetLobby.PhaseLobby || mine == null);
            if (mine != null && mine.locked && !string.IsNullOrEmpty(mine.hero) && _selected?.id != mine.hero)
            {
                _selected = roster.Find(mine.hero);
                _detailDirty = true;
            }
            _lockButton.SetEnabled(CanLockIn);
        }

        // Rebuilds the seat views when who sits where changes; otherwise only their picks and names are updated.
        void SyncSeats(NetLobby.State state)
        {
            string key = string.Join(",", state.seats.Select(s => s.team + ":" + (s.bot ? "bot" : s.client.ToString())));
            if (key != _seatKey)
            {
                _seatKey = key;
                _seats.Clear();
                _allyList.Clear();
                _enemyList.Clear();
                Team mine = GameClient.LocalSeat(state)?.Team ?? Team.Blue;
                int botNumber = 0;
                foreach (NetLobby.Seat s in state.seats)
                {
                    var seat = new Seat
                    {
                        Team = s.Team,
                        IsBot = s.bot,
                        BotNumber = s.bot ? ++botNumber : 0,
                        ClientId = s.bot ? -1 : s.client,
                        IsLocal = !s.bot && s.client == (long)GameClient.LocalClientId,
                        Name = s.name,
                    };
                    CreateSeatView(seat, ally: seat.Team == mine);
                    (seat.Team == mine ? _allyList : _enemyList).Add(seat.Root);
                    _seats.Add(seat);
                }
            }
            for (int i = 0; i < _seats.Count && i < state.seats.Count; i++)
            {
                NetLobby.Seat s = state.seats[i];
                Seat seat = _seats[i];
                seat.Locked = s.locked;
                seat.Hero = s.locked ? roster.Find(s.hero) : null;
                if (seat.Name != s.name && !seat.IsLocal && !seat.IsBot) seat.Who.text = (seat.Name = s.name).ToUpperInvariant();
            }
        }

        bool CanLockIn => MatchSettings.Mode == MatchMode.PvE
            ? _selected != null && _layout != null
            : _selected != null && _lobbyPhase == NetLobby.PhaseLobby && LocalSeat is { Locked: false };

        void SendLockIn()
        {
            if (!CanLockIn) return;
            MatchSettings.SelectedHeroId = _selected.id;
            GameClient.SendPick(_selected.id);
            _lockButton.SetEnabled(false);
        }

        // The status line, every frame (the timers count down between the server's updates).
        void UpdateLobbyStatus()
        {
            string text;
            int left = Mathf.CeilToInt(Mathf.Max(0f, _lobbyDeadline - Time.unscaledTime));
            string time = $"{left / 60}:{left % 60:00}";
            NetLobby.State state = GameClient.Lobby;
            if (_lobbyMessage != null || state == null)
            {
                text = _lobbyMessage ?? "Connecting…";
            }
            else if (state.phase == NetLobby.PhaseQueue)
            {
                text = $"Searching for players… {time} · {state.players} player{(state.players == 1 ? "" : "s")} found";
            }
            else if (state.phase == NetLobby.PhaseLobby)
            {
                Seat mine = LocalSeat;
                bool everyone = state.seats.Where(s => !s.bot).All(s => s.locked);
                text = everyone ? $"Everyone's locked in · starting in {left}"
                    : mine is { Locked: true } ? $"Locked in as {mine.Hero?.displayName} · waiting for the others · {time}"
                    : $"Pick a hero · {time}";
            }
            else
            {
                text = _layout != null ? $"Loading {_layout.displayName}…" : "Loading the match…";
            }
            if (_status.text != text) _status.text = text;
        }
    }
}
