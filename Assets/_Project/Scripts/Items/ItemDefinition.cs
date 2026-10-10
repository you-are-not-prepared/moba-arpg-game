using UnityEngine;

namespace Moba.Items
{
    // Equipment: passive stats while held in the inventory. Only flat attack damage for now; add stat
    // fields here as they're needed and apply them in Inventory.Recalculate.
    [CreateAssetMenu(menuName = "MOBA/Item", fileName = "Item")]
    public class ItemDefinition : InventoryItem
    {
        [Header("Stats")]
        public float attackDamage;

        public override string KindLabel => "Item";
        public override string StatsText => attackDamage != 0f ? $"+{attackDamage:0} attack damage" : "";
    }
}
