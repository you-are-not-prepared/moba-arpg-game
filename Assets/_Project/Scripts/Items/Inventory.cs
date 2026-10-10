using System;
using UnityEngine;

namespace Moba.Items
{
    // Eight item slots (2x4 on the HUD). Item stats are summed into the unit's other components.
    [RequireComponent(typeof(Unit))]
    public class Inventory : MonoBehaviour
    {
        public const int SlotCount = 8;

        [SerializeField] ItemDefinition[] slots = new ItemDefinition[SlotCount];

        Attacker _attacker;

        public float BonusAttackDamage { get; private set; }
        public event Action Changed;

        void Awake()
        {
            _attacker = GetComponent<Attacker>();
            EnsureSlots();
            Recalculate();
        }

        void OnValidate() => EnsureSlots();

        public ItemDefinition Get(int slot) => slots[slot];

        public bool IsFull => FirstEmptySlot() < 0;

        public int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] == null) return i;
            return -1;
        }

        public bool TryAdd(ItemDefinition item, out int slot)
        {
            slot = FirstEmptySlot();
            if (item == null || slot < 0) return false;
            slots[slot] = item;
            Recalculate();
            return true;
        }

        public ItemDefinition RemoveAt(int slot)
        {
            ItemDefinition item = slots[slot];
            if (item == null) return null;
            slots[slot] = null;
            Recalculate();
            return item;
        }

        // Moves an item to another slot, swapping with whatever is there.
        public void Move(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= SlotCount || to >= SlotCount) return;
            (slots[from], slots[to]) = (slots[to], slots[from]);
            Changed?.Invoke();
        }

        void Recalculate()
        {
            float attackDamage = 0f;
            foreach (ItemDefinition item in slots)
                if (item != null) attackDamage += item.attackDamage;

            BonusAttackDamage = attackDamage;
            if (_attacker != null) _attacker.bonusDamage = attackDamage;
            Changed?.Invoke();
        }

        void EnsureSlots()
        {
            if (slots == null || slots.Length != SlotCount) Array.Resize(ref slots, SlotCount);
        }
    }
}
