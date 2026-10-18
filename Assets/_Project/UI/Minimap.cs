using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Top-down map of the play area, drawn in layers: lane, fog of war, then units and the camera's view.
    // LMB (click or drag) moves the camera there; RMB orders the hero to move there.
    public class Minimap : VisualElement
    {
        public Vector2 worldMin = new(-70f, -70f);
        public Vector2 worldMax = new(70f, 70f);

        static readonly Color LaneColor = new(0.62f, 0.52f, 0.36f);
        static readonly Color AllyColor = new(0.3f, 0.6f, 1f);
        static readonly Color EnemyColor = new(0.95f, 0.3f, 0.25f);
        static readonly Color SelfColor = new(1f, 0.85f, 0.25f);
        static readonly Color ViewColor = new(1f, 1f, 1f, 0.85f);

        static readonly Vector2[] ViewportCorners = { new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f) };
        readonly Vector2[] _view = new Vector2[4];
        readonly VisualElement _terrain;
        readonly VisualElement _fog;
        readonly VisualElement _overlay;
        Texture2D _fogTexture;
        bool _dragging;

        public Minimap()
        {
            AddToClassList("minimap");

            _terrain = Layer();
            _terrain.generateVisualContent += DrawTerrain;
            _fog = Layer();
            _overlay = Layer();
            _overlay.generateVisualContent += DrawOverlay;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        VisualElement Layer()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("minimap__layer");
            Add(layer);
            return layer;
        }

        // Called every frame by the HUD: redraws units/camera and picks up the fog texture once it exists.
        public void Refresh()
        {
            Texture2D fog = FogOfWar.Instance != null && FogOfWar.Instance.isActiveAndEnabled ? FogOfWar.Instance.Texture : null;
            if (fog != _fogTexture)
            {
                _fogTexture = fog;
                // The fog texture is runtime data, so it's assigned here rather than in USS.
                _fog.style.backgroundImage = fog != null ? new StyleBackground(fog) : new StyleBackground(StyleKeyword.None);
            }
            _terrain.MarkDirtyRepaint();
            _overlay.MarkDirtyRepaint();
        }

        public Vector2 WorldToLocal(Vector3 world)
        {
            Rect r = contentRect;
            return new Vector2(
                Mathf.InverseLerp(worldMin.x, worldMax.x, world.x) * r.width,
                (1f - Mathf.InverseLerp(worldMin.y, worldMax.y, world.z)) * r.height);
        }

        public Vector3 LocalToWorld(Vector2 local)
        {
            Rect r = contentRect;
            return new Vector3(
                Mathf.Lerp(worldMin.x, worldMax.x, local.x / r.width),
                0f,
                Mathf.Lerp(worldMin.y, worldMax.y, 1f - local.y / r.height));
        }

        void DrawTerrain(MeshGenerationContext ctx)
        {
            if (contentRect.width < 1f || contentRect.height < 1f) return;
            MapLayout layout = MapLayout.Current;
            if (layout == null || layout.info?.lanes == null) return;

            Painter2D p = ctx.painter2D;
            p.strokeColor = LaneColor;
            p.lineWidth = 8f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (LanePath lane in layout.info.lanes)
            {
                if (lane.points == null || lane.points.Length < 2) continue;
                p.BeginPath();
                p.MoveTo(WorldToLocal(lane.points[0]));
                for (int i = 1; i < lane.points.Length; i++) p.LineTo(WorldToLocal(lane.points[i]));
                p.Stroke();
            }
        }

        void DrawOverlay(MeshGenerationContext ctx)
        {
            if (contentRect.width < 1f || contentRect.height < 1f) return;
            GameManager game = GameManager.Instance;
            if (game == null) return;
            Painter2D p = ctx.painter2D;

            Unit self = game.hero != null ? game.hero.Unit : null;
            foreach (Unit u in Unit.All)
            {
                if (!u.IsAlive || u == self || !u.IsVisibleToPlayer) continue;
                Color c = u.team == game.playerTeam ? AllyColor : EnemyColor;
                float radius = u.kind switch
                {
                    UnitKind.Core => 7f,
                    UnitKind.Tower => 5f,
                    UnitKind.Hero => 5f,
                    _ => 2.2f,
                };
                if (u.kind == UnitKind.Tower || u.kind == UnitKind.Core) DrawSquare(p, WorldToLocal(u.transform.position), radius, c);
                else DrawDot(p, WorldToLocal(u.transform.position), radius, c);
            }

            if (self != null && self.IsAlive)
            {
                Vector2 pos = WorldToLocal(self.transform.position);
                DrawDot(p, pos, 6f, Color.black);
                DrawDot(p, pos, 4.5f, SelfColor);
            }

            DrawCameraView(p);
        }

        void DrawCameraView(Painter2D p)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var ground = new Plane(Vector3.up, Vector3.zero);
            for (int i = 0; i < 4; i++)
            {
                Ray ray = cam.ViewportPointToRay(ViewportCorners[i]);
                if (!ground.Raycast(ray, out float t)) return;
                _view[i] = WorldToLocal(ray.GetPoint(t));
            }

            p.strokeColor = ViewColor;
            p.lineWidth = 1.5f;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(_view[0]);
            for (int i = 1; i < 4; i++) p.LineTo(_view[i]);
            p.ClosePath();
            p.Stroke();
        }

        static void DrawDot(Painter2D p, Vector2 center, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
        }

        static void DrawSquare(Painter2D p, Vector2 center, float half, Color color)
        {
            p.fillColor = Color.black;
            p.BeginPath();
            p.MoveTo(center + new Vector2(-half - 1.5f, -half - 1.5f));
            p.LineTo(center + new Vector2(half + 1.5f, -half - 1.5f));
            p.LineTo(center + new Vector2(half + 1.5f, half + 1.5f));
            p.LineTo(center + new Vector2(-half - 1.5f, half + 1.5f));
            p.ClosePath();
            p.Fill();
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(center + new Vector2(-half, -half));
            p.LineTo(center + new Vector2(half, -half));
            p.LineTo(center + new Vector2(half, half));
            p.LineTo(center + new Vector2(-half, half));
            p.ClosePath();
            p.Fill();
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            Vector3 world = LocalToWorld(evt.localPosition);

            if (evt.button == 0 && game.cameraRig != null)
            {
                game.cameraRig.FocusOn(world);
                _dragging = true;
                this.CapturePointer(evt.pointerId);
            }
            else if (evt.button == 1 && game.hero != null && !game.IsOver)
            {
                game.hero.OrderMove(world);
            }
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            GameManager game = GameManager.Instance;
            if (game != null && game.cameraRig != null) game.cameraRig.FocusOn(LocalToWorld(evt.localPosition));
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging || evt.button != 0) return;
            _dragging = false;
            this.ReleasePointer(evt.pointerId);
        }
    }
}
