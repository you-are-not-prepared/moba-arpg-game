using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // Timed buff (Corvin's Thunderhead): a storm cloud floats above the unit and strikes a random enemy in reach every
    // `interval` seconds, enemy heroes first. Game-server clients get the cloud only (SyncFromServer); the strikes'
    // bolts arrive through NetworkMatch.
    [DisallowMultipleComponent]
    public class StormCloud : MonoBehaviour
    {
        const float Height = 4.2f;

        Unit _unit;
        float _remaining, _interval, _radius, _damage, _nextStrike;
        Material _bolt;
        bool _visualOnly;
        Transform _cloud;
        Renderer[] _cloudRenderers;
        static readonly List<Unit> s_Candidates = new();

        public float Remaining => _remaining;

        public static void Apply(Unit unit, float duration, float interval, float radius, float damage, Material cloudMaterial,
            Material boltMaterial)
        {
            if (unit == null || !unit.IsAlive || duration <= 0f) return;
            StormCloud storm = Get(unit);
            storm._visualOnly = false;
            storm.Begin(duration, cloudMaterial);
            storm._interval = Mathf.Max(0.1f, interval);
            storm._radius = radius;
            storm._damage = damage;
            storm._bolt = boltMaterial;
            storm._nextStrike = 0f;
        }

        // Game-server clients: show the cloud the server reports (`remaining` 0 = none).
        public static void SyncFromServer(Unit unit, float remaining, Material cloudMaterial)
        {
            bool active = unit.TryGetComponent(out StormCloud storm) && storm.enabled;
            if (remaining <= 0f)
            {
                if (active) storm._remaining = 0f;
                return;
            }
            if (active)
            {
                storm._remaining = remaining;
                return;
            }
            storm = Get(unit);
            storm._visualOnly = true;
            storm.Begin(remaining, cloudMaterial);
        }

        static StormCloud Get(Unit unit) =>
            unit.TryGetComponent(out StormCloud storm) ? storm : unit.gameObject.AddComponent<StormCloud>();

        void Awake() => _unit = GetComponent<Unit>();

        void OnDisable()
        {
            if (_cloud != null) _cloud.gameObject.SetActive(false);
        }

        void Begin(float duration, Material cloudMaterial)
        {
            _remaining = duration;
            if (_cloud == null) BuildCloud();
            foreach (Renderer r in _cloudRenderers) r.sharedMaterial = cloudMaterial;
            _cloud.gameObject.SetActive(true);
            enabled = true;
        }

        void BuildCloud()
        {
            _cloud = new GameObject("StormCloud").transform;
            _cloud.SetParent(transform, false);
            _cloud.localPosition = Vector3.up * Height;
            // A few overlapping puffs.
            Vector3[] puffs = { new(0f, 0f, 0f), new(0.9f, -0.1f, 0.3f), new(-0.8f, -0.05f, -0.2f), new(0.2f, 0.15f, -0.8f) };
            var renderers = new List<Renderer>();
            foreach (Vector3 p in puffs)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(puff.GetComponent<Collider>());
                puff.transform.SetParent(_cloud, false);
                puff.transform.localPosition = p;
                puff.transform.localScale = new Vector3(1.6f, 0.8f, 1.4f);
                renderers.Add(puff.GetComponent<Renderer>());
            }
            _cloudRenderers = renderers.ToArray();
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f || !_unit.IsAlive)
            {
                _remaining = 0f;
                enabled = false;
                return;
            }
            bool shown = FogOfWar.ShouldShow(_unit.team, transform.position);
            foreach (Renderer r in _cloudRenderers) r.enabled = shown;
            _cloud.localRotation = Quaternion.Euler(0f, Time.time * 30f, 0f);

            if (_visualOnly) return;
            _nextStrike -= Time.deltaTime;
            if (_nextStrike > 0f) return;
            _nextStrike = _interval;
            Strike();
        }

        void Strike()
        {
            s_Candidates.Clear();
            bool heroes = false;
            foreach (Unit u in Unit.All)
            {
                if (!u.IsEnemyOf(_unit) || !u.IsTargetable || _unit.EdgeDistanceTo(u) > _radius) continue;
                if (u.kind == UnitKind.Hero && !heroes)
                {
                    heroes = true;
                    s_Candidates.Clear();
                }
                if (heroes && u.kind != UnitKind.Hero) continue;
                s_Candidates.Add(u);
            }
            if (s_Candidates.Count == 0) return;
            Unit target = s_Candidates[Random.Range(0, s_Candidates.Count)];
            s_Candidates.Clear();
            LightningBolt.Spawn(_cloud.position, target.AimPoint, _bolt, _unit.team);
            target.TakeDamage(_damage, _unit);
            SpellEvents.RaiseHit(_unit, target, _damage);
        }
    }
}
