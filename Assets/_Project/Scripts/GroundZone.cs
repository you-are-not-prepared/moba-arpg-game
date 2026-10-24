using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // An area on the ground (ZoneSpell): a warning ring for `delay` seconds, then it freezes the enemies inside once
    // and, for `duration` seconds, slows and damages enemies inside and heals and speeds up allied heroes inside.
    // The simulating side (offline or the game server) applies the effects; game-server clients get a visual-only
    // copy (CreateVisual, through NetworkMatch).
    public class GroundZone : MonoBehaviour
    {
        const float Tick = 0.25f;

        Unit _caster;
        Team _team;
        float _radius, _delay, _duration;
        float _freeze, _slow, _damagePerSecond, _healPerSecond, _haste;
        Material _warning, _zone;
        bool _visualOnly, _triggered;
        float _time, _nextTick;
        Renderer _renderer;
        readonly Dictionary<NavMeshAgent, float> _hasted = new();
        static readonly List<Unit> s_Inside = new();

        // `healPerSecond` is a fraction of max health per second (0.04 = 4%); `haste` 0.2 = 20% faster.
        public static GroundZone Create(Unit caster, Vector3 center, float radius, float delay, float duration, float freeze,
            float slow, float damagePerSecond, float healPerSecond, float haste, Material warning, Material zone)
        {
            GroundZone z = Spawn(center, radius, delay, duration, caster.team, warning, zone);
            z._caster = caster;
            z._freeze = freeze;
            z._slow = slow;
            z._damagePerSecond = damagePerSecond;
            z._healPerSecond = healPerSecond;
            z._haste = haste;
            Net.NetworkMatch.Instance?.BroadcastZone(center, radius, delay, duration, caster.team, warning, zone);
            return z;
        }

        public static void CreateVisual(Vector3 center, float radius, float delay, float duration, Team team, Material warning,
            Material zone) => Spawn(center, radius, delay, duration, team, warning, zone)._visualOnly = true;

        static GroundZone Spawn(Vector3 center, float radius, float delay, float duration, Team team, Material warning, Material zone)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "GroundZone";
            DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.position = center + Vector3.up * 0.05f;
            disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var z = disc.AddComponent<GroundZone>();
            z._renderer = disc.GetComponent<Renderer>();
            z._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            z._team = team;
            z._radius = radius;
            z._delay = delay;
            z._duration = duration;
            z._warning = warning != null ? warning : zone;
            z._zone = zone;
            z._renderer.sharedMaterial = delay > 0f ? z._warning : zone;
            z._renderer.enabled = FogOfWar.ShouldShow(team, center);
            return z;
        }

        void Update()
        {
            _time += Time.deltaTime;
            bool active = _time >= _delay;
            Material material = active ? _zone : _warning;
            if (_renderer.sharedMaterial != material) _renderer.sharedMaterial = material;
            _renderer.enabled = FogOfWar.ShouldShow(_team, transform.position);

            if (!_visualOnly && active)
            {
                if (!_triggered)
                {
                    _triggered = true;
                    if (_freeze > 0f)
                        foreach (Unit u in Inside(enemies: true)) Stun.Apply(u, _freeze);
                }
                if (_time >= _nextTick)
                {
                    _nextTick = _time + Tick;
                    ApplyTick();
                }
            }
            if (_time >= _delay + _duration) Destroy(gameObject);
        }

        void ApplyTick()
        {
            foreach (Unit u in Inside(enemies: true))
            {
                if (_slow > 0f) Slow.Apply(u, _slow, Tick + 0.15f);
                if (_damagePerSecond > 0f && _caster != null)
                {
                    u.TakeDamage(_damagePerSecond * Tick, _caster);
                    SpellEvents.RaiseHit(_caster, u, _damagePerSecond * Tick);
                }
            }

            var stillInside = new HashSet<NavMeshAgent>();
            foreach (Unit u in Inside(enemies: false))
            {
                if (u.kind != UnitKind.Hero) continue;
                if (_healPerSecond > 0f) u.Heal(u.maxHealth * _healPerSecond * Tick);
                if (_haste > 0f && u.TryGetComponent(out NavMeshAgent agent))
                {
                    stillInside.Add(agent);
                    if (!_hasted.ContainsKey(agent))
                    {
                        agent.speed *= 1f + _haste;
                        _hasted[agent] = 1f + _haste;
                    }
                }
            }
            var left = new List<NavMeshAgent>();
            foreach (NavMeshAgent agent in _hasted.Keys)
                if (!stillInside.Contains(agent)) left.Add(agent);
            foreach (NavMeshAgent agent in left) EndHaste(agent);
        }

        void EndHaste(NavMeshAgent agent)
        {
            if (agent != null && _hasted.TryGetValue(agent, out float multiplier)) agent.speed /= multiplier;
            _hasted.Remove(agent);
        }

        void OnDestroy()
        {
            foreach (NavMeshAgent agent in new List<NavMeshAgent>(_hasted.Keys)) EndHaste(agent);
        }

        List<Unit> Inside(bool enemies)
        {
            s_Inside.Clear();
            Vector3 center = transform.position;
            foreach (Unit u in Unit.All)
            {
                if (enemies ? u.team == _team || !u.IsTargetable : u.team != _team || !u.IsAlive) continue;
                Vector3 d = u.transform.position - center;
                d.y = 0f;
                if (d.magnitude - u.radius <= _radius) s_Inside.Add(u);
            }
            return new List<Unit>(s_Inside);
        }
    }
}
