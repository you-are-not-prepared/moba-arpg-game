using UnityEngine;

namespace Moba
{
    // Spawns the hero picked in champion select (or the roster's first hero when a map scene is played directly)
    // and hands it to the systems that follow it. Runs right after NavMeshSurface (-102) has loaded the NavMesh, so
    // the hero's agent can be placed, and before everything else that reads the hero.
    [DefaultExecutionOrder(-101)]
    public class MatchBootstrap : MonoBehaviour
    {
        public HeroRoster roster;
        public Transform heroSpawn;
        public GameManager game;
        public MobaCamera cameraRig;

        void Awake()
        {
            HeroDefinition definition = roster.Find(MatchSettings.SelectedHeroId);
            if (definition == null && roster.heroes.Count > 0) definition = roster.heroes[0];
            if (definition == null || definition.prefab == null)
            {
                Debug.LogError("[MatchBootstrap] No hero to spawn: the roster is empty or a hero has no prefab.");
                return;
            }

            HeroController hero = Instantiate(definition.prefab, heroSpawn.position, heroSpawn.rotation);
            hero.name = definition.displayName;
            hero.spawnPoint = heroSpawn;
            definition.ApplyTo(hero);

            game.hero = hero;
            cameraRig.hero = hero;
            cameraRig.transform.position = hero.transform.position;
        }
    }
}
