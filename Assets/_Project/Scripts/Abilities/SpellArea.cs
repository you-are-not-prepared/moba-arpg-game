using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // What a hit does besides damage (all optional).
    public struct SpellHitEffects
    {
        public float Stun;
        // 0.3 = 30% slower for SlowSeconds.
        public float Slow;
        public float SlowSeconds;
        // Bonus damage: this fraction of the target's missing health.
        public float Execute;
    }

    // Damage to every enemy in a circle or a cone, as spells hit: typed damage, the spell-hit event (for passives),
    // a mod's burn, and stuns, slows or executes. Returns how many enemies were hit.
    public static class SpellArea
    {
        static readonly List<Unit> s_Targets = new();

        public static int Circle(Unit caster, Vector3 center, float radius, float damage, in SpellModifiers m,
            SpellHitEffects effects = default) =>
            Hit(caster, damage, m, effects, u => Flat(u.transform.position - center).magnitude - u.radius <= radius);

        // Enemies within `reach` of the caster's edge and `angle` degrees around `direction`.
        public static int Cone(Unit caster, Vector3 direction, float reach, float angle, float damage, in SpellModifiers m,
            SpellHitEffects effects = default)
        {
            Vector3 origin = caster.transform.position;
            Vector3 forward = Flat(direction).normalized;
            return Hit(caster, damage, m, effects, u =>
            {
                Vector3 to = Flat(u.transform.position - origin);
                if (to.magnitude - u.radius - caster.radius > reach) return false;
                // Units overlapping the caster always count.
                return to.magnitude <= caster.radius + u.radius || Vector3.Angle(forward, to) <= angle * 0.5f;
            });
        }

        static int Hit(Unit caster, float damage, SpellModifiers m, SpellHitEffects fx, System.Func<Unit, bool> inside)
        {
            if (caster == null) return 0;
            s_Targets.Clear();
            foreach (Unit u in Unit.All)
                if (u.IsEnemyOf(caster) && u.IsTargetable && inside(u)) s_Targets.Add(u);
            int count = s_Targets.Count;
            float burnDps = m.burnFraction > 0f && m.burnDuration > 0f ? damage * m.burnFraction / m.burnDuration : 0f;
            foreach (Unit u in s_Targets)
            {
                float hit = damage + (fx.Execute > 0f ? (u.maxHealth - u.Health) * fx.Execute : 0f);
                u.TakeDamage(hit, caster, m.damageType);
                SpellEvents.RaiseHit(caster, u, hit);
                if (!u.IsAlive) continue;
                if (burnDps > 0f) Burning.Apply(u, burnDps, m.burnDuration, caster, m.damageType);
                if (fx.Stun > 0f) Stun.Apply(u, fx.Stun);
                if (fx.Slow > 0f && fx.SlowSeconds > 0f) Slow.Apply(u, fx.Slow, fx.SlowSeconds);
            }
            s_Targets.Clear();
            return count;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
