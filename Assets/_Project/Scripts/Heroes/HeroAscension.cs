using System;
using System.Collections.Generic;
using Moba.Abilities;
using Moba.Items;
using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Ascension for one hero: tracks which choices are due (one per threshold level reached), offers the two
    // talents of the next tier, and applies the picked talent's stats. Added by HeroDefinition.ApplyTo.
    [RequireComponent(typeof(HeroController))]
    public class HeroAscension : MonoBehaviour
    {
        public static readonly int[] Levels = { 6, 11, 18 };

        AscensionTree _tree;
        HeroProgression _progression;
        readonly List<Talent> _picked = new();
        int _path = -1;

        // Raised after a talent is picked (and when a new choice becomes due).
        public event Action Changed;

        public IReadOnlyList<Talent> Picked => _picked;
        // 0 = path A, 1 = path B, -1 before the first pick.
        public int PathIndex => _path;
        public string PathName => _path >= 0 ? _tree.Path(_path).root.name : null;

        // Index of the choice waiting to be made (0, 1, 2), or -1.
        public int PendingTier
        {
            get
            {
                if (_tree == null || _progression == null || _picked.Count >= Levels.Length) return -1;
                return _progression.Level >= Levels[_picked.Count] ? _picked.Count : -1;
            }
        }

        public bool HasPendingChoice => PendingTier >= 0;

        // Bonuses that spells read when cast.
        public float UltimateDurationBonus { get; private set; }
        public float StunDurationBonus { get; private set; }

        public void Init(AscensionTree tree)
        {
            _tree = tree;
            _progression = GetComponent<HeroProgression>();
            if (_progression != null) _progression.LeveledUp += OnLeveledUp;
        }

        void OnDestroy()
        {
            if (_progression != null) _progression.LeveledUp -= OnLeveledUp;
        }

        void OnLeveledUp(int level)
        {
            if (HasPendingChoice) Changed?.Invoke();
        }

        // The two talents of the pending choice: option 0 and option 1.
        public (Talent first, Talent second) Options(int tier)
        {
            return tier switch
            {
                0 => (_tree.pathA.root, _tree.pathB.root),
                1 => (_tree.Path(_path).second[0], _tree.Path(_path).second[1]),
                _ => (_tree.Path(_path).third[0], _tree.Path(_path).third[1]),
            };
        }

        public void Choose(int option)
        {
            int tier = PendingTier;
            if (tier < 0 || option is < 0 or > 1) return;
            (Talent a, Talent b) = Options(tier);
            Talent talent = option == 0 ? a : b;
            if (tier == 0) _path = option;
            _picked.Add(talent);
            Apply(talent);
            Changed?.Invoke();
        }

        void Apply(Talent t)
        {
            if (t.maxHealth != 0f && TryGetComponent(out Unit unit)) unit.AddMaxHealth(t.maxHealth);
            if (TryGetComponent(out Attacker attacker))
            {
                attacker.damage += t.attackDamage;
                attacker.attackSpeedMultiplier *= 1f + t.attackSpeed;
            }
            if (t.moveSpeed != 0f && TryGetComponent(out NavMeshAgent agent)) agent.speed *= 1f + t.moveSpeed;
            if (TryGetComponent(out AbilityCaster caster))
            {
                if (t.maxMana != 0f) caster.AddMaxMana(t.maxMana);
                caster.cooldownMultiplier *= 1f - t.cooldownReduction;
                caster.ultimateCooldownMultiplier *= 1f - t.ultimateCooldownReduction;
                caster.spellDamageMultiplier *= 1f + t.spellDamage;
                caster.passiveExtraProjectiles += t.extraProjectiles;
            }
            if (t.itemStats != 0f && TryGetComponent(out Inventory inventory))
                inventory.SetStatMultiplier(inventory.StatMultiplier + t.itemStats);
            if (_progression != null) _progression.healthPerLevel += t.healthPerLevel;
            if (t.isolationDamage != 0f && TryGetComponent(out IsolationPassive isolation)) isolation.damageBonus += t.isolationDamage;
            UltimateDurationBonus += t.ultimateDuration;
            StunDurationBonus += t.stunDuration;
        }

        // Duration bonus for a timed ultimate cast by `unit` (0 without ascension).
        public static float DurationBonusOf(Unit unit) =>
            unit != null && unit.TryGetComponent(out HeroAscension a) ? a.UltimateDurationBonus : 0f;

        public static float StunBonusOf(Unit unit) =>
            unit != null && unit.TryGetComponent(out HeroAscension a) ? a.StunDurationBonus : 0f;
    }
}
