using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Slowed units move slower. Structures are immune. Re-applying keeps the stronger slow and the longer duration.
    // The speed change swaps multipliers (like Haste), so other speed changes are kept.
    [DisallowMultipleComponent]
    public class Slow : MonoBehaviour
    {
        NavMeshAgent _agent;
        float _remaining;
        float _strength;
        float _applied = 1f;
        bool _fromServer;

        public static void Apply(Unit target, float strength, float duration)
        {
            if (target == null || !target.IsAlive || duration <= 0f || strength <= 0f) return;
            if (target.kind == UnitKind.Tower || target.kind == UnitKind.Core) return;
            if (!target.TryGetComponent(out Slow slow)) slow = target.gameObject.AddComponent<Slow>();
            slow.Refresh(Mathf.Clamp01(strength), duration);
        }

        public static bool IsSlowed(Unit unit) =>
            unit != null && unit.TryGetComponent(out Slow slow) && slow.enabled && slow._remaining > 0f;

        // Game-server clients: slowed for as long as the server says (shown only; the server moves the unit).
        public static void SetFromServer(Unit unit, bool slowed)
        {
            if (!unit.TryGetComponent(out Slow slow))
            {
                if (!slowed) return;
                slow = unit.gameObject.AddComponent<Slow>();
            }
            slow._fromServer = true;
            slow._remaining = slowed ? float.PositiveInfinity : 0f;
            slow.enabled = slowed;
        }

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        void Refresh(float strength, float duration)
        {
            if (_remaining <= 0f || strength >= _strength) _strength = strength;
            _remaining = Mathf.Max(_remaining, duration);
            SetMultiplier(1f - _strength);
            enabled = true;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining > 0f) return;
            _remaining = 0f;
            enabled = false;
        }

        void OnDisable()
        {
            _strength = 0f;
            SetMultiplier(1f);
        }

        void SetMultiplier(float multiplier)
        {
            if (_fromServer) return;
            if (_agent != null) _agent.speed = _agent.speed / _applied * multiplier;
            _applied = multiplier;
        }
    }
}
