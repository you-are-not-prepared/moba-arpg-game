using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Moba.Abilities
{
    // A melee spell, either a swing at everything in a cone in front of the caster (Cleave) or, with a lunge, a quick
    // dash to a point that hits everything around where it lands (Lunge). Adaptive in the shop: the swing and the
    // impact take the hero's damage type and colours.
    [CreateAssetMenu(menuName = "MOBA/Spells/Melee Spell", fileName = "MeleeSpell")]
    public class MeleeSpell : AbilityDefinition
    {
        public float[] damageByRank = { 70f, 100f, 130f, 160f, 190f };

        [Header("Swing (no lunge)")]
        [Tooltip("How far the cone reaches past the caster's edge.")]
        public float reach = 3f;
        [Tooltip("Width of the cone in degrees.")]
        public float coneAngle = 120f;

        [Header("Lunge")]
        public bool lunge;
        public float lungeSeconds = 0.25f;
        public float lungeHeight = 0.8f;
        [Tooltip("Hits everything within this radius of where the caster lands.")]
        public float landingRadius = 2.2f;

        [Header("On hit")]
        public float stunSeconds;
        [Tooltip("Bonus damage: this fraction of the target's missing health.")]
        public float executeFraction;
        [Tooltip("Heat the caster gains when the spell hits anything (Kindled Blade).")]
        public int heatStacks;

        void Reset()
        {
            tags = SpellTags.Melee | SpellTags.AreaOfEffect;
            targeting = AbilityTargeting.Point;
            range = 0f;
        }

        public override float IndicatorRange => lunge ? range : reach;

        public float DamageAt(int rank) =>
            damageByRank is { Length: > 0 } ? damageByRank[Mathf.Clamp(rank - 1, 0, damageByRank.Length - 1)] : 0f;

        public override void Execute(in AbilityCastContext context)
        {
            Unit caster = context.Caster;
            SpellModifiers m = context.Modifiers;
            float damage = DamageAt(context.Rank) * m.damageMultiplier;
            Vector3 dir = context.Point - caster.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = caster.transform.forward;
            var effects = new SpellHitEffects
            {
                Stun = stunSeconds > 0f ? stunSeconds + HeroAscension.StunBonusOf(caster) : 0f,
                Execute = executeFraction,
            };
            int heat = heatStacks;

            if (!lunge)
            {
                float r = m.Radius(reach);
                SpellLook.Play(SpellLookKind.Slash, m.damageType, caster, caster.transform.position, dir, caster.radius + r, 0f);
                if (SpellArea.Cone(caster, dir, r, coneAngle, damage, m, effects) > 0) AddHeat(caster, heat);
                return;
            }

            Vector3 to = context.Point;
            to = NavMesh.SamplePosition(to, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : caster.transform.position;
            float radius = m.Radius(landingRadius);
            Leap.Perform(caster, to, lungeSeconds, lungeHeight, () =>
            {
                if (caster == null || !caster.IsAlive) return;
                Vector3 center = caster.transform.position;
                SpellLook.Play(SpellLookKind.Explosion, m.damageType, caster, center, dir, radius, 0f);
                if (SpellArea.Circle(caster, center, radius, damage, m, effects) > 0) AddHeat(caster, heat);
            });
        }

        static void AddHeat(Unit caster, int stacks)
        {
            if (stacks > 0 && caster.TryGetComponent(out KindledBlade blade)) blade.AddStacks(stacks);
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers m)
        {
            string hit = $"{DamageAt(rank) * m.damageMultiplier:0} {DamageWord(m)}";
            var parts = new List<string>
            {
                lunge ? $"dash up to {range:0.#}m, then {hit} within {m.Radius(landingRadius):0.#}m" : $"{hit} in a {coneAngle:0}° cone, {m.Radius(reach):0.#}m",
            };
            if (stunSeconds > 0f) parts.Add($"stuns for {stunSeconds:0.##}s");
            if (executeFraction > 0f) parts.Add($"+{executeFraction * 100f:0}% of the target's missing health");
            if (heatStacks > 0) parts.Add($"+{heatStacks} Heat on hit");
            if (m.burnFraction > 0f) parts.Add($"burns for {DamageAt(rank) * m.damageMultiplier * m.burnFraction:0} over {m.burnDuration:0.#}s");
            return string.Join(" · ", parts);
        }
    }
}
