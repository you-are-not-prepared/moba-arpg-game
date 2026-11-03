using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // A timed buff on the caster (Buff): attack speed, attack range, movement speed, less damage taken, more Heat.
    // E.g. Kael's Hawk Eye, Torvald's Unbreakable, Oren's Phoenix Form. Ascension adds duration.
    [CreateAssetMenu(menuName = "MOBA/Spells/Buff Spell", fileName = "BuffSpell")]
    public class BuffSpell : AbilityDefinition
    {
        public float[] durationByRank = { 6f };
        [Tooltip("0.4 = 40% more attack speed.")] public float attackSpeed;
        [Tooltip("Metres added to attack range.")] public float attackRange;
        [Tooltip("0.2 = 20% faster.")] public float moveSpeed;
        [Tooltip("Per rank: 0.4 = takes 40% less damage.")] public float[] damageReductionByRank = { 0f };
        [Tooltip("Extra Heat per attack (Kindled Blade).")] public int heatPerAttack;

        void Reset()
        {
            targeting = AbilityTargeting.Instant;
            range = 0f;
        }

        static float ByRank(float[] values, int rank) =>
            values is { Length: > 0 } ? values[Mathf.Clamp(rank - 1, 0, values.Length - 1)] : 0f;

        public float Duration(int rank) => ByRank(durationByRank, rank);
        public float DamageReduction(int rank) => ByRank(damageReductionByRank, rank);

        public override void Execute(in AbilityCastContext context)
        {
            Buff.Apply(context.Caster, Duration(context.Rank) + HeroAscension.DurationBonusOf(context.Caster), new Buff.Stats
            {
                AttackSpeed = attackSpeed,
                AttackRange = attackRange,
                MoveSpeed = moveSpeed,
                DamageReduction = DamageReduction(context.Rank),
                HeatPerAttack = heatPerAttack,
            });
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers modifiers)
        {
            var parts = new List<string>();
            if (attackSpeed != 0f) parts.Add($"+{attackSpeed * 100f:0}% attack speed");
            if (attackRange != 0f) parts.Add($"+{attackRange:0.#}m attack range");
            if (moveSpeed != 0f) parts.Add($"+{moveSpeed * 100f:0}% movement speed");
            if (DamageReduction(rank) > 0f) parts.Add($"{DamageReduction(rank) * 100f:0}% less damage taken");
            if (heatPerAttack > 0) parts.Add($"+{heatPerAttack} Heat per attack");
            return $"For {Duration(rank):0.#}s: " + string.Join(" · ", parts);
        }
    }
}
