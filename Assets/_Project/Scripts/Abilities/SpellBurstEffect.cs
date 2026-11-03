using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    // The look of a Burst (BurstSpell) for one damage type: a warning circle for `delay`, then the hit. A meteor falls,
    // arrows rain, ice shards or thorns burst out of the ground, lightning strikes, or a cyclone spins up. Visual only.
    public class SpellBurstEffect : MonoBehaviour
    {
        const float Linger = 0.7f;

        // A part that moves through up to four points over time (e.g. falls, sticks, sinks).
        struct Piece
        {
            public Transform Transform;
            public Vector3[] Points;
            public float[] Times;
        }

        readonly List<Piece> _pieces = new();
        Transform _warning;
        Vector3 _center;
        float _radius, _delay, _time;
        DamageType _type;
        Color[] _colors;
        bool _hit;

        public static void Create(Vector3 center, float radius, float delay, DamageType type, Color[] colors)
        {
            var root = new GameObject($"Burst ({type})");
            root.transform.position = center;
            var e = root.AddComponent<SpellBurstEffect>();
            e._center = center;
            e._radius = radius;
            e._delay = Mathf.Max(0.05f, delay);
            e._type = type;
            e._colors = colors;
            e.Build();
        }

        void Build()
        {
            _warning = SpellLook.Part(transform, PrimitiveType.Cylinder, Vector3.up * 0.06f,
                new Vector3(_radius * 2f, 0.01f, _radius * 2f), SpellLook.Ring(_colors[0]));

            switch (_type)
            {
                case DamageType.Fire:
                {
                    var meteor = new GameObject("Meteor").transform;
                    meteor.SetParent(transform, false);
                    SpellLook.BuildProjectile(meteor, DamageType.Fire, _colors, _radius * 0.55f);
                    Vector3 start = new(-3f, 13f, -3f);
                    meteor.localRotation = Quaternion.LookRotation(-start);
                    Add(meteor, (0f, start), (_delay, Vector3.up * 0.3f), (_delay + 0.01f, Vector3.down * 5f));
                    break;
                }
                case DamageType.Physical:
                    for (int i = 0; i < 9; i++)
                    {
                        var arrow = new GameObject("Arrow").transform;
                        arrow.SetParent(transform, false);
                        SpellLook.BuildArrow(arrow, _colors, 1.1f);
                        Vector3 ground = Spot(i, 9) + Vector3.up * 0.5f;
                        arrow.localRotation = Quaternion.LookRotation(Vector3.down + Random.insideUnitSphere * 0.15f);
                        float land = _delay - Random.Range(0f, 0.15f);
                        Add(arrow, (0f, ground + Vector3.up * 14f), (land, ground), (_delay + Linger * 0.6f, ground),
                            (_delay + Linger, ground + Vector3.down * 1.5f));
                    }
                    break;
                case DamageType.Frost:
                case DamageType.Nature:
                {
                    bool ice = _type == DamageType.Frost;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 ground = Spot(i, 8);
                        float height = Random.Range(0.9f, 1.6f) * (ice ? 1.2f : 1f);
                        Transform part = SpellLook.Part(transform, ice ? SpellLook.Octahedron() : SpellLook.Pyramid(), Vector3.zero,
                            ice ? new Vector3(0.35f, 0.35f, height) : new Vector3(0.25f, 0.25f, height),
                            SpellLook.Glow(_colors[i % 2 == 0 ? 1 : 0], ice ? 1.3f : 1f));
                        part.localRotation = Quaternion.LookRotation(Vector3.up + Random.insideUnitSphere * 0.25f);
                        Vector3 below = ground + Vector3.down * height * (ice ? 1.1f : 1.05f);
                        Add(part, (0f, below), (_delay, below), (_delay + 0.12f, ground + Vector3.up * (ice ? 0.2f : 0f)),
                            (_delay + Linger, below));
                    }
                    break;
                }
                case DamageType.Air:
                    for (int i = 0; i < 3; i++)
                    {
                        Transform ring = SpellLook.Part(transform, SpellLook.Crescent(), Vector3.zero,
                            Vector3.one * _radius * (0.7f + i * 0.25f), SpellLook.Glow(_colors[i == 1 ? 2 : 1], 1.1f));
                        ring.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                        ring.gameObject.AddComponent<SpellLookSpin>().degreesPerSecond = new Vector3(0f, 720f - i * 120f, 0f);
                        Vector3 low = Vector3.up * (0.2f + i * 0.6f);
                        Add(ring, (0f, low + Vector3.down * 3f), (_delay, low), (_delay + Linger, low + Vector3.up * 2.5f));
                    }
                    break;
            }
        }

        // A spot spread over the area (index i of n), on the ground.
        Vector3 Spot(int i, int n)
        {
            float angle = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float distance = Mathf.Sqrt(Random.Range(0.05f, 1f)) * _radius * 0.85f;
            return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        }

        void Add(Transform t, params (float time, Vector3 point)[] keys)
        {
            var piece = new Piece { Transform = t, Points = new Vector3[keys.Length], Times = new float[keys.Length] };
            for (int i = 0; i < keys.Length; i++)
            {
                piece.Times[i] = keys[i].time;
                piece.Points[i] = keys[i].point;
            }
            _pieces.Add(piece);
            t.localPosition = piece.Points[0];
        }

        void Update()
        {
            _time += Time.deltaTime;
            foreach (Piece p in _pieces)
            {
                if (p.Transform == null) continue;
                int last = p.Times.Length - 1;
                Vector3 position = p.Points[last];
                for (int i = 0; i < last; i++)
                {
                    if (_time > p.Times[i + 1]) continue;
                    float t = Mathf.InverseLerp(p.Times[i], p.Times[i + 1], _time);
                    position = Vector3.Lerp(p.Points[i], p.Points[i + 1], t);
                    break;
                }
                p.Transform.localPosition = position;
            }

            if (!_hit && _time >= _delay)
            {
                _hit = true;
                if (_warning != null) Destroy(_warning.gameObject);
                SpellExplosion.Spawn(_center, _radius, SpellLook.Glow(_colors[1], 1.2f), broadcast: false);
                if (_type == DamageType.Lightning)
                    for (int i = 0; i < 3; i++) Bolt(_center + Spot(i, 3) * 0.4f);
            }
            if (_time >= _delay + Linger) Destroy(gameObject);
        }

        // A jagged strike from the sky down to `ground`.
        void Bolt(Vector3 ground)
        {
            var go = new GameObject("Strike");
            go.transform.SetParent(transform, true);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = SpellLook.Lines;
            const int points = 10;
            line.positionCount = points;
            line.widthMultiplier = 0.35f;
            line.widthCurve = AnimationCurve.Linear(0f, 0.6f, 1f, 1f);
            line.startColor = _colors[2];
            line.endColor = _colors[0];
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 top = ground + new Vector3(Random.Range(-2f, 2f), 14f, Random.Range(-2f, 2f));
            for (int i = 0; i < points; i++)
            {
                float t = i / (points - 1f);
                Vector3 p = Vector3.Lerp(top, ground, t);
                if (i > 0 && i < points - 1) p += Random.insideUnitSphere * 0.7f;
                line.SetPosition(i, p);
            }
            Destroy(go, 0.25f);
        }
    }
}
