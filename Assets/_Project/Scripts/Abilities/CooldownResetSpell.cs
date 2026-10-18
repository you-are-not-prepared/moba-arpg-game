using UnityEngine;

namespace Moba.Abilities
{
    // Instantly ends the cooldowns of the caster's basic spells (Q/W/E).
    [CreateAssetMenu(menuName = "MOBA/Spells/Cooldown Reset", fileName = "CooldownReset")]
    public class CooldownResetSpell : AbilityDefinition
    {
        void Reset() => targeting = AbilityTargeting.Instant;

        public override void Execute(in AbilityCastContext context) => context.Abilities.ResetBasicCooldowns();

        public override string StatsText => "Resets Q, W and E cooldowns";

        public override string Describe(int rank, in SpellModifiers modifiers) => StatsText;
    }
}
