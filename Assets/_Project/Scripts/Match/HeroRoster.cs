using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    // Every hero available in champion select, in display order.
    [CreateAssetMenu(menuName = "MOBA/Hero Roster", fileName = "HeroRoster")]
    public class HeroRoster : ScriptableObject
    {
        public List<HeroDefinition> heroes = new();

        public HeroDefinition Find(string id)
        {
            foreach (HeroDefinition hero in heroes)
                if (hero != null && hero.id == id) return hero;
            return null;
        }
    }
}
