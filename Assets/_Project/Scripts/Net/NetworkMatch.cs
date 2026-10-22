using Moba.Abilities;
using Unity.Netcode;
using UnityEngine;

namespace Moba.Net
{
    // Match-wide state on the game server (on the scene's Game object): the winner, the match clock, the next creep
    // wave, whether cheats are allowed, and visuals for attack and spell projectiles and explosions (the server
    // applies the damage; clients only see the shots).
    [RequireComponent(typeof(GameManager))]
    public class NetworkMatch : NetworkBehaviour
    {
        public static NetworkMatch Instance { get; private set; }

        // Whether this game server lets players use the cheat menu (GameServer's "cheats" setting).
        public static bool CheatsAllowed => Instance != null && Instance.IsSpawned && Instance._cheats.Value;

        // -1 = still running, otherwise (int)Team.
        readonly NetworkVariable<int> _winner = new(-1);
        readonly NetworkVariable<bool> _cheats = new();
        // Server times: when the match clock read 0, and when the blue team's next creep wave comes.
        readonly NetworkVariable<float> _clockStart = new();
        readonly NetworkVariable<float> _nextWave = new();

        // Smaller drifts aren't worth a message.
        const float ClockTolerance = 0.25f;

        GameManager _game;
        int _nextProjectileId;

        void Awake()
        {
            Instance = this;
            _game = GetComponent<GameManager>();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _cheats.Value = GameServer.CheatsAllowed;
                _game.MatchEnded += winner => _winner.Value = (int)winner;
                return;
            }
            _winner.OnValueChanged += (_, winner) => ApplyWinner(winner);
            ApplyWinner(_winner.Value);
        }

        void Update()
        {
            if (!IsSpawned) return;
            float now = NetworkManager.ServerTime.TimeAsFloat;
            CreepSpawner spawner = _game.playerSpawner;
            if (IsServer)
            {
                float start = now - _game.MatchTime;
                if (Mathf.Abs(start - _clockStart.Value) > ClockTolerance) _clockStart.Value = start;
                if (spawner != null)
                {
                    float wave = now + spawner.NextWaveIn;
                    if (Mathf.Abs(wave - _nextWave.Value) > ClockTolerance) _nextWave.Value = wave;
                }
                return;
            }
            if (_game.IsOver) return; // the clock stops at the end
            _game.SetMatchTimeFromServer(Mathf.Max(0f, now - _clockStart.Value));
            if (spawner != null) spawner.SetNextWaveFromServer(Mathf.Max(0f, _nextWave.Value - now));
        }

        void ApplyWinner(int winner)
        {
            if (winner >= 0) _game.SetWinnerFromServer((Team)winner);
        }

        // Server: show a basic-attack projectile on every client.
        public void BroadcastProjectile(Unit source, Unit target)
        {
            if (!IsSpawned || !IsServer || source == null || target == null) return;
            if (!source.TryGetComponent(out NetworkObject from) || !target.TryGetComponent(out NetworkObject to)) return;
            if (!from.IsSpawned || !to.IsSpawned) return;
            ProjectileClientRpc(from, to);
        }

        [ClientRpc]
        void ProjectileClientRpc(NetworkObjectReference source, NetworkObjectReference target)
        {
            if (IsServer) return;
            if (!source.TryGet(out NetworkObject from) || !target.TryGet(out NetworkObject to)) return;
            if (!from.TryGetComponent(out Attacker attacker) || !to.TryGetComponent(out Unit targetUnit)) return;
            Projectile.Launch(from.transform.TransformPoint(attacker.projectileOrigin), from.GetComponent<Unit>(), targetUnit,
                0f, attacker.projectileSpeed, attacker.projectileSize, attacker.projectileMaterial, visualOnly: true);
        }

        // Server: show a spell projectile on every client. Returns its id for BroadcastSpellProjectileEnd (0 = not sent).
        public int BroadcastSpellProjectile(Vector3 position, Vector3 direction, in SpellProjectile.Payload p)
        {
            if (!IsSpawned || !IsServer) return 0;
            int id = ++_nextProjectileId;
            SpellProjectileClientRpc(id, position, direction, p.Speed, p.MaxDistance, p.Size, (byte)p.Team,
                NetItems.MaterialId(p.ProjectileMaterial));
            return id;
        }

        public void BroadcastSpellProjectileEnd(int id, Vector3 position)
        {
            if (IsSpawned && IsServer) SpellProjectileEndClientRpc(id, position);
        }

        // Server: show an explosion (or a shield's block flash) on every client.
        public void BroadcastExplosion(Vector3 center, float radius, Material material)
        {
            if (IsSpawned && IsServer) ExplosionClientRpc(center, radius, NetItems.MaterialId(material));
        }

        [ClientRpc]
        void SpellProjectileClientRpc(int id, Vector3 position, Vector3 direction, float speed, float maxDistance, float size,
            byte team, short material)
        {
            if (IsServer) return;
            var payload = new SpellProjectile.Payload
            {
                Team = (Team)team,
                Speed = speed,
                MaxDistance = maxDistance,
                Size = size,
                ProjectileMaterial = NetItems.Material(material),
            };
            SpellProjectile.LaunchVisual(id, position, direction, payload);
        }

        [ClientRpc]
        void SpellProjectileEndClientRpc(int id, Vector3 position)
        {
            if (!IsServer) SpellProjectile.EndVisual(id, position);
        }

        [ClientRpc]
        void ExplosionClientRpc(Vector3 center, float radius, short material)
        {
            if (!IsServer) SpellExplosion.Spawn(center, radius, NetItems.Material(material));
        }
    }
}
