using System;

namespace Moba.EditorTools
{
    // Placeholder ascension trees. Path A builds on each hero's passive, path B on the ultimate, and path C is a
    // hybrid playstyle that draws on both. Level 6 picks the path; levels 11 and 18 each offer two of its talents.
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

        // Kael: projectiles (Split Shot), the Skybreaker Arrow, or a mobile kiting archer.
        static AscensionTree KaelAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("kael_a", "Storm of Arrows", "Every shot becomes a volley.", t => t.extraProjectiles = 1),
                    T("kael_a1", "Splintering Shots", "Arrowheads that shatter on impact.", t => t.spellDamage = 0.15f),
                    T("kael_a2", "Quickdraw", "Nock, draw, loose. Again.", t => t.attackSpeed = 0.2f),
                    T("kael_a3", "Endless Quiver", "There is always one more arrow.", t => t.extraProjectiles = 1),
                    T("kael_a4", "Barrage Rhythm", "Fall into the rhythm of the hunt.", t => t.cooldownReduction = 0.15f)),
                AscPath(
                    T("kael_b", "Skybreaker", "The great bow is always strung.", t => t.ultimateCooldownReduction = 0.25f),
                    T("kael_b1", "Pinning Shot", "Nail them where they stand.", t => t.stunDuration = 0.75f),
                    T("kael_b2", "Hunter's Focus", "Patience sharpens every arrow.", t => t.attackDamage = 15f),
                    T("kael_b3", "Heaven's Fall", "The sky itself holds them down.", t =>
                    {
                        t.stunDuration = 0.75f;
                        t.spellDamage = 0.2f;
                    }),
                    T("kael_b4", "Sky Hunter", "Draw the great bow again and again.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("kael_c", "Windrunner", "Never where they aim.", t =>
                    {
                        t.moveSpeed = 0.1f;
                        t.attackSpeed = 0.1f;
                    }),
                    T("kael_c1", "Hit and Run", "Shoot, step, shoot.", t => t.moveSpeed = 0.1f),
                    T("kael_c2", "Steady Aim", "Every arrow counts.", t => t.attackDamage = 12f),
                    T("kael_c3", "Gale Volley", "The wind carries extra arrows.", t =>
                    {
                        t.extraProjectiles = 1;
                        t.ultimateCooldownReduction = 0.15f;
                    }),
                    T("kael_c4", "Sky Piercer", "Strike from the edge of sight.", t =>
                    {
                        t.stunDuration = 0.5f;
                        t.spellDamage = 0.1f;
                    })),
            },
        };

        // Vesh: cooldowns (Quickened Mind), Rewind, or burst casting.
        static AscensionTree VeshAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("vesh_a", "Time Weaver", "Seconds bend around your spells.", t => t.cooldownReduction = 0.1f),
                    T("vesh_a1", "Accelerated Mind", "Think faster, cast more.", t => t.maxMana = 80f),
                    T("vesh_a2", "Temporal Flow", "Every spell lands a moment sooner.", t => t.spellDamage = 0.15f),
                    T("vesh_a3", "Perpetual Motion", "Never stop casting.", t => t.cooldownReduction = 0.1f),
                    T("vesh_a4", "Echo", "Each spell leaves an echo behind.", t => t.extraProjectiles = 1)),
                AscPath(
                    T("vesh_b", "Chronoshift", "Rewind comes around sooner.", t => t.ultimateCooldownReduction = 0.3f),
                    T("vesh_b1", "Borrowed Time", "Take a few years from tomorrow.", t => t.maxHealth = 120f),
                    T("vesh_b2", "Haste of Ages", "Walk between the ticks of the clock.", t => t.moveSpeed = 0.1f),
                    T("vesh_b3", "Rewound Wounds", "Some injuries never happened.", t => t.maxHealth = 200f),
                    T("vesh_b4", "Endless Loop", "Again. And again. And again.", t => t.ultimateCooldownReduction = 0.3f)),
                AscPath(
                    T("vesh_c", "Paradox", "Cast it all, rewind, cast it all again.", t =>
                    {
                        t.spellDamage = 0.1f;
                        t.ultimateCooldownReduction = 0.15f;
                    }),
                    T("vesh_c1", "Temporal Spike", "A moment, sharpened to a point.", t => t.spellDamage = 0.15f),
                    T("vesh_c2", "Mana Loop", "Spend it twice.", t => t.maxMana = 100f),
                    T("vesh_c3", "Collapse", "Every second lands at once.", t => t.spellDamage = 0.2f),
                    T("vesh_c4", "Infinite Loop", "There is no last cast.", t =>
                    {
                        t.cooldownReduction = 0.1f;
                        t.ultimateCooldownReduction = 0.2f;
                    })),
            },
        };

        // Brann: items (Masterwork Gear), mod sockets (Overcharged Sockets), or a sturdy frontline builder.
        static AscensionTree BrannAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("brann_a", "Master Smith", "Better hands, better steel.", t => t.itemStats = 0.1f),
                    T("brann_a1", "Tempered Steel", "Folded a hundred times.", t => t.attackDamage = 10f),
                    T("brann_a2", "Reinforced Plating", "Rivets in all the right places.", t => t.maxHealth = 150f),
                    T("brann_a3", "Perfect Alloy", "The forge's finest work.", t => t.itemStats = 0.15f),
                    T("brann_a4", "Heavy Hands", "Swing the hammer faster.", t => t.attackSpeed = 0.15f)),
                AscPath(
                    T("brann_b", "Runesmith", "Every socket hums with power.", t => t.spellDamage = 0.15f),
                    T("brann_b1", "Arcane Battery", "Store the spark for later.", t => t.maxMana = 100f),
                    T("brann_b2", "Efficient Runes", "Waste nothing.", t => t.cooldownReduction = 0.15f),
                    T("brann_b3", "Overload", "Push the runes past their limit.", t => t.spellDamage = 0.25f),
                    T("brann_b4", "Splitting Rune", "One spell, many paths.", t => t.extraProjectiles = 1)),
                AscPath(
                    T("brann_c", "Bastion", "Build it to last.", t =>
                    {
                        t.itemStats = 0.05f;
                        t.maxHealth = 150f;
                    }),
                    T("brann_c1", "Riveted Armor", "One more plate. Then another.", t => t.maxHealth = 200f),
                    T("brann_c2", "Socket Ward", "Runes that hit back.", t => t.spellDamage = 0.1f),
                    T("brann_c3", "Living Fortress", "Grows sturdier with every fight.", t => t.healthPerLevel = 30f),
                    T("brann_c4", "Rune Hammer", "Steel and sorcery in one swing.", t =>
                    {
                        t.attackDamage = 15f;
                        t.spellDamage = 0.1f;
                    })),
            },
        };

        // Torvald: toughness (Iron Hide), the Aegis Wall, or an aggressive warlord.
        static AscensionTree TorvaldAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("torvald_a", "Juggernaut", "Grow harder to stop with every fight.", t =>
                    {
                        t.healthPerLevel = 25f;
                        t.maxHealth = 100f;
                    }),
                    T("torvald_a1", "Iron Skin", "Blades bend on it.", t => t.maxHealth = 200f),
                    T("torvald_a2", "Unstoppable", "Heavy, but always moving.", t => t.moveSpeed = 0.1f),
                    T("torvald_a3", "Mountain", "Immovable.", t => t.maxHealth = 300f),
                    T("torvald_a4", "Crushing Blows", "Every swing lands like a landslide.", t => t.attackDamage = 20f)),
                AscPath(
                    T("torvald_b", "Aegis Warden", "Hold the wall a little longer.", t => t.ultimateDuration = 2f),
                    T("torvald_b1", "Bulwark Drill", "Raise the shield on instinct.", t => t.ultimateCooldownReduction = 0.25f),
                    T("torvald_b2", "Shield Bash", "The best defence hits back.", t => t.attackDamage = 15f),
                    T("torvald_b3", "Unbreakable Wall", "Nothing gets through. Nothing.", t => t.ultimateDuration = 2f),
                    T("torvald_b4", "Rapid Guard", "Always ready to block.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("torvald_c", "Warlord", "Lead the charge behind the shield.", t =>
                    {
                        t.attackDamage = 12f;
                        t.ultimateDuration = 1f;
                    }),
                    T("torvald_c1", "Battle Cry", "Swing faster, push harder.", t => t.attackSpeed = 0.15f),
                    T("torvald_c2", "Forced March", "Never fall behind the front line.", t => t.moveSpeed = 0.1f),
                    T("torvald_c3", "Shield Charge", "The wall moves forward.", t =>
                    {
                        t.ultimateDuration = 1f;
                        t.moveSpeed = 0.1f;
                    }),
                    T("torvald_c4", "Warlord's Might", "One blow ends the fight.", t => t.attackDamage = 25f)),
            },
        };

        // Nyx: lone targets (Lone Prey), Bloodrush, or a slippery skirmisher.
        static AscensionTree NyxAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("nyx_a", "Lone Hunter", "Separate them, then strike.", t => t.isolationDamage = 0.1f),
                    T("nyx_a1", "Predator", "Sharper claws.", t => t.attackDamage = 15f),
                    T("nyx_a2", "Stalker's Pace", "Close the gap before they notice.", t => t.moveSpeed = 0.1f),
                    T("nyx_a3", "Apex", "Nothing hunts the hunter.", t => t.isolationDamage = 0.15f),
                    T("nyx_a4", "Frenzy", "Faster and faster.", t => t.attackSpeed = 0.2f)),
                AscPath(
                    T("nyx_b", "Blood Frenzy", "The rush lasts longer.", t => t.ultimateDuration = 2f),
                    T("nyx_b1", "Hemorrhage", "Strike while they bleed.", t => t.attackSpeed = 0.15f),
                    T("nyx_b2", "Thirst", "Feed on the fight.", t => t.maxHealth = 150f),
                    T("nyx_b3", "Endless Hunt", "The rush never ends.", t => t.ultimateDuration = 2f),
                    T("nyx_b4", "Quick Return", "Rush again, sooner.", t => t.ultimateCooldownReduction = 0.3f)),
                AscPath(
                    T("nyx_c", "Shadowstep", "In, out, gone.", t =>
                    {
                        t.moveSpeed = 0.1f;
                        t.isolationDamage = 0.05f;
                    }),
                    T("nyx_c1", "Ambush", "The first strike is the deepest.", t => t.attackDamage = 15f),
                    T("nyx_c2", "Night Run", "The rush carries you further.", t => t.ultimateDuration = 1.5f),
                    T("nyx_c3", "Phantom", "Too fast to pin down.", t =>
                    {
                        t.moveSpeed = 0.1f;
                        t.attackSpeed = 0.1f;
                    }),
                    T("nyx_c4", "Execution", "Alone, and out of time.", t =>
                    {
                        t.isolationDamage = 0.1f;
                        t.attackDamage = 10f;
                    })),
            },
        };
    }
}
