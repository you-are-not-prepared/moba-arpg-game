using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Moba.Net
{
    // The queue and champion select, before the map loads (clients stay in their menu; see NetLobby).
    //   Queue: the first player starts a short wait for more (NetLobby.QueueSeconds); it ends early once the match
    //          is full. Leaving empties it, and the next first player starts it again (with a fresh random map).
    //   Lobby: players are dealt onto both teams in join order, bots take the free seats (up to the "bots" setting)
    //          and pick a moment apart. Picks are locked in; nobody gets a hero their own team already has. It ends
    //          LockedInCountdown seconds after everyone has locked in, when PickSeconds run out (random heroes for
    //          anyone still picking), or right away when a player asks to start now. Players joining now take a
    //          free or bot seat; a player who leaves gives their seat to a bot.
    public partial class GameServer
    {
        const float BotFirstPick = 1.5f;
        const float BotPickGapMin = 0.8f;
        const float BotPickGapMax = 2.5f;
        const float StartNowDelay = 1f;
        // Someone joining during the final countdown still gets this long to pick.
        const float LateJoinPickSeconds = 20f;
        const float BroadcastInterval = 1f;

        sealed class LobbySeat
        {
            public Team Team;
            public bool Bot;
            public ulong ClientId;
            public string Hero = "";
            public bool Locked;
            public float BotPickAt;
        }

        readonly List<ulong> _queue = new();
        readonly List<LobbySeat> _seats = new();
        // When the queue closes, picking ends, or the match starts (realtime).
        float _deadline;
        bool _countingDown;
        float _nextBroadcast;

        int HumanCount => _phase == Phase.Queue ? _queue.Count : _seats.Count(s => !s.Bot);

        static float Now => Time.realtimeSinceStartup;

        void Update()
        {
            if (_manager == null) return;
            switch (_phase)
            {
                case Phase.Queue:
                    if (_queue.Count > 0 && (Now >= _deadline || _queue.Count >= MatchSettings.TeamSize * 2)) StartLobby();
                    break;
                case Phase.Lobby:
                    UpdateLobby();
                    break;
                default:
                    return;
            }
            if (Now >= _nextBroadcast) BroadcastLobby();
        }

        // ---------- Players coming and going ----------

        void AddPlayer(ulong clientId)
        {
            if (_phase == Phase.Queue)
            {
                if (_queue.Count == 0)
                {
                    _deadline = Now + NetLobby.QueueSeconds;
                    Debug.Log($"[GameServer] {NameOf(clientId)} is looking for a match: waiting {NetLobby.QueueSeconds:0}s for more players.");
                }
                _queue.Add(clientId);
            }
            else
            {
                TakeSeat(clientId);
            }
            Debug.Log($"[GameServer] Client {clientId} ({NameOf(clientId)}) joined the {(_phase == Phase.Queue ? "queue" : "lobby")}.");
            BroadcastLobby();
        }

        void RemovePlayer(ulong clientId)
        {
            if (_phase == Phase.Queue)
            {
                _queue.Remove(clientId);
            }
            else
            {
                LobbySeat seat = _seats.Find(s => !s.Bot && s.ClientId == clientId);
                if (seat != null)
                {
                    if (BotsOn(seat.Team) < _bots)
                    {
                        // A bot takes over (keeping a locked-in pick).
                        seat.Bot = true;
                        seat.BotPickAt = seat.Locked ? 0f : Now + BotFirstPick;
                    }
                    else
                    {
                        _seats.Remove(seat);
                    }
                }
            }
            if (HumanCount == 0) ResetQueue();
            BroadcastLobby();
        }

        // Nobody left: back to an empty queue, with a fresh map if it's random.
        void ResetQueue()
        {
            _phase = Phase.Queue;
            _queue.Clear();
            _seats.Clear();
            _countingDown = false;
            _layout = PickLayout(_mapSetting);
            Debug.Log($"[GameServer] Everyone left. Waiting for players (map: {_layout.displayName}).");
        }

        // ---------- Champion select ----------

        void StartLobby()
        {
            _phase = Phase.Lobby;
            _seats.Clear();
            _countingDown = false;
            _deadline = Now + NetLobby.PickSeconds;
            for (int i = 0; i < _queue.Count; i++)
                _seats.Add(new LobbySeat { Team = i % 2 == 0 ? Team.Blue : Team.Red, ClientId = _queue[i] });
            _queue.Clear();

            float pickAt = Now + BotFirstPick;
            foreach (Team team in new[] { Team.Blue, Team.Red })
                while (SeatsOn(team) < MatchSettings.TeamSize && BotsOn(team) < _bots)
                {
                    _seats.Add(new LobbySeat { Team = team, Bot = true, BotPickAt = pickAt });
                    pickAt += Random.Range(BotPickGapMin, BotPickGapMax);
                }
            Debug.Log($"[GameServer] Champion select: {HumanCount} player(s), {_seats.Count(s => s.Bot)} bot(s) on {_layout.displayName}.");
            BroadcastLobby();
        }

        void UpdateLobby()
        {
            foreach (LobbySeat seat in _seats)
                if (seat.Bot && !seat.Locked && Now >= seat.BotPickAt) LockRandom(seat);

            if (!_countingDown && _seats.Where(s => !s.Bot).All(s => s.Locked))
            {
                _countingDown = true;
                _deadline = Mathf.Min(_deadline, Now + NetLobby.LockedInCountdown);
                BroadcastLobby();
            }
            if (Now < _deadline) return;
            foreach (LobbySeat seat in _seats)
                if (!seat.Locked) LockRandom(seat);
            StartMatch();
        }

        // A player joining champion select: a free seat on the team with fewer players, otherwise a bot's seat.
        void TakeSeat(ulong clientId)
        {
            int blue = _seats.Count(s => !s.Bot && s.Team == Team.Blue);
            int red = _seats.Count(s => !s.Bot && s.Team == Team.Red);
            foreach (Team team in red < blue ? new[] { Team.Red, Team.Blue } : new[] { Team.Blue, Team.Red })
            {
                LobbySeat seat;
                if (SeatsOn(team) < MatchSettings.TeamSize)
                {
                    seat = new LobbySeat { Team = team };
                    _seats.Add(seat);
                }
                else
                {
                    seat = _seats.FirstOrDefault(s => s.Bot && s.Team == team);
                    if (seat == null) continue;
                    seat.Bot = false;
                    seat.Hero = "";
                    seat.Locked = false;
                }
                seat.ClientId = clientId;
                _countingDown = false;
                _deadline = Mathf.Max(_deadline, Now + LateJoinPickSeconds);
                return;
            }
        }

        void OnPickMessage(ulong sender, FastBufferReader reader)
        {
            string heroId = NetLobby.Read(reader);
            LobbySeat seat = _seats.Find(s => !s.Bot && s.ClientId == sender);
            if (_phase != Phase.Lobby || seat == null || seat.Locked) return;
            if (_roster.Find(heroId) == null || TakenOn(seat.Team, heroId, seat)) return;
            seat.Hero = heroId;
            seat.Locked = true;
            Debug.Log($"[GameServer] {NameOf(sender)} locked in {heroId}.");
            BroadcastLobby();
        }

        // Any player may start right away (handy with friends or for tests): everyone still picking gets a random hero.
        void OnStartNowMessage(ulong sender, FastBufferReader reader)
        {
            if (_phase != Phase.Lobby || _seats.All(s => s.Bot || s.ClientId != sender)) return;
            foreach (LobbySeat seat in _seats)
                if (!seat.Locked) LockRandom(seat);
            _countingDown = true;
            _deadline = Mathf.Min(_deadline, Now + StartNowDelay);
            Debug.Log($"[GameServer] {NameOf(sender)} started the match.");
            BroadcastLobby();
        }

        void LockRandom(LobbySeat seat)
        {
            var options = _roster.heroes.Where(h => h != null && !TakenOn(seat.Team, h.id, seat)).ToList();
            if (options.Count == 0) options = _roster.heroes.Where(h => h != null).ToList();
            seat.Hero = options[Random.Range(0, options.Count)].id;
            seat.Locked = true;
        }

        bool TakenOn(Team team, string heroId, LobbySeat except) =>
            _seats.Any(s => s != except && s.Team == team && s.Hero == heroId);

        int SeatsOn(Team team) => _seats.Count(s => s.Team == team);
        int BotsOn(Team team) => _seats.Count(s => s.Bot && s.Team == team);

        // ---------- Telling the clients ----------

        void BroadcastLobby()
        {
            _nextBroadcast = Now + BroadcastInterval;
            if (_manager == null || !_manager.IsListening || _manager.ConnectedClientsIds.Count == 0) return;
            var state = new NetLobby.State
            {
                phase = _phase switch
                {
                    Phase.Queue => NetLobby.PhaseQueue,
                    Phase.Lobby => NetLobby.PhaseLobby,
                    _ => NetLobby.PhaseStarting,
                },
                map = _layout.id,
                timeLeft = _phase is Phase.Queue or Phase.Lobby ? Mathf.Max(0f, _deadline - Now) : 0f,
                players = HumanCount,
            };
            foreach (LobbySeat seat in _seats)
                state.seats.Add(new NetLobby.Seat
                {
                    team = seat.Team.ToString(),
                    bot = seat.Bot,
                    client = seat.Bot ? -1 : (long)seat.ClientId,
                    name = seat.Bot ? "Bot" : NameOf(seat.ClientId),
                    hero = seat.Hero,
                    locked = seat.Locked,
                });
            NetLobby.SendToAll(NetLobby.StateMessage, JsonUtility.ToJson(state));
        }
    }
}
