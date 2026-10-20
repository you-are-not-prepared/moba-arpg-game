using System;
using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // Gold, XP, level and score for a hero. Bounties are paid when this hero lands the killing blow.
    [RequireComponent(typeof(Unit))]
    public class HeroProgression : MonoBehaviour
    {
        public int maxLevel = 18;
        public int startingGold = 500;
        [Tooltip("Gold earned every second regardless of last hits (also while dead).")]
        public float passiveGoldPerSecond = 1f;
        [Tooltip("XP to go from level N to N+1 = base + perLevel * (N - 1).")]
        public float xpToLevelBase = 280f;
        public float xpToLevelPerLevel = 100f;

        [Header("Growth per level")]
        public float healthPerLevel = 80f;
        public float manaPerLevel = 30f;
        public float damagePerLevel = 3f;

        Unit _unit;
        Attacker _attacker;
        AbilityCaster _abilities;
        float _passiveGoldRemainder;

        public int Level { get; private set; } = 1;
        // XP earned toward the next level (resets on level-up).
        public float Xp { get; private set; }
        public int Gold { get; private set; }
        public int CreepScore { get; private set; }
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        // Enemy heroes this hero damaged in the AssistWindow before someone else killed them.
        public int Assists { get; private set; }
        public const float AssistWindow = 10f;
        public bool IsMaxLevel => Level >= maxLevel;
        public float XpToNextLevel => xpToLevelBase + xpToLevelPerLevel * (Level - 1);

        // (gold, xp, world position of the kill)
        public event Action<int, float, Vector3> BountyEarned;
        public event Action<int> LeveledUp;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
            _abilities = GetComponent<AbilityCaster>();
            Gold = startingGold;
            if (_abilities != null) _abilities.SkillPoints = 1;
        }

        void Update()
        {
            _passiveGoldRemainder += passiveGoldPerSecond * Time.deltaTime;
            if (_passiveGoldRemainder < 1f) return;
            int whole = Mathf.FloorToInt(_passiveGoldRemainder);
            _passiveGoldRemainder -= whole;
            Gold += whole;
        }

        void OnEnable() => Unit.AnyDied += OnAnyDied;
        void OnDisable() => Unit.AnyDied -= OnAnyDied;

        void OnAnyDied(Unit victim, Unit killer)
        {
            if (victim == _unit)
            {
                Deaths++;
                return;
            }
            if (killer != _unit && victim.kind == UnitKind.Hero && victim.IsEnemyOf(_unit)
                && victim.WasDamagedBy(_unit, AssistWindow))
            {
                Assists++;
                return;
            }
            if (killer == null || killer != _unit) return;

            if (victim.kind == UnitKind.Creep) CreepScore++;
            else if (victim.kind == UnitKind.Hero) Kills++;

            AddGold(victim.goldBounty);
            AddXp(victim.xpBounty);
            if (victim.goldBounty > 0 || victim.xpBounty > 0f)
                BountyEarned?.Invoke(victim.goldBounty, victim.xpBounty, victim.transform.position);
        }

        public void AddGold(int amount) => Gold += amount;

        public bool TrySpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        public void AddXp(float amount)
        {
            if (IsMaxLevel || amount <= 0f) return;
            Xp += amount;
            while (!IsMaxLevel && Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                LevelUp();
            }
            if (IsMaxLevel) Xp = 0f;
        }

        // Cheat menu: jump straight to the next level (progress toward it is dropped).
        public void GainLevel()
        {
            if (IsMaxLevel) return;
            Xp = 0f;
            LevelUp();
        }

        void LevelUp()
        {
            Level++;
            _unit.AddMaxHealth(healthPerLevel);
            if (_attacker != null) _attacker.damage += damagePerLevel;
            if (_abilities != null)
            {
                _abilities.AddMaxMana(manaPerLevel);
                _abilities.SkillPoints++;
            }
            LeveledUp?.Invoke(Level);
        }
    }
}
