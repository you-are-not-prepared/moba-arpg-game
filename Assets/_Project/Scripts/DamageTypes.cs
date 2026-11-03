using UnityEngine;

namespace Moba
{
    // The six damage types of spells and ultimates. Auto attacks, creeps and towers deal None (untyped).
    public enum DamageType : byte { None, Fire, Frost, Nature, Lightning, Physical, Air }

    // The elemental cycle, in the order of the resistance hexagon (clockwise from the top): each type is strong against
    // the next one and weak against the one before, Fire > Frost > Nature > Lightning > Physical > Air > Fire.
    // A hit on a hero compares the hit's type with the hero's base type (HeroDefinition.BaseDamageType).
    public static class DamageTypes
    {
        public const int Count = 6;
        // +10% when strong, -10% when weak.
        public const float Advantage = 0.1f;

        public static readonly DamageType[] All =
        {
            DamageType.Fire, DamageType.Frost, DamageType.Nature, DamageType.Lightning, DamageType.Physical, DamageType.Air,
        };

        // Index into All and into per-hero resistance arrays (-1 for None).
        public static int Index(DamageType type) => (int)type - 1;

        // The type `type` is strong against.
        public static DamageType Beats(DamageType type) =>
            type == DamageType.None ? DamageType.None : All[(Index(type) + 1) % Count];

        // The type that is strong against `type`.
        public static DamageType BeatenBy(DamageType type) =>
            type == DamageType.None ? DamageType.None : All[(Index(type) + Count - 1) % Count];

        // Multiplier for a hit of type `attack` on a unit whose base type is `defender`.
        public static float Matchup(DamageType attack, DamageType defender)
        {
            if (attack == DamageType.None || defender == DamageType.None) return 1f;
            if (Beats(attack) == defender) return 1f + Advantage;
            if (Beats(defender) == attack) return 1f - Advantage;
            return 1f;
        }

        public static string Name(DamageType type) => type == DamageType.None ? "Untyped" : type.ToString();

        public static Color ColorOf(DamageType type) => type switch
        {
            DamageType.Fire => new Color(0.92f, 0.27f, 0.22f),
            DamageType.Frost => new Color(0.24f, 0.56f, 0.95f),
            DamageType.Nature => new Color(0.52f, 0.8f, 0.24f),
            DamageType.Lightning => new Color(0.62f, 0.35f, 0.9f),
            DamageType.Physical => new Color(0.66f, 0.47f, 0.27f),
            DamageType.Air => new Color(0.75f, 0.77f, 0.8f),
            _ => new Color(0.6f, 0.62f, 0.66f),
        };
    }
}
