using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba.Net
{
    // Connecting this game to a game server as a player. The server then loads its map on this client and spawns
    // the picked hero. If the connection fails or drops, the player is taken back to the menu with a message.
    public static class GameClient
    {
        // Shown on the menu after being disconnected (cleared once shown).
        public static string LastError { get; set; }
        public static event Action<string> ConnectFailed;
        // The answer to QueryServer: what the server runs, or null if it couldn't be reached.
        public static event Action<NetSession.ServerInfo> ServerInfoReceived;

        static bool s_Connected;
        static bool s_Probing;

        // Asks the game server which map it's running, without joining: the server refuses the connection and puts
        // the answer in the reason.
        public static void QueryServer()
        {
            NetworkManager manager = NetSession.EnsureManager();
            if (manager.IsListening) return;
            NetSession.TryParseAddress(NetSession.ServerAddress, out string host, out ushort port);
            NetSession.Transport.SetConnectionData(host, port);
            manager.NetworkConfig.ConnectionData = NetSession.Encode(new NetSession.JoinRequest { probe = true });
            Listen(manager);
            s_Probing = true;
            if (!manager.StartClient())
            {
                s_Probing = false;
                ServerInfoReceived?.Invoke(null);
            }
        }

        // Logged in (and `useAccount`), it first gets a join ticket so the server can record the match on this account.
        public static async void Connect(string heroId, string playerName, bool useAccount = true)
        {
            MatchSettings.Mode = MatchMode.PvP;
            string ticket = useAccount ? await Online.MatchService.JoinTicket() : null;
            NetworkManager manager = NetSession.EnsureManager();
            s_Probing = false;
            if (manager.IsListening) manager.Shutdown();

            NetSession.TryParseAddress(NetSession.ServerAddress, out string host, out ushort port);
            NetSession.Transport.SetConnectionData(host, port);
            manager.NetworkConfig.ConnectionData = NetSession.Encode(new NetSession.JoinRequest
            {
                heroId = heroId,
                playerName = playerName,
                ticket = ticket ?? "",
            });

            Listen(manager);
            s_Connected = false;
            if (!manager.StartClient()) Fail("Couldn't start the network connection.");
        }

        static void Listen(NetworkManager manager)
        {
            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
        }

        public static void Leave()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && !manager.IsServer) manager.Shutdown();
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
            if (s_Probing)
            {
                s_Probing = false;
                ServerInfoReceived?.Invoke(ReadInfo(reason));
                return;
            }
            bool wasConnected = s_Connected;
            s_Connected = false;

            if (!wasConnected)
            {
                Fail(reason ?? $"Couldn't reach the game server at {NetSession.ServerAddress}. " +
                     "Is it running? (docker compose up in the moba-test folder)");
                return;
            }

            // The match is over and the server closed for the next one: same as Continue on the end screen.
            GameManager game = GameManager.Instance;
            if (game != null && game.IsOver)
            {
                game.ShowMatchSummary();
                return;
            }

            // Lost the server mid-match (it restarted or crashed): back to the menu.
            LastError = reason ?? "Lost the connection to the game server.";
            if (SceneManager.GetActiveScene().name != MatchSettings.FrontendScene)
                SceneManager.LoadScene(MatchSettings.FrontendScene);
        }

        static NetSession.ServerInfo ReadInfo(string reason)
        {
            try
            {
                return string.IsNullOrEmpty(reason) ? null : JsonUtility.FromJson<NetSession.ServerInfo>(reason);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static void Fail(string message)
        {
            Debug.LogWarning($"[GameClient] {message}");
            ConnectFailed?.Invoke(message);
        }

        // Quick tests with a built client: "-join vesh -name Bob" joins the game server as soon as the game starts, as
        // an anonymous player (no login), e.g. a second player next to the Editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void JoinFromCommandLine()
        {
            if (GameServer.IsServerProcess) return;
            string[] args = Environment.GetCommandLineArgs();
            int join = Array.IndexOf(args, "-join");
            if (join < 0 || join + 1 >= args.Length) return;
            int name = Array.IndexOf(args, "-name");
            Connect(args[join + 1], name >= 0 && name + 1 < args.Length ? args[name + 1] : "Player", useAccount: false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastError = null;
            ConnectFailed = null;
            ServerInfoReceived = null;
            s_Connected = false;
            s_Probing = false;
        }
    }
}
