using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // A timed stat buff from an ultimate (BuffSpell): attack speed, attack range, movement speed, damage taken, and
    // extra Heat per attack. Casting again refreshes it. A ring in the hero's spell colour shows it's active.
    // On a game server the clients only show the ring (SyncFromServer); the server applies the stats.
    [DisallowMultipleComponent]
    public class Buff : MonoBehaviour
    {
        public struct Stats
        {
            public float AttackSpeed;
            public float AttackRange;
            public float MoveSpeed;
            // 0.4 = takes 40% less damage.
            public float DamageReduction;
            public int HeatPerAttack;
        }

        Unit _unit;
        NavMeshAgent _agent;
        Attacker _attacker;
        float _remaining;
        Stats _applied;
        bool _statsApplied;
        Transform _ring;
        Renderer _ringRenderer;

        public float Remaining => _remaining;

        public static void Apply(Unit unit, float duration, in Stats stats)
        {
            if (unit == null || !unit.IsAlive || duration <= 0f) return;
            Buff buff = Get(unit);
            buff.RemoveStats();
            buff.AddStats(stats);
            buff.Begin(duration);
        }

        // Game-server clients: show the ring the server reports (`remaining` 0 = none).
        public static void SyncFromServer(Unit unit, float remaining)
        {
            bool active = unit.TryGetComponent(out Buff buff) && buff.enabled;
            if (remaining <= 0f)
            {
                if (active) buff._remaining = 0f;
                return;
            }
            if (active) buff._remaining = remaining;
            else Get(unit).Begin(remaining);
        }

        static Buff Get(Unit unit) =>
            unit.TryGetComponent(out Buff buff) ? buff : unit.gameObject.AddComponent<Buff>();

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _agent = GetComponent<NavMeshAgent>();
            _attacker = GetComponent<Attacker>();
        }

        void OnDisable()
        {
            RemoveStats();
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        void Begin(float duration)
        {
            _remaining = duration;
            if (_ring == null)
            {
                Color color = Abilities.SpellLook.ColorsOf(_unit)[0];
                _ring = Abilities.SpellLook.Part(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f),
                    new Vector3(2.6f, 0.01f, 2.6f), Abilities.SpellLook.Ring(color));
                _ring.name = "BuffAura";
                _ringRenderer = _ring.GetComponent<Renderer>();
            }
            _ring.gameObject.SetActive(true);
            enabled = true;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f || !_unit.IsAlive)
            {
                _remaining = 0f;
                enabled = false;
                return;
            }
            float pulse = 2.6f + 0.25f * Mathf.Sin(Time.time * 8f);
            _ring.localScale = new Vector3(pulse, 0.01f, pulse);
            _ringRenderer.enabled = FogOfWar.ShouldShow(_unit.team, transform.position);
        }

        void AddStats(in Stats s)
        {
            if (_attacker != null)
            {
                _attacker.attackSpeedMultiplier *= 1f + s.AttackSpeed;
                _attacker.range += s.AttackRange;
            }
            if (_agent != null) _agent.speed *= 1f + s.MoveSpeed;
            _unit.incomingDamageMultiplier *= 1f - s.DamageReduction;
            if (TryGetComponent(out KindledBlade heat)) heat.extraStacksPerAttack += s.HeatPerAttack;
            _applied = s;
            _statsApplied = true;
        }

        void RemoveStats()
        {
            if (!_statsApplied) return;
            _statsApplied = false;
            Stats s = _applied;
            if (_attacker != null)
            {
                _attacker.attackSpeedMultiplier /= 1f + s.AttackSpeed;
                _attacker.range -= s.AttackRange;
            }
            if (_agent != null) _agent.speed /= 1f + s.MoveSpeed;
            if (s.DamageReduction < 1f) _unit.incomingDamageMultiplier /= 1f - s.DamageReduction;
            if (TryGetComponent(out KindledBlade heat)) heat.extraStacksPerAttack -= s.HeatPerAttack;
        }
    }
}
