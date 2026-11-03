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
        [Tooltip("More than 1: that many bursts in a line from the caster toward the point, `spacing` apart, one after another.")]
        public int count = 1;
        public float spacing = 3f;
        public float stunSeconds;
        [Range(0f, 0.9f)] public float slow;
        public float slowSeconds = 2f;
        [Tooltip("Each hit burns for this fraction of its damage over 3s.")]
        public float burnFraction;

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
            SpellModifiers m = context.Modifiers;
            if (burnFraction > 0f && m.burnFraction <= 0f)
            {
                m.burnFraction = burnFraction;
                m.burnDuration = 3f;
            }
            float damage = DamageAt(context.Rank) * m.damageMultiplier;
            float r = m.Radius(radius);
            var effects = new SpellHitEffects
            {
                Stun = stunSeconds > 0f ? stunSeconds + HeroAscension.StunBonusOf(caster) : 0f,
                Slow = slow,
                SlowSeconds = slowSeconds,
            };
            Vector3 dir = context.Point - caster.transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : caster.transform.forward;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                // A line ends at the target point; each burst lands a little after the one before.
                Vector3 center = count > 1 ? context.Point - dir * spacing * (count - 1 - i) : context.Point;
                float wait = delay + i * 0.25f;
                SpellLook.Play(SpellLookKind.Burst, m.damageType, caster, center, Vector3.zero, r, wait);
                Delayed.Run(wait, () => SpellArea.Circle(caster, center, r, damage, m, effects));
            }
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers m)
        {
            string where = count > 1 ? $"{count} {m.Radius(radius):0.#}m circles in a line" : $"a {m.Radius(radius):0.#}m circle";
            var parts = new List<string> { $"after {delay:0.#}s, {DamageAt(rank) * m.damageMultiplier:0} {DamageWord(m)} in {where}" };
            if (stunSeconds > 0f) parts.Add($"stuns for {stunSeconds:0.#}s");
            if (slow > 0f) parts.Add($"slows {slow * 100f:0}% for {slowSeconds:0.#}s");
            if (burnFraction > 0f && m.burnFraction <= 0f) parts.Add("burns");
            if (m.burnFraction > 0f) parts.Add($"burns for {DamageAt(rank) * m.damageMultiplier * m.burnFraction:0} over {m.burnDuration:0.#}s");
            return string.Join(" · ", parts);
        }
    }
}
