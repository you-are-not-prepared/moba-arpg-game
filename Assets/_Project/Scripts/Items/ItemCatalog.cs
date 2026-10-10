using System.Collections.Generic;
using UnityEngine;

namespace Moba.Items
{
    // Everything a shop sells (items, spells, mods). The shop groups them by kind, keeping this order within a kind.
    [CreateAssetMenu(menuName = "MOBA/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        public List<InventoryItem> items = new();
    }
}
