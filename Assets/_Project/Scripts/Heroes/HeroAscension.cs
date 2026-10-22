using System;
using System.Collections.Generic;
using Moba.Abilities;
using Moba.Items;
using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Ascension for one hero: tracks which choices are due (one per threshold level reached), offers the
    // talents of the next tier (one per path at level 6, then two), and applies the picked talent's stats.
    // Added by HeroDefinition.ApplyTo.
    [RequireComponent(typeof(HeroController))]
    public class HeroAscension : MonoBehaviour
    {
        public static readonly int[] Levels = { 6, 11, 18 };

        AscensionTree _tree;
        HeroProgression _progression;
        readonly List<Talent> _picked = new();
        readonly List<int> _pickedOptions = new();
        int _path = -1;

        // Raised after a talent is picked (and when a new choice becomes due).
        public event Action Changed;

        public IReadOnlyList<Talent> Picked => _picked;
        // The option index chosen at each tier (what a game server sends its clients).
        public IReadOnlyList<int> PickedOptions => _pickedOptions;
        // 0 = path A, 1 = path B, 2 = path C..., -1 before the first pick.
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

        // The talents offered at `tier`: every path's first talent at tier 0, then the chosen path's two options.
        public IReadOnlyList<Talent> Options(int tier)
        {
            if (tier == 0)
            {
                var roots = new List<Talent>(_tree.PathCount);
                foreach (AscensionPath path in _tree.paths) roots.Add(path.root);
                return roots;
            }
            AscensionPath chosen = _tree.Path(_path);
            return tier == 1 ? chosen.second : chosen.third;
        }

        // Option index into Options(PendingTier). At tier 0 the option is also the path index.
        public void Choose(int option)
        {
            int tier = PendingTier;
            if (tier >= 0) Pick(tier, option);
        }

        // Game-server clients: the next choice the server made (the level it needed was reached there).
        public void ChooseFromServer(int option)
        {
            if (_tree != null && _picked.Count < Levels.Length) Pick(_picked.Count, option);
        }

        void Pick(int tier, int option)
        {
            IReadOnlyList<Talent> options = Options(tier);
            if (option < 0 || option >= options.Count) return;
            Talent talent = options[option];
            if (tier == 0) _path = option;
            _picked.Add(talent);
            _pickedOptions.Add(option);
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
