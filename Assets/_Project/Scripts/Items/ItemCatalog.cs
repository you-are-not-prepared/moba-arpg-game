using System.Collections.Generic;
using UnityEngine;

namespace Moba.Items
{
    // The list of items a shop sells, in display order.
    [CreateAssetMenu(menuName = "MOBA/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        public List<ItemDefinition> items = new();
    }
}
