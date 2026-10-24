using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    // MOBA-style fog of war for the local player's team: allies reveal circles (Unit.sightRadius); everything
    // else is fogged. Enemy heroes/creeps in fog are hidden and unclickable; structures stay visible but dimmed.
    // A low-res texture (alpha = darkness) drives the ground overlay and the minimap; props are dimmed to match.
    [DefaultExecutionOrder(-60)] // before HeroController and the HUD read visibility
    public class FogOfWar : MonoBehaviour
    {
        public static FogOfWar Instance { get; private set; }

        public Vector2 worldMin = new(-70f, -70f);
        public Vector2 worldMax = new(70f, 70f);
        [Tooltip("Texels per side of the fog texture.")]
        public int resolution = 128;
        [Range(0f, 1f)] public float fogDarkness = 0.6f;
        [Tooltip("Width (m) of the soft edge at the rim of each vision circle.")]
        public float edgeSoftness = 2f;
        [Tooltip("How fast fog edges fade in/out (per second).")]
        public float fadeSpeed = 5f;
        [Tooltip("Ground overlay that renders the fog texture.")]
        public Renderer overlay;
        [Tooltip("Props under these roots (rocks, structures, shops) are dimmed while in fog.")]
        public Transform[] tintRoots;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        Team _team;
        bool[] _visible;
        float[] _target;
        float[] _shown;
        Color32[] _pixels;
        readonly List<Unit> _sources = new();
        readonly Dictionary<Unit, UnitVisual> _unitVisuals = new();
        readonly List<Unit> _stale = new();
        readonly List<TintedProp> _props = new();
        MaterialPropertyBlock _block;

        public Texture2D Texture { get; private set; }

        struct UnitVisual
        {
            public Renderer[] Renderers;
            public Collider[] Colliders;
            public bool Shown;
        }

        sealed class TintedProp
        {
            public Renderer Renderer;
            public Color BaseColor;
            public int Texel;
            public float Applied = -1f;
        }

        // Whether a point is inside the player's vision (true when there's no fog).
        public static bool IsPointVisible(Vector3 world) =>
            Instance == null || !Instance.isActiveAndEnabled || Instance.IsVisible(world);

        // Whether something belonging to `team` at `world` should be drawn for the local player.
        public static bool ShouldShow(Team team, Vector3 world) =>
            Instance == null || !Instance.isActiveAndEnabled || team == Instance._team || Instance.IsVisible(world);

        void Awake()
        {
            Instance = this;
            int n = resolution * resolution;
            _visible = new bool[n];
            _target = new float[n];
            _shown = new float[n];
            _pixels = new Color32[n];
            for (int i = 0; i < n; i++) _shown[i] = fogDarkness;

            Texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "FogOfWar",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _block = new MaterialPropertyBlock();
        }

        void Start()
        {
            _team = GameManager.Instance != null ? GameManager.Instance.playerTeam : Team.Blue;
            if (overlay != null)
            {
                overlay.GetPropertyBlock(_block);
                _block.SetTexture(MainTexId, Texture);
                overlay.SetPropertyBlock(_block);
            }
            CollectProps();
        }

        void OnDisable()
        {
            // Without fog everything is visible again.
            foreach (KeyValuePair<Unit, UnitVisual> pair in _unitVisuals)
                if (pair.Key != null) Show(pair.Key, pair.Value, true);
            _unitVisuals.Clear();
            if (overlay != null) overlay.enabled = false;
        }

        void OnEnable()
        {
            if (overlay != null) overlay.enabled = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Texture != null) Destroy(Texture);
        }

        void Update()
        {
            // The local player's team (on a game server it's known once their hero arrives).
            if (GameManager.Instance != null) _team = GameManager.Instance.playerTeam;
            GatherSources();
            UpdateUnits();
            UpdateGrid();
            UpdateProps();
        }

        public bool IsVisible(Vector3 world) => _visible[TexelAt(world)];

        void GatherSources()
        {
            _sources.Clear();
            foreach (Unit u in Unit.All)
                if (u.team == _team && u.IsAlive && u.sightRadius > 0f) _sources.Add(u);
        }

        bool SeenBySources(Unit target)
        {
            Vector3 p = target.transform.position;
            foreach (Unit s in _sources)
            {
                Vector3 d = p - s.transform.position;
                d.y = 0f;
                if (d.magnitude - target.radius <= s.sightRadius) return true;
            }
            return false;
        }

        void UpdateUnits()
        {
            foreach (Unit u in Unit.All)
            {
                bool structure = u.kind == UnitKind.Tower || u.kind == UnitKind.Core;
                // Dead enemy heroes stay hidden (their controller hides them; fog mustn't show them again).
                bool visible = u.team == _team || structure || (u.IsAlive && SeenBySources(u));
                u.IsVisibleToPlayer = visible;
                if (u.team == _team || structure) continue;

                if (!_unitVisuals.TryGetValue(u, out UnitVisual visual))
                {
                    visual = new UnitVisual
                    {
                        Renderers = u.GetComponentsInChildren<Renderer>(true),
                        Colliders = u.GetComponentsInChildren<Collider>(true),
                        Shown = true,
                    };
                }
                // Also re-hide when something else switched a hidden unit's renderers back on (e.g. a hero respawning).
                if (visual.Shown != visible || (!visible && AnyRendererOn(visual))) Show(u, visual, visible);
                visual.Shown = visible;
                _unitVisuals[u] = visual;
            }

            _stale.Clear();
            foreach (Unit u in _unitVisuals.Keys)
                if (u == null) _stale.Add(u);
            foreach (Unit u in _stale) _unitVisuals.Remove(u);
        }

        static bool AnyRendererOn(UnitVisual visual)
        {
            foreach (Renderer r in visual.Renderers)
                if (r != null && r.enabled) return true;
            return false;
        }

        static void Show(Unit unit, UnitVisual visual, bool shown)
        {
            foreach (Renderer r in visual.Renderers) if (r != null) r.enabled = shown;
            foreach (Collider c in visual.Colliders) if (c != null) c.enabled = shown;
        }

        void UpdateGrid()
        {
            System.Array.Clear(_visible, 0, _visible.Length);
            for (int i = 0; i < _target.Length; i++) _target[i] = fogDarkness;
            float cellX = (worldMax.x - worldMin.x) / resolution;
            float cellZ = (worldMax.y - worldMin.y) / resolution;

            foreach (Unit s in _sources)
            {
                Vector3 p = s.transform.position;
                float r = s.sightRadius;
                float inner = Mathf.Max(0f, r - edgeSoftness);
                int x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - r - worldMin.x) / cellX));
                int x1 = Mathf.Min(resolution - 1, Mathf.FloorToInt((p.x + r - worldMin.x) / cellX));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((p.z - r - worldMin.y) / cellZ));
                int z1 = Mathf.Min(resolution - 1, Mathf.FloorToInt((p.z + r - worldMin.y) / cellZ));
                float r2 = r * r;
                float inner2 = inner * inner;
                for (int z = z0; z <= z1; z++)
                {
                    float dz = worldMin.y + (z + 0.5f) * cellZ - p.z;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = worldMin.x + (x + 0.5f) * cellX - p.x;
                        float d2 = dx * dx + dz * dz;
                        if (d2 > r2) continue;
                        int i = z * resolution + x;
                        _visible[i] = true;
                        // Fully clear inside, fading to fog over the last edgeSoftness metres.
                        float darkness = d2 <= inner2 ? 0f : fogDarkness * Mathf.InverseLerp(inner, r, Mathf.Sqrt(d2));
                        if (darkness < _target[i]) _target[i] = darkness;
                    }
                }
            }

            float step = fadeSpeed * fogDarkness * Time.unscaledDeltaTime;
            for (int i = 0; i < _shown.Length; i++)
            {
                _shown[i] = Mathf.MoveTowards(_shown[i], _target[i], step);
                _pixels[i] = new Color32(0, 0, 0, (byte)(_shown[i] * 255f));
            }
            Texture.SetPixels32(_pixels);
            Texture.Apply(false);
        }

        void CollectProps()
        {
            _props.Clear();
            if (tintRoots == null) return;
            foreach (Transform root in tintRoots)
            {
                if (root == null) continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = r.sharedMaterial;
                    if (m == null || !m.HasProperty(BaseColorId)) continue;
                    _props.Add(new TintedProp
                    {
                        Renderer = r,
                        BaseColor = m.GetColor(BaseColorId),
                        Texel = TexelAt(r.bounds.center),
                    });
                }
            }
        }

        void UpdateProps()
        {
            foreach (TintedProp prop in _props)
            {
                if (prop.Renderer == null) continue;
                // Quantize so the property block is only rewritten when the tint visibly changes.
                float dim = Mathf.Round(_shown[prop.Texel] * 20f) / 20f;
                if (Mathf.Approximately(dim, prop.Applied)) continue;
                prop.Applied = dim;
                Color c = prop.BaseColor * (1f - dim);
                c.a = prop.BaseColor.a;
                prop.Renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                prop.Renderer.SetPropertyBlock(_block);
            }
        }

        int TexelAt(Vector3 world)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((world.x - worldMin.x) / (worldMax.x - worldMin.x) * resolution), 0, resolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((world.z - worldMin.y) / (worldMax.y - worldMin.y) * resolution), 0, resolution - 1);
            return z * resolution + x;
        }
    }
}
