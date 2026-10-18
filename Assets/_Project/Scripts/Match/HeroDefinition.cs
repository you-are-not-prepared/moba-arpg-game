using Moba.Abilities;
using Moba.Items;
using UnityEngine;

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
            if (ultimate != null)
            {
                caster.SetAbility(AbilityCaster.UltimateSlot, ultimate);
                caster.ultimateLocked = true;
            }
        }

        // "600 HP · 30 damage · 5.5 range" from the prefab, for champion select.
        public string StatsLine
        {
            get
            {
                if (prefab == null) return "";
                var unit = prefab.GetComponent<Unit>();
                var attacker = prefab.GetComponent<Attacker>();
                return $"{unit.maxHealth:0} HP · {attacker.damage:0} damage · {attacker.range:0.#} range";
            }
        }
    }
}
