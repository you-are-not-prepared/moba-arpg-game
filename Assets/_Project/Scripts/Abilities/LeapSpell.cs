using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Moba.Abilities
{
    // Leaps to a point (walking into range first): damage and a burn around the landing spot, then the caster's
    // basic attacks burn for a while (BurningBlades).
    [CreateAssetMenu(menuName = "MOBA/Spells/Leap", fileName = "Leap")]
    public class LeapSpell : AbilityDefinition
    {
        [Header("Landing")]
        [Tooltip("Damage per rank (last entry repeats).")]
        public float[] damageByRank = { 150f, 225f, 300f };
        public float landingRadius = 3f;
        [Tooltip("Burn damage as a fraction of the landing damage, dealt over burnSeconds.")]
        public float burnFraction = 0.3f;
        public float burnSeconds = 3f;
        public Material landingMaterial;

        [Header("Leap")]
        public float leapSeconds = 0.35f;
        public float arcHeight = 2f;

        [Header("Burning blades afterwards")]
        public float bladesSeconds = 5f;
        public float bladesBurnPerSecond = 15f;

        static readonly List<Unit> s_Targets = new();

        void Reset()
        {
            targeting = AbilityTargeting.Point;
            range = 6f;
            areaRadius = 3f;
        }

        public float DamageAt(int rank) =>
            damageByRank is { Length: > 0 } ? damageByRank[Mathf.Clamp(rank - 1, 0, damageByRank.Length - 1)] : 0f;

        public override void Execute(in AbilityCastContext context)
        {
            Unit caster = context.Caster;
            Vector3 to = context.Point;
            if (NavMesh.SamplePosition(to, out NavMeshHit hit, 3f, NavMesh.AllAreas)) to = hit.position;
            else to = caster.transform.position;
            float damage = DamageAt(context.Rank) * context.Modifiers.damageMultiplier;
            float bladesDuration = bladesSeconds + HeroAscension.DurationBonusOf(caster);
            Leap.Perform(caster, to, leapSeconds, arcHeight, () => Land(caster, damage, bladesDuration));
        }

        void Land(Unit caster, float damage, float bladesDuration)
        {
            if (caster == null || !caster.IsAlive) return;
            Vector3 center = caster.transform.position;
            s_Targets.Clear();
            foreach (Unit u in Unit.All)
            {
                if (!u.IsEnemyOf(caster) || !u.IsTargetable) continue;
                Vector3 d = u.transform.position - center;
                d.y = 0f;
                if (d.magnitude - u.radius <= landingRadius) s_Targets.Add(u);
            }
            foreach (Unit u in s_Targets)
            {
                u.TakeDamage(damage, caster);
                SpellEvents.RaiseHit(caster, u, damage);
                if (u.IsAlive && burnFraction > 0f) Burning.Apply(u, damage * burnFraction / burnSeconds, burnSeconds, caster);
            }
            s_Targets.Clear();
            SpellExplosion.Spawn(center, landingRadius, landingMaterial);
            BurningBlades.Apply(caster, bladesDuration, bladesBurnPerSecond);
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers modifiers) =>
            $"Leap up to {range:0.#}m: {DamageAt(rank) * modifiers.damageMultiplier:0} damage in {landingRadius:0.#}m and a burn · " +
            $"attacks burn for {bladesSeconds:0.#}s";
    }
}
