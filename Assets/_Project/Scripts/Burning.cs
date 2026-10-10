using UnityEngine;

namespace Moba
{
    // Damage over time. Re-applying refreshes the duration and keeps the stronger burn (they don't stack).
    // Damage is credited to the source, so a burn can land the last hit.
    [DisallowMultipleComponent]
    public class Burning : MonoBehaviour
    {
        const float TickInterval = 0.5f;

        Unit _unit;
        Unit _source;
        float _dps;
        float _remaining;
        float _tick;
        float _pending;

        public static void Apply(Unit target, float damagePerSecond, float duration, Unit source)
        {
            if (target == null || !target.IsAlive || damagePerSecond <= 0f || duration <= 0f) return;
            if (!target.TryGetComponent(out Burning burn)) burn = target.gameObject.AddComponent<Burning>();
            burn.Refresh(damagePerSecond, duration, source);
        }

        public static bool IsBurning(Unit unit) =>
            unit != null && unit.TryGetComponent(out Burning burn) && burn.enabled && burn._remaining > 0f;

        void Awake() => _unit = GetComponent<Unit>();

        void Refresh(float dps, float duration, Unit source)
        {
            if (_remaining <= 0f || dps >= _dps)
            {
                _dps = dps;
                _source = source;
            }
            _remaining = Mathf.Max(_remaining, duration);
            enabled = true;
        }

        void Update()
        {
            if (_remaining <= 0f || _unit == null || !_unit.IsAlive)
            {
                _remaining = 0f;
                _pending = 0f;
                _tick = 0f;
                enabled = false;
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, _remaining);
            _remaining -= dt;
            _pending += _dps * dt;
            _tick += dt;
            if (_tick < TickInterval && _remaining > 0f) return;

            _tick = 0f;
            float damage = _pending;
            _pending = 0f;
            _unit.TakeDamage(damage, _source);
        }
    }
}
