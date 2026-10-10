using UnityEngine;

namespace Moba.Abilities
{
    // Short expanding blast showing an explosion's radius. Visual only.
    public class SpellExplosion : MonoBehaviour
    {
        const float Lifetime = 0.25f;
        float _age;
        float _radius;

        public static void Spawn(Vector3 center, float radius, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpellExplosion";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = center + Vector3.up * 0.4f;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            var e = go.AddComponent<SpellExplosion>();
            e._radius = radius;
            e.UpdateScale();
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Lifetime) Destroy(gameObject);
            else UpdateScale();
        }

        void UpdateScale()
        {
            float d = Mathf.Lerp(0.4f, 2f, _age / Lifetime) * _radius;
            transform.localScale = new Vector3(d, d * 0.45f, d);
        }
    }
}
