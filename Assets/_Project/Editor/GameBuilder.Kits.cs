using Moba.Abilities;
using UnityEditor;
using UnityEngine;

namespace Moba.EditorTools
{
    // Hero kits: every hero's signature spell (in Q at the start, 5 ranks) and the ultimates offered at the first
    // ascension: two unique to the hero (the original one in GameBuilder.cs and a second one here) and one shared by
    // every hero with the same base damage type and attack range (melee or ranged), ranked 1-3 by ascension.
    public static partial class GameBuilder
    {
        const int SignatureCost = 300;

        // ---------- Building blocks ----------

        // A signature spell: adaptive (the hero's damage type and look), sells back at full price.
        static T Signature<T>(string id, string name, string glyph, Color color, string description, float[] cooldowns,
            float[] manaCosts) where T : AbilityDefinition
        {
            var spell = LoadOrCreate<T>($"{HeroesDir}/Sig_{id}.asset");
            spell.displayName = name;
            spell.description = description;
            spell.cost = SignatureCost;
            spell.sellsAtFullPrice = true;
            spell.glyph = glyph;
            spell.color = TestKindColors ? TestSpellColor : color;
            spell.adaptive = true;
            spell.damageType = DamageType.None;
            spell.typeNames = new string[DamageTypes.Count];
            spell.typeGlyphs = new string[DamageTypes.Count];
            spell.cooldownByRank = cooldowns;
            spell.manaCostByRank = manaCosts;
            return spell;
        }

        // An ultimate (3 ranks, free). `adaptive` ones take the caster's damage type and look (shared ultimates and
        // projectile ultimates); the others deal `type`.
        static T Ultimate<T>(string id, string name, string glyph, Color color, string description, DamageType type,
            bool adaptive, float[] cooldowns, float mana) where T : AbilityDefinition
        {
            var spell = LoadOrCreate<T>($"{HeroesDir}/Ult_{id}.asset");
            spell.displayName = name;
            spell.description = description;
            spell.cost = 0;
            spell.glyph = glyph;
            spell.color = color;
            spell.adaptive = adaptive;
            spell.damageType = adaptive ? DamageType.None : type;
            spell.typeNames = new string[DamageTypes.Count];
            spell.typeGlyphs = new string[DamageTypes.Count];
            spell.cooldownByRank = cooldowns;
            spell.manaCostByRank = new[] { mana };
            return spell;
        }

        static float[] Ranks(params float[] values) => values;

        static ProjectileSpell Shot(ProjectileSpell spell, float damage, float perRank, float explosion, bool pierce,
            float speed, float distance, float size)
        {
            spell.tags = SpellTags.Projectile | (explosion > 0f ? SpellTags.AreaOfEffect : SpellTags.None);
            spell.targeting = AbilityTargeting.Point;
            spell.range = 0f;
            spell.baseDamage = damage;
            spell.damagePerRank = perRank;
            spell.explosionRadius = explosion;
            spell.pierce = pierce;
            spell.stunDuration = 0f;
            spell.slow = 0f;
            spell.chainJumps = 0;
            spell.cooldownRefund = 0f;
            spell.projectileSpeed = speed;
            spell.maxDistance = distance;
            spell.projectileSize = size;
            spell.spreadAngle = 12f;
            return spell;
        }

        static BurstSpell Blast(BurstSpell spell, float range, float radius, float delay, float[] damage)
        {
            spell.tags = SpellTags.AreaOfEffect;
            spell.targeting = AbilityTargeting.Point;
            spell.range = range;
            spell.radius = radius;
            spell.areaRadius = radius;
            spell.delay = delay;
            spell.damageByRank = damage;
            spell.count = 1;
            spell.spacing = 3f;
            spell.stunSeconds = 0f;
            spell.slow = 0f;
            spell.burnFraction = 0f;
            return spell;
        }

        static MeleeSpell Strike(MeleeSpell spell, float[] damage, bool lunge, float reachOrRange, float coneOrRadius)
        {
            spell.tags = SpellTags.Melee | SpellTags.AreaOfEffect;
            spell.targeting = AbilityTargeting.Point;
            spell.damageByRank = damage;
            spell.lunge = lunge;
            if (lunge)
            {
                spell.range = reachOrRange;
                spell.landingRadius = coneOrRadius;
                spell.areaRadius = coneOrRadius;
                spell.lungeSeconds = 0.25f;
                spell.lungeHeight = 0.8f;
            }
            else
            {
                spell.range = 0f;
                spell.reach = reachOrRange;
                spell.coneAngle = coneOrRadius;
            }
            spell.stunSeconds = 0f;
            spell.executeFraction = 0f;
            spell.heatStacks = 0;
            return spell;
        }

        static T Done<T>(T spell) where T : Object
        {
            EditorUtility.SetDirty(spell);
            return spell;
        }

        // ---------- Signature spells (Q) ----------

        static AbilityDefinition PiercingShot(Mats m)
        {
            var s = Shot(Signature<ProjectileSpell>("PiercingShot", "Piercing Shot", "PS", Rgb(0.35f, 0.78f, 0.45f),
                "A long arrow that flies through every enemy in its path.",
                Ranks(5f, 4.5f, 4f, 3.5f, 3f), Ranks(30f, 32f, 34f, 36f, 38f)), 60f, 25f, 0f, true, 24f, 16f, 0.6f);
            s.projectileMaterial = m.Arrow;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        static AbilityDefinition ChronoBolt(Mats m)
        {
            var s = Shot(Signature<ProjectileSpell>("ChronoBolt", "Chrono Bolt", "CB", Rgb(0.65f, 0.45f, 0.95f),
                "A bolt that explodes on the first enemy; a hit winds your other cooldowns forward.",
                Ranks(4f, 3.75f, 3.5f, 3f, 2.5f), Ranks(35f, 37f, 39f, 41f, 43f)), 55f, 25f, 1.5f, false, 20f, 14f, 0.55f);
            s.cooldownRefund = 1.5f;
            s.projectileMaterial = m.Fire;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        static AbilityDefinition BlastCharge(Mats m) => Done(BurnBlast(Blast(Signature<BurstSpell>("BlastCharge", "Blast Charge",
            "BC", Rgb(0.85f, 0.55f, 0.25f), "Lobs a charge that blows up after a moment and sets everything near it alight.",
            Ranks(6f, 5.5f, 5f, 4.5f, 4f), Ranks(45f, 48f, 51f, 54f, 57f)), 8f, 2.2f, 0.5f, Ranks(70f, 95f, 120f, 145f, 170f)), 0.3f));

        static BurstSpell BurnBlast(BurstSpell spell, float burn)
        {
            spell.burnFraction = burn;
            return spell;
        }

        static AbilityDefinition ShieldBash(Mats m)
        {
            var s = Strike(Signature<MeleeSpell>("ShieldBash", "Shield Bash", "SB", Rgb(0.55f, 0.68f, 0.85f),
                "Slams the shield into everything in front of you, stunning them for a moment.",
                Ranks(8f, 7.5f, 7f, 6.5f, 6f), Ranks(40f, 42f, 44f, 46f, 48f)), Ranks(55f, 75f, 95f, 115f, 135f), false, 2.5f, 90f);
            s.stunSeconds = 0.75f;
            return Done(s);
        }

        static AbilityDefinition Pounce(Mats m) => Done(Strike(Signature<MeleeSpell>("Pounce", "Pounce", "PO", Rgb(0.85f, 0.25f, 0.4f),
            "Leaps onto a spot and rakes everything where you land.",
            Ranks(7f, 6.5f, 6f, 5.5f, 5f), Ranks(40f, 42f, 44f, 46f, 48f)), Ranks(65f, 90f, 115f, 140f, 165f), true, 5f, 1.8f));

        static AbilityDefinition GaleShot(Mats m)
        {
            var s = Shot(Signature<ProjectileSpell>("GaleShot", "Gale Shot", "GS", Rgb(0.4f, 0.85f, 0.8f),
                "A blade of wind that cuts through every enemy in its path and slows them.",
                Ranks(4f, 3.75f, 3.5f, 3f, 2.5f), Ranks(30f, 32f, 34f, 36f, 38f)), 55f, 22f, 0f, true, 22f, 14f, 0.9f);
            s.slow = 0.2f;
            s.slowSeconds = 1f;
            s.projectileMaterial = m.Arrow;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        static AbilityDefinition SearingStrike(Mats m)
        {
            var s = Strike(Signature<MeleeSpell>("SearingStrike", "Searing Strike", "SS", Rgb(1f, 0.45f, 0.2f),
                "A burning sweep in front of you. Hitting anything stokes 2 Heat.",
                Ranks(6f, 5.5f, 5f, 4.5f, 4f), Ranks(35f, 37f, 39f, 41f, 43f)), Ranks(60f, 85f, 110f, 135f, 160f), false, 2.5f, 100f);
            s.heatStacks = 2;
            return Done(s);
        }

        static AbilityDefinition IceLance(Mats m)
        {
            var s = Shot(Signature<ProjectileSpell>("IceLance", "Ice Lance", "IL", Rgb(0.55f, 0.8f, 1f),
                "A lance of ice that hits the first enemy and slows it heavily.",
                Ranks(5f, 4.5f, 4f, 3.75f, 3.5f), Ranks(35f, 37f, 39f, 41f, 43f)), 60f, 25f, 0f, false, 22f, 14f, 0.7f);
            s.slow = 0.35f;
            s.slowSeconds = 2f;
            s.projectileMaterial = m.Arrow;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        static AbilityDefinition MendingSeed(Mats m)
        {
            var s = Signature<HealSpell>("MendingSeed", "Mending Seed", "MS", Rgb(0.45f, 0.8f, 0.35f),
                "Heals an allied hero (or yourself).", Ranks(9f, 8.5f, 8f, 7.5f, 7f), Ranks(50f, 54f, 58f, 62f, 66f));
            s.targeting = AbilityTargeting.Unit;
            s.unitFilter = UnitTargetFilter.Ally;
            s.range = 7f;
            s.area = false;
            s.healByRank = Ranks(70f, 100f, 130f, 160f, 190f);
            return Done(s);
        }

        static AbilityDefinition ArcBolt(Mats m)
        {
            var s = Shot(Signature<ProjectileSpell>("ArcBolt", "Arc Bolt", "AB", Rgb(0.65f, 0.45f, 1f),
                "A bolt that bursts on the first enemy and arcs to one more nearby.",
                Ranks(4f, 3.75f, 3.5f, 3f, 2.5f), Ranks(30f, 32f, 34f, 36f, 38f)), 55f, 22f, 1.2f, false, 22f, 14f, 0.55f);
            s.chainJumps = 1;
            s.chainFraction = 0.6f;
            s.boltMaterial = m.Lightning;
            s.projectileMaterial = m.Fire;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        // ---------- Second unique ultimates ----------

        static AbilityDefinition HawkEye(Mats m)
        {
            var s = Ultimate<BuffSpell>("HawkEye", "Hawk Eye", "HE", Rgb(0.35f, 0.78f, 0.45f),
                "For a few seconds you see further and shoot faster: more attack range and attack speed.",
                DamageType.None, false, Ranks(70f, 60f, 50f), 80f);
            s.targeting = AbilityTargeting.Instant;
            s.durationByRank = Ranks(6f, 7f, 8f);
            s.attackSpeed = 0.4f;
            s.attackRange = 2f;
            s.moveSpeed = 0f;
            s.damageReductionByRank = Ranks(0f);
            s.heatPerAttack = 0;
            return Done(s);
        }

        static AbilityDefinition TimeStop(Mats m)
        {
            var s = Ultimate<ZoneSpell>("TimeStop", "Time Stop", "TS", Rgb(0.65f, 0.45f, 0.95f),
                "Mark an area: after a moment, time stops for every enemy inside.",
                DamageType.None, false, Ranks(90f, 80f, 70f), 100f);
            s.targeting = AbilityTargeting.Point;
            s.range = 9f;
            s.radius = 3f;
            s.areaRadius = 3f;
            s.delay = 0.5f;
            s.durationByRank = Ranks(0.5f);
            s.freezeByRank = Ranks(1.5f, 1.75f, 2f);
            s.enemySlow = 0f;
            s.enemyDamagePerSecond = 0f;
            s.allyHealByRank = Ranks(0f);
            s.allyMoveSpeed = 0f;
            s.warningMaterial = m.FrostWarning;
            s.zoneMaterial = m.StormAura;
            return Done(s);
        }

        static AbilityDefinition Bombardment(Mats m)
        {
            var s = BurnBlast(Blast(Ultimate<BurstSpell>("Bombardment", "Bombardment", "BO", Rgb(0.85f, 0.55f, 0.25f),
                "Three charges blow up one after another in a line toward the point, setting everything alight.",
                DamageType.Fire, false, Ranks(80f, 70f, 60f), 100f), 10f, 2.2f, 0.6f, Ranks(110f, 160f, 210f)), 0.3f);
            s.count = 3;
            s.spacing = 3f;
            return Done(s);
        }

        static AbilityDefinition Unbreakable(Mats m)
        {
            var s = Ultimate<BuffSpell>("Unbreakable", "Unbreakable", "UB", Rgb(0.55f, 0.68f, 0.85f),
                "For a few seconds you take far less damage and move a little faster.",
                DamageType.None, false, Ranks(80f, 70f, 60f), 80f);
            s.targeting = AbilityTargeting.Instant;
            s.durationByRank = Ranks(5f);
            s.attackSpeed = 0f;
            s.attackRange = 0f;
            s.moveSpeed = 0.1f;
            s.damageReductionByRank = Ranks(0.4f, 0.5f, 0.6f);
            s.heatPerAttack = 0;
            return Done(s);
        }

        static AbilityDefinition ShadowExecute(Mats m)
        {
            var s = Strike(Ultimate<MeleeSpell>("ShadowExecute", "Shadow Execute", "SE", Rgb(0.85f, 0.25f, 0.4f),
                "Dash onto your prey and cut into it: the more health it has lost, the harder it hits.",
                DamageType.Physical, false, Ranks(60f, 50f, 40f), 80f), Ranks(120f, 170f, 220f), true, 7f, 2f);
            s.executeFraction = 0.15f;
            return Done(s);
        }

        static AbilityDefinition GaleForce(Mats m)
        {
            var s = Shot(Ultimate<ProjectileSpell>("GaleForce", "Gale Force", "GF", Rgb(0.4f, 0.85f, 0.8f),
                "A huge wave of wind that cuts through everything in its path and slows it.",
                DamageType.Air, true, Ranks(70f, 60f, 50f), 100f), 150f, 75f, 0f, true, 20f, 18f, 2.4f);
            s.slow = 0.4f;
            s.slowSeconds = 2f;
            s.projectileMaterial = m.Arrow;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        static AbilityDefinition PhoenixForm(Mats m)
        {
            var s = Ultimate<BuffSpell>("PhoenixForm", "Phoenix Form", "PF", Rgb(1f, 0.45f, 0.2f),
                "For a few seconds you burn brighter: more attack speed and movement speed, and every attack builds 2 Heat.",
                DamageType.None, false, Ranks(70f, 60f, 50f), 80f);
            s.targeting = AbilityTargeting.Instant;
            s.durationByRank = Ranks(6f, 7f, 8f);
            s.attackSpeed = 0.3f;
            s.attackRange = 0f;
            s.moveSpeed = 0.1f;
            s.damageReductionByRank = Ranks(0f);
            s.heatPerAttack = 1;
            return Done(s);
        }

        static AbilityDefinition Avalanche(Mats m)
        {
            var s = Shot(Ultimate<ProjectileSpell>("Avalanche", "Avalanche", "AV", Rgb(0.55f, 0.8f, 1f),
                "A wall of ice that rolls through everything in its path, stunning it and leaving it slowed.",
                DamageType.Frost, true, Ranks(80f, 70f, 60f), 100f), 140f, 70f, 0f, true, 18f, 16f, 2.2f);
            s.stunDuration = 1f;
            s.slow = 0.3f;
            s.slowSeconds = 2f;
            s.projectileMaterial = m.Arrow;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        static AbilityDefinition WildGrowth(Mats m)
        {
            var s = Ultimate<HealSpell>("WildGrowth", "Wild Growth", "WG", Rgb(0.45f, 0.8f, 0.35f),
                "Heals every allied hero around you at once.", DamageType.None, false, Ranks(90f, 80f, 70f), 120f);
            s.targeting = AbilityTargeting.Instant;
            s.unitFilter = UnitTargetFilter.Ally;
            s.range = 0f;
            s.area = true;
            s.radius = 12f;
            s.healByRank = Ranks(150f, 225f, 300f);
            return Done(s);
        }

        static AbilityDefinition ChainStorm(Mats m)
        {
            var s = Shot(Ultimate<ProjectileSpell>("ChainStorm", "Chain Storm", "CS", Rgb(0.65f, 0.45f, 1f),
                "A bolt that bursts on the first enemy, then arcs from enemy to enemy up to five times.",
                DamageType.Lightning, true, Ranks(70f, 60f, 50f), 100f), 120f, 60f, 1.5f, false, 22f, 16f, 0.9f);
            s.chainJumps = 5;
            s.chainFraction = 0.7f;
            s.boltMaterial = m.Lightning;
            s.projectileMaterial = m.Fire;
            s.explosionMaterial = m.Explosion;
            return Done(s);
        }

        // ---------- Shared ultimates (by base damage type and attack range) ----------

        static readonly Color SharedColor = new(0.85f, 0.75f, 0.45f);

        static AbilityDefinition ArrowStorm(Mats m)
        {
            var s = Blast(Ultimate<BurstSpell>("Shared_ArrowStorm", "Arrow Storm", "AS", SharedColor,
                "Physical, ranged. A storm of arrows falls on a wide area: heavy damage, and everything in it is slowed.",
                DamageType.Physical, true, Ranks(80f, 70f, 60f), 100f), 11f, 4f, 0.8f, Ranks(180f, 260f, 340f));
            s.slow = 0.25f;
            s.slowSeconds = 1.5f;
            return Done(s);
        }

        static AbilityDefinition Whirlwind(Mats m)
        {
            var s = Ultimate<AuraSpell>("Shared_Whirlwind", "Whirlwind", "WW", SharedColor,
                "Physical, melee. Spin for a few seconds, hitting everything around you twice a second while moving faster.",
                DamageType.Physical, true, Ranks(70f, 60f, 50f), 80f);
            s.tags = SpellTags.Melee | SpellTags.AreaOfEffect;
            s.targeting = AbilityTargeting.Instant;
            s.range = 0f;
            s.durationByRank = Ranks(3f);
            s.radius = 3f;
            s.interval = 0.5f;
            s.damageByRank = Ranks(40f, 60f, 80f);
            s.moveSpeed = 0.15f;
            s.burnFraction = 0f;
            return Done(s);
        }

        static AbilityDefinition Thunderstrike(Mats m)
        {
            var s = Blast(Ultimate<BurstSpell>("Shared_Thunderstrike", "Thunderstrike", "TS", SharedColor,
                "Lightning, ranged. Lightning hammers an area after a moment, stunning everything in it.",
                DamageType.Lightning, true, Ranks(80f, 70f, 60f), 100f), 10f, 3f, 0.8f, Ranks(170f, 250f, 330f));
            s.stunSeconds = 1f;
            return Done(s);
        }

        static AbilityDefinition Meteor(Mats m) => Done(BurnBlast(Blast(Ultimate<BurstSpell>("Shared_Meteor", "Meteor", "MT",
            SharedColor, "Fire, ranged. A meteor crashes down on a wide area after a second and leaves it burning.",
            DamageType.Fire, true, Ranks(80f, 70f, 60f), 100f), 11f, 3.5f, 1f, Ranks(200f, 290f, 380f)), 0.4f));

        static AbilityDefinition Inferno(Mats m)
        {
            var s = Ultimate<AuraSpell>("Shared_Inferno", "Inferno", "IN", SharedColor,
                "Fire, melee. Become a blaze for a few seconds, burning everything around you twice a second.",
                DamageType.Fire, true, Ranks(70f, 60f, 50f), 80f);
            s.tags = SpellTags.Melee | SpellTags.AreaOfEffect;
            s.targeting = AbilityTargeting.Instant;
            s.range = 0f;
            s.durationByRank = Ranks(4f);
            s.radius = 3.5f;
            s.interval = 0.5f;
            s.damageByRank = Ranks(35f, 50f, 65f);
            s.moveSpeed = 0f;
            s.burnFraction = 0.5f;
            return Done(s);
        }

        static AbilityDefinition Cyclone(Mats m)
        {
            var s = Blast(Ultimate<BurstSpell>("Shared_Cyclone", "Cyclone", "CY", SharedColor,
                "Air, ranged. A cyclone tears through a wide area after a moment and leaves everything in it badly slowed.",
                DamageType.Air, true, Ranks(80f, 70f, 60f), 100f), 10f, 4f, 0.6f, Ranks(150f, 220f, 290f));
            s.slow = 0.5f;
            s.slowSeconds = 2.5f;
            return Done(s);
        }

        static AbilityDefinition Blizzard(Mats m)
        {
            var s = Ultimate<ZoneSpell>("Shared_Blizzard", "Blizzard", "BZ", SharedColor,
                "Frost, ranged. A blizzard rages over a wide area for a few seconds, hurting and slowing everything in it.",
                DamageType.Frost, false, Ranks(90f, 80f, 70f), 100f);
            s.targeting = AbilityTargeting.Point;
            s.range = 10f;
            s.radius = 4.5f;
            s.areaRadius = 4.5f;
            s.delay = 0f;
            s.durationByRank = Ranks(4f, 4.5f, 5f);
            s.freezeByRank = Ranks(0f);
            s.enemySlow = 0.4f;
            s.enemyDamagePerSecond = 60f;
            s.allyHealByRank = Ranks(0f);
            s.allyMoveSpeed = 0f;
            s.warningMaterial = null;
            s.zoneMaterial = m.FrostField;
            return Done(s);
        }

        static AbilityDefinition Overgrowth(Mats m)
        {
            var s = Blast(Ultimate<BurstSpell>("Shared_Overgrowth", "Overgrowth", "OG", SharedColor,
                "Nature, ranged. Roots burst out of the ground in a wide area, holding everything in it in place.",
                DamageType.Nature, true, Ranks(80f, 70f, 60f), 100f), 10f, 3.5f, 0.7f, Ranks(130f, 190f, 250f));
            s.stunSeconds = 1.25f;
            return Done(s);
        }
    }
}
