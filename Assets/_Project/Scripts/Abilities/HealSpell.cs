using UnityEngine;

namespace Moba.Abilities
{
    // Heals an allied hero (unit-targeted, Maela's Mending Seed), or with `area` every allied hero around the caster
    // (Wild Growth). Spell damage bonuses (and ultimate talents) raise the healing too.
    [CreateAssetMenu(menuName = "MOBA/Spells/Heal Spell", fileName = "HealSpell")]
    public class HealSpell : AbilityDefinition
    {
        public float[] healByRank = { 80f, 110f, 140f, 170f, 200f };
        [Tooltip("Heal every allied hero within `radius` of the caster instead of one target.")]
        public bool area;
        public float radius = 12f;

        void Reset()
        {
            targeting = AbilityTargeting.Unit;
            unitFilter = UnitTargetFilter.Ally;
            range = 7f;
        }

        public float HealAt(int rank) =>
            healByRank is { Length: > 0 } ? healByRank[Mathf.Clamp(rank - 1, 0, healByRank.Length - 1)] : 0f;

        public override bool IsValidTarget(Unit caster, Unit target) =>
            base.IsValidTarget(caster, target) && target.kind == UnitKind.Hero;

        public override void Execute(in AbilityCastContext context)
        {
            Unit caster = context.Caster;
            float heal = HealAt(context.Rank) * context.Modifiers.damageMultiplier;
            if (!area)
            {
                Heal(caster, context.Target, heal, context.Modifiers.damageType);
                return;
            }
            float r = context.Modifiers.Radius(radius);
            foreach (Unit u in Unit.All)
            {
                if (u.kind != UnitKind.Hero || u.IsEnemyOf(caster) || !u.IsAlive) continue;
                Vector3 d = u.transform.position - caster.transform.position;
                d.y = 0f;
                if (d.magnitude <= r) Heal(caster, u, heal, context.Modifiers.damageType);
            }
        }

        static void Heal(Unit caster, Unit target, float amount, DamageType look)
        {
            if (target == null || !target.IsAlive) return;
            target.Heal(amount);
            SpellLook.Play(SpellLookKind.Explosion, look, caster, target.transform.position, Vector3.zero, 1.2f, 0f);
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers m) =>
            area
                ? $"heals every allied hero within {m.Radius(radius):0.#}m for {HealAt(rank) * m.damageMultiplier:0}"
                : $"heals an allied hero for {HealAt(rank) * m.damageMultiplier:0}";
    }
}
