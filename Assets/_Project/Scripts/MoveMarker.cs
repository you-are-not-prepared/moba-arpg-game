using UnityEngine;

namespace Moba
{
    // Short-lived ground ring showing where a move order was issued.
    public class MoveMarker : MonoBehaviour
    {
        const float Lifetime = 0.35f;
        float _age;

        public static void Spawn(Vector3 position, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "MoveMarker";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position + Vector3.up * 0.05f;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<MoveMarker>().UpdateScale();
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Lifetime) Destroy(gameObject);
            else UpdateScale();
        }

        void UpdateScale()
        {
            float s = Mathf.Lerp(1.2f, 0.2f, _age / Lifetime);
            transform.localScale = new Vector3(s, 0.02f, s);
        }
    }
}
