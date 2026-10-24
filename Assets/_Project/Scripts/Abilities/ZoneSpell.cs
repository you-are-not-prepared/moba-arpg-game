using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // Places an area on the ground at a point (GroundZone): after an optional warning delay it can freeze the enemies
    // inside, then for a while slows and damages enemies and heals and speeds up allied heroes inside. One spell type
    // for many effects: Ilsa's Glacial Prison (freeze + slowing field), Maela's Sanctuary (healing grove).
    [CreateAssetMenu(menuName = "MOBA/Spells/Zone Spell", fileName = "ZoneSpell")]
    public class ZoneSpell : AbilityDefinition
    {
        public float radius = 3.5f;
        [Tooltip("Seconds of warning before the zone takes effect.")]
        public float delay;
        [Tooltip("Seconds the zone lasts once active, per rank (last entry repeats).")]
        public float[] durationByRank = { 3f };
        [Tooltip("Seconds enemies inside are frozen (stunned) when the zone takes effect, per rank. 0 = none.")]
        public float[] freezeByRank = { 0f };
        [Tooltip("0.3 = enemies inside move 30% slower.")]
        [Range(0f, 0.9f)] public float enemySlow;
        public float enemyDamagePerSecond;
        [Tooltip("Allied heroes inside heal this fraction of max health per second, per rank (0.04 = 4%).")]
        public float[] allyHealByRank = { 0f };
        [Tooltip("0.2 = allied heroes inside move 20% faster.")]
        public float allyMoveSpeed;
        public Material zoneMaterial;
        [Tooltip("Shown during the delay (defaults to the zone material).")]
        public Material warningMaterial;

        void Reset()
        {
            targeting = AbilityTargeting.Point;
            range = 9f;
            areaRadius = radius;
        }

        static float ByRank(float[] values, int rank) =>
            values is { Length: > 0 } ? values[Mathf.Clamp(rank - 1, 0, values.Length - 1)] : 0f;

        public float Duration(int rank) => ByRank(durationByRank, rank);
        public float Freeze(int rank) => ByRank(freezeByRank, rank);
        public float AllyHeal(int rank) => ByRank(allyHealByRank, rank);
        public bool Offensive => freezeByRank is { Length: > 0 } && freezeByRank[0] > 0f || enemyDamagePerSecond > 0f;

        public override void Execute(in AbilityCastContext context)
        {
            Unit caster = context.Caster;
            float freeze = Freeze(context.Rank);
            if (freeze > 0f) freeze += HeroAscension.StunBonusOf(caster);
            float damage = enemyDamagePerSecond * context.Modifiers.damageMultiplier;
            GroundZone.Create(caster, context.Point, radius, delay, Duration(context.Rank) + HeroAscension.DurationBonusOf(caster),
                freeze, enemySlow, damage, AllyHeal(context.Rank), allyMoveSpeed, warningMaterial, zoneMaterial);
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers modifiers)
        {
            var parts = new List<string>();
            if (Freeze(rank) > 0f) parts.Add($"after {delay:0.##}s freezes enemies for {Freeze(rank):0.##}s");
            if (enemySlow > 0f) parts.Add($"slows enemies {enemySlow * 100f:0}%");
            if (enemyDamagePerSecond > 0f) parts.Add($"{enemyDamagePerSecond * modifiers.damageMultiplier:0} damage/s to enemies");
            if (AllyHeal(rank) > 0f) parts.Add($"allies heal {AllyHeal(rank) * 100f:0}% max health/s");
            if (allyMoveSpeed > 0f) parts.Add($"+{allyMoveSpeed * 100f:0}% ally movement speed");
            return $"{radius:0.#}m area for {Duration(rank):0.#}s: " + string.Join(" · ", parts);
        }
    }
}
