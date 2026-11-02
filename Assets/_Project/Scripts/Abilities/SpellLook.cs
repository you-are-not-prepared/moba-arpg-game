using System.Collections.Generic;
using UnityEngine;

namespace Moba.Abilities
{
    public enum SpellLookKind : byte { Explosion, Burst, Slash }

    // How adaptive spells look on a hero: the shape comes from the damage type (Fireball, Ice Shard, Thorn, Spark,
    // Arrow, Wind Blade), the colours from the hero (HeroDefinition.spellColors: main, secondary, highlight).
    // Visual only. Play shows an effect here and, on a game server, on every client.
    public static class SpellLook
    {
        static SpellLookLibrary s_Library;
        static readonly Dictionary<(int, Color), Material> s_Materials = new();
        static readonly Dictionary<string, Mesh> s_Meshes = new();

        static SpellLookLibrary Library => s_Library != null
            ? s_Library
            : s_Library = Resources.Load<SpellLookLibrary>(SpellLookLibrary.ResourcePath);

        // For trails and line renderers (tinted through vertex colour).
        public static Material Lines => Library != null ? Library.lines : null;

        // The hero's spell colours; for anything else, its base type's colour.
        public static Color[] ColorsOf(Unit unit)
        {
            if (unit != null && unit.TryGetComponent(out HeroController hero) && hero.definition != null
                && hero.definition.spellColors is { Length: >= 3 } colors)
                return colors;
            Color c = DamageTypes.ColorOf(unit != null ? unit.baseDamageType : DamageType.None);
            return new[] { c, Color.Lerp(c, Color.white, 0.4f), Color.white };
        }

        // ---------- Effects ----------

        // Shows an effect, and on a game server tells the clients to show it too (they call it with broadcast: false).
        public static void Play(SpellLookKind kind, DamageType type, Unit caster, Vector3 position, Vector3 direction,
            float radius, float delay, bool broadcast = true)
        {
            if (broadcast) Net.NetworkMatch.Instance?.BroadcastLook(kind, type, caster, position, direction, radius, delay);
            Color[] colors = ColorsOf(caster);
            Team team = caster != null ? caster.team : Team.Blue;
            switch (kind)
            {
                case SpellLookKind.Explosion:
                    if (FogOfWar.ShouldShow(team, position)) SpellExplosion.Spawn(position, radius, Glow(colors[1], 1.2f), broadcast: false);
                    break;
                case SpellLookKind.Burst:
                    if (FogOfWar.ShouldShow(team, position)) SpellBurstEffect.Create(position, radius, delay, type, colors);
                    break;
                case SpellLookKind.Slash:
                    if (caster != null) MeleeSwing.PlayToward(caster, direction, Slash(colors[1]), radius);
                    break;
            }
        }

        // The flying part of a projectile spell, built under `root` (pointing along its +Z).
        public static void BuildProjectile(Transform root, DamageType type, Color[] c, float size)
        {
            switch (type)
            {
                case DamageType.Fire:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size, Glow(c[1]));
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 0f, size * 0.12f), Vector3.one * size * 0.6f, Glow(c[2], 1.6f));
                    Trail(root, c[2], c[0], size * 0.9f, 0.25f);
                    break;
                case DamageType.Frost:
                    Part(root, Octahedron(), Vector3.zero, new Vector3(size * 0.45f, size * 0.45f, size * 1.5f), Glow(c[1]));
                    Part(root, Octahedron(), new Vector3(0f, 0f, size * 0.15f), new Vector3(size * 0.2f, size * 0.2f, size * 0.9f), Glow(c[2], 1.6f));
                    Trail(root, c[2], c[0], size * 0.35f, 0.2f);
                    break;
                case DamageType.Nature:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size * 0.6f, Glow(c[0]));
                    for (int i = 0; i < 6; i++)
                    {
                        Quaternion r = Quaternion.LookRotation(Quaternion.Euler(i < 4 ? 0f : i == 4 ? 90f : -90f, i * 90f, 0f) * Vector3.forward);
                        Transform spike = Part(root, Pyramid(), r * Vector3.forward * size * 0.3f,
                            new Vector3(size * 0.18f, size * 0.18f, size * 0.45f), Glow(c[1]));
                        spike.localRotation = r;
                    }
                    root.gameObject.AddComponent<SpellLookSpin>().degreesPerSecond = new Vector3(0f, 0f, 540f);
                    Trail(root, c[1], c[0], size * 0.45f, 0.22f);
                    break;
                case DamageType.Lightning:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size * 0.55f, Glow(c[0], 1.2f));
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size * 0.3f, Glow(c[2], 1.6f));
                    root.gameObject.AddComponent<SpellLookCrackle>().Init(c[1], c[2], size * 0.8f, Lines);
                    Trail(root, c[1], c[0], size * 0.5f, 0.18f);
                    break;
                case DamageType.Air:
                    Transform blade = Part(root, Crescent(), Vector3.zero, Vector3.one * size * 1.3f, Glow(c[1], 1.2f));
                    blade.localRotation = Quaternion.identity;
                    Trail(root, c[2], c[0], size * 1.1f, 0.15f);
                    break;
                default: // Physical: an arrow.
                    BuildArrow(root, c, size);
                    break;
            }
        }

        public static void BuildArrow(Transform root, Color[] c, float size)
        {
            Transform shaft = Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(size * 0.1f, size * 0.8f, size * 0.1f),
                Glow(new Color(0.48f, 0.34f, 0.2f), 0.6f));
            shaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(root, Pyramid(), new Vector3(0f, 0f, size * 0.8f), new Vector3(size * 0.22f, size * 0.22f, size * 0.4f), Glow(c[2], 1.2f));
            for (int i = 0; i < 2; i++)
            {
                Transform fin = Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -size * 0.65f),
                    new Vector3(i == 0 ? size * 0.35f : size * 0.04f, i == 0 ? size * 0.04f : size * 0.35f, size * 0.3f), Glow(c[0], 1.2f));
                fin.localRotation = Quaternion.identity;
            }
            Trail(root, c[1], c[0], size * 0.15f, 0.15f);
        }

        // ---------- Building blocks ----------

        public static Transform Part(Transform parent, PrimitiveType primitive, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return Setup(go, parent, localPosition, scale, material);
        }

        public static Transform Part(Transform parent, Mesh mesh, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            return Setup(go, parent, localPosition, scale, material);
        }

        static Transform Setup(GameObject go, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        public static TrailRenderer Trail(Transform root, Color head, Color tail, float width, float time)
        {
            var trail = root.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Lines;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(head, 0f), new GradientColorKey(tail, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        // A glowing solid colour (a copy of the library's emissive material, cached per colour).
        public static Material Glow(Color color, float intensity = 1f)
        {
            if (Library == null || Library.glow == null) return null;
            return Cached(0, color * intensity, Library.glow, m =>
            {
                m.SetColor("_BaseColor", color);
                m.EnableKeyword("_EMISSION");
                // Kept low: brighter emission tonemaps toward white and loses the hero's colour.
                m.SetColor("_EmissionColor", color * intensity * 0.55f);
            });
        }

        public static Material Ring(Color color) =>
            Library == null || Library.ring == null ? null : Cached(1, color, Library.ring, m => m.SetColor("_Color", color));

        public static Material Slash(Color color) =>
            Library == null || Library.slash == null ? null : Cached(2, color, Library.slash, m => m.SetColor("_Color", color));

        static Material Cached(int kind, Color key, Material source, System.Action<Material> setup)
        {
            if (s_Materials.TryGetValue((kind, key), out Material m) && m != null) return m;
            m = new Material(source) { name = source.name + " (look)" };
            setup(m);
            s_Materials[(kind, key)] = m;
            return m;
        }

        // A diamond, stretched along Z by its scale: ice shards.
        public static Mesh Octahedron() => CachedMesh("Shard", () => Build(
            new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down },
            new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 }));

        // A four-sided point along +Z, base at the origin: arrow heads, thorns, spikes.
        public static Mesh Pyramid() => CachedMesh("Spike", () => Build(
            new[] { Vector3.forward, new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) },
            new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 1, 2, 3, 1, 3, 4 }));

        // A flat crescent in the XZ plane, bulging toward +Z: a wind blade.
        public static Mesh Crescent() => CachedMesh("WindBlade", () =>
        {
            const int segments = 14;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float angle = Mathf.Lerp(-70f, 70f, u) * Mathf.Deg2Rad;
                float thickness = Mathf.Sin(u * Mathf.PI) * 0.35f;
                var outer = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle) - 0.6f);
                vertices.Add(outer);
                vertices.Add(outer * (1f - thickness) + Vector3.forward * thickness * 0.2f);
                if (i == segments) continue;
                int v = i * 2;
                triangles.AddRange(new[] { v, v + 1, v + 2, v + 1, v + 3, v + 2 });
            }
            // The underside as its own vertices, so each face gets its own normals (seen from above and below).
            int top = vertices.Count;
            for (int i = 0; i < top; i++) vertices.Add(vertices[i]);
            int topTriangles = triangles.Count;
            for (int i = 0; i < topTriangles; i += 3)
                triangles.AddRange(new[] { triangles[i] + top, triangles[i + 2] + top, triangles[i + 1] + top });
            return Build(vertices.ToArray(), triangles.ToArray());
        });

        static Mesh CachedMesh(string name, System.Func<Mesh> build)
        {
            if (s_Meshes.TryGetValue(name, out Mesh mesh) && mesh != null) return mesh;
            mesh = build();
            mesh.name = name;
            s_Meshes[name] = mesh;
            return mesh;
        }

        static Mesh Build(Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // Spins a look (thorn seeds, cyclones).
    public class SpellLookSpin : MonoBehaviour
    {
        public Vector3 degreesPerSecond;

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }

    // Small arcs flickering around a lightning orb.
    public class SpellLookCrackle : MonoBehaviour
    {
        const int Arcs = 3;
        const int Points = 5;
        const float Interval = 0.06f;

        readonly List<LineRenderer> _lines = new();
        float _radius, _next;

        public void Init(Color a, Color b, float radius, Material material)
        {
            _radius = radius;
            for (int i = 0; i < Arcs; i++)
            {
                var go = new GameObject("Crackle");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.positionCount = Points;
                line.widthMultiplier = 0.05f;
                line.startColor = a;
                line.endColor = b;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                _lines.Add(line);
            }
        }

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + Interval;
            foreach (LineRenderer line in _lines)
            {
                Vector3 from = Random.onUnitSphere * _radius * 0.3f, to = Random.onUnitSphere * _radius;
                for (int i = 0; i < Points; i++)
                {
                    float t = i / (Points - 1f);
                    Vector3 p = Vector3.Lerp(from, to, t);
                    if (i > 0 && i < Points - 1) p += Random.insideUnitSphere * _radius * 0.25f;
                    line.SetPosition(i, p);
                }
            }
        }
    }
}
