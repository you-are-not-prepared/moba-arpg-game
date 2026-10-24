using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Moba
{
    // A hero's ascension tree. At level 6 the player picks a path (one card per path: A, B, C...); that pick
    // decides the later choices, two talents each at levels 11 and 18: A -> A1/A2 -> A3/A4, C -> C1/C2 -> C3/C4.
    [Serializable]
    public class AscensionTree
    {
        public const int MinPaths = 2;
        // Letters A-D; four cards is also what fits on screen.
        public const int MaxPaths = 4;

        [Tooltip("2-4 paths. Level 6 offers one card per path; the chosen path decides the cards at 11 and 18.")]
        public List<AscensionPath> paths = new();

        public int PathCount => paths?.Count ?? 0;

        public bool IsComplete =>
            PathCount is >= MinPaths and <= MaxPaths && paths.TrueForAll(p => p != null && p.IsComplete);

        public AscensionPath Path(int index) => paths[index];

        public static string Letter(int index) => ((char)('A' + index)).ToString();
    }

    [Serializable]
    public class AscensionPath
    {
        // Picked at level 6; choosing it commits to this path.
        public Talent root = new();
        // Level 11 options.
        public Talent[] second = { new(), new() };
        // Level 18 options.
        public Talent[] third = { new(), new() };

        public bool IsComplete =>
            root.IsSet && second is { Length: 2 } && second[0].IsSet && second[1].IsSet
            && third is { Length: 2 } && third[0].IsSet && third[1].IsSet;
    }

    // One talent: a name, optional flavour text, and the stat changes it applies when picked (all stack).
    [Serializable]
    public class Talent
    {
        public string id;
        public string name;
        [TextArea] public string flavor;

        [Header("Stats")]
        public float maxHealth;
        public float maxMana;
        public float attackDamage;
        [Tooltip("0.15 = 15% more attack speed.")] public float attackSpeed;
        [Tooltip("0.1 = 10% faster.")] public float moveSpeed;
        [Tooltip("0.1 = all cooldowns 10% shorter.")] public float cooldownReduction;
        [Tooltip("0.2 = the ultimate's cooldown 20% shorter.")] public float ultimateCooldownReduction;
        [Tooltip("0.15 = spells deal 15% more damage.")] public float spellDamage;
        [Tooltip("Extra projectiles on projectile spells.")] public int extraProjectiles;
        [Tooltip("Added to the item stat multiplier (0.1 = items give 10% more).")] public float itemStats;

        [Header("Passive and ultimate hooks")]
        [Tooltip("Extra max health gained on each later level-up.")] public float healthPerLevel;
        [Tooltip("Added to an isolation passive's bonus (0.1 = +10% damage to isolated targets).")] public float isolationDamage;
        [Tooltip("Seconds added to timed ultimates (shield wall, haste).")] public float ultimateDuration;
        [Tooltip("Seconds added to stuns from projectile spells.")] public float stunDuration;
        [Tooltip("Added to a Tailwind passive's speed (0.1 = +10% movement speed).")] public float tailwindSpeed;
        [Tooltip("Seconds added to a Tailwind passive.")] public float tailwindDuration;
        [Tooltip("Attack speed while Tailwind lasts (0.2 = +20%).")] public float tailwindAttackSpeed;
        [Tooltip("Added to a Heat burst's damage.")] public float heatBurstDamage;
        [Tooltip("Added to the slow of a hero's spell hits (0.1 = 10% stronger).")] public float spellSlow;
        [Tooltip("Seconds added to the slow of a hero's spell hits.")] public float spellSlowSeconds;
        [Tooltip("Added to the healing from spell damage (0.1 = 10% more of the damage).")] public float allyHeal;
        [Tooltip("More enemies a charged spell's hit jumps to.")] public int chainJumps;
        [Tooltip("Charged spells come this many casts sooner (every 4th -> every 3rd).")] public int chargeSooner;

        public bool IsSet => !string.IsNullOrEmpty(name);

        // "+150 max health · +15% attack speed", for the choice cards and tooltips.
        public List<string> EffectLines()
        {
            var lines = new List<string>();
            void Flat(float v, string what) { if (v != 0f) lines.Add($"{Signed(v)} {what}"); }
            void Pct(float v, string what) { if (v != 0f) lines.Add($"{Signed(v * 100f)}% {what}"); }
            void Secs(float v, string what) { if (v != 0f) lines.Add($"{Signed(v)}s {what}"); }

            Flat(maxHealth, "max health");
            Flat(healthPerLevel, "max health per level");
            Flat(maxMana, "max mana");
            Flat(attackDamage, "attack damage");
            Pct(attackSpeed, "attack speed");
            Pct(moveSpeed, "movement speed");
            if (cooldownReduction != 0f) lines.Add($"{cooldownReduction * 100f:0.#}% shorter cooldowns");
            if (ultimateCooldownReduction != 0f) lines.Add($"{ultimateCooldownReduction * 100f:0.#}% shorter ultimate cooldown");
            Pct(spellDamage, "spell damage");
            if (extraProjectiles != 0) lines.Add($"{Signed(extraProjectiles)} projectile{(Math.Abs(extraProjectiles) == 1 ? "" : "s")} on projectile spells");
            Pct(itemStats, "stats from items");
            Pct(isolationDamage, "damage to isolated targets");
            Secs(ultimateDuration, "ultimate duration");
            Secs(stunDuration, "stun duration");
            Pct(tailwindSpeed, "Tailwind movement speed");
            Secs(tailwindDuration, "Tailwind duration");
            Pct(tailwindAttackSpeed, "attack speed during Tailwind");
            Flat(heatBurstDamage, "Heat burst damage");
            Pct(spellSlow, "slow from spell hits");
            Secs(spellSlowSeconds, "slow duration");
            Pct(allyHeal, "of spell damage healed to allies");
            if (chainJumps != 0) lines.Add($"{Signed(chainJumps)} chain jump{(Math.Abs(chainJumps) == 1 ? "" : "s")} on charged spells");
            if (chargeSooner != 0) lines.Add($"charged spells {chargeSooner} cast{(chargeSooner == 1 ? "" : "s")} sooner");
            return lines;
        }

        static string Signed(float v) =>
            (v > 0f ? "+" : "") + v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
