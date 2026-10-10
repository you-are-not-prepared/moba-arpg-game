using UnityEngine;

namespace Moba.Items
{
    // A purchasable item. Only flat attack damage for now; add stat fields here as they're needed
    // and apply them in Inventory.Recalculate.
    [CreateAssetMenu(menuName = "MOBA/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        [TextArea] public string description;
        public int cost = 300;

        [Header("Stats")]
        public float attackDamage;

        [Header("Display")]
        [Tooltip("Optional. Without an icon the slot shows the colour and glyph below.")]
        public Sprite icon;
        public Color color = new(0.45f, 0.5f, 0.6f);
        [Tooltip("1-3 characters drawn on the slot when there's no icon.")]
        public string glyph = "?";

        public string StatsText => attackDamage != 0f ? $"+{attackDamage:0} attack damage" : "";
    }
}
