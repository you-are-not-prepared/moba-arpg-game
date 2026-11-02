using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // A hero's six affinities as a radar hexagon (Fire at the top, then clockwise in DamageTypes.All order), with
    // each type's dot and value around it. The dashed ring is 0%; the centre is MinValue, the outer edge MaxValue.
    public class AffinityHexagon : VisualElement
    {
        const float MinValue = -0.3f;
        const float MaxValue = 0.3f;
        // Wide enough for the side labels ("Physical -10%") to fit beside the hexagon.
        const float Width = 330f;
        const float Height = 250f;
        const float Radius = 74f;
        const float DotRadius = 7f;
        const float LabelRadius = Radius + 22f;

        static readonly Color Grid = new(0.3f, 0.34f, 0.42f);
        static readonly Color ZeroRing = new(0.95f, 0.78f, 0.3f, 0.7f);

        readonly Label[] _labels = new Label[DamageTypes.Count];
        float[] _values = new float[DamageTypes.Count];
        Color _color = Color.white;

        public AffinityHexagon()
        {
            pickingMode = PickingMode.Ignore;
            style.width = Width;
            style.height = Height;
            generateVisualContent += Draw;
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("affinity-hex__label");
                label.style.color = DamageTypes.ColorOf(DamageTypes.All[i]);
                _labels[i] = label;
                Add(label);
            }
        }

        public void SetHero(HeroDefinition hero)
        {
            _values = new float[DamageTypes.Count];
            if (hero?.affinities != null)
                for (int i = 0; i < DamageTypes.Count && i < hero.affinities.Length; i++) _values[i] = hero.affinities[i];
            _color = DamageTypes.ColorOf(hero != null ? hero.BaseDamageType : DamageType.None);

            Vector2 center = new(Width * 0.5f, Height * 0.5f);
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                Label label = _labels[i];
                float pct = _values[i] * 100f;
                label.text = $"{DamageTypes.Name(DamageTypes.All[i])} {(pct > 0f ? "+" : "")}{pct:0}%";
                // Centred on the label point: left/right side labels hang outward so they don't cover the hexagon.
                bool right = Direction(i).x > 0.1f, left = Direction(i).x < -0.1f;
                // Top and bottom labels sit further out, clear of their dots.
                Vector2 p = center + Direction(i) * (right || left ? LabelRadius : LabelRadius + 10f);
                label.style.position = Position.Absolute;
                label.style.top = p.y - 10f;
                label.style.left = right ? p.x + 2f : left ? StyleKeyword.Auto : p.x - 40f;
                label.style.right = left ? Width - p.x + 2f : StyleKeyword.Auto;
                label.style.width = right || left ? StyleKeyword.Auto : 80f;
                label.style.unityTextAlign = right ? TextAnchor.MiddleLeft : left ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            }
            MarkDirtyRepaint();
        }

        // Unit vector from the centre to type i's corner (screen space, y down): 0 = top, then clockwise.
        static Vector2 Direction(int i)
        {
            float angle = (-90f + 60f * i) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        static float Scale(float value) => Mathf.Clamp01((value - MinValue) / (MaxValue - MinValue));

        void Draw(MeshGenerationContext mgc)
        {
            Painter2D p = mgc.painter2D;
            Vector2 center = new(Width * 0.5f, Height * 0.5f);

            // Grid: three rings and the spokes.
            p.lineWidth = 1f;
            p.strokeColor = Grid;
            for (int ring = 1; ring <= 3; ring++) Polygon(p, center, Radius * ring / 3f, null, stroke: true, fill: false);
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                p.BeginPath();
                p.MoveTo(center);
                p.LineTo(center + Direction(i) * Radius);
                p.Stroke();
            }
            p.lineWidth = 1.5f;
            p.strokeColor = ZeroRing;
            Polygon(p, center, Radius * Scale(0f), null, stroke: true, fill: false);

            // The hero's shape.
            p.fillColor = new Color(_color.r, _color.g, _color.b, 0.28f);
            p.strokeColor = _color;
            p.lineWidth = 2.5f;
            Polygon(p, center, Radius, _values, stroke: true, fill: true);
            p.fillColor = _color;
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                p.BeginPath();
                p.Arc(center + Direction(i) * Radius * Scale(_values[i]), 3f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            // Each type's dot just outside its corner.
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                p.fillColor = DamageTypes.ColorOf(DamageTypes.All[i]);
                p.BeginPath();
                p.Arc(center + Direction(i) * (Radius + 10f), DotRadius, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }

        // A hexagon of `radius`, or with `values` the hero's shape (each corner at its value's distance).
        static void Polygon(Painter2D p, Vector2 center, float radius, float[] values, bool stroke, bool fill)
        {
            p.BeginPath();
            for (int i = 0; i < DamageTypes.Count; i++)
            {
                float r = values != null ? radius * Scale(values[i]) : radius;
                Vector2 point = center + Direction(i) * r;
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
            if (fill) p.Fill();
            if (stroke) p.Stroke();
        }
    }
}
