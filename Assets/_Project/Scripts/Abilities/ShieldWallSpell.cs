using UnityEngine;

namespace Moba.Abilities
{
    // Raises a curved shield in front of the caster that blocks enemy projectiles for a few seconds.
    // The caster can move and turn freely; the shield goes with them (see ProjectileShield).
    [CreateAssetMenu(menuName = "MOBA/Spells/Shield Wall", fileName = "ShieldWall")]
    public class ShieldWallSpell : AbilityDefinition
    {
        public float duration = 5f;
        [Tooltip("Distance from the caster to the wall.")]
        public float radius = 2.4f;
        [Tooltip("Width of the wall as an angle around the caster.")]
        public float arcDegrees = 150f;
        public float height = 2.6f;
        public Material wallMaterial;
        [Tooltip("Flash where a projectile is stopped.")]
        public Material blockFlashMaterial;

        void Reset() => targeting = AbilityTargeting.Instant;

        public override void Execute(in AbilityCastContext context) =>
            ProjectileShield.Raise(context.Caster, duration + HeroAscension.DurationBonusOf(context.Caster), radius, arcDegrees,
                height, wallMaterial, blockFlashMaterial);

        public override string StatsText => $"Blocks enemy projectiles from the front for {duration:0.#}s";

        public override string Describe(int rank, in SpellModifiers modifiers) => StatsText;
    }
}
