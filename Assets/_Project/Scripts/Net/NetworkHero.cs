using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Moba.Net
{
    // A hero on the game server.
    //  - Server -> clients: which hero it is, level, XP, gold, K/D/A, CS and the respawn timer.
    //  - Owning client -> server: the player's orders (move, attack, attack move, stop). The server checks them
    //    and runs them through the same HeroController code offline play uses.
    [RequireComponent(typeof(HeroController))]
    public class NetworkHero : NetworkBehaviour
    {
        readonly NetworkVariable<FixedString32Bytes> _heroId = new();
        readonly NetworkVariable<FixedString64Bytes> _playerName = new();
        readonly NetworkVariable<int> _level = new(1);
        readonly NetworkVariable<float> _xp = new();
        readonly NetworkVariable<int> _gold = new();
        readonly NetworkVariable<int> _kills = new();
        readonly NetworkVariable<int> _deaths = new();
        readonly NetworkVariable<int> _assists = new();
        readonly NetworkVariable<int> _creepScore = new();
        readonly NetworkVariable<float> _respawn = new();

        HeroController _hero;
        HeroProgression _progression;

        public string HeroId => _heroId.Value.ToString();
        public string PlayerName => _playerName.Value.ToString();
        // Bots are spawned by the server and owned by it; players own their hero.
        public bool IsBot => OwnerClientId == NetworkManager.ServerClientId;

        void Awake()
        {
            _hero = GetComponent<HeroController>();
            _progression = GetComponent<HeroProgression>();
        }

        string _initHeroId = "", _initPlayerName = "";

        // Server, before Spawn(): applied in OnNetworkSpawn, once the network variables can be written.
        public void Init(string heroId, string playerName)
        {
            _initHeroId = heroId ?? "";
            _initPlayerName = playerName ?? "";
        }

        public override void OnNetworkSpawn()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.heroes.Contains(_hero)) game.heroes.Add(_hero);
            if (IsServer)
            {
                _heroId.Value = _initHeroId;
                _playerName.Value = _initPlayerName;
                _hero.isPlayer = false; // no local input on the server
                return;
            }

            // Client: show the right hero, mirror its progression, and take input if it's ours.
            HeroRoster roster = FindFirstObjectByType<MatchBootstrap>()?.roster;
            if (roster != null) _hero.definition = roster.Find(HeroId);
            if (_hero.definition != null)
            {
                _hero.name = IsBot ? $"{_hero.definition.displayName} (bot)" : _hero.definition.displayName;
                // Shown in the R slot (casting it through the server comes with spells).
                if (_hero.definition.ultimate != null)
                    _hero.Abilities.SetAbility(Abilities.AbilityCaster.UltimateSlot, _hero.definition.ultimate);
            }
            _hero.isPlayer = IsOwner;
            if (IsOwner)
            {
                _hero.Remote = this;
                if (game != null)
                {
                    game.hero = _hero;
                    if (game.cameraRig != null)
                    {
                        game.cameraRig.hero = _hero;
                        game.cameraRig.transform.position = transform.position;
                    }
                }
            }
            ApplyProgression();
        }

        public override void OnNetworkDespawn()
        {
            GameManager game = GameManager.Instance;
            if (game != null) game.heroes.Remove(_hero);
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                _level.Value = _progression.Level;
                _xp.Value = _progression.Xp;
                _gold.Value = _progression.Gold;
                _kills.Value = _progression.Kills;
                _deaths.Value = _progression.Deaths;
                _assists.Value = _progression.Assists;
                _creepScore.Value = _progression.CreepScore;
                _respawn.Value = _hero.Unit.IsAlive ? 0f : Mathf.Max(0f, _hero.RespawnRemaining);
            }
            else
            {
                ApplyProgression();
            }
        }

        void ApplyProgression()
        {
            _progression.SetFromServer(_level.Value, _xp.Value, _gold.Value, _kills.Value, _deaths.Value,
                _assists.Value, _creepScore.Value);
            _hero.SetRespawnFromServer(_hero.Unit.IsAlive, _respawn.Value);
        }

        // ---------- Orders (owning client -> server) ----------

        public void SendMove(Vector3 point) => MoveServerRpc(point);
        public void SendAttack(Unit target) { if (TryRef(target, out NetworkObjectReference r)) AttackServerRpc(r); }
        public void SendAttackMove(Vector3 point, Unit direct)
        {
            bool hasTarget = TryRef(direct, out NetworkObjectReference r);
            AttackMoveServerRpc(point, hasTarget, r);
        }
        public void SendStop() => StopServerRpc();

        [ServerRpc]
        void MoveServerRpc(Vector3 point) => _hero.OrderMove(point, showMarker: false);

        [ServerRpc]
        void AttackServerRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out NetworkObject obj) && obj.TryGetComponent(out Unit unit) && unit.IsEnemyOf(_hero.Unit))
                _hero.OrderAttack(unit);
        }

        [ServerRpc]
        void AttackMoveServerRpc(Vector3 point, bool hasTarget, NetworkObjectReference target)
        {
            Unit direct = null;
            if (hasTarget && target.TryGet(out NetworkObject obj) && obj.TryGetComponent(out Unit unit) && unit.IsEnemyOf(_hero.Unit))
                direct = unit;
            _hero.IssueAttackMove(point, direct);
        }

        [ServerRpc]
        void StopServerRpc() => _hero.Stop();

        static bool TryRef(Unit unit, out NetworkObjectReference reference)
        {
            reference = default;
            if (unit == null || !unit.TryGetComponent(out NetworkObject obj) || !obj.IsSpawned) return false;
            reference = obj;
            return true;
        }
    }
}
