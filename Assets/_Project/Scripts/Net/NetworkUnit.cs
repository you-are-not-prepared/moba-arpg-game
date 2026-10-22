using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace Moba.Net
{
    // Replicates a Unit (hero, creep, tower, core) from the server: team, health, max health, stunned and burning,
    // and for moving units the facing. Position comes from NetworkTransform (smoothed); facing is applied as is, since
    // units have no turn rate (smoothing it would look like one).
    // On a connected client it also switches off everything that would simulate the unit locally (AI, the
    // NavMeshAgent, regeneration...), so the client only shows what the server says.
    [RequireComponent(typeof(Unit))]
    public class NetworkUnit : NetworkBehaviour
    {
        readonly NetworkVariable<byte> _team = new();
        readonly NetworkVariable<float> _health = new();
        readonly NetworkVariable<float> _maxHealth = new();
        readonly NetworkVariable<byte> _status = new();
        // Facing in hundredths of a degree (moving units only).
        readonly NetworkVariable<ushort> _yaw = new();

        const byte StatusStunned = 1;
        const byte StatusBurning = 2;

        Unit _unit;
        bool _moving;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _moving = TryGetComponent(out NetworkTransform _);
        }

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
            _status.OnValueChanged += (_, _) => ApplyStatus();
            ApplyTeam((Team)_team.Value);
            ApplyHealth();
            ApplyStatus();
        }

        // A destroyed tower or core is part of the map scene, so clients keep their copy unless told: remove it.
        // (Its last health update may not have arrived, so it would otherwise linger with some health left.)
        public override void OnNetworkDespawn()
        {
            if (IsServer || !_unit.destroyOnDeath || NetworkManager.ShutdownInProgress) return;
            _unit.SetHealthFromServer(0f, _unit.maxHealth);
            Destroy(gameObject);
        }

        // Server: copy the unit's state into the network variables (only changes are sent).
        void Update()
        {
            if (IsSpawned && IsServer) PushState();
        }

        void LateUpdate()
        {
            if (IsSpawned && !IsServer && _moving) transform.rotation = Quaternion.Euler(0f, _yaw.Value / 100f, 0f);
        }

        void PushState()
        {
            _team.Value = (byte)_unit.team;
            _health.Value = _unit.Health;
            _maxHealth.Value = _unit.maxHealth;
            _status.Value = (byte)((Stun.IsStunned(_unit) ? StatusStunned : 0) | (Burning.IsBurning(_unit) ? StatusBurning : 0));
            if (_moving) _yaw.Value = (ushort)(Mathf.RoundToInt(Mathf.Repeat(transform.eulerAngles.y, 360f) * 100f) % 36000);
        }

        void ApplyTeam(Team team)
        {
            _unit.team = team;
            if (TryGetComponent(out TeamColors colors)) colors.Apply(team);
        }

        void ApplyHealth() => _unit.SetHealthFromServer(_health.Value, _maxHealth.Value);

        void ApplyStatus()
        {
            Stun.SetFromServer(_unit, (_status.Value & StatusStunned) != 0);
            Burning.SetFromServer(_unit, (_status.Value & StatusBurning) != 0);
        }

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
