using UnityEngine;

namespace Moba.Abilities
{
    // Self buff: for a while the caster's spells cost less mana (free by default) and get shorter cooldowns. Cooldowns
    // already running aren't changed; the ultimate's own cooldown starts before the storm does.
    [CreateAssetMenu(menuName = "MOBA/Spells/Spell Storm", fileName = "SpellStorm")]
    public class SpellStormSpell : AbilityDefinition
    {
        [Tooltip("Seconds per rank (last entry repeats).")]
        public float[] durationByRank = { 6f, 7f, 8f };
        [Tooltip("0.5 = cooldowns 50% shorter during the storm.")]
        [Range(0f, 1f)] public float cooldownReduction = 0.5f;
        [Tooltip("1 = spells cost no mana during the storm.")]
        [Range(0f, 1f)] public float manaCostReduction = 1f;
        public Material auraMaterial;

        void Reset() => targeting = AbilityTargeting.Instant;

        public float Duration(int rank) =>
            durationByRank is { Length: > 0 } ? durationByRank[Mathf.Clamp(rank - 1, 0, durationByRank.Length - 1)] : 0f;

        public float CooldownMultiplier => 1f - cooldownReduction;
        public float ManaCostMultiplier => 1f - manaCostReduction;

        public override void Execute(in AbilityCastContext context) =>
            SpellStorm.Apply(context.Caster, Duration(context.Rank) + HeroAscension.DurationBonusOf(context.Caster),
                CooldownMultiplier, ManaCostMultiplier, auraMaterial);

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers modifiers)
        {
            string mana = manaCostReduction >= 1f ? "spells cost no mana" : $"spells cost {manaCostReduction * 100f:0}% less mana";
            return $"For {Duration(rank):0.#}s, {mana} and have {cooldownReduction * 100f:0}% shorter cooldowns";
        }
    }
}
