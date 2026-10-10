using UnityEngine;

namespace Moba
{
    // Flat circle on the ground (cast range, area-of-effect preview).
    [RequireComponent(typeof(LineRenderer))]
    public class RangeIndicator : MonoBehaviour
    {
        const int Segments = 64;
        readonly Vector3[] _points = new Vector3[Segments];
        LineRenderer _line;

        public static RangeIndicator Create(string name, Material material, Color color, float width)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var indicator = go.AddComponent<RangeIndicator>();
            indicator._line = line;
            indicator.Hide();
            return indicator;
        }

        public void Show(Vector3 center, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                _points[i] = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + 0.06f, center.z + Mathf.Sin(a) * radius);
            }
            _line.SetPositions(_points);
            _line.enabled = true;
        }

        public void Hide() => _line.enabled = false;
    }
}
