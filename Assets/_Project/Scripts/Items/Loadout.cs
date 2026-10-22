using Moba.Abilities;

namespace Moba.Items
{
    // The drag-and-drop moves by name, as a game-server client sends them (see Loadout.Perform).
    public enum LoadoutAction : byte { MoveItem, Equip, UnequipSpell, UnequipMod, MoveSpell, MoveMod }

    // Moving spells and mods between the inventory and spell slots (the HUD's drag and drop).
    // Every operation returns null on success, or a short message explaining why it didn't happen.
    public static class Loadout
    {
        // One move by name: (from, to) for items and spells, (spell slot, socket, to) for mods. The indexes are
        // checked first, since on the game server they come from the network.
        public static string Perform(Inventory inventory, AbilityCaster caster, LoadoutAction action, int a, int b, int c)
        {
            static bool Item(int i) => i >= 0 && i < Inventory.SlotCount;
            static bool Spell(int i) => i >= 0 && i < AbilityCaster.SlotCount;
            static bool Socket(int i) => i >= 0 && i < AbilityCaster.MaxModSockets;

            switch (action)
            {
                case LoadoutAction.MoveItem when Item(a) && Item(b):
                    inventory.Move(a, b);
                    return null;
                case LoadoutAction.Equip when Item(a) && Spell(b):
                    return EquipFromInventory(inventory, caster, a, b);
                case LoadoutAction.UnequipSpell when Spell(a) && Item(b):
                    return UnequipSpell(inventory, caster, a, b);
                case LoadoutAction.UnequipMod when Spell(a) && Socket(b) && Item(c):
                    return UnequipMod(inventory, caster, a, b, c);
                case LoadoutAction.MoveSpell when Spell(a) && Spell(b):
                    return MoveSpell(caster, a, b);
                case LoadoutAction.MoveMod when Spell(a) && Socket(b) && Spell(c):
                    return MoveMod(caster, a, b, c);
                default:
                    return "That move isn't possible.";
            }
        }

        // Inventory slot -> spell slot. Spells swap with the slot's current spell; mods go into the first free socket.
        public static string EquipFromInventory(Inventory inventory, AbilityCaster caster, int inventorySlot, int spellSlot)
        {
            InventoryItem item = inventory.Get(inventorySlot);
            switch (item)
            {
                case null:
                    return null;
                case AbilityDefinition spell:
                    if (caster.IsSlotLocked(spellSlot)) return "That slot holds your hero's ultimate.";
                    inventory.Set(inventorySlot, caster.SetAbility(spellSlot, spell));
                    return null;
                case ModDefinition mod:
                    string error = CanSocket(caster, spellSlot, mod);
                    if (error != null) return error;
                    caster.SetMod(spellSlot, caster.FirstFreeSocket(spellSlot), mod);
                    inventory.Set(inventorySlot, null);
                    return null;
                default:
                    return "Only spells and mods can go in spell slots.";
            }
        }

        // Spell slot -> inventory slot (swaps with a spell there; otherwise falls back to the first empty slot).
        public static string UnequipSpell(Inventory inventory, AbilityCaster caster, int spellSlot, int inventorySlot)
        {
            AbilityDefinition spell = caster.GetSlot(spellSlot).ability;
            if (spell == null) return null;
            if (caster.IsSlotLocked(spellSlot)) return "Your ultimate can't be unequipped.";

            InventoryItem occupant = inventorySlot >= 0 ? inventory.Get(inventorySlot) : null;
            if (inventorySlot >= 0 && (occupant == null || occupant is AbilityDefinition))
            {
                caster.SetAbility(spellSlot, occupant as AbilityDefinition);
                inventory.Set(inventorySlot, spell);
                return null;
            }

            int empty = inventory.FirstEmptySlot();
            if (empty < 0) return "Inventory is full.";
            caster.SetAbility(spellSlot, null);
            inventory.Set(empty, spell);
            return null;
        }

        // Spell slot -> spell slot: swaps the two spells. Ranks, cooldowns and mods stay with their slots.
        public static string MoveSpell(AbilityCaster caster, int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot) return null;
            if (caster.IsSlotLocked(fromSlot) || caster.IsSlotLocked(toSlot)) return "Your ultimate stays in its slot.";
            AbilityDefinition moving = caster.GetSlot(fromSlot).ability;
            caster.SetAbility(fromSlot, caster.SetAbility(toSlot, moving));
            return null;
        }

        // Mod socket -> inventory slot (or the first empty slot if that one is taken).
        public static string UnequipMod(Inventory inventory, AbilityCaster caster, int spellSlot, int socket, int inventorySlot)
        {
            ModDefinition mod = caster.GetMod(spellSlot, socket);
            if (mod == null) return null;
            int target = inventorySlot >= 0 && inventory.Get(inventorySlot) == null ? inventorySlot : inventory.FirstEmptySlot();
            if (target < 0) return "Inventory is full.";
            caster.SetMod(spellSlot, socket, null);
            inventory.Set(target, mod);
            return null;
        }

        // Mod socket -> another spell slot's first free socket.
        public static string MoveMod(AbilityCaster caster, int fromSlot, int socket, int toSlot)
        {
            if (fromSlot == toSlot) return null;
            ModDefinition mod = caster.GetMod(fromSlot, socket);
            if (mod == null) return null;
            string error = CanSocket(caster, toSlot, mod);
            if (error != null) return error;
            caster.SetMod(fromSlot, socket, null);
            caster.SetMod(toSlot, caster.FirstFreeSocket(toSlot), mod);
            return null;
        }

        static string CanSocket(AbilityCaster caster, int spellSlot, ModDefinition mod)
        {
            AbilityDefinition spell = caster.GetSlot(spellSlot).ability;
            if (spell == null) return "Equip a spell in that slot first.";
            if (!mod.Supports(spell)) return $"{mod.displayName} doesn't work with {spell.displayName} (needs a {mod.requiredTags} spell).";
            if (caster.FirstFreeSocket(spellSlot) < 0)
                return $"All {caster.SocketCount(spellSlot)} mod sockets are full. Drag a mod out first.";
            return null;
        }
    }
}
