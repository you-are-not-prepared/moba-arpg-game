using System;

namespace Moba.Abilities
{
    // What a spell is. Mods require tags (e.g. Fork needs Projectile) to affect a spell.
    [Flags]
    public enum SpellTags
    {
        None = 0,
        Projectile = 1 << 0,
        AreaOfEffect = 1 << 1,
        Fire = 1 << 2,
    }

    // The combined effect of the mods socketed with a spell, computed once per cast.
    public struct SpellModifiers
    {
        public int extraProjectiles;
        public int forkCount;
        public float forkAngle;
        // Multiplies hit damage (1 = unchanged).
        public float damageMultiplier;
        // Burn: total burn damage as a fraction of the hit damage, dealt over burnDuration seconds.
        public float burnFraction;
        public float burnDuration;

        public static SpellModifiers None => new() { damageMultiplier = 1f };
    }
}
