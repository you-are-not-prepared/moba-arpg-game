using System.Collections;
using UnityEngine;

namespace Moba
{
    // Sends a wave of creeps down the lane on a fixed timer.
    public class CreepSpawner : MonoBehaviour
    {
        public Team team;
        public Material teamMaterial;
        public CreepAI meleePrefab;
        public CreepAI rangedPrefab;
        public int meleePerWave = 3;
        public int rangedPerWave = 2;
        public float firstWaveDelay = 3f;
        public float waveInterval = 25f;
        public float spawnSpacing = 0.7f;
        [Tooltip("World-space waypoints from this base to the enemy core.")]
        public Vector3[] lane;

        float _nextWaveTime;

        public float NextWaveIn => Mathf.Max(0f, _nextWaveTime - Time.time);

        IEnumerator Start()
        {
            // On a game-server client the server spawns the creeps.
            if (Net.NetSession.IsRemoteClient) yield break;
            _nextWaveTime = Time.time + firstWaveDelay;
            yield return new WaitForSeconds(firstWaveDelay);
            while (true)
            {
                _nextWaveTime = Time.time + waveInterval;
                StartCoroutine(SpawnWave());
                yield return new WaitForSeconds(waveInterval);
            }
        }

        IEnumerator SpawnWave()
        {
            for (int i = 0; i < meleePerWave; i++)
            {
                Spawn(meleePrefab);
                yield return new WaitForSeconds(spawnSpacing);
            }
            for (int i = 0; i < rangedPerWave; i++)
            {
                Spawn(rangedPrefab);
                yield return new WaitForSeconds(spawnSpacing);
            }
        }

        void Spawn(CreepAI prefab)
        {
            CreepAI creep = Instantiate(prefab, transform.position, transform.rotation);
            creep.name = $"{prefab.name}_{team}";
            creep.GetComponent<Unit>().team = team;
            if (creep.TryGetComponent(out TeamColors colors))
            {
                colors.Apply(team);
            }
            else
            {
                foreach (Renderer r in creep.GetComponentsInChildren<Renderer>()) r.sharedMaterial = teamMaterial;
                Attacker attacker = creep.GetComponent<Attacker>();
                if (attacker.projectileSpeed > 0f) attacker.projectileMaterial = teamMaterial;
            }
            creep.Init(lane);
            // On the game server, every client gets the creep too.
            if (Net.NetSession.IsNetworked && creep.TryGetComponent(out Unity.Netcode.NetworkObject networkObject))
                networkObject.Spawn(destroyWithScene: true);
        }

        void OnDrawGizmos()
        {
            if (lane == null || lane.Length == 0) return;
            Gizmos.color = team == Team.Blue ? Color.cyan : Color.red;
            Gizmos.DrawLine(transform.position, lane[0]);
            for (int i = 1; i < lane.Length; i++) Gizmos.DrawLine(lane[i - 1], lane[i]);
        }
    }
}
