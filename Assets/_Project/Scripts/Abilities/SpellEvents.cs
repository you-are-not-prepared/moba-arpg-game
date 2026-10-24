using System;
using UnityEngine;

namespace Moba.Abilities
{
    // Spell hits, for passives that react to them (a slow on hit, healing from damage dealt). Raised by whatever deals
    // spell damage (projectiles, leaps, ground zones, chains, storm strikes) where the match is simulated: offline
    // or on the game server.
    public static class SpellEvents
    {
        // (caster, target, damage before the target's modifiers)
        public static event Action<Unit, Unit, float> Hit;

        public static void RaiseHit(Unit caster, Unit target, float damage)
        {
            if (caster != null && target != null) Hit?.Invoke(caster, target, damage);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Hit = null;
    }
}
