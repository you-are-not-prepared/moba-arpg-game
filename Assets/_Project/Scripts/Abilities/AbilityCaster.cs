using System;
using UnityEngine;

namespace Moba.Abilities
{
    public enum CastBlock { None, Dead, Empty, NotLearned, OnCooldown, NotEnoughMana }

    // Four spell slots (Q/W/E/R), mana and cooldowns for one unit. Each slot holds an equipped spell and
    // three mod sockets. Ranks belong to the slot (learned with skill points from HeroProgression), so
    // swapping the spell in a slot keeps its rank, cooldown and socketed mods.
    [RequireComponent(typeof(Unit))]
    public class AbilityCaster : MonoBehaviour
    {
        public const int SlotCount = 4;
        public const int ModSocketCount = 3;
        public const int UltimateSlot = 3;
        public static readonly string[] SlotKeys = { "Q", "W", "E", "R" };

        [Serializable]
        public class Slot
        {
            public AbilityDefinition ability;
            public ModDefinition[] mods = new ModDefinition[ModSocketCount];
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
        // A spell or mod was equipped, removed or moved.
        public event Action LoadoutChanged;

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

        // ---------- Ranks (per slot) ----------

        public static int MaxRank(int slot) => slot == UltimateSlot ? 3 : 5;

        // Basic slots: levels 1/3/5/7/9. Ultimate slot: 6/11/16.
        public static int RequiredLevel(int slot, int rank) => slot == UltimateSlot ? 6 + (rank - 1) * 5 : rank * 2 - 1;

        public bool CanLevelUp(int index)
        {
            Slot s = slots[index];
            return SkillPoints > 0 && s.rank < MaxRank(index) && HeroLevel >= RequiredLevel(index, s.rank + 1);
        }

        public bool TryLevelUp(int index)
        {
            if (!CanLevelUp(index)) return false;
            slots[index].rank++;
            SkillPoints--;
            RankLearned?.Invoke(index);
            return true;
        }

        // ---------- Loadout ----------

        // Equips a spell (null clears) and returns the previous one. Rank, cooldown and mods stay with the slot.
        public AbilityDefinition SetAbility(int index, AbilityDefinition ability)
        {
            AbilityDefinition previous = slots[index].ability;
            slots[index].ability = ability;
            LoadoutChanged?.Invoke();
            return previous;
        }

        public ModDefinition GetMod(int index, int socket) => slots[index].mods[socket];

        // Sockets a mod (null clears) and returns the previous one.
        public ModDefinition SetMod(int index, int socket, ModDefinition mod)
        {
            ModDefinition previous = slots[index].mods[socket];
            slots[index].mods[socket] = mod;
            LoadoutChanged?.Invoke();
            return previous;
        }

        public int FirstFreeSocket(int index)
        {
            ModDefinition[] mods = slots[index].mods;
            for (int i = 0; i < mods.Length; i++)
                if (mods[i] == null) return i;
            return -1;
        }

        // A socketed mod only applies if it supports the slot's current spell.
        public bool IsModActive(int index, int socket)
        {
            ModDefinition mod = slots[index].mods[socket];
            return mod != null && mod.Supports(slots[index].ability);
        }

        public SpellModifiers BuildModifiers(int index)
        {
            SpellModifiers m = SpellModifiers.None;
            for (int i = 0; i < ModSocketCount; i++)
                if (IsModActive(index, i)) slots[index].mods[i].Apply(ref m);
            return m;
        }

        // ---------- Casting ----------

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

            a.Execute(new AbilityCastContext(_unit, this, index, s.rank, point, target, BuildModifiers(index)));
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
            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] ??= new Slot();
                if (slots[i].mods == null || slots[i].mods.Length != ModSocketCount)
                    Array.Resize(ref slots[i].mods, ModSocketCount);
            }
        }
    }
}
