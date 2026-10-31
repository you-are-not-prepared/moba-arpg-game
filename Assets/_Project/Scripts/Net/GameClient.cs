using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba.Net
{
    // Finding a match on a game server as a player. Connecting puts the player in the server's queue, then its
    // champion select (Lobby / LobbyChanged, shown by the menu), and when that ends the server loads the map on
    // this client and spawns the locked-in hero. If the connection fails or drops, the player goes back to the menu
    // with a message.
    public static class GameClient
    {
        // Shown on the menu after being disconnected (cleared once shown).
        public static string LastError { get; set; }
        public static event Action<string> ConnectFailed;
        // The server's queue and champion select, while they last (null otherwise).
        public static NetLobby.State Lobby { get; private set; }
        public static event Action<NetLobby.State> LobbyChanged;

        static bool s_Connected;
        // -join: the hero to lock in as soon as champion select starts.
        static string s_AutoPick;

        public static bool IsConnecting => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                                           && !NetworkManager.Singleton.IsServer;

        public static ulong LocalClientId => NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;

        // Looks for a match. Logged in (and `useAccount`), it first gets a join ticket so the server can record the
        // match on this account.
        public static async void FindMatch(string playerName, bool useAccount = true)
        {
            MatchSettings.Mode = MatchMode.PvP;
            Lobby = null;
            string ticket = useAccount ? await Online.MatchService.JoinTicket() : null;
            NetworkManager manager = NetSession.EnsureManager();
            if (manager.IsListening) manager.Shutdown();

            NetSession.TryParseAddress(NetSession.ServerAddress, out string host, out ushort port);
            NetSession.Transport.SetConnectionData(host, port);
            manager.NetworkConfig.ConnectionData = NetSession.Encode(new NetSession.JoinRequest
            {
                playerName = playerName,
                ticket = ticket ?? "",
            });

            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
            manager.OnClientDisconnectCallback += OnDisconnected;

            s_Connected = false;
            if (!manager.StartClient())
            {
                Fail("Couldn't start the network connection.");
                return;
            }
            manager.CustomMessagingManager.RegisterNamedMessageHandler(NetLobby.StateMessage, OnLobbyMessage);
        }

        public static void Leave()
        {
            Lobby = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && !manager.IsServer) manager.Shutdown();
        }

        // Champion select: lock in a hero / start the match now (random heroes for anyone still picking).
        public static void SendPick(string heroId) => NetLobby.SendToServer(NetLobby.PickMessage, heroId);
        public static void SendStartNow() => NetLobby.SendToServer(NetLobby.StartNowMessage, "");

        // This player's seat in champion select (null while queueing).
        public static NetLobby.Seat LocalSeat(NetLobby.State state) =>
            state?.seats.Find(s => !s.bot && s.client == (long)LocalClientId);

        static void OnLobbyMessage(ulong sender, FastBufferReader reader)
        {
            NetLobby.State state = NetLobby.ReadState(reader);
            if (state == null) return;
            Lobby = state;
            LobbyChanged?.Invoke(state);
            AutoPick(state);
        }

        static void AutoPick(NetLobby.State state)
        {
            NetLobby.Seat seat = LocalSeat(state);
            if (string.IsNullOrEmpty(s_AutoPick) || state.phase != NetLobby.PhaseLobby || seat == null || seat.locked) return;
            // The wanted hero, or any other one the team doesn't have yet.
            var taken = new HashSet<string>();
            foreach (NetLobby.Seat s in state.seats)
                if (s != seat && s.team == seat.team && !string.IsNullOrEmpty(s.hero)) taken.Add(s.hero);
            string pick = taken.Contains(s_AutoPick) ? null : s_AutoPick;
            HeroRoster roster = UnityEngine.Object.FindFirstObjectByType<UI.FrontendController>()?.roster;
            if (pick == null && roster != null)
                pick = roster.heroes.Find(h => h != null && !taken.Contains(h.id))?.id;
            if (pick != null) SendPick(pick);
        }

        static void OnConnected(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId) s_Connected = true;
        }

        static void OnDisconnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || clientId != manager.LocalClientId) return;
            string reason = string.IsNullOrEmpty(manager.DisconnectReason) ? null : manager.DisconnectReason;
            bool wasConnected = s_Connected;
            s_Connected = false;
            Lobby = null;

            if (!wasConnected)
            {
                Fail(reason ?? $"Couldn't reach the game server at {NetSession.ServerAddress}. " +
                     "Is it running? (docker compose up in the moba-arpg-game folder)");
                return;
            }

            // The match is over and the server closed for the next one: same as Continue on the end screen.
            GameManager game = GameManager.Instance;
            if (game != null && game.IsOver)
            {
                game.ShowMatchSummary();
                return;
            }

            // Lost the server (it restarted or crashed): back to the menu with the reason.
            LastError = reason ?? "Lost the connection to the game server.";
            if (SceneManager.GetActiveScene().name != MatchSettings.FrontendScene)
                SceneManager.LoadScene(MatchSettings.FrontendScene);
            else
                Fail(LastError);
        }

        static void Fail(string message)
        {
            Debug.LogWarning($"[GameClient] {message}");
            ConnectFailed?.Invoke(message);
        }

        // Quick tests with a built client: "-join vesh -name Bob" looks for a match as soon as the game starts, as an
        // anonymous player (no login), and locks in that hero (or another one if the team has it already).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void JoinFromCommandLine()
        {
            if (GameServer.IsServerProcess) return;
            string[] args = Environment.GetCommandLineArgs();
            int join = Array.IndexOf(args, "-join");
            if (join < 0 || join + 1 >= args.Length) return;
            int name = Array.IndexOf(args, "-name");
            s_AutoPick = args[join + 1];
            FindMatch(name >= 0 && name + 1 < args.Length ? args[name + 1] : "Player", useAccount: false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastError = null;
            ConnectFailed = null;
            Lobby = null;
            LobbyChanged = null;
            s_Connected = false;
            s_AutoPick = null;
        }
    }
}
