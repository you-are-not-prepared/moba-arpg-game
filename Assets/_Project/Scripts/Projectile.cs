using UnityEngine;

namespace Moba
{
    // Homing projectile that always hits unless its target dies first.
    public class Projectile : MonoBehaviour
    {
        Unit _source;
        Unit _target;
        Team _team;
        bool _blockable;
        bool _visualOnly;
        Renderer _renderer;
        float _damage;
        float _speed;

        // visualOnly: just the shot (game-server clients); the damage is dealt by the server.
        public static void Launch(Vector3 from, Unit source, Unit target, float damage, float speed, float size,
            Material material, bool visualOnly = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(from, Quaternion.identity);
            go.transform.localScale = Vector3.one * size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;

            Projectile p = go.AddComponent<Projectile>();
            p._source = source;
            p._team = source != null ? source.team : Team.Red;
            // Tower shots aren't stopped by shields.
            p._blockable = source == null || source.kind != UnitKind.Tower;
            p._renderer = go.GetComponent<Renderer>();
            p._target = target;
            p._damage = damage;
            p._speed = speed;
            p._visualOnly = visualOnly;
        }

        void Update()
        {
            if (_target == null || !_target.IsTargetable)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 aim = _target.AimPoint;
            Vector3 from = transform.position;
            transform.position = Vector3.MoveTowards(from, aim, _speed * Time.deltaTime);
            if (_blockable && ProjectileShield.TryBlock(_team, from, transform.position, out _))
            {
                Destroy(gameObject);
                return;
            }
            _renderer.enabled = FogOfWar.ShouldShow(_team, transform.position);
            if ((transform.position - aim).sqrMagnitude < 0.04f)
            {
                if (!_visualOnly) _target.TakeDamage(_damage, _source);
                Destroy(gameObject);
            }
        }
    }
}
