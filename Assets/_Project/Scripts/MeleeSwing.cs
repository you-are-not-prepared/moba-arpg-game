using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    // The look of a melee attack: the attacker's model lunges at the target and back while a bright arc sweeps
    // across in front of it (Moba/MeleeSlash), alternating sides. Visual only: Attacker deals the hit, and on a
    // game server the clients play this when NetworkMatch tells them. Added to the attacker on its first swing.
    [DisallowMultipleComponent]
    public class MeleeSwing : MonoBehaviour
    {
        const float Duration = 0.3f;
        const float LungeDistance = 0.35f;
        const float ArcDegrees = 150f;
        const int ArcSegments = 18;
        const float BladeWidth = 0.7f;
        // Tilt of the arc around the attack direction, so it reads as a diagonal slash.
        const float Tilt = 25f;

        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        // Children that are effects rather than the model, so they don't lunge.
        static readonly HashSet<string> s_Effects = new() { "MeleeSlash", "HasteAura", "ShieldWall" };

        Unit _unit;
        readonly List<Transform> _parts = new();
        readonly List<Vector3> _rest = new();
        Transform _slash;
        Renderer _slashRenderer;
        MaterialPropertyBlock _block;
        Mesh _mesh;
        Vector3 _lungeLocal;
        float _time;
        bool _flip;

        public static void Play(Unit attacker, Unit target, Material slashMaterial)
        {
            if (attacker == null || target == null || slashMaterial == null) return;
            // Hidden in fog of war: no swing either.
            if (!FogOfWar.ShouldShow(attacker.team, attacker.transform.position)) return;
            if (!attacker.TryGetComponent(out MeleeSwing swing)) swing = attacker.gameObject.AddComponent<MeleeSwing>();
            swing.Begin(target, slashMaterial);
        }

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _block = new MaterialPropertyBlock();
            foreach (Transform child in transform)
            {
                if (s_Effects.Contains(child.name) || child.GetComponent<Renderer>() == null) continue;
                _parts.Add(child);
                _rest.Add(child.localPosition);
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] != null) _parts[i].localPosition = _rest[i];
            if (_slashRenderer != null) _slashRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void Begin(Unit target, Material material)
        {
            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();
            _lungeLocal = transform.InverseTransformDirection(dir);

            if (_slash == null) CreateSlash();
            _slashRenderer.sharedMaterial = material;
            // In front of the attacker at chest height, facing the target; every other swing comes from the other side.
            _flip = !_flip;
            _slash.localPosition = Vector3.up * (_unit.barHeight * 0.45f);
            _slash.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, _flip ? Tilt : -Tilt);
            _slash.localScale = new Vector3(_flip ? -1f : 1f, 1f, 1f);
            _slashRenderer.enabled = true;

            _time = 0f;
            SetProgress(0f);
            enabled = true;
        }

        void Update()
        {
            _time += Time.deltaTime;
            float p = Mathf.Clamp01(_time / Duration);
            // Out fast, back slower: the peak lands early, when the hit does.
            float lunge = Mathf.Sin(Mathf.Pow(p, 0.55f) * Mathf.PI) * LungeDistance;
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] != null) _parts[i].localPosition = _rest[i] + _lungeLocal * lunge;
            SetProgress(p);
            if (p >= 1f) enabled = false;
        }

        void SetProgress(float p)
        {
            _slashRenderer.GetPropertyBlock(_block);
            _block.SetFloat(ProgressId, p);
            _slashRenderer.SetPropertyBlock(_block);
        }

        void CreateSlash()
        {
            var go = new GameObject("MeleeSlash");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh = BuildArc(_unit.radius + 0.45f);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _slash = go.transform;
            _slashRenderer = renderer;
        }

        // A flat band around the attacker, centred on local +Z: UV.x along the arc (from -X to +X), UV.y outward.
        static Mesh BuildArc(float innerRadius)
        {
            var vertices = new Vector3[(ArcSegments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[ArcSegments * 6];
            float half = ArcDegrees * 0.5f;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float u = (float)i / ArcSegments;
                float angle = Mathf.Lerp(-half, half, u) * Mathf.Deg2Rad;
                var along = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = along * innerRadius;
                vertices[i * 2 + 1] = along * (innerRadius + BladeWidth);
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
                if (i == ArcSegments) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = "MeleeSlash" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
