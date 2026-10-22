using UnityEngine;

namespace Moba
{
    // Paints a unit in its team's colour (creeps share one prefab for both teams). Used by the creep spawner and,
    // on game-server clients, whenever the replicated team arrives.
    public class TeamColors : MonoBehaviour
    {
        public Material blue;
        public Material red;

        public void Apply(Team team)
        {
            Material material = team == Team.Blue ? blue : red;
            if (material == null) return;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
            if (TryGetComponent(out Attacker attacker) && attacker.projectileSpeed > 0f) attacker.projectileMaterial = material;
        }
    }
}
