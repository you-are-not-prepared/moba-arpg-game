using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // Fires projectile(s) toward the target point; each explodes on the first enemy it hits.
    // Mods add projectiles (fanned out), forks (two new projectiles from the hit target at a fixed angle),
    // more damage and burning.
    [CreateAssetMenu(menuName = "MOBA/Spells/Projectile Spell", fileName = "ProjectileSpell")]
    public class ProjectileSpell : AbilityDefinition
    {
        [Header("Damage")]
        public float baseDamage = 60f;
        public float damagePerRank = 25f;
        [Tooltip("Explosion radius on hit. 0 = damages only the unit hit.")]
        public float explosionRadius = 2.5f;
        [Tooltip("Keep flying through enemies, hitting each one once.")]
        public bool pierce;
        [Tooltip("Seconds each enemy hit is stunned. 0 = no stun.")]
        public float stunDuration;
        [Tooltip("0.4 = enemies hit move 40% slower for slowSeconds. 0 = no slow.")]
        [Range(0f, 0.9f)] public float slow;
        public float slowSeconds = 2f;
        [Tooltip("The first hit jumps to this many more enemies within 5m, for chainFraction of the damage each.")]
        public int chainJumps;
        public float chainFraction = 0.6f;
        public Material boltMaterial;
        [Tooltip("The first hit shortens the caster's other basic cooldowns by this many seconds.")]
        public float cooldownRefund;

        [Header("Projectile")]
        public float projectileSpeed = 18f;
        public float maxDistance = 14f;
        public float projectileSize = 0.6f;
        public float launchHeight = 1.2f;
        [Tooltip("Angle between neighbouring projectiles when firing more than one.")]
        public float spreadAngle = 15f;
        public Material projectileMaterial;
        public Material explosionMaterial;

        void Reset()
        {
            tags = SpellTags.Projectile | SpellTags.AreaOfEffect;
            targeting = AbilityTargeting.Point;
            range = 0f;
        }

        public override float IndicatorRange => maxDistance;

        public float DamageAt(int rank) => baseDamage + damagePerRank * (Mathf.Max(1, rank) - 1);

        public override void Execute(in AbilityCastContext context)
        {
            Unit caster = context.Caster;
            SpellModifiers m = context.Modifiers;

            Vector3 dir = context.Point - caster.transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : caster.transform.forward;

            float damage = DamageAt(context.Rank) * m.damageMultiplier;
            var payload = new SpellProjectile.Payload
            {
                Caster = caster,
                Team = caster.team,
                Damage = damage,
                DamageType = m.damageType,
                ExplosionRadius = explosionRadius,
                BurnDps = m.burnFraction > 0f && m.burnDuration > 0f ? damage * m.burnFraction / m.burnDuration : 0f,
                BurnDuration = m.burnDuration,
                ForksRemaining = m.forkCount,
                ForkAngle = m.forkAngle,
                Pierce = pierce,
                StunDuration = stunDuration > 0f ? stunDuration + HeroAscension.StunBonusOf(caster) : 0f,
                Slow = slow,
                SlowSeconds = slowSeconds,
                CooldownRefund = cooldownRefund,
                ChainJumps = chainJumps,
                ChainFraction = chainFraction,
                ChainRange = 5f,
                BoltMaterial = boltMaterial,
                Speed = projectileSpeed,
                MaxDistance = maxDistance,
                Size = projectileSize,
                Height = launchHeight,
                ProjectileMaterial = projectileMaterial,
                ExplosionMaterial = explosionMaterial,
                Look = adaptive ? m.damageType : DamageType.None,
            };

            if (caster.TryGetComponent(out StaticCharge charge) && charge.NextIsCharged)
            {
                payload.ChainJumps += charge.jumps;
                payload.ChainFraction = charge.fraction;
                payload.ChainRange = charge.range;
                payload.BoltMaterial = charge.boltMaterial;
            }

            Vector3 origin = caster.transform.position + dir * (caster.radius + 0.3f);
            int count = 1 + Mathf.Max(0, m.extraProjectiles);
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * spreadAngle;
                SpellProjectile.Launch(origin, Quaternion.Euler(0f, angle, 0f) * dir, payload, ignore: null);
            }
        }

        public override string StatsText => Describe(1, SpellModifiers.None);

        public override string Describe(int rank, in SpellModifiers m)
        {
            float damage = DamageAt(rank) * m.damageMultiplier;
            var parts = new List<string>
            {
                explosionRadius > 0f
                    ? $"{damage:0} {DamageWord(m)} in a {explosionRadius:0.#}m explosion"
                    : $"{damage:0} {DamageWord(m)}",
            };
            int count = 1 + Mathf.Max(0, m.extraProjectiles);
            if (count > 1) parts.Add($"{count} projectiles");
            if (pierce) parts.Add("pierces");
            if (stunDuration > 0f) parts.Add($"stuns for {stunDuration:0.#}s");
            if (slow > 0f) parts.Add($"slows {slow * 100f:0}% for {slowSeconds:0.#}s");
            if (chainJumps > 0) parts.Add($"jumps to {chainJumps} more {(chainJumps == 1 ? "enemy" : "enemies")} for {chainFraction * 100f:0}%");
            if (cooldownRefund > 0f) parts.Add($"a hit shortens your other cooldowns by {cooldownRefund:0.#}s");
            if (m.forkCount > 0) parts.Add(m.forkCount == 1 ? "forks once" : $"forks {m.forkCount} times");
            if (m.burnFraction > 0f) parts.Add($"burns for {damage * m.burnFraction:0} over {m.burnDuration:0.#}s");
            return string.Join(" · ", parts);
        }
    }
}
