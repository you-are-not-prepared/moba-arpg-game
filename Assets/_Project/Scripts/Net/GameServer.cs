using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moba.Online;
using Moba.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Moba.Net
{
    // Runs this process as the game server: the Linux server build (in Docker) or any build started with -server.
    // One match per run:
    //   queue    players connect and wait in the menu while the server gathers them (see GameServer.Lobby);
    //   lobby    champion select on the server: players on both teams, bots in the free seats;
    //   match    the map loads on every client, each player gets the hero they locked in, and the server runs it.
    // Logged-in players are identified with the backend (join tickets), and the finished match is recorded there for
    // them (see Online.MatchReporter). A player who leaves keeps their hero in the match and can rejoin it. When the
    // match ends the server quits after a short delay, and Docker restarts it for the next one.
    //
    // Settings (command line "-map classic" / environment MOBA_MAP=classic):
    //   map    classic | zones | delve | random (default random, rolled again whenever the queue empties)
    //   port   UDP port to listen on (default 7777)
    //   bots   bots per team for the free seats (default 3; 0 = players only)
    //   cheats on | off: whether players may use the cheat menu (default off; for local testing)
    //   backend_url   the moba-backend API (default http://localhost:8080)
    //   backend_key   the backend's game-server key; without it matches aren't recorded
    public partial class GameServer : MonoBehaviour
    {
        // Read by NetworkMatch, which tells the clients.
        public static bool CheatsAllowed { get; private set; }
        // For the backend's /server endpoints (empty = don't use the backend).
        public static string BackendKey { get; private set; } = "";

        const float QuitDelayAfterMatch = 15f;

        enum Phase { Queue, Lobby, Loading, Match }

        HeroRoster _roster;
        MapLayoutCatalog _layouts;
        MapLayoutInfo _layout;
        string _mapSetting;
        int _bots;
        NetworkManager _manager;
        Phase _phase = Phase.Queue;

        // What each connected player asked for, and their backend account (checked from their join ticket).
        readonly Dictionary<ulong, NetSession.JoinRequest> _joins = new();
        readonly Dictionary<ulong, Task<RedeemTicketResponse>> _accounts = new();
        // Players rejoining a running match, with the hero they left behind.
        readonly Dictionary<ulong, NetworkHero> _rejoining = new();

        public static bool IsServerProcess
        {
            get
            {
                // UNITY_SERVER is also defined in the Editor while its build target is Dedicated Server, so the
                // Editor only becomes a server when explicitly asked (-server).
#if UNITY_SERVER && !UNITY_EDITOR
                return true;
#else
                return Array.IndexOf(Environment.GetCommandLineArgs(), "-server") >= 0;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!IsServerProcess) return;
            // The first scene (Frontend) knows the roster and the map layouts. The server stays in it, unseen, until
            // the match starts: clients connecting from their menu don't have to load anything.
            var frontend = FindFirstObjectByType<FrontendController>();
            if (frontend == null)
            {
                Debug.LogError("[GameServer] The first scene must be the Frontend scene.");
                Application.Quit(1);
                return;
            }
            var go = new GameObject("GameServer");
            DontDestroyOnLoad(go);
            go.AddComponent<GameServer>().Begin(frontend.roster, frontend.layouts);
            frontend.gameObject.SetActive(false);
        }

        void Begin(HeroRoster roster, MapLayoutCatalog layouts)
        {
            _roster = roster;
            _layouts = layouts;
            Application.targetFrameRate = 30;

            _mapSetting = Setting("map", "random");
            _layout = PickLayout(_mapSetting);
            ushort port = ushort.TryParse(Setting("port", NetSession.DefaultPort.ToString()), out ushort p) ? p : NetSession.DefaultPort;
            _bots = int.TryParse(Setting("bots", "3"), out int b) ? Mathf.Clamp(b, 0, MatchSettings.TeamSize) : MatchSettings.TeamSize;
            CheatsAllowed = Setting("cheats", "off").ToLowerInvariant() is "on" or "1" or "true";
            BackendClient.UrlOverride = Setting("backend_url", BackendClient.DefaultUrl);
            BackendKey = Setting("backend_key", "");

            _manager = NetSession.EnsureManager();
            NetSession.Transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            _manager.ConnectionApprovalCallback = Approve;
            _manager.OnClientConnectedCallback += OnClientConnected;
            _manager.OnClientDisconnectCallback += OnClientDisconnected;
            if (!_manager.StartServer())
            {
                Debug.LogError($"[GameServer] Couldn't listen on UDP port {port}.");
                Application.Quit(1);
                return;
            }
            _manager.SceneManager.OnSynchronizeComplete += OnClientSynchronized;
            _manager.SceneManager.OnLoadEventCompleted += (_, _, _, _) => OnMapLoaded();
            _manager.CustomMessagingManager.RegisterNamedMessageHandler(NetLobby.PickMessage, OnPickMessage);
            _manager.CustomMessagingManager.RegisterNamedMessageHandler(NetLobby.StartNowMessage, OnStartNowMessage);
            Debug.Log($"[GameServer] Listening on UDP {port}. Map: {_layout.displayName}, up to {_bots} bots per team, " +
                      $"cheats {(CheatsAllowed ? "on" : "off")}, " +
                      (BackendKey.Length > 0 ? $"recording matches at {BackendClient.BaseUrl}." : "not recording matches (no backend_key)."));
            Debug.Log("[GameServer] Waiting for players.");
        }

        // ---------- Connections ----------

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            NetSession.JoinRequest join = NetSession.Decode(request.Payload);
            ulong clientId = request.ClientNetworkId;
            response.CreatePlayerObject = false;
            if (join == null)
            {
                response.Approved = false;
                response.Reason = "The game server didn't understand the request. Is the game up to date?";
                return;
            }

            Task<RedeemTicketResponse> account = !string.IsNullOrEmpty(join.ticket) && BackendKey.Length > 0
                ? MatchService.RedeemTicket(join.ticket, BackendKey)
                : null;

            switch (_phase)
            {
                case Phase.Queue:
                case Phase.Lobby:
                    response.Approved = HumanCount < MatchSettings.TeamSize * 2;
                    if (!response.Approved) response.Reason = "This match is full.";
                    break;
                case Phase.Loading:
                    response.Approved = false;
                    response.Reason = "The match on this server is starting. Try again when it's over.";
                    break;
                default:
                    // A running match only takes back players who left it (their account still has a hero there).
                    response.Approved = false;
                    response.Reason = "A match is already running on this server. Try again when it ends.";
                    if (account != null && GameManager.Instance != null && !GameManager.Instance.IsOver)
                    {
                        response.Pending = true;
                        ApproveRejoin(clientId, account, response);
                    }
                    return;
            }
            if (!response.Approved) return;
            _joins[clientId] = join;
            if (account != null) _accounts[clientId] = account;
        }

        async void ApproveRejoin(ulong clientId, Task<RedeemTicketResponse> account, NetworkManager.ConnectionApprovalResponse response)
        {
            RedeemTicketResponse who = await account;
            NetworkHero hero = who != null ? LeftBehindBy(who.accountId) : null;
            if (hero != null)
            {
                _rejoining[clientId] = hero;
                response.Approved = true;
                response.Reason = null;
            }
            response.Pending = false;
        }

        void OnClientConnected(ulong clientId)
        {
            if (_phase is Phase.Queue or Phase.Lobby) AddPlayer(clientId);
        }

        void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[GameServer] Client {clientId} left.");
            _joins.Remove(clientId);
            _accounts.Remove(clientId);
            _rejoining.Remove(clientId);
            if (_phase is Phase.Queue or Phase.Lobby) RemovePlayer(clientId);
        }

        // A player rejoining the running match has loaded the map: hand their hero back.
        void OnClientSynchronized(ulong clientId)
        {
            if (!_rejoining.Remove(clientId, out NetworkHero hero) || hero == null) return;
            hero.GetComponent<NetworkObject>().ChangeOwnership(clientId);
            Debug.Log($"[GameServer] Client {clientId} ({hero.PlayerName}) rejoined and took back {hero.HeroId}.");
        }

        // The player's name: their account's username once the ticket is checked, otherwise what the game sent.
        string NameOf(ulong clientId)
        {
            if (_accounts.TryGetValue(clientId, out Task<RedeemTicketResponse> t) && t.IsCompletedSuccessfully && t.Result != null)
                return t.Result.username;
            return _joins.TryGetValue(clientId, out NetSession.JoinRequest join) && !string.IsNullOrEmpty(join.playerName)
                ? join.playerName
                : $"Player {clientId}";
        }

        async Task<string> AccountOf(ulong clientId) =>
            _accounts.TryGetValue(clientId, out Task<RedeemTicketResponse> t) ? (await t)?.accountId ?? "" : "";

        // ---------- The match ----------

        // Champion select is over: bots go into the lineup the map spawns, players are spawned once every client has
        // loaded the map (OnMapLoaded).
        void StartMatch()
        {
            _phase = Phase.Loading;
            MatchSettings.Mode = MatchMode.PvP;
            MatchSettings.Lineup.Clear();
            foreach (LobbySeat seat in _seats)
                if (seat.Bot) MatchSettings.Lineup.Add(new LobbySlot { team = seat.Team, heroId = seat.Hero, isBot = true });
            BroadcastLobby();
            Debug.Log($"[GameServer] Champion select done: {HumanCount} player(s). Loading {_layout.displayName}.");
            _manager.SceneManager.LoadScene(_layout.sceneName, LoadSceneMode.Single);
        }

        async void OnMapLoaded()
        {
            GameManager game = GameManager.Instance;
            MatchBootstrap bootstrap = FindFirstObjectByType<MatchBootstrap>();
            if (game == null || bootstrap == null || _phase != Phase.Loading) return;
            _phase = Phase.Match;
            game.MatchEnded += _ => StartCoroutine(QuitSoon());

            foreach (LobbySeat seat in _seats.ToArray())
            {
                if (seat.Bot || !_manager.ConnectedClients.ContainsKey(seat.ClientId)) continue;
                string accountId = await AccountOf(seat.ClientId);
                if (this == null || !_manager.ConnectedClients.ContainsKey(seat.ClientId)) continue;
                string name = NameOf(seat.ClientId);
                bootstrap.SpawnPlayer(seat.ClientId, seat.Hero, name, accountId, seat.Team, null);
                Debug.Log($"[GameServer] {name}{(accountId.Length > 0 ? " (logged in)" : "")} plays {seat.Hero} on {seat.Team}.");
            }
            game.SetPaused(false);
            Debug.Log("[GameServer] The match starts.");
        }

        // A hero this account played and left (now owned by the server).
        static NetworkHero LeftBehindBy(string accountId)
        {
            foreach (NetworkHero hero in FindObjectsByType<NetworkHero>(FindObjectsSortMode.None))
                if (hero.IsSpawned && !hero.IsBot && hero.AccountId == accountId && hero.OwnerClientId == NetworkManager.ServerClientId)
                    return hero;
            return null;
        }

        MapLayoutInfo PickLayout(string id)
        {
            MapLayoutInfo found = _layouts.layouts.Find(l => l.id == id);
            return found ?? _layouts.layouts[Random.Range(0, _layouts.layouts.Count)];
        }

        IEnumerator QuitSoon()
        {
            Debug.Log($"[GameServer] Match over. Shutting down in {QuitDelayAfterMatch}s for a fresh match.");
            yield return new WaitForSecondsRealtime(QuitDelayAfterMatch);
            _manager.Shutdown();
            Application.Quit(0);
        }

        // "-name value" on the command line, else the MOBA_NAME environment variable, else the default.
        static string Setting(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-" + name);
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            string env = Environment.GetEnvironmentVariable("MOBA_" + name.ToUpperInvariant());
            return string.IsNullOrEmpty(env) ? fallback : env;
        }
    }
}
