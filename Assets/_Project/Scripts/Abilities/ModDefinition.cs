using System.Collections.Generic;
using Moba.Items;
using UnityEngine;

namespace Moba.Abilities
{
    // A support mod, socketed into a spell slot (3 sockets per slot). Purely data: each field adds to the
    // SpellModifiers of the spell it's socketed with, if the spell has all requiredTags.
    [CreateAssetMenu(menuName = "MOBA/Mod", fileName = "Mod")]
    public class ModDefinition : InventoryItem
    {
        [Tooltip("The spell must have all of these tags for the mod to apply.")]
        public SpellTags requiredTags;

        [Header("Projectiles")]
        public int extraProjectiles;
        [Tooltip("Times a projectile splits in two when it hits a target.")]
        public int forkCount;
        [Tooltip("Fork projectiles leave the target at ± this angle from the incoming direction.")]
        public float forkAngle = 30f;

        [Header("Damage")]
        [Tooltip("Multiplies hit damage (1.3 = 30% more).")]
        public float damageMultiplier = 1f;
        [Tooltip("Burn damage as a fraction of hit damage, dealt over burnDuration.")]
        public float burnFraction;
        public float burnDuration = 3f;

        public override string KindLabel => "Mod";

        public bool Supports(AbilityDefinition spell) =>
            spell != null && (spell.tags & requiredTags) == requiredTags;

        public void Apply(ref SpellModifiers m)
        {
            m.extraProjectiles += extraProjectiles;
            if (forkCount > 0)
            {
                m.forkCount += forkCount;
                m.forkAngle = forkAngle;
            }
            m.damageMultiplier *= damageMultiplier;
            if (burnFraction > 0f)
            {
                m.burnFraction += burnFraction;
                m.burnDuration = Mathf.Max(m.burnDuration, burnDuration);
            }
        }

        public override string StatsText
        {
            get
            {
                var parts = new List<string>();
                if (extraProjectiles > 0) parts.Add($"+{extraProjectiles} projectiles");
                if (forkCount > 0) parts.Add(forkCount == 1 ? "Forks once on hit" : $"Forks {forkCount} times on hit");
                if (!Mathf.Approximately(damageMultiplier, 1f)) parts.Add($"{(damageMultiplier - 1f) * 100f:+0;-0}% damage");
                if (burnFraction > 0f) parts.Add($"Burns for {burnFraction * 100f:0}% of hit damage over {burnDuration:0.#}s");
                string requires = requiredTags == SpellTags.None ? "" : $" (requires {requiredTags} spell)";
                return string.Join(" · ", parts) + requires;
            }
        }
    }
}
