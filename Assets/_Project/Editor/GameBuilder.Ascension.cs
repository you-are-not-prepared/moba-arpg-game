using System;
using Moba.Abilities;

namespace Moba.EditorTools
{
    // Ascension trees (first versions, to tune). Each hero has three paths, one per ultimate: two unique ones and the
    // one shared by its damage type and range (see GameBuilder.Kits). Level 6 picks the path, which puts its ultimate
    // in R; levels 11 and 16 each offer two talents that make that ultimate stronger AND build on the hero's passive.
    public static partial class GameBuilder
    {
        // How a talent improves the ultimate.
        enum Up { Damage, Cooldown, Duration, Radius, Stun, Items }

        static Talent T(string id, string name, string flavor, Action<Talent> stats)
        {
            var talent = new Talent { id = id, name = name, flavor = flavor };
            stats(talent);
            return talent;
        }

        static (Up, float) U(Up up, float value) => (up, value);

        // A passive part of a talent: its flavour line and its stats.
        static (string, Action<Talent>) P(string flavor, Action<Talent> stats) => (flavor, stats);

        // Three paths for `hero`. Each path: its ultimate and four ultimate upgrades (level 11 options, then level 16
        // options). `passives`: the four passive upgrades, paired with the ultimate upgrades in the same order.
        static AscensionTree Tree(string hero, (string, Action<Talent>)[] passives,
            params (AbilityDefinition ultimate, (Up, float)[] ups)[] paths)
        {
            var tree = new AscensionTree();
            for (int p = 0; p < paths.Length; p++)
            {
                (AbilityDefinition ultimate, (Up, float)[] ups) = paths[p];
                string letter = AscensionTree.Letter(p).ToLowerInvariant();
                var talents = new Talent[4];
                for (int i = 0; i < 4; i++)
                {
                    (Up up, float value) = ups[i];
                    (string flavor, Action<Talent> passive) = passives[i];
                    talents[i] = T($"{hero}_{letter}{i + 1}", $"{UpName(up, i >= 2)} {ultimate.displayName}", flavor, t =>
                    {
                        Apply(t, up, value);
                        passive(t);
                    });
                }
                tree.paths.Add(new AscensionPath
                {
                    ultimate = ultimate,
                    root = T($"{hero}_{letter}", ultimate.displayName, ultimate.description, _ => { }),
                    second = new[] { talents[0], talents[1] },
                    third = new[] { talents[2], talents[3] },
                });
            }
            return tree;
        }

        static string UpName(Up up, bool strong) => up switch
        {
            Up.Damage => strong ? "Mighty" : "Empowered",
            Up.Cooldown => strong ? "Ever-Ready" : "Swift",
            Up.Duration => strong ? "Endless" : "Lasting",
            Up.Radius => strong ? "Sweeping" : "Wide",
            Up.Items => strong ? "Masterwork" : "Tuned",
            _ => strong ? "Paralyzing" : "Crushing",
        };

        static void Apply(Talent t, Up up, float value)
        {
            switch (up)
            {
                case Up.Damage: t.ultimateDamage += value; break;
                case Up.Cooldown: t.ultimateCooldownReduction += value; break;
                case Up.Duration: t.ultimateDuration += value; break;
                case Up.Radius: t.ultimateRadius += value; break;
                case Up.Stun: t.stunDuration += value; break;
                // Overcharged Sockets is a passive ultimate: its path makes the gear in those sockets better.
                case Up.Items: t.itemStats += value; break;
            }
        }

        // Ultimate upgrade sets: level 11 (two options), then level 16 (two options).
        static (Up, float)[] Ups(params (Up, float)[] ups) => ups;

        // ---------- Heroes ----------

        static AscensionTree KaelAscension(Mats m) => Tree("kael", new[]
            {
                P("Split Shot: your arrows hit 10% harder.", t => t.spellDamage = 0.1f),
                P("Quick hands: 15% more attack speed.", t => t.attackSpeed = 0.15f),
                P("Split Shot: one more projectile on every projectile spell.", t => t.extraProjectiles = 1),
                P("Split Shot: your arrows hit 20% harder.", t => t.spellDamage = 0.2f),
            },
            (SkybreakerArrow(m), Ups(U(Up.Stun, 0.5f), U(Up.Damage, 0.2f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (HawkEye(m), Ups(U(Up.Duration, 1f), U(Up.Cooldown, 0.15f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (ArrowStorm(m), Ups(U(Up.Radius, 0.2f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));

        static AscensionTree VeshAscension(Mats m) => Tree("vesh", new[]
            {
                P("Quickened Mind: cooldowns another 5% shorter.", t => t.cooldownReduction = 0.05f),
                P("Deep reserves: +150 max mana.", t => t.maxMana = 150f),
                P("Quickened Mind: cooldowns another 10% shorter.", t => t.cooldownReduction = 0.1f),
                P("Folded time: spells deal 15% more damage.", t => t.spellDamage = 0.15f),
            },
            (Rewind(), Ups(U(Up.Cooldown, 0.15f), U(Up.Cooldown, 0.1f), U(Up.Cooldown, 0.2f), U(Up.Cooldown, 0.15f))),
            (TimeStop(m), Ups(U(Up.Stun, 0.25f), U(Up.Radius, 0.2f), U(Up.Stun, 0.5f), U(Up.Cooldown, 0.2f))),
            (Thunderstrike(m), Ups(U(Up.Stun, 0.25f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));

        static AscensionTree BrannAscension(Mats m) => Tree("brann", new[]
            {
                P("Masterwork Gear: items give another 5% more.", t => t.itemStats = 0.05f),
                P("Weighted tools: +10 attack damage.", t => t.attackDamage = 10f),
                P("Masterwork Gear: items give another 10% more.", t => t.itemStats = 0.1f),
                P("Reinforced plating: +200 max health.", t => t.maxHealth = 200f),
            },
            (OverchargedSockets(), Ups(U(Up.Items, 0.05f), U(Up.Items, 0.05f), U(Up.Items, 0.1f), U(Up.Items, 0.1f))),
            (Bombardment(m), Ups(U(Up.Damage, 0.2f), U(Up.Radius, 0.2f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (Meteor(m), Ups(U(Up.Radius, 0.2f), U(Up.Damage, 0.2f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))));

        static AscensionTree TorvaldAscension(Mats m) => Tree("torvald", new[]
            {
                P("Iron Hide: +15 more max health per level.", t => t.healthPerLevel = 15f),
                P("Thick skin: +150 max health.", t => t.maxHealth = 150f),
                P("Iron Hide: +25 more max health per level.", t => t.healthPerLevel = 25f),
                P("Light on your feet: 8% faster.", t => t.moveSpeed = 0.08f),
            },
            (AegisWall(m), Ups(U(Up.Duration, 1f), U(Up.Cooldown, 0.15f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (Unbreakable(m), Ups(U(Up.Duration, 1f), U(Up.Cooldown, 0.15f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (Whirlwind(m), Ups(U(Up.Duration, 1f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));

        static AscensionTree NyxAscension(Mats m) => Tree("nyx", new[]
            {
                P("Lone Prey: another +5% damage to isolated targets.", t => t.isolationDamage = 0.05f),
                P("Frenzy: 10% more attack speed.", t => t.attackSpeed = 0.1f),
                P("Lone Prey: another +10% damage to isolated targets.", t => t.isolationDamage = 0.1f),
                P("Sharpened claws: +15 attack damage.", t => t.attackDamage = 15f),
            },
            (Bloodrush(m), Ups(U(Up.Duration, 1.5f), U(Up.Cooldown, 0.15f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (ShadowExecute(m), Ups(U(Up.Damage, 0.2f), U(Up.Cooldown, 0.15f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (Whirlwind(m), Ups(U(Up.Duration, 1f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));

        static AscensionTree ThessaAscension(Mats m) => Tree("thessa", new[]
            {
                P("Tailwind: another +10% movement speed.", t => t.tailwindSpeed = 0.1f),
                P("Tailwind: lasts 1s longer.", t => t.tailwindDuration = 1f),
                P("Tailwind: +20% attack speed while it lasts.", t => t.tailwindAttackSpeed = 0.2f),
                P("Tailwind: another +15% movement speed.", t => t.tailwindSpeed = 0.15f),
            },
            (EyeOfTheStorm(m), Ups(U(Up.Duration, 1f), U(Up.Cooldown, 0.15f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (GaleForce(m), Ups(U(Up.Damage, 0.2f), U(Up.Cooldown, 0.15f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (Cyclone(m), Ups(U(Up.Radius, 0.2f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));

        static AscensionTree OrenAscension(Mats m) => Tree("oren", new[]
            {
                P("Kindled Blade: Heat bursts deal +25 damage.", t => t.heatBurstDamage = 25f),
                P("Stoked: 10% more attack speed.", t => t.attackSpeed = 0.1f),
                P("Kindled Blade: Heat bursts deal +50 damage.", t => t.heatBurstDamage = 50f),
                P("Forge heart: +200 max health.", t => t.maxHealth = 200f),
            },
            (BlazingLeap(m), Ups(U(Up.Damage, 0.2f), U(Up.Duration, 2f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (PhoenixForm(m), Ups(U(Up.Duration, 1f), U(Up.Cooldown, 0.15f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (Inferno(m), Ups(U(Up.Duration, 1f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));

        static AscensionTree IlsaAscension(Mats m) => Tree("ilsa", new[]
            {
                P("Chilling Touch: spell hits slow 5% more.", t => t.spellSlow = 0.05f),
                P("Chilling Touch: slows last 0.5s longer.", t => t.spellSlowSeconds = 0.5f),
                P("Chilling Touch: spell hits slow 10% more.", t => t.spellSlow = 0.1f),
                P("Shatter: spells deal 15% more damage.", t => t.spellDamage = 0.15f),
            },
            (GlacialPrison(m), Ups(U(Up.Stun, 0.25f), U(Up.Radius, 0.2f), U(Up.Stun, 0.5f), U(Up.Cooldown, 0.2f))),
            (Avalanche(m), Ups(U(Up.Stun, 0.25f), U(Up.Damage, 0.2f), U(Up.Stun, 0.5f), U(Up.Damage, 0.3f))),
            (Blizzard(m), Ups(U(Up.Duration, 1f), U(Up.Radius, 0.2f), U(Up.Damage, 0.3f), U(Up.Radius, 0.25f))));

        static AscensionTree MaelaAscension(Mats m) => Tree("maela", new[]
            {
                P("Verdant Bond: heals 5% more of the damage dealt.", t => t.allyHeal = 0.05f),
                P("Sap: +150 max mana.", t => t.maxMana = 150f),
                P("Verdant Bond: heals 10% more of the damage dealt.", t => t.allyHeal = 0.1f),
                P("Wildgrowth: spells deal (and heal) 15% more.", t => t.spellDamage = 0.15f),
            },
            (Sanctuary(m), Ups(U(Up.Duration, 1f), U(Up.Radius, 0.2f), U(Up.Duration, 2f), U(Up.Cooldown, 0.25f))),
            (WildGrowth(m), Ups(U(Up.Damage, 0.2f), U(Up.Radius, 0.2f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (Overgrowth(m), Ups(U(Up.Stun, 0.25f), U(Up.Radius, 0.2f), U(Up.Stun, 0.5f), U(Up.Damage, 0.3f))));

        static AscensionTree CorvinAscension(Mats m) => Tree("corvin", new[]
            {
                P("Static Charge: charged hits jump to 1 more enemy.", t => t.chainJumps = 1),
                P("Static Charge: charged one cast sooner.", t => t.chargeSooner = 1),
                P("Static Charge: charged hits jump to 1 more enemy.", t => t.chainJumps = 1),
                P("Voltage: spells deal 15% more damage.", t => t.spellDamage = 0.15f),
            },
            (Thunderhead(m), Ups(U(Up.Duration, 1f), U(Up.Damage, 0.2f), U(Up.Duration, 2f), U(Up.Radius, 0.25f))),
            (ChainStorm(m), Ups(U(Up.Damage, 0.2f), U(Up.Cooldown, 0.15f), U(Up.Damage, 0.3f), U(Up.Cooldown, 0.25f))),
            (Thunderstrike(m), Ups(U(Up.Stun, 0.25f), U(Up.Damage, 0.2f), U(Up.Radius, 0.25f), U(Up.Damage, 0.3f))));
    }
}
