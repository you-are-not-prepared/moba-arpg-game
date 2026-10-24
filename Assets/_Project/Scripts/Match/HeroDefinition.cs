using Moba.Abilities;
using Moba.Items;
using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // A playable hero: its prefab (stats, model), a passive, and an ultimate that is locked into the R slot.
    // Passives are plain numbers applied on spawn, so new simple passives are just new fields here.
    [CreateAssetMenu(menuName = "MOBA/Hero", fileName = "Hero")]
    public class HeroDefinition : ScriptableObject
    {
        public string id;
        public string displayName = "Hero";
        public string title;
        [TextArea] public string description;
        public Color color = Color.white;
        [Tooltip("1-2 characters shown on the portrait.")]
        public string glyph = "H";
        public HeroController prefab;

        [Header("Passive")]
        public string passiveName;
        [TextArea] public string passiveDescription;
        [Tooltip("Extra projectiles on every projectile spell.")]
        public int extraProjectiles;
        [Tooltip("Multiplies every cooldown (0.8 = 20% cooldown reduction).")]
        [Range(0.1f, 1f)] public float cooldownMultiplier = 1f;
        [Tooltip("Multiplies stats from items (1.1 = 10% more).")]
        public float itemStatMultiplier = 1f;
        [Tooltip("Extra max health gained on every level-up.")]
        public float healthPerLevelBonus;
        [Tooltip("Multiplies movement speed (0.85 = 15% slower).")]
        public float moveSpeedMultiplier = 1f;
        [Tooltip("Bonus damage (0.15 = 15%) against targets with no allied hero within isolationRadius.")]
        public float isolationDamageBonus;
        public float isolationRadius = 7f;
        [Tooltip("Movement speed (0.25 = 25%) for tailwindDuration seconds after every spell cast. 0 = none.")]
        public float tailwindSpeed;
        public float tailwindDuration = 2f;
        [Tooltip("Heat stacks from basic attacks (attack speed each); at full Heat the next attack bursts. 0 = none.")]
        public int heatMaxStacks;
        public float heatAttackSpeed = 0.04f;
        public float heatBurstDamage = 50f;
        [Tooltip("Spell hits slow by this much (0.2 = 20%) for spellSlowSeconds. 0 = none.")]
        public float spellSlow;
        public float spellSlowSeconds = 1.5f;
        [Tooltip("Slowed enemies take this much more damage from the hero (0.1 = 10%).")]
        public float slowedDamageBonus;
        [Tooltip("Spell damage dealt heals the nearest allied hero by this fraction (0.15 = 15%). 0 = none.")]
        public float allyHealFraction;
        [Tooltip("Every chargeEvery-th spell is charged: its first hit jumps to chargeJumps more enemies for half damage. 0 = none.")]
        public int chargeEvery;
        public int chargeJumps = 1;
        [Tooltip("Effects the passive spawns (e.g. the Heat burst's explosion).")]
        public Material passiveEffectMaterial;

        [Header("Ascension")]
        [Tooltip("Talent choices at levels 6, 11 and 18: path A or B, then two options within that path.")]
        public AscensionTree ascension = new();

        [Header("Ultimate")]
        [Tooltip("Locked into the R slot; bought spells go in Q/W/E.")]
        public AbilityDefinition ultimate;

        // Applies the passive and the ultimate to a freshly spawned hero.
        public void ApplyTo(HeroController hero)
        {
            hero.definition = this;
            AbilityCaster caster = hero.Abilities;
            caster.passiveExtraProjectiles = extraProjectiles;
            caster.cooldownMultiplier = cooldownMultiplier;
            if (hero.TryGetComponent(out Inventory inventory)) inventory.SetStatMultiplier(itemStatMultiplier);
            if (hero.TryGetComponent(out HeroProgression progression)) progression.healthPerLevel += healthPerLevelBonus;
            if (hero.TryGetComponent(out NavMeshAgent agent)) agent.speed *= moveSpeedMultiplier;
            if (isolationDamageBonus > 0f)
            {
                var isolation = hero.gameObject.AddComponent<IsolationPassive>();
                isolation.damageBonus = isolationDamageBonus;
                isolation.radius = isolationRadius;
            }
            if (tailwindSpeed > 0f)
            {
                var tailwind = hero.gameObject.AddComponent<TailwindPassive>();
                tailwind.moveSpeedBonus = tailwindSpeed;
                tailwind.duration = tailwindDuration;
            }
            if (heatMaxStacks > 0)
            {
                var heat = hero.gameObject.AddComponent<KindledBlade>();
                heat.maxStacks = heatMaxStacks;
                heat.attackSpeedPerStack = heatAttackSpeed;
                heat.burstDamage = heatBurstDamage;
                heat.burstMaterial = passiveEffectMaterial;
            }
            if (chargeEvery > 0)
            {
                var charge = hero.gameObject.AddComponent<StaticCharge>();
                charge.every = chargeEvery;
                charge.jumps = chargeJumps;
                charge.boltMaterial = passiveEffectMaterial;
            }
            if (spellSlow > 0f || allyHealFraction > 0f)
            {
                var onHit = hero.gameObject.AddComponent<SpellHitPassive>();
                onHit.slow = spellSlow;
                onHit.slowSeconds = spellSlowSeconds;
                onHit.slowedDamageBonus = slowedDamageBonus;
                onHit.healFraction = allyHealFraction;
            }
            if (ascension != null && ascension.IsComplete) hero.gameObject.AddComponent<HeroAscension>().Init(ascension);
            if (ultimate != null)
            {
                caster.SetAbility(AbilityCaster.UltimateSlot, ultimate);
                caster.ultimateLocked = true;
            }
        }

        // "600 HP · 30 damage · 5.5 range · 6.5 speed" from the prefab (passives included), for champion select.
        public string StatsLine
        {
            get
            {
                if (prefab == null) return "";
                var unit = prefab.GetComponent<Unit>();
                var attacker = prefab.GetComponent<Attacker>();
                float speed = prefab.GetComponent<NavMeshAgent>().speed * moveSpeedMultiplier;
                string range = attacker.projectileSpeed > 0f ? $"{attacker.range:0.#} range" : "melee";
                return $"{unit.maxHealth:0} HP · {attacker.damage:0} damage · {range} · {speed:0.#} speed";
            }
        }
    }
}
