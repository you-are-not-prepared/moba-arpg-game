using System;
using UnityEngine;

namespace Moba.Abilities
{
    public enum CastBlock { None, Dead, Empty, NotLearned, OnCooldown, NotEnoughMana }

    // Four ability slots (Q/W/E/R), mana, cooldowns and ranks for one unit.
    // Ranks are learned with skill points (granted by HeroProgression on level-up).
    [RequireComponent(typeof(Unit))]
    public class AbilityCaster : MonoBehaviour
    {
        public const int SlotCount = 4;
        public static readonly string[] SlotKeys = { "Q", "W", "E", "R" };

        [Serializable]
        public class Slot
        {
            public AbilityDefinition ability;
            [NonSerialized] public int rank;
            [NonSerialized] public float cooldownRemaining;
            [NonSerialized] public float cooldownTotal;
        }

        [SerializeField] Slot[] slots = new Slot[SlotCount];
        public float maxMana = 300f;
        public float manaRegenPerSecond = 2f;

        Unit _unit;
        HeroProgression _progression;

        public float Mana { get; private set; }
        public int SkillPoints { get; set; }
        public Unit Unit => _unit;
        int HeroLevel => _progression != null ? _progression.Level : 1;

        // (slot) after an ability was cast successfully.
        public event Action<int> Casted;
        // (slot) after a rank was learned.
        public event Action<int> RankLearned;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _progression = GetComponent<HeroProgression>();
            EnsureSlots();
            Mana = maxMana;
        }

        void OnValidate() => EnsureSlots();

        void Update()
        {
            foreach (Slot s in slots)
                if (s.cooldownRemaining > 0f) s.cooldownRemaining = Mathf.Max(0f, s.cooldownRemaining - Time.deltaTime);

            if (_unit.IsAlive) Mana = Mathf.Min(maxMana, Mana + manaRegenPerSecond * Time.deltaTime);
        }

        public Slot GetSlot(int index) => slots[index];

        public void SetAbility(int index, AbilityDefinition ability)
        {
            slots[index].ability = ability;
            slots[index].rank = 0;
            slots[index].cooldownRemaining = 0f;
        }

        public CastBlock GetCastBlock(int index)
        {
            Slot s = slots[index];
            if (!_unit.IsAlive) return CastBlock.Dead;
            if (s.ability == null) return CastBlock.Empty;
            if (s.rank <= 0) return CastBlock.NotLearned;
            if (s.cooldownRemaining > 0f) return CastBlock.OnCooldown;
            if (Mana < s.ability.ManaCost(s.rank)) return CastBlock.NotEnoughMana;
            return CastBlock.None;
        }

        public bool CanLevelUp(int index)
        {
            Slot s = slots[index];
            return SkillPoints > 0 && s.ability != null && s.rank < s.ability.maxRank
                   && HeroLevel >= s.ability.RequiredLevel(s.rank + 1);
        }

        public bool TryLevelUp(int index)
        {
            if (!CanLevelUp(index)) return false;
            slots[index].rank++;
            SkillPoints--;
            RankLearned?.Invoke(index);
            return true;
        }

        // Cast-range check for the order a slot would be cast with. Unit targets use edge-to-edge distance.
        public bool InCastRange(int index, Vector3 point, Unit target)
        {
            AbilityDefinition a = slots[index].ability;
            if (a == null || a.range <= 0f || a.targeting == AbilityTargeting.Instant) return true;
            if (a.targeting == AbilityTargeting.Unit) return target != null && _unit.EdgeDistanceTo(target) <= a.range;
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude <= a.range;
        }

        // Validates, pays mana, starts the cooldown and executes. Range must already be satisfied.
        public bool TryCast(int index, Vector3 point, Unit target)
        {
            if (GetCastBlock(index) != CastBlock.None) return false;
            Slot s = slots[index];
            AbilityDefinition a = s.ability;

            switch (a.targeting)
            {
                case AbilityTargeting.Instant:
                    point = transform.position;
                    target = null;
                    break;
                case AbilityTargeting.Unit:
                    if (!a.IsValidTarget(_unit, target)) return false;
                    point = target.transform.position;
                    break;
                case AbilityTargeting.Point:
                    target = null;
                    break;
            }
            if (!InCastRange(index, point, target)) return false;

            Mana -= a.ManaCost(s.rank);
            s.cooldownTotal = a.Cooldown(s.rank);
            s.cooldownRemaining = s.cooldownTotal;

            Vector3 facing = point - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);

            a.Execute(new AbilityCastContext(_unit, this, index, s.rank, point, target));
            Casted?.Invoke(index);
            return true;
        }

        public void RestoreMana() => Mana = maxMana;

        public void SpendMana(float amount) => Mana = Mathf.Max(0f, Mana - amount);

        public void AddMaxMana(float amount)
        {
            maxMana += amount;
            Mana += amount;
        }

        void EnsureSlots()
        {
            if (slots == null || slots.Length != SlotCount) Array.Resize(ref slots, SlotCount);
            for (int i = 0; i < SlotCount; i++) slots[i] ??= new Slot();
        }
    }
}
