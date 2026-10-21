using UnityEngine;

namespace Moba.Abilities
{
    // Self buff: bonus movement and attack speed for a while, optionally refreshed on every hero kill.
    [CreateAssetMenu(menuName = "MOBA/Spells/Haste", fileName = "Haste")]
    public class HasteSpell : AbilityDefinition
    {
        public float duration = 7f;
        [Tooltip("0.6 = 60% more movement speed.")]
        public float moveSpeedBonus = 0.6f;
        [Tooltip("0.2 = 20% more attack speed.")]
        public float attackSpeedBonus = 0.2f;
        public bool refreshOnHeroKill = true;
        public Material auraMaterial;

        void Reset() => targeting = AbilityTargeting.Instant;

        public override void Execute(in AbilityCastContext context) =>
            Haste.Apply(context.Caster, duration + HeroAscension.DurationBonusOf(context.Caster), moveSpeedBonus,
                attackSpeedBonus, refreshOnHeroKill, auraMaterial);

        public override string StatsText =>
            $"+{moveSpeedBonus * 100f:0}% movement speed, +{attackSpeedBonus * 100f:0}% attack speed for {duration:0.#}s" +
            (refreshOnHeroKill ? " · refreshed on hero kill" : "");

        public override string Describe(int rank, in SpellModifiers modifiers) => StatsText;
    }
}
