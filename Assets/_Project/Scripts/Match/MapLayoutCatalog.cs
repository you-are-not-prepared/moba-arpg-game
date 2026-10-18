using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    // Every layout champion select can roll.
    [CreateAssetMenu(menuName = "MOBA/Map Layout Catalog", fileName = "MapLayoutCatalog")]
    public class MapLayoutCatalog : ScriptableObject
    {
        public List<MapLayoutInfo> layouts = new();

        public MapLayoutInfo Find(string id)
        {
            foreach (MapLayoutInfo layout in layouts)
                if (layout.id == id) return layout;
            return null;
        }
    }
}
