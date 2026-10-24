using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Moba.Net
{
    // The matchmaking queue and champion select on a game server (see GameServer.Lobby), before the map loads.
    // The server sends its whole state as JSON in a named message whenever it changes (and once a second for the
    // timers); clients answer with a pick or "start now".
    public static class NetLobby
    {
        public const string StateMessage = "moba.lobby.state";
        public const string PickMessage = "moba.lobby.pick";
        public const string StartNowMessage = "moba.lobby.start";

        // How long the server waits for more players after the first one, how long picking may take, and the
        // countdown once everyone has locked in.
        public const float QueueSeconds = 15f;
        public const float PickSeconds = 60f;
        public const float LockedInCountdown = 5f;

        public const string PhaseQueue = "queue";
        public const string PhaseLobby = "lobby";
        public const string PhaseStarting = "starting";

        [Serializable]
        public class State
        {
            public string phase;
            // Map layout id ("classic", "zones", "delve").
            public string map;
            // Seconds until the queue closes, picking ends or the match starts.
            public float timeLeft;
            // Players connected (bots not counted).
            public int players;
            public List<Seat> seats = new();
        }

        [Serializable]
        public class Seat
        {
            public string team;
            public bool bot;
            // The player's client id (players only).
            public long client;
            public string name;
            // Hero id once picked ("" before).
            public string hero;
            public bool locked;

            public Team Team => team == nameof(Team.Red) ? Team.Red : Team.Blue;
        }

        public static void SendToServer(string message, string text) =>
            Send(text, w => NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(message, NetworkManager.ServerClientId, w,
                NetworkDelivery.ReliableFragmentedSequenced));

        public static void SendToAll(string message, string text) =>
            Send(text, w => NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(message, w,
                NetworkDelivery.ReliableFragmentedSequenced));

        public static string Read(FastBufferReader reader)
        {
            reader.ReadValueSafe(out string text);
            return text;
        }

        public static State ReadState(FastBufferReader reader)
        {
            try
            {
                return JsonUtility.FromJson<State>(Read(reader));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static void Send(string text, Action<FastBufferWriter> send)
        {
            text ??= "";
            using var writer = new FastBufferWriter(64 + text.Length * 2, Allocator.Temp);
            writer.WriteValueSafe(text);
            send(writer);
        }
    }
}
