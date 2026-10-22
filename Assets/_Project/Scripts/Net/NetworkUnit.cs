using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Moba.Net
{
    // Replicates a Unit (hero, creep, tower, core) from the server: team, health and max health.
    // On a connected client it also switches off everything that would simulate the unit locally (AI, the
    // NavMeshAgent, regeneration...), so the client only shows what the server says.
    [RequireComponent(typeof(Unit))]
    public class NetworkUnit : NetworkBehaviour
    {
        readonly NetworkVariable<byte> _team = new();
        readonly NetworkVariable<float> _health = new();
        readonly NetworkVariable<float> _maxHealth = new();

        Unit _unit;

        void Awake() => _unit = GetComponent<Unit>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                PushState();
                return;
            }

            DisableSimulation();
            _team.OnValueChanged += (_, team) => ApplyTeam((Team)team);
            _health.OnValueChanged += (_, _) => ApplyHealth();
            _maxHealth.OnValueChanged += (_, _) => ApplyHealth();
            ApplyTeam((Team)_team.Value);
            ApplyHealth();
        }

        // Server: copy the unit's state into the network variables (only changes are sent).
        void Update()
        {
            if (IsSpawned && IsServer) PushState();
        }

        void PushState()
        {
            _team.Value = (byte)_unit.team;
            _health.Value = _unit.Health;
            _maxHealth.Value = _unit.maxHealth;
        }

        void ApplyTeam(Team team)
        {
            _unit.team = team;
            if (TryGetComponent(out TeamColors colors)) colors.Apply(team);
        }

        void ApplyHealth() => _unit.SetHealthFromServer(_health.Value, _maxHealth.Value);

        void DisableSimulation()
        {
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is NetworkBehaviour || behaviour is Unit || behaviour is HeroController
                    || behaviour is TeamColors) continue;
                behaviour.enabled = false;
            }
            if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
            if (TryGetComponent(out NavMeshObstacle obstacle)) obstacle.enabled = false;
        }
    }
}
