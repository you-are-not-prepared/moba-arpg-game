using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // Damage to every enemy in a circle or a cone, as spells hit: typed damage, the spell-hit event (for passives) and
    // a mod's burn. Returns how many enemies were hit.
    public static class SpellArea
    {
        static readonly List<Unit> s_Targets = new();

        public static int Circle(Unit caster, Vector3 center, float radius, float damage, in SpellModifiers m) =>
            Hit(caster, damage, m, u => Flat(u.transform.position - center).magnitude - u.radius <= radius);

        // Enemies within `reach` of the caster's edge and `angle` degrees around `direction`.
        public static int Cone(Unit caster, Vector3 direction, float reach, float angle, float damage, in SpellModifiers m)
        {
            Vector3 origin = caster.transform.position;
            Vector3 forward = Flat(direction).normalized;
            return Hit(caster, damage, m, u =>
            {
                Vector3 to = Flat(u.transform.position - origin);
                if (to.magnitude - u.radius - caster.radius > reach) return false;
                // Units overlapping the caster always count.
                return to.magnitude <= caster.radius + u.radius || Vector3.Angle(forward, to) <= angle * 0.5f;
            });
        }

        static int Hit(Unit caster, float damage, SpellModifiers m, System.Func<Unit, bool> inside)
        {
            if (caster == null) return 0;
            s_Targets.Clear();
            foreach (Unit u in Unit.All)
                if (u.IsEnemyOf(caster) && u.IsTargetable && inside(u)) s_Targets.Add(u);
            int count = s_Targets.Count;
            float burnDps = m.burnFraction > 0f && m.burnDuration > 0f ? damage * m.burnFraction / m.burnDuration : 0f;
            foreach (Unit u in s_Targets)
            {
                u.TakeDamage(damage, caster, m.damageType);
                SpellEvents.RaiseHit(caster, u, damage);
                if (burnDps > 0f && u.IsAlive) Burning.Apply(u, burnDps, m.burnDuration, caster, m.damageType);
            }
            s_Targets.Clear();
            return count;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
