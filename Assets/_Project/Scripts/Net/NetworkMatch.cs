using Unity.Netcode;
using UnityEngine;

namespace Moba.Net
{
    // Match-wide state on the game server (on the scene's Game object): the winner, and visuals for basic-attack
    // projectiles (the server applies the damage; clients only see the shot).
    [RequireComponent(typeof(GameManager))]
    public class NetworkMatch : NetworkBehaviour
    {
        public static NetworkMatch Instance { get; private set; }

        // -1 = still running, otherwise (int)Team.
        readonly NetworkVariable<int> _winner = new(-1);

        GameManager _game;

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
                _game.MatchEnded += winner => _winner.Value = (int)winner;
                return;
            }
            _winner.OnValueChanged += (_, winner) => ApplyWinner(winner);
            ApplyWinner(_winner.Value);
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
    }
}
