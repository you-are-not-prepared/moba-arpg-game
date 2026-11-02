using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // Marks a circle at a point; after a short warning everything inside takes damage. Adaptive in the shop (Burst):
    // a meteor, falling arrows, ice shards, thorns, a lightning strike or a cyclone, in the hero's colours.
    [CreateAssetMenu(menuName = "MOBA/Spells/Burst Spell", fileName = "BurstSpell")]
    public class BurstSpell : AbilityDefinition
    {
        public float radius = 2.5f;
        [Tooltip("Seconds between the cast and the hit.")]
        public float delay = 0.6f;
        public float[] damageByRank = { 80f, 110f, 140f, 170f, 200f };

        void Reset()
        {
            tags = SpellTags.AreaOfEffect;
            targeting = AbilityTargeting.Point;
            range = 9f;
            areaRadius = radius;
        }

        public float DamageAt(int rank) =>
            damageByRank is { Length: > 0 } ? damageByRank[Mathf.Clamp(rank - 1, 0, damageByRank.Length - 1)] : 0f;

        public override void Execute(in AbilityCastContext context)
        {
            Unit caster = context.Caster;
            Vector3 center = context.Point;
            SpellLook.Play(SpellLookKind.Burst, context.Modifiers.damageType, caster, center, Vector3.zero, radius, delay);
            SpellModifiers m = context.Modifiers;
            float damage = DamageAt(context.Rank) * m.damageMultiplier;
            float r = radius;
            Delayed.Run(delay, () => SpellArea.Circle(caster, center, r, damage, m));
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers m)
        {
            var parts = new List<string> { $"after {delay:0.#}s, {DamageAt(rank) * m.damageMultiplier:0} {DamageWord(m)} in a {radius:0.#}m circle" };
            if (m.burnFraction > 0f) parts.Add($"burns for {DamageAt(rank) * m.damageMultiplier * m.burnFraction:0} over {m.burnDuration:0.#}s");
            return string.Join(" · ", parts);
        }
    }
}
