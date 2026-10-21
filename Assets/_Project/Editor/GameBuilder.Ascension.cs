using System;

namespace Moba.EditorTools
{
    // Placeholder ascension trees, built on each hero's passive (path A) and ultimate (path B).
    // Level 6 picks the path; levels 11 and 18 each offer two talents from that path.
    public static partial class GameBuilder
    {
        static Talent T(string id, string name, string flavor, Action<Talent> stats)
        {
            var talent = new Talent { id = id, name = name, flavor = flavor };
            stats(talent);
            return talent;
        }

        static AscensionPath AscPath(Talent root, Talent second1, Talent second2, Talent third1, Talent third2) => new()
        {
            root = root,
            second = new[] { second1, second2 },
            third = new[] { third1, third2 },
        };

        // Kael: projectiles (Split Shot) or the Skybreaker Arrow.
        static AscensionTree KaelAscension() => new()
        {
            pathA = AscPath(
                T("kael_a", "Storm of Arrows", "Every shot becomes a volley.", t => t.extraProjectiles = 1),
                T("kael_a1", "Splintering Shots", "Arrowheads that shatter on impact.", t => t.spellDamage = 0.15f),
                T("kael_a2", "Quickdraw", "Nock, draw, loose. Again.", t => t.attackSpeed = 0.2f),
                T("kael_a3", "Endless Quiver", "There is always one more arrow.", t => t.extraProjectiles = 1),
                T("kael_a4", "Barrage Rhythm", "Fall into the rhythm of the hunt.", t => t.cooldownReduction = 0.15f)),
            pathB = AscPath(
                T("kael_b", "Skybreaker", "The great bow is always strung.", t => t.ultimateCooldownReduction = 0.25f),
                T("kael_b1", "Pinning Shot", "Nail them where they stand.", t => t.stunDuration = 0.75f),
                T("kael_b2", "Hunter's Focus", "Patience sharpens every arrow.", t => t.attackDamage = 15f),
                T("kael_b3", "Heaven's Fall", "The sky itself holds them down.", t =>
                {
                    t.stunDuration = 0.75f;
                    t.spellDamage = 0.2f;
                }),
                T("kael_b4", "Sky Hunter", "Draw the great bow again and again.", t => t.ultimateCooldownReduction = 0.25f)),
        };

        // Vesh: cooldowns (Quickened Mind) or Rewind.
        static AscensionTree VeshAscension() => new()
        {
            pathA = AscPath(
                T("vesh_a", "Time Weaver", "Seconds bend around your spells.", t => t.cooldownReduction = 0.1f),
                T("vesh_a1", "Accelerated Mind", "Think faster, cast more.", t => t.maxMana = 80f),
                T("vesh_a2", "Temporal Flow", "Every spell lands a moment sooner.", t => t.spellDamage = 0.15f),
                T("vesh_a3", "Perpetual Motion", "Never stop casting.", t => t.cooldownReduction = 0.1f),
                T("vesh_a4", "Echo", "Each spell leaves an echo behind.", t => t.extraProjectiles = 1)),
            pathB = AscPath(
                T("vesh_b", "Chronoshift", "Rewind comes around sooner.", t => t.ultimateCooldownReduction = 0.3f),
                T("vesh_b1", "Borrowed Time", "Take a few years from tomorrow.", t => t.maxHealth = 120f),
                T("vesh_b2", "Haste of Ages", "Walk between the ticks of the clock.", t => t.moveSpeed = 0.1f),
                T("vesh_b3", "Rewound Wounds", "Some injuries never happened.", t => t.maxHealth = 200f),
                T("vesh_b4", "Endless Loop", "Again. And again. And again.", t => t.ultimateCooldownReduction = 0.3f)),
        };

        // Brann: items (Masterwork Gear) or mod sockets (Overcharged Sockets).
        static AscensionTree BrannAscension() => new()
        {
            pathA = AscPath(
                T("brann_a", "Master Smith", "Better hands, better steel.", t => t.itemStats = 0.1f),
                T("brann_a1", "Tempered Steel", "Folded a hundred times.", t => t.attackDamage = 10f),
                T("brann_a2", "Reinforced Plating", "Rivets in all the right places.", t => t.maxHealth = 150f),
                T("brann_a3", "Perfect Alloy", "The forge's finest work.", t => t.itemStats = 0.15f),
                T("brann_a4", "Heavy Hands", "Swing the hammer faster.", t => t.attackSpeed = 0.15f)),
            pathB = AscPath(
                T("brann_b", "Runesmith", "Every socket hums with power.", t => t.spellDamage = 0.15f),
                T("brann_b1", "Arcane Battery", "Store the spark for later.", t => t.maxMana = 100f),
                T("brann_b2", "Efficient Runes", "Waste nothing.", t => t.cooldownReduction = 0.15f),
                T("brann_b3", "Overload", "Push the runes past their limit.", t => t.spellDamage = 0.25f),
                T("brann_b4", "Splitting Rune", "One spell, many paths.", t => t.extraProjectiles = 1)),
        };

        // Torvald: toughness (Iron Hide) or the Aegis Wall.
        static AscensionTree TorvaldAscension() => new()
        {
            pathA = AscPath(
                T("torvald_a", "Juggernaut", "Grow harder to stop with every fight.", t =>
                {
                    t.healthPerLevel = 25f;
                    t.maxHealth = 100f;
                }),
                T("torvald_a1", "Iron Skin", "Blades bend on it.", t => t.maxHealth = 200f),
                T("torvald_a2", "Unstoppable", "Heavy, but always moving.", t => t.moveSpeed = 0.1f),
                T("torvald_a3", "Mountain", "Immovable.", t => t.maxHealth = 300f),
                T("torvald_a4", "Crushing Blows", "Every swing lands like a landslide.", t => t.attackDamage = 20f)),
            pathB = AscPath(
                T("torvald_b", "Aegis Warden", "Hold the wall a little longer.", t => t.ultimateDuration = 2f),
                T("torvald_b1", "Bulwark Drill", "Raise the shield on instinct.", t => t.ultimateCooldownReduction = 0.25f),
                T("torvald_b2", "Shield Bash", "The best defence hits back.", t => t.attackDamage = 15f),
                T("torvald_b3", "Unbreakable Wall", "Nothing gets through. Nothing.", t => t.ultimateDuration = 2f),
                T("torvald_b4", "Rapid Guard", "Always ready to block.", t => t.ultimateCooldownReduction = 0.25f)),
        };

        // Nyx: lone targets (Lone Prey) or Bloodrush.
        static AscensionTree NyxAscension() => new()
        {
            pathA = AscPath(
                T("nyx_a", "Lone Hunter", "Separate them, then strike.", t => t.isolationDamage = 0.1f),
                T("nyx_a1", "Predator", "Sharper claws.", t => t.attackDamage = 15f),
                T("nyx_a2", "Stalker's Pace", "Close the gap before they notice.", t => t.moveSpeed = 0.1f),
                T("nyx_a3", "Apex", "Nothing hunts the hunter.", t => t.isolationDamage = 0.15f),
                T("nyx_a4", "Frenzy", "Faster and faster.", t => t.attackSpeed = 0.2f)),
            pathB = AscPath(
                T("nyx_b", "Blood Frenzy", "The rush lasts longer.", t => t.ultimateDuration = 2f),
                T("nyx_b1", "Hemorrhage", "Strike while they bleed.", t => t.attackSpeed = 0.15f),
                T("nyx_b2", "Thirst", "Feed on the fight.", t => t.maxHealth = 150f),
                T("nyx_b3", "Endless Hunt", "The rush never ends.", t => t.ultimateDuration = 2f),
                T("nyx_b4", "Quick Return", "Rush again, sooner.", t => t.ultimateCooldownReduction = 0.3f)),
        };
    }
}
