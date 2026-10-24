using UnityEngine;

namespace Moba.Abilities
{
    // Self buff: a storm cloud follows the caster for a while and strikes a random enemy in reach at a steady pace,
    // enemy heroes first (StormCloud).
    [CreateAssetMenu(menuName = "MOBA/Spells/Storm Cloud", fileName = "StormCloud")]
    public class StormCloudSpell : AbilityDefinition
    {
        public float[] durationByRank = { 6f };
        [Tooltip("Seconds between strikes.")]
        public float interval = 0.5f;
        [Tooltip("Enemies within this edge distance of the caster can be struck.")]
        public float radius = 6f;
        public float[] damageByRank = { 60f, 90f, 120f };
        public Material cloudMaterial;
        public Material boltMaterial;

        void Reset() => targeting = AbilityTargeting.Instant;

        static float ByRank(float[] values, int rank) =>
            values is { Length: > 0 } ? values[Mathf.Clamp(rank - 1, 0, values.Length - 1)] : 0f;

        public float Duration(int rank) => ByRank(durationByRank, rank);
        public float DamageAt(int rank) => ByRank(damageByRank, rank);

        public override void Execute(in AbilityCastContext context) =>
            StormCloud.Apply(context.Caster, Duration(context.Rank) + HeroAscension.DurationBonusOf(context.Caster), interval,
                radius, DamageAt(context.Rank) * context.Modifiers.damageMultiplier, cloudMaterial, boltMaterial);

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers modifiers) =>
            $"For {Duration(rank):0.#}s, strikes an enemy within {radius:0.#}m every {interval:0.##}s for " +
            $"{DamageAt(rank) * modifiers.damageMultiplier:0} damage (heroes first)";
    }
}
