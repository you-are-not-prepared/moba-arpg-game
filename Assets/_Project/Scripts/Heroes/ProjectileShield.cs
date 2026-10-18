using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // A raised shield: a curved wall in front of its hero that destroys enemy projectiles flying into it from the
    // front. It moves and turns with the hero. Tower shots go through (as with a wind wall in other MOBAs).
    // Projectiles ask TryBlock every frame with the segment they are about to travel.
    [DisallowMultipleComponent]
    public class ProjectileShield : MonoBehaviour
    {
        const int ArcSegments = 28;
        static readonly List<ProjectileShield> s_Active = new();

        Unit _owner;
        float _remaining;
        float _radius;
        float _cosHalfArc;
        Material _flashMaterial;
        GameObject _wall;
        MeshFilter _wallFilter;
        Renderer _wallRenderer;
        Mesh _mesh;
        float _meshRadius, _meshArc, _meshHeight;

        public float Remaining => _remaining;

        public static bool IsRaised(Unit unit) =>
            unit != null && unit.TryGetComponent(out ProjectileShield s) && s.enabled && s._remaining > 0f;

        public static void Raise(Unit owner, float duration, float radius, float arcDegrees, float height,
            Material wallMaterial, Material flashMaterial)
        {
            if (owner == null || !owner.IsAlive || duration <= 0f) return;
            if (!owner.TryGetComponent(out ProjectileShield shield)) shield = owner.gameObject.AddComponent<ProjectileShield>();
            shield.Setup(duration, radius, arcDegrees, height, wallMaterial, flashMaterial);
        }

        // True if a projectile of `team` moving from `from` to `to` this frame runs into an enemy shield's front.
        // `hitPoint` is where it struck the wall.
        public static bool TryBlock(Team team, Vector3 from, Vector3 to, out Vector3 hitPoint)
        {
            foreach (ProjectileShield s in s_Active)
            {
                if (s._owner.team == team || !s._owner.IsAlive) continue;
                if (s.Intersects(from, to, out hitPoint))
                {
                    if (s._flashMaterial != null) SpellExplosion.Spawn(hitPoint, 0.6f, s._flashMaterial);
                    return true;
                }
            }
            hitPoint = default;
            return false;
        }

        void Awake() => _owner = GetComponent<Unit>();

        void OnEnable()
        {
            if (!s_Active.Contains(this)) s_Active.Add(this);
        }

        void OnDisable()
        {
            s_Active.Remove(this);
            if (_wall != null) _wall.SetActive(false);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void Setup(float duration, float radius, float arcDegrees, float height, Material wallMaterial, Material flashMaterial)
        {
            _remaining = duration;
            _radius = radius;
            _cosHalfArc = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
            _flashMaterial = flashMaterial;

            if (_wall == null)
            {
                _wall = new GameObject("ShieldWall");
                _wall.transform.SetParent(transform, false);
                _wallFilter = _wall.AddComponent<MeshFilter>();
                var renderer = _wall.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _wallRenderer = renderer;
            }
            _wallRenderer.sharedMaterial = wallMaterial;
            if (_mesh == null || _meshRadius != radius || _meshArc != arcDegrees || _meshHeight != height)
            {
                if (_mesh != null) Destroy(_mesh);
                _mesh = BuildWallMesh(radius, arcDegrees, height);
                _wallFilter.sharedMesh = _mesh;
                _meshRadius = radius;
                _meshArc = arcDegrees;
                _meshHeight = height;
            }
            _wall.SetActive(true);
            enabled = true;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f || !_owner.IsAlive)
            {
                _remaining = 0f;
                enabled = false;
                return;
            }
            // Drawn only where the local player can see it (enemy shields in fog stay hidden).
            _wallRenderer.enabled = FogOfWar.ShouldShow(_owner.team, transform.position);
        }

        // Flat (XZ) segment vs the shield's arc: the projectile must cross the circle from outside to inside,
        // and the crossing point must lie within the arc in front of the owner.
        bool Intersects(Vector3 from, Vector3 to, out Vector3 hitPoint)
        {
            hitPoint = default;
            Vector3 c = _owner.transform.position;
            var a = new Vector2(from.x - c.x, from.z - c.z);
            var d = new Vector2(to.x - from.x, to.z - from.z);
            float A = Vector2.Dot(d, d);
            if (A < 1e-6f) return false;
            float B = 2f * Vector2.Dot(a, d);
            float C = Vector2.Dot(a, a) - _radius * _radius;
            if (C < 0f) return false; // started inside the shield's circle
            float disc = B * B - 4f * A * C;
            if (disc < 0f) return false;
            float t = (-B - Mathf.Sqrt(disc)) / (2f * A);
            if (t < 0f || t > 1f) return false;

            Vector2 q = a + d * t;
            Vector3 forward = _owner.transform.forward;
            var f = new Vector2(forward.x, forward.z).normalized;
            if (Vector2.Dot(q.normalized, f) < _cosHalfArc) return false;

            hitPoint = new Vector3(c.x + q.x, c.y, c.z + q.y);
            return true;
        }

        // A curved band standing on the ground, centred on local +Z. UV.x runs along the arc, UV.y up the wall.
        static Mesh BuildWallMesh(float radius, float arcDegrees, float height)
        {
            var vertices = new Vector3[(ArcSegments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[ArcSegments * 6];
            float half = arcDegrees * 0.5f;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float u = (float)i / ArcSegments;
                float angle = Mathf.Lerp(-half, half, u) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                vertices[i * 2] = p + Vector3.up * 0.05f;
                vertices[i * 2 + 1] = p + Vector3.up * height;
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
            var mesh = new Mesh { name = "ShieldWall" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
