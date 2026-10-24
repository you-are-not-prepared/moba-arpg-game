using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Moba.Net
{
    // Who runs the game logic. Offline play and the game server simulate everything ("authority"); a client
    // connected to a game server only sends its player's orders and shows what the server replicates.
    public static class NetSession
    {
        public const ushort DefaultPort = 7777;
        public const string DefaultAddress = "127.0.0.1";
        const string AddressPrefsKey = "moba.gameserver.address";
        const string PrefabsResource = "NetworkPrefabs";

        static NetworkManager Manager => NetworkManager.Singleton;

        public static bool IsNetworked => Manager != null && Manager.IsListening;

        // True offline and on the server: this process runs the match.
        public static bool HasAuthority => !IsNetworked || Manager.IsServer;

        // A client connected to a game server (it doesn't simulate the match).
        public static bool IsRemoteClient => IsNetworked && !Manager.IsServer;

        // "host:port" of the game server; override with PlayerPrefs "moba.gameserver.address".
        public static string ServerAddress => PlayerPrefs.GetString(AddressPrefsKey, $"{DefaultAddress}:{DefaultPort}");

        // The NetworkManager for this process, created on first use and kept across scene loads.
        public static NetworkManager EnsureManager()
        {
            if (Manager != null) return Manager;
            var go = new GameObject("NetworkManager");
            Object.DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30,
            };
            var prefabs = Resources.Load<NetworkPrefabsList>(PrefabsResource);
            if (prefabs != null) manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
            else Debug.LogError($"[Net] Resources/{PrefabsResource} is missing; run MOBA > Rebuild All Scenes.");
            return manager;
        }

        public static UnityTransport Transport => EnsureManager().GetComponent<UnityTransport>();

        // What a client tells the server when it connects: its name and (logged in) a join ticket the server redeems
        // with the backend to learn the account. The hero is picked later, in the server's champion select.
        [System.Serializable]
        public class JoinRequest
        {
            public string playerName;
            public string ticket;
        }

        public static byte[] Encode(JoinRequest request) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));

        public static JoinRequest Decode(byte[] payload)
        {
            try
            {
                return payload is { Length: > 0 and < 1024 }
                    ? JsonUtility.FromJson<JoinRequest>(Encoding.UTF8.GetString(payload))
                    : null;
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }

        public static bool TryParseAddress(string text, out string host, out ushort port)
        {
            host = DefaultAddress;
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Trim().Split(':');
            host = parts[0];
            return parts.Length < 2 || ushort.TryParse(parts[1], out port);
        }
    }
}
