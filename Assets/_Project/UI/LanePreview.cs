using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // A small top-down drawing of a map layout's lanes and bases (champion select's map card).
    public class LanePreview : VisualElement
    {
        static readonly Color LaneColor = new(0.62f, 0.52f, 0.36f);
        static readonly Color BlueColor = new(0.3f, 0.6f, 1f);
        static readonly Color RedColor = new(0.95f, 0.3f, 0.25f);

        public Vector2 worldMin = new(-70f, -70f);
        public Vector2 worldMax = new(70f, 70f);

        MapLayoutInfo _layout;

        public LanePreview()
        {
            style.flexGrow = 1;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Show(MapLayoutInfo layout)
        {
            _layout = layout;
            MarkDirtyRepaint();
        }

        Vector2 ToLocal(Vector3 world)
        {
            Rect r = contentRect;
            return new Vector2(
                Mathf.InverseLerp(worldMin.x, worldMax.x, world.x) * r.width,
                (1f - Mathf.InverseLerp(worldMin.y, worldMax.y, world.z)) * r.height);
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (_layout?.lanes == null || contentRect.width < 1f) return;
            Painter2D p = ctx.painter2D;
            p.strokeColor = LaneColor;
            p.lineWidth = 6f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (LanePath lane in _layout.lanes)
            {
                if (lane.points == null || lane.points.Length < 2) continue;
                p.BeginPath();
                p.MoveTo(ToLocal(lane.points[0]));
                for (int i = 1; i < lane.points.Length; i++) p.LineTo(ToLocal(lane.points[i]));
                p.Stroke();
            }
            Base(p, ToLocal(_layout.blueCore), BlueColor);
            Base(p, ToLocal(_layout.redCore), RedColor);
        }

        static void Base(Painter2D p, Vector2 c, Color color)
        {
            const float half = 7f;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-half, -half));
            p.LineTo(c + new Vector2(half, -half));
            p.LineTo(c + new Vector2(half, half));
            p.LineTo(c + new Vector2(-half, half));
            p.ClosePath();
            p.Fill();
        }
    }
}
