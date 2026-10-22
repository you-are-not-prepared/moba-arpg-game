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

        static bool s_Connected;

        public static void Connect(string heroId, string playerName)
        {
            NetworkManager manager = NetSession.EnsureManager();
            if (manager.IsListening) manager.Shutdown();

            NetSession.TryParseAddress(NetSession.ServerAddress, out string host, out ushort port);
            NetSession.Transport.SetConnectionData(host, port);
            manager.NetworkConfig.ConnectionData = NetSession.Encode(new NetSession.JoinRequest
            {
                heroId = heroId,
                playerName = playerName,
            });

            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
            manager.OnClientDisconnectCallback += OnDisconnected;

            s_Connected = false;
            if (!manager.StartClient()) Fail("Couldn't start the network connection.");
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
            bool wasConnected = s_Connected;
            s_Connected = false;

            if (!wasConnected)
            {
                Fail(reason ?? $"Couldn't reach the game server at {NetSession.ServerAddress}. " +
                     "Is it running? (docker compose up in the moba-test folder)");
                return;
            }

            // Lost the server mid-match (it ended, restarted, or crashed): back to the menu.
            LastError = reason ?? "Lost the connection to the game server.";
            if (SceneManager.GetActiveScene().name != MatchSettings.FrontendScene)
                SceneManager.LoadScene(MatchSettings.FrontendScene);
        }

        static void Fail(string message)
        {
            Debug.LogWarning($"[GameClient] {message}");
            ConnectFailed?.Invoke(message);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastError = null;
            ConnectFailed = null;
            s_Connected = false;
        }
    }
}
