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
        // Thessa: Tailwind (casting gives speed), Eye of the Storm, or a hit-and-run skirmisher.
        static AscensionTree ThessaAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("thessa_a", "Gale", "The wind never quite lets go.", t => t.tailwindDuration = 0.5f),
                    T("thessa_a1", "Jet Stream", "Ride the gust.", t => t.tailwindSpeed = 0.1f),
                    T("thessa_a2", "Updraft", "One spell carries her further.", t => t.tailwindDuration = 1f),
                    T("thessa_a3", "Slipstream", "Strike while the wind is behind you.", t => t.tailwindAttackSpeed = 0.2f),
                    T("thessa_a4", "Second Gust", "The wind keeps blowing.", t => t.tailwindDuration = 1.5f)),
                AscPath(
                    T("thessa_b", "Hurricane", "The storm gathers a little longer.", t => t.ultimateDuration = 1f),
                    T("thessa_b1", "Eye Widens", "A longer calm at the centre.", t => t.ultimateDuration = 2f),
                    T("thessa_b2", "Charged Air", "Every spell crackles.", t => t.spellDamage = 0.15f),
                    T("thessa_b3", "Ceaseless", "Spells come back faster, always.", t => t.cooldownReduction = 0.1f),
                    T("thessa_b4", "Calm Before", "The next storm is never far.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("thessa_c", "Skirmisher", "Never stand still.", t => t.attackSpeed = 0.1f),
                    T("thessa_c1", "Quickdraw", "Faster hands.", t => t.attackSpeed = 0.15f),
                    T("thessa_c2", "Light Feet", "Faster feet.", t => t.moveSpeed = 0.1f),
                    T("thessa_c3", "Crosswind", "One more on the wind.", t => t.extraProjectiles = 1),
                    T("thessa_c4", "Windward", "Spells come back faster.", t => t.cooldownReduction = 0.1f)),
            },
        };

        // Oren: Heat bursts (Kindled Blade), Blazing Leap, or a sturdier Phoenix.
        static AscensionTree OrenAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("oren_a", "Wildfire", "The fire spreads.", t => t.heatBurstDamage = 25f),
                    T("oren_a1", "Flashpoint", "Bigger flames.", t => t.heatBurstDamage = 25f),
                    T("oren_a2", "Stoke", "Fan the flames faster.", t => t.attackSpeed = 0.1f),
                    T("oren_a3", "Inferno", "Everything burns.", t => t.heatBurstDamage = 50f),
                    T("oren_a4", "Forge Heart", "Hot iron doesn't break.", t => t.maxHealth = 200f)),
                AscPath(
                    T("oren_b", "Meteor", "The blades stay hot longer.", t => t.ultimateDuration = 2f),
                    T("oren_b1", "Quick Ignition", "Ready to leap again.", t => t.ultimateCooldownReduction = 0.2f),
                    T("oren_b2", "Scorch", "Landings hit harder.", t => t.spellDamage = 0.15f),
                    T("oren_b3", "Afterburn", "The fire refuses to die.", t => t.ultimateDuration = 3f),
                    T("oren_b4", "Second Wind", "Always another leap.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("oren_c", "Phoenix", "Rises from the ashes.", t => t.maxHealth = 150f),
                    T("oren_c1", "Searing Edge", "Every cut burns deeper.", t => t.attackDamage = 15f),
                    T("oren_c2", "Ember Stride", "Close the gap.", t => t.moveSpeed = 0.08f),
                    T("oren_c3", "Rebirth", "Stronger with every level.", t => t.healthPerLevel = 30f),
                    T("oren_c4", "Sunfire", "The last blow lands hardest.", t => t.attackDamage = 25f)),
            },
        };

        // Ilsa: stronger slows (Chilling Touch), Glacial Prison, or a kiting Glacier Walker.
        static AscensionTree IlsaAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("ilsa_a", "Deep Freeze", "The cold bites harder.", t => t.spellSlow = 0.05f),
                    T("ilsa_a1", "Frostbite", "Hardly able to move.", t => t.spellSlow = 0.1f),
                    T("ilsa_a2", "Lingering Frost", "The chill stays.", t => t.spellSlowSeconds = 1f),
                    T("ilsa_a3", "Shatter", "Frozen things break.", t => t.spellDamage = 0.2f),
                    T("ilsa_a4", "Permafrost", "Winter never ends.", t => t.spellSlow = 0.1f)),
                AscPath(
                    T("ilsa_b", "Winter's Grasp", "The ice holds longer.", t => t.ultimateDuration = 1f),
                    T("ilsa_b1", "Deep Ice", "Frozen solid.", t => t.stunDuration = 0.5f),
                    T("ilsa_b2", "Cold Snap", "Ready sooner.", t => t.ultimateCooldownReduction = 0.15f),
                    T("ilsa_b3", "Endless Winter", "The field lingers.", t => t.ultimateDuration = 2f),
                    T("ilsa_b4", "Glacial Age", "Ice, again and again.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("ilsa_c", "Glacier Walker", "Glide over the ice.", t => t.moveSpeed = 0.05f),
                    T("ilsa_c1", "Frost Armour", "A shell of ice.", t => t.maxHealth = 150f),
                    T("ilsa_c2", "Hailstorm", "One more shard.", t => t.extraProjectiles = 1),
                    T("ilsa_c3", "Arctic Mind", "Clear and cold thoughts.", t => t.cooldownReduction = 0.1f),
                    T("ilsa_c4", "Glacier Heart", "A deep well of power.", t => t.maxMana = 150f)),
            },
        };

        // Maela: stronger healing (Verdant Bond), Sanctuary, or a sturdy Warden of the Wild.
        static AscensionTree MaelaAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("maela_a", "Lifebloom", "Life flows further.", t => t.allyHeal = 0.05f),
                    T("maela_a1", "Deep Roots", "Draw life from deeper.", t => t.allyHeal = 0.1f),
                    T("maela_a2", "Wildgrowth", "Every spell grows stronger.", t => t.spellDamage = 0.1f),
                    T("maela_a3", "Full Bloom", "Life overflowing.", t => t.allyHeal = 0.1f),
                    T("maela_a4", "Barkskin", "Hard to cut down.", t => t.maxHealth = 200f)),
                AscPath(
                    T("maela_b", "Sacred Grove", "The grove stands longer.", t => t.ultimateDuration = 1f),
                    T("maela_b1", "Old Growth", "Ancient and patient.", t => t.ultimateDuration = 2f),
                    T("maela_b2", "Quick Sprouts", "The seeds are ready.", t => t.ultimateCooldownReduction = 0.15f),
                    T("maela_b3", "Seasons", "Everything comes back.", t => t.cooldownReduction = 0.1f),
                    T("maela_b4", "Evergreen", "Never bare.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("maela_c", "Warden of the Wild", "The forest protects its own.", t => t.maxHealth = 150f),
                    T("maela_c1", "Thorns", "Even she has teeth.", t => t.attackDamage = 10f),
                    T("maela_c2", "Swiftwood", "Light on her feet.", t => t.moveSpeed = 0.08f),
                    T("maela_c3", "Sap", "A deep reserve.", t => t.maxMana = 150f),
                    T("maela_c4", "Ironbark", "Grows tougher every season.", t => t.healthPerLevel = 25f)),
            },
        };

        // Corvin: more chains (Static Charge), Thunderhead, or a Conduit for constant casting.
        static AscensionTree CorvinAscension() => new()
        {
            paths =
            {
                AscPath(
                    T("corvin_a", "Overcharge", "The charge builds faster.", t => t.chargeSooner = 1),
                    T("corvin_a1", "Arc", "The lightning reaches further.", t => t.chainJumps = 1),
                    T("corvin_a2", "Voltage", "Every spell hits harder.", t => t.spellDamage = 0.1f),
                    T("corvin_a3", "Chain Reaction", "It never stops at one.", t => t.chainJumps = 1),
                    T("corvin_a4", "Full Charge", "Always crackling.", t => t.chargeSooner = 1)),
                AscPath(
                    T("corvin_b", "Tempest", "The storm lingers.", t => t.ultimateDuration = 1f),
                    T("corvin_b1", "Brewing Storm", "Thunder that won't pass.", t => t.ultimateDuration = 2f),
                    T("corvin_b2", "Thunderclap", "Every strike shakes the ground.", t => t.spellDamage = 0.15f),
                    T("corvin_b3", "Gathering Clouds", "The sky darkens sooner.", t => t.ultimateCooldownReduction = 0.2f),
                    T("corvin_b4", "Eye of Thunder", "Storms on call.", t => t.ultimateCooldownReduction = 0.25f)),
                AscPath(
                    T("corvin_c", "Conduit", "Power flows through him.", t => t.cooldownReduction = 0.05f),
                    T("corvin_c1", "Flow", "Spells return faster.", t => t.cooldownReduction = 0.1f),
                    T("corvin_c2", "Battery", "A deeper charge.", t => t.maxMana = 150f),
                    T("corvin_c3", "Forked Bolt", "One more spark.", t => t.extraProjectiles = 1),
                    T("corvin_c4", "Storm Mind", "Thought becomes lightning.", t => t.spellDamage = 0.15f)),
            },
        };

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
