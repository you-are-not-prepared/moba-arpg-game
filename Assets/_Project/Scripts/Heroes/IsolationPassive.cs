using UnityEngine;

namespace Moba
{
    // Hero passive: more damage against isolated targets, i.e. with no other hero of their own team nearby.
    // Applies to everything the hero deals (attacks, spells, burns).
    [RequireComponent(typeof(Unit))]
    public class IsolationPassive : MonoBehaviour, IOutgoingDamageModifier
    {
        [Tooltip("0.15 = 15% more damage.")]
        public float damageBonus = 0.15f;
        [Tooltip("A target is isolated when no allied hero of theirs is within this distance (XZ).")]
        public float radius = 7f;

        Unit _unit;

        void Awake() => _unit = GetComponent<Unit>();
        void OnEnable() => _unit.AddDamageModifier(this);
        void OnDisable() => _unit.RemoveDamageModifier(this);

        public float ModifyOutgoingDamage(Unit target, float amount) =>
            IsIsolated(target) ? amount * (1f + damageBonus) : amount;

        public bool IsIsolated(Unit target)
        {
            if (target == null) return false;
            float r2 = radius * radius;
            foreach (Unit u in Unit.All)
            {
                if (u == target || u.kind != UnitKind.Hero || u.team != target.team || !u.IsAlive) continue;
                Vector3 d = u.transform.position - target.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return false;
            }
            return true;
        }
    }
}
