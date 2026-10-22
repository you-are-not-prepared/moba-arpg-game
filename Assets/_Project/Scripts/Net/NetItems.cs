using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moba.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba.Net
{
    // Short ids for items, spells and mods, and for the materials their visuals use, so the game server and its
    // clients can name them in messages. The list is the shops' catalog in order, then every roster hero's
    // ultimate; both sides load the same map with the same assets, so they build the same list.
    public static class NetItems
    {
        public const short None = -1;

        static List<InventoryItem> s_Items;
        static Dictionary<InventoryItem, short> s_Ids;
        static List<Material> s_Materials;

        public static short Id(InventoryItem item)
        {
            Ensure();
            return item != null && s_Ids.TryGetValue(item, out short id) ? id : None;
        }

        public static InventoryItem Get(short id)
        {
            Ensure();
            return id >= 0 && id < s_Items.Count ? s_Items[id] : null;
        }

        public static short MaterialId(Material material)
        {
            Ensure();
            return material != null ? (short)s_Materials.IndexOf(material) : None;
        }

        public static Material Material(short id)
        {
            Ensure();
            return id >= 0 && id < s_Materials.Count ? s_Materials[id] : null;
        }

        static void Ensure()
        {
            if (s_Items != null) return;
            s_Items = new List<InventoryItem>();
            foreach (Team team in new[] { Team.Blue, Team.Red })
            {
                ItemCatalog catalog = Shop.ForTeam(team)?.catalog;
                if (catalog == null) continue;
                foreach (InventoryItem item in catalog.items)
                    if (item != null && !s_Items.Contains(item)) s_Items.Add(item);
            }
            HeroRoster roster = Object.FindFirstObjectByType<MatchBootstrap>()?.roster;
            if (roster != null)
                foreach (HeroDefinition hero in roster.heroes)
                    if (hero != null && hero.ultimate != null && !s_Items.Contains(hero.ultimate)) s_Items.Add(hero.ultimate);

            s_Ids = new Dictionary<InventoryItem, short>();
            for (int i = 0; i < s_Items.Count; i++) s_Ids[s_Items[i]] = (short)i;

            // Every material field of every item (projectiles, explosions, auras...), in a fixed order.
            s_Materials = new List<Material>();
            foreach (InventoryItem item in s_Items)
            {
                IEnumerable<FieldInfo> fields = item.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(Material))
                    .OrderBy(f => f.Name, System.StringComparer.Ordinal);
                foreach (FieldInfo field in fields)
                    if (field.GetValue(item) is Material m && m != null && !s_Materials.Contains(m)) s_Materials.Add(m);
            }
        }

        // Each map builds its own list.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Items = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => s_Items = null;
    }
}
