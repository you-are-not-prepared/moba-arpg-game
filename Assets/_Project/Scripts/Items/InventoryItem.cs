using UnityEngine;

namespace Moba.Items
{
    // Anything that can be bought and held in an inventory slot: equipment items, spells and mods.
    public abstract class InventoryItem : ScriptableObject
    {
        public string displayName = "Item";
        [TextArea] public string description;
        public int cost = 300;

        [Header("Display")]
        [Tooltip("Optional. Without an icon the slot shows the colour and glyph below.")]
        public Sprite icon;
        public Color color = new(0.45f, 0.5f, 0.6f);
        [Tooltip("1-3 characters drawn on the slot when there's no icon.")]
        public string glyph = "?";

        // "Item", "Spell", "Mod": shown in tooltips and used to group the shop.
        public abstract string KindLabel { get; }

        // One-line summary of what the item gives (e.g. "+10 attack damage").
        public virtual string StatsText => "";
    }
}
