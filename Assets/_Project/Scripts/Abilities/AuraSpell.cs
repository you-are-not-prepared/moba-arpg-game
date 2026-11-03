using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // For a few seconds the caster hits every enemy around them, twice a second, while moving faster: Whirlwind
    // (Physical melee) and Inferno (Fire melee). Each hit spins a swing in the hero's look.
    [CreateAssetMenu(menuName = "MOBA/Spells/Aura Spell", fileName = "AuraSpell")]
    public class AuraSpell : AbilityDefinition
    {
        public float[] durationByRank = { 3f };
        public float radius = 3f;
        public float interval = 0.5f;
        [Tooltip("Damage per hit, per rank.")]
        public float[] damageByRank = { 40f, 60f, 80f };
        [Tooltip("0.15 = 15% faster while it lasts.")]
        public float moveSpeed;
        [Tooltip("Each hit also burns for this fraction of its damage over 2s.")]
        public float burnFraction;

        void Reset()
        {
            tags = SpellTags.Melee | SpellTags.AreaOfEffect;
            targeting = AbilityTargeting.Instant;
            range = 0f;
        }

        static float ByRank(float[] values, int rank) =>
            values is { Length: > 0 } ? values[Mathf.Clamp(rank - 1, 0, values.Length - 1)] : 0f;

        public float Duration(int rank) => ByRank(durationByRank, rank);
        public float DamageAt(int rank) => ByRank(damageByRank, rank);

        public override float IndicatorRange => radius;

        public override void Execute(in AbilityCastContext context)
        {
            SpellModifiers m = context.Modifiers;
            if (burnFraction > 0f && m.burnFraction <= 0f)
            {
                m.burnFraction = burnFraction;
                m.burnDuration = 2f;
            }
            float duration = Duration(context.Rank) + HeroAscension.DurationBonusOf(context.Caster);
            Aura.Apply(context.Caster, duration, interval, m.Radius(radius), DamageAt(context.Rank) * m.damageMultiplier, m);
            if (moveSpeed > 0f) Buff.Apply(context.Caster, duration, new Buff.Stats { MoveSpeed = moveSpeed });
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers m)
        {
            var parts = new List<string>
            {
                $"for {Duration(rank):0.#}s, {DamageAt(rank) * m.damageMultiplier:0} {DamageWord(m)} every {interval:0.#}s within {m.Radius(radius):0.#}m",
            };
            if (moveSpeed > 0f) parts.Add($"+{moveSpeed * 100f:0}% movement speed");
            if (burnFraction > 0f) parts.Add("burns");
            return string.Join(" · ", parts);
        }
    }
}
