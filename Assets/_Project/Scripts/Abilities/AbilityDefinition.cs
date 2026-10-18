using Moba.Items;
using UnityEngine;

namespace Moba.Abilities
{
    public enum AbilityTargeting
    {
        [Tooltip("Casts immediately on key press (self-buffs, auras, etc.).")]
        Instant,
        [Tooltip("Left-click a ground position.")]
        Point,
        [Tooltip("Left-click a unit that passes the unit filter.")]
        Unit,
    }

    public enum UnitTargetFilter { Enemy, Ally, Any }

    // A spell: bought in the shop, then equipped in a Q/W/E/R slot. Create a subclass per spell type,
    // implement Execute, then make assets from it. Ranks, level gates and mod sockets belong to the slot
    // (see AbilityCaster); the spell provides per-rank numbers and behaviour.
    public abstract class AbilityDefinition : InventoryItem
    {
        public SpellTags tags;

        [Header("Targeting")]
        public AbilityTargeting targeting = AbilityTargeting.Instant;
        public UnitTargetFilter unitFilter = UnitTargetFilter.Enemy;
        [Tooltip("Cast range (edge-to-edge for unit targets). 0 = unlimited: cast immediately toward the target.")]
        public float range = 6f;
        [Tooltip("Radius of the area-of-effect preview drawn at the cursor for Point abilities. 0 = none.")]
        public float areaRadius;

        [Header("Per rank")]
        [Tooltip("Cooldown in seconds per rank (last entry repeats).")]
        public float[] cooldownByRank = { 8f };
        [Tooltip("Mana cost per rank (last entry repeats).")]
        public float[] manaCostByRank = { 50f };

        public override string KindLabel => "Spell";

        public float Cooldown(int rank) => ByRank(cooldownByRank, rank);
        public float ManaCost(int rank) => ByRank(manaCostByRank, rank);

        // Passive abilities (e.g. an ultimate that only grants an effect while learned) can't be cast.
        public virtual bool IsPassive => false;

        // Extra mod sockets this ability gives the basic slots while it's learned (for ultimates).
        public virtual int BonusBasicSockets(int rank) => 0;

        // Radius of the ring shown around the caster while targeting (defaults to cast range).
        public virtual float IndicatorRange => range;

        // Rank- and mod-aware summary for tooltips, e.g. "85 fire damage in a 2.5m radius".
        public virtual string Describe(int rank, in SpellModifiers modifiers) => "";

        public virtual bool IsValidTarget(Unit caster, Unit target)
        {
            if (target == null || !target.IsTargetable) return false;
            return unitFilter switch
            {
                UnitTargetFilter.Enemy => target.IsEnemyOf(caster),
                UnitTargetFilter.Ally => !target.IsEnemyOf(caster),
                _ => true,
            };
        }

        // Called once the cast is validated, in range, and mana/cooldown have been paid.
        public abstract void Execute(in AbilityCastContext context);

        static float ByRank(float[] values, int rank)
        {
            if (values == null || values.Length == 0) return 0f;
            return values[Mathf.Clamp(rank - 1, 0, values.Length - 1)];
        }
    }

    public readonly struct AbilityCastContext
    {
        public readonly Unit Caster;
        public readonly AbilityCaster Abilities;
        public readonly int Slot;
        public readonly int Rank;
        // Target ground position for Point abilities; the target's position for Unit; the caster's for Instant.
        public readonly Vector3 Point;
        // Target unit for Unit abilities; otherwise null.
        public readonly Unit Target;
        // Combined effect of the mods socketed with this spell.
        public readonly SpellModifiers Modifiers;

        public AbilityCastContext(Unit caster, AbilityCaster abilities, int slot, int rank, Vector3 point, Unit target,
            SpellModifiers modifiers)
        {
            Caster = caster;
            Abilities = abilities;
            Slot = slot;
            Rank = rank;
            Point = point;
            Target = target;
            Modifiers = modifiers;
        }
    }
}
