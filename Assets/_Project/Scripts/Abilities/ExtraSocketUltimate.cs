using UnityEngine;

namespace Moba.Abilities
{
    // A passive ultimate: while learned, every basic spell slot (Q/W/E) gets extra mod sockets.
    [CreateAssetMenu(menuName = "MOBA/Spells/Extra Socket Ultimate", fileName = "ExtraSocketUltimate")]
    public class ExtraSocketUltimate : AbilityDefinition
    {
        [Min(1)] public int bonusSockets = 1;

        public override bool IsPassive => true;

        public override int BonusBasicSockets(int rank) => rank > 0 ? bonusSockets : 0;

        public override void Execute(in AbilityCastContext context) { }

        public override string StatsText => $"Passive: +{bonusSockets} mod socket{(bonusSockets == 1 ? "" : "s")} on Q, W and E";

        public override string Describe(int rank, in SpellModifiers modifiers) => StatsText;
    }
}
