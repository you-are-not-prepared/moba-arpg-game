using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // A straight-flying spell projectile. On the first enemy it touches it explodes (area damage + burn + stun),
    // then, if it has forks left, splits into two projectiles leaving the target at ±ForkAngle.
    // A piercing projectile keeps flying, hitting each enemy along its path once (forks only on the first hit).
    public class SpellProjectile : MonoBehaviour
    {
        public struct Payload
        {
            public Unit Caster;
            public Team Team;
            public float Damage;
            public float ExplosionRadius;
            public float BurnDps;
            public float BurnDuration;
            public int ForksRemaining;
            public float ForkAngle;
            public bool Pierce;
            public float StunDuration;
            public float Speed;
            public float MaxDistance;
            public float Size;
            public float Height;
            public Material ProjectileMaterial;
            public Material ExplosionMaterial;
        }

        static readonly List<Unit> s_Targets = new();

        Payload _p;
        Vector3 _dir;
        float _travelled;
        Unit _ignore;
        Renderer _renderer;
        HashSet<Unit> _pierced;

        // `ignore` can't be hit by this projectile (forks never hit the target they split from).
        public static void Launch(Vector3 groundOrigin, Vector3 direction, in Payload payload, Unit ignore)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpellProjectile";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(groundOrigin + Vector3.up * payload.Height, Quaternion.LookRotation(direction));
            go.transform.localScale = Vector3.one * payload.Size;
            if (payload.ProjectileMaterial != null) go.GetComponent<Renderer>().sharedMaterial = payload.ProjectileMaterial;

            var p = go.AddComponent<SpellProjectile>();
            p._p = payload;
            p._dir = direction.normalized;
            p._ignore = ignore;
            p._renderer = go.GetComponent<Renderer>();
            p._renderer.enabled = FogOfWar.ShouldShow(payload.Team, go.transform.position);
        }

        void Update()
        {
            float step = _p.Speed * Time.deltaTime;
            Vector3 from = transform.position;
            Vector3 to = from + _dir * step;

            Unit hit = FirstHit(from, to, out float t);
            if (hit != null)
            {
                transform.position = Vector3.Lerp(from, to, t);
                OnHit(hit);
                return;
            }

            transform.position = to;
            _renderer.enabled = FogOfWar.ShouldShow(_p.Team, to);
            _travelled += step;
            if (_travelled >= _p.MaxDistance) Destroy(gameObject);
        }

        // Earliest enemy along this frame's movement (flat XZ swept circle).
        Unit FirstHit(Vector3 from, Vector3 to, out float bestT)
        {
            var a = new Vector2(from.x, from.z);
            Vector2 ab = new Vector2(to.x, to.z) - a;
            float len2 = ab.sqrMagnitude;
            float radius = _p.Size * 0.5f;
            Unit best = null;
            bestT = 1f;

            foreach (Unit u in Unit.All)
            {
                if (u.team == _p.Team || u == _ignore || !u.IsTargetable) continue;
                if (_pierced != null && _pierced.Contains(u)) continue;
                var c = new Vector2(u.transform.position.x, u.transform.position.z);
                float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / len2) : 0f;
                if ((c - (a + ab * t)).magnitude > u.radius + radius) continue;
                if (best == null || t < bestT)
                {
                    best = u;
                    bestT = t;
                }
            }
            return best;
        }

        void OnHit(Unit hit)
        {
            Vector3 hitGround = hit.transform.position;
            Explode(new Vector3(transform.position.x, hitGround.y, transform.position.z), hit);

            bool firstHit = _pierced == null || _pierced.Count == 0;
            if (_p.ForksRemaining > 0 && firstHit)
            {
                Payload child = _p;
                child.ForksRemaining--;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 dir = Quaternion.Euler(0f, side * _p.ForkAngle, 0f) * _dir;
                    Launch(hitGround + dir * (hit.radius + 0.2f), dir, child, ignore: hit);
                }
            }

            if (_p.Pierce)
            {
                (_pierced ??= new HashSet<Unit>()).Add(hit);
                return;
            }
            Destroy(gameObject);
        }

        void Explode(Vector3 center, Unit primary)
        {
            s_Targets.Clear();
            if (_p.ExplosionRadius <= 0f)
            {
                s_Targets.Add(primary);
            }
            else
            {
                foreach (Unit u in Unit.All)
                {
                    if (u.team == _p.Team || !u.IsTargetable) continue;
                    Vector3 d = u.transform.position - center;
                    d.y = 0f;
                    if (d.magnitude - u.radius <= _p.ExplosionRadius) s_Targets.Add(u);
                }
            }

            foreach (Unit u in s_Targets)
            {
                u.TakeDamage(_p.Damage, _p.Caster);
                if (_p.BurnDps > 0f && u.IsAlive) Burning.Apply(u, _p.BurnDps, _p.BurnDuration, _p.Caster);
                if (_p.StunDuration > 0f && u.IsAlive) Stun.Apply(u, _p.StunDuration);
            }
            s_Targets.Clear();

            if (_p.ExplosionRadius > 0f) SpellExplosion.Spawn(center, _p.ExplosionRadius, _p.ExplosionMaterial);
        }
    }
}
