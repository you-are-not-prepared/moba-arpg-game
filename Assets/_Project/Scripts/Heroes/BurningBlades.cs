using UnityEngine;

namespace Moba
{
    // Timed buff (after Oren's Blazing Leap): the unit's basic attacks set their target burning. Attacker asks it on
    // every hit (BurnOnHit).
    [DisallowMultipleComponent]
    public class BurningBlades : MonoBehaviour
    {
        const float BurnSeconds = 3f;

        Unit _unit;
        float _remaining;
        float _burnPerSecond;

        public float Remaining => _remaining;

        public static void Apply(Unit unit, float duration, float burnPerSecond)
        {
            if (unit == null || !unit.IsAlive || duration <= 0f || burnPerSecond <= 0f) return;
            if (!unit.TryGetComponent(out BurningBlades blades)) blades = unit.gameObject.AddComponent<BurningBlades>();
            blades._remaining = duration;
            blades._burnPerSecond = burnPerSecond;
            blades.enabled = true;
        }

        // Called by Attacker when `attacker` hits `target`.
        public static void BurnOnHit(Unit attacker, Unit target)
        {
            if (attacker.TryGetComponent(out BurningBlades blades) && blades.enabled && target.IsAlive)
                Burning.Apply(target, blades._burnPerSecond, BurnSeconds, attacker);
        }

        void Awake() => _unit = GetComponent<Unit>();

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f || !_unit.IsAlive) enabled = false;
        }
    }
}
