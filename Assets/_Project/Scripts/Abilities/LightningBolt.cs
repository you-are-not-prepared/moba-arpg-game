using UnityEngine;

namespace Moba.Abilities
{
    // A short jagged flash between two points (chain hits, storm strikes). Visual only; a game server shows it on its
    // clients too.
    public class LightningBolt : MonoBehaviour
    {
        const float Lifetime = 0.18f;
        const int Points = 9;
        const float Jitter = 0.35f;

        LineRenderer _line;
        float _age;

        public static void Spawn(Vector3 from, Vector3 to, Material material, Team team)
        {
            Net.NetworkMatch.Instance?.BroadcastBolt(from, to, material, team);
            if (!FogOfWar.ShouldShow(team, from) && !FogOfWar.ShouldShow(team, to)) return;

            var go = new GameObject("LightningBolt");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = Points;
            line.widthMultiplier = 0.18f;
            line.startColor = Color.white;
            line.endColor = new Color(0.75f, 0.55f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < Points; i++)
            {
                float t = i / (Points - 1f);
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < Points - 1) p += Random.insideUnitSphere * Jitter;
                line.SetPosition(i, p);
            }
            go.AddComponent<LightningBolt>()._line = line;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            _line.widthMultiplier = Mathf.Lerp(0.18f, 0.02f, _age / Lifetime);
        }
    }
}
