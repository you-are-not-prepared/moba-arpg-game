using UnityEngine;

namespace Moba
{
    // Stunned units can't move, attack or cast. Structures are immune. Re-applying keeps the longer duration.
    [DisallowMultipleComponent]
    public class Stun : MonoBehaviour
    {
        float _remaining;

        public static void Apply(Unit target, float duration)
        {
            if (target == null || !target.IsAlive || duration <= 0f) return;
            if (target.kind == UnitKind.Tower || target.kind == UnitKind.Core) return;
            if (!target.TryGetComponent(out Stun stun)) stun = target.gameObject.AddComponent<Stun>();
            stun._remaining = Mathf.Max(stun._remaining, duration);
            stun.enabled = true;
        }

        public static bool IsStunned(Unit unit) =>
            unit != null && unit.TryGetComponent(out Stun stun) && stun.enabled && stun._remaining > 0f;

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                enabled = false;
            }
        }
    }
}
