using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Moba
{
    // A hero's ascension tree: at levels 6, 11 and 18 the player picks one of two talents. The first pick
    // (path A or B) decides the later choices: A -> A1/A2 -> A3/A4, B -> B1/B2 -> B3/B4.
    [Serializable]
    public class AscensionTree
    {
        public AscensionPath pathA = new();
        public AscensionPath pathB = new();

        public bool IsComplete =>
            pathA.IsComplete && pathB.IsComplete;

        public AscensionPath Path(int index) => index == 0 ? pathA : pathB;
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
            return lines;
        }

        static string Signed(float v) =>
            (v > 0f ? "+" : "") + v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
