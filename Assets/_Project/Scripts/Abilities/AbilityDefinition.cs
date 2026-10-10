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

    // Data + behaviour for one ability. Create a subclass per ability type, implement Execute,
    // then make assets from it (CreateAssetMenu on the subclass) and drop them into an AbilityCaster slot.
    public abstract class AbilityDefinition : ScriptableObject
    {
        public string displayName = "Ability";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Targeting")]
        public AbilityTargeting targeting = AbilityTargeting.Instant;
        public UnitTargetFilter unitFilter = UnitTargetFilter.Enemy;
        [Tooltip("Cast range (edge-to-edge for unit targets). 0 = unlimited.")]
        public float range = 6f;
        [Tooltip("Radius of the area-of-effect preview drawn at the cursor for Point abilities. 0 = none.")]
        public float areaRadius;

        [Header("Ranks")]
        public bool isUltimate;
        [Min(1)] public int maxRank = 5;
        [Tooltip("Cooldown in seconds per rank (last entry repeats).")]
        public float[] cooldownByRank = { 8f };
        [Tooltip("Mana cost per rank (last entry repeats).")]
        public float[] manaCostByRank = { 50f };
        [Tooltip("Hero level required to learn each rank. Leave empty for the defaults: " +
                 "basic abilities 1/3/5/7/9, ultimates 6/11/16.")]
        public int[] requiredLevelByRank;

        public float Cooldown(int rank) => ByRank(cooldownByRank, rank);
        public float ManaCost(int rank) => ByRank(manaCostByRank, rank);

        public int RequiredLevel(int rank)
        {
            if (requiredLevelByRank != null && requiredLevelByRank.Length > 0)
                return requiredLevelByRank[Mathf.Clamp(rank - 1, 0, requiredLevelByRank.Length - 1)];
            return isUltimate ? 6 + (rank - 1) * 5 : rank * 2 - 1;
        }

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

        public AbilityCastContext(Unit caster, AbilityCaster abilities, int slot, int rank, Vector3 point, Unit target)
        {
            Caster = caster;
            Abilities = abilities;
            Slot = slot;
            Rank = rank;
            Point = point;
            Target = target;
        }
    }
}
