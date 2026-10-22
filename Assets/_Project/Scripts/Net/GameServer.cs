using System;
using System.Collections;
using System.Collections.Generic;
using Moba.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Moba.Net
{
    // Runs this process as the game server: the Linux server build (in Docker) or any build started with -server.
    // It loads a map, adds bots, gives every player who connects the hero they picked, and runs the whole match.
    // The match stays paused until the first player joins. When it ends the server quits after a short delay, and
    // Docker restarts it for a fresh match.
    //
    // Settings (command line "-map classic" / environment MOBA_MAP=classic):
    //   map    classic | zones | delve | random (default random)
    //   port   UDP port to listen on (default 7777)
    //   bots   number of bots per team besides players; blue gets one fewer (default 3)
    //   cheats on | off: whether players may use the cheat menu (default off; for local testing)
    public class GameServer : MonoBehaviour
    {
        // Read by NetworkMatch, which tells the clients.
        public static bool CheatsAllowed { get; private set; }

        const float QuitDelayAfterMatch = 15f;

        static readonly Dictionary<ulong, NetSession.JoinRequest> s_Joining = new();

        HeroRoster _roster;
        MapLayoutCatalog _layouts;
        NetworkManager _manager;
        bool _started;

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
            // The first scene (Frontend) knows the roster and the map layouts.
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

            MapLayoutInfo layout = PickLayout(Setting("map", "random"));
            ushort port = ushort.TryParse(Setting("port", NetSession.DefaultPort.ToString()), out ushort p) ? p : NetSession.DefaultPort;
            int bots = int.TryParse(Setting("bots", "3"), out int b) ? Mathf.Clamp(b, 0, 5) : 3;
            CheatsAllowed = Setting("cheats", "off").ToLowerInvariant() is "on" or "1" or "true";
            PrepareBots(bots);

            _manager = NetSession.EnsureManager();
            NetSession.Transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            _manager.ConnectionApprovalCallback = Approve;
            _manager.OnClientDisconnectCallback += id => Debug.Log($"[GameServer] Client {id} left.");
            if (!_manager.StartServer())
            {
                Debug.LogError($"[GameServer] Couldn't listen on UDP port {port}.");
                Application.Quit(1);
                return;
            }
            _manager.SceneManager.OnSynchronizeComplete += SpawnJoinedPlayer;
            _manager.SceneManager.OnLoadEventCompleted += (_, _, _, _) => OnMapLoaded();
            _manager.SceneManager.LoadScene(layout.sceneName, LoadSceneMode.Single);
            Debug.Log($"[GameServer] Listening on UDP {port}. Map: {layout.displayName}, {bots} bots per team, " +
                      $"cheats {(CheatsAllowed ? "on" : "off")}.");
        }

        // Players may connect while the match runs, with a hero the roster knows.
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            NetSession.JoinRequest join = NetSession.Decode(request.Payload);
            bool knownHero = join != null && _roster != null && _roster.Find(join.heroId) != null;
            bool running = GameManager.Instance == null || !GameManager.Instance.IsOver;
            response.Approved = knownHero && running;
            response.CreatePlayerObject = false;
            if (!knownHero) response.Reason = "The server doesn't know that hero.";
            else if (!running) response.Reason = "The match on this server has just ended. Try again in a few seconds.";
            if (response.Approved) s_Joining[request.ClientNetworkId] = join;
        }

        // Once a client has loaded the map, give it its hero.
        void SpawnJoinedPlayer(ulong clientId)
        {
            if (!s_Joining.Remove(clientId, out NetSession.JoinRequest join)) return;
            MatchBootstrap bootstrap = FindFirstObjectByType<MatchBootstrap>();
            if (bootstrap == null) return;
            bootstrap.SpawnPlayer(clientId, join.heroId, join.playerName);
            Debug.Log($"[GameServer] Client {clientId} ({join.playerName}) joined as {join.heroId}.");
            if (!_started && GameManager.Instance != null)
            {
                _started = true;
                GameManager.Instance.SetPaused(false);
                Debug.Log("[GameServer] First player in: the match starts.");
            }
        }

        // Bots fill both teams; players join the blue team.
        void PrepareBots(int perTeam)
        {
            MatchSettings.Mode = MatchMode.PvP;
            MatchSettings.Lineup.Clear();
            AddBots(Team.Blue, Mathf.Max(0, perTeam - 1));
            AddBots(Team.Red, perTeam);
        }

        void AddBots(Team team, int count)
        {
            var taken = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var options = _roster.heroes.FindAll(h => h != null && !taken.Contains(h.id));
                if (options.Count == 0) options = _roster.heroes.FindAll(h => h != null);
                HeroDefinition hero = options[Random.Range(0, options.Count)];
                taken.Add(hero.id);
                MatchSettings.Lineup.Add(new LobbySlot { team = team, heroId = hero.id, isBot = true });
            }
        }

        MapLayoutInfo PickLayout(string id)
        {
            MapLayoutInfo found = _layouts.layouts.Find(l => l.id == id);
            return found ?? _layouts.layouts[Random.Range(0, _layouts.layouts.Count)];
        }

        // The match waits (paused: no creeps, bots or timer) until the first player has joined.
        void OnMapLoaded()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            game.MatchEnded += _ => StartCoroutine(QuitSoon());
            if (!_started) game.SetPaused(true);
            Debug.Log("[GameServer] Map loaded. Waiting for the first player.");
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
