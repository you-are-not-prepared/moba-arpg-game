using System.Collections.Generic;
using System.Linq;
using Moba.Bots;
using UnityEngine;

namespace Moba
{
    // Spawns everyone in the match lineup: the local player's hero (the roster's first hero when a map scene is
    // played directly) and, in PvE, the bots with their lanes. Hands the player's hero to the systems that follow it.
    // Runs right after NavMeshSurface (-102) has loaded the NavMesh, so agents can be placed, and before everything
    // else that reads the heroes.
    [DefaultExecutionOrder(-101)]
    public class MatchBootstrap : MonoBehaviour
    {
        const float SpawnSpacing = 2.2f;

        public HeroRoster roster;
        [Tooltip("Blue team's spawn (the red team's mirrors it at the red core).")]
        public Transform heroSpawn;
        public GameManager game;
        public MobaCamera cameraRig;

        MapLayoutInfo _layout;
        Transform _redSpawn;

        void Awake()
        {
            _layout = game.TryGetComponent(out MapLayout layout) ? layout.info : null;
            _redSpawn = CreateRedSpawn();

            List<LobbySlot> lineup = MatchSettings.Lineup.Count > 0
                ? MatchSettings.Lineup
                : new List<LobbySlot> { new() { team = Team.Blue, heroId = MatchSettings.SelectedHeroId, isLocal = true } };

            var spawnIndex = new Dictionary<Team, int> { [Team.Blue] = 0, [Team.Red] = 0 };
            var botIndex = new Dictionary<Team, int> { [Team.Blue] = 0, [Team.Red] = 0 };
            int perTeam = Mathf.Max(MatchSettings.TeamSize, lineup.Count(s => s.team == Team.Blue), lineup.Count(s => s.team == Team.Red));

            foreach (LobbySlot slot in lineup)
            {
                HeroDefinition definition = roster.Find(slot.heroId);
                if (definition == null && slot.isLocal && roster.heroes.Count > 0) definition = roster.heroes[0];
                if (definition == null || definition.prefab == null)
                {
                    Debug.LogError($"[MatchBootstrap] Can't spawn hero '{slot.heroId}': not in the roster or it has no prefab.");
                    continue;
                }

                HeroController hero = Spawn(definition, slot.team, spawnIndex[slot.team]++, perTeam, slot.isBot);
                game.heroes.Add(hero);
                if (slot.isLocal)
                {
                    game.hero = hero;
                    cameraRig.hero = hero;
                    cameraRig.transform.position = hero.transform.position;
                }
                else if (slot.isBot)
                {
                    hero.gameObject.AddComponent<BotBrain>().Init(LaneFor(slot.team, botIndex[slot.team]++));
                }
            }
        }

        HeroController Spawn(HeroDefinition definition, Team team, int index, int perTeam, bool isBot)
        {
            Transform teamSpawn = team == Team.Blue ? heroSpawn : _redSpawn;
            // Line the team up side by side; each hero respawns at its own spot.
            var spot = new GameObject($"Spawn_{team}_{index}").transform;
            spot.SetParent(teamSpawn, false);
            spot.localPosition = Vector3.right * ((index - (perTeam - 1) * 0.5f) * SpawnSpacing);

            HeroController hero = Instantiate(definition.prefab, spot.position, teamSpawn.rotation);
            hero.name = isBot ? $"{definition.displayName} (bot)" : definition.displayName;
            hero.isPlayer = !isBot;
            hero.spawnPoint = spot;
            hero.Unit.team = team;
            definition.ApplyTo(hero);
            return hero;
        }

        Transform CreateRedSpawn()
        {
            var spawn = new GameObject("HeroSpawn_Red").transform;
            if (_layout == null)
            {
                spawn.SetPositionAndRotation(-heroSpawn.position, Quaternion.LookRotation(-heroSpawn.forward));
                return spawn;
            }
            Vector3 core = _layout.redCore;
            Vector3 outward = new Vector3(core.x, 0f, core.z).normalized;
            spawn.SetPositionAndRotation(core + outward * 6f, Quaternion.LookRotation(-outward));
            return spawn;
        }

        // Bots spread over the lanes. Blue bots take the side lanes first (the player usually goes mid);
        // red bots take mid first. Lanes run blue -> red, so red bots walk them backwards.
        Vector3[] LaneFor(Team team, int botIndex)
        {
            if (_layout?.lanes == null || _layout.lanes.Length == 0)
            {
                Vector3 enemyCore = team == Team.Blue ? _redSpawn.position : heroSpawn.position;
                return new[] { enemyCore };
            }

            List<LanePath> ordered = _layout.lanes
                .OrderBy(l => (l.name == "Mid") == (team == Team.Blue) ? 1 : 0)
                .ToList();
            Vector3[] points = ordered[botIndex % ordered.Count].points;
            return team == Team.Blue ? points.ToArray() : points.Reverse().ToArray();
        }
    }
}
