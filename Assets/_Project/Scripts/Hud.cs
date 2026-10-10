using UnityEngine;

namespace Moba
{
    // Placeholder IMGUI HUD: world-space health bars, hero status, wave timer and end-of-match banner.
    // Replace with a proper UI Toolkit / uGUI HUD once the game loop settles.
    [RequireComponent(typeof(GameManager))]
    public class Hud : MonoBehaviour
    {
        static readonly Color Ally = new(0.25f, 0.6f, 1f);
        static readonly Color Enemy = new(0.95f, 0.3f, 0.25f);
        static readonly Color Self = new(0.35f, 0.9f, 0.35f);
        static readonly Color Backing = new(0f, 0f, 0f, 0.65f);

        GameManager _game;
        Camera _camera;
        GUIStyle _label;
        GUIStyle _banner;

        void Awake() => _game = GetComponent<GameManager>();

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            EnsureStyles();

            if (Event.current.type == EventType.Repaint) DrawHealthBars();
            DrawStatus();
            if (_game.IsOver) DrawBanner();
        }

        void DrawHealthBars()
        {
            HeroController hero = _game.hero;
            foreach (Unit u in Unit.All)
            {
                if (!u.IsAlive) continue;
                Vector3 screen = _camera.WorldToScreenPoint(u.transform.position + Vector3.up * u.barHeight);
                if (screen.z <= 0f) continue;

                float width = u.kind switch
                {
                    UnitKind.Hero => 70f,
                    UnitKind.Tower => 90f,
                    UnitKind.Core => 120f,
                    _ => 34f,
                };
                float height = u.kind == UnitKind.Creep ? 4f : 7f;
                var rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height, width, height);

                Color fill = hero != null && u == hero.Unit ? Self : u.team == _game.playerTeam ? Ally : Enemy;
                if (!u.IsTargetable) fill = Color.Lerp(fill, Color.gray, 0.6f);

                DrawRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), Backing);
                DrawRect(new Rect(rect.x, rect.y, rect.width * (u.Health / u.maxHealth), rect.height), fill);
            }
        }

        void DrawStatus()
        {
            HeroController hero = _game.hero;
            var lines = new System.Text.StringBuilder();
            if (hero != null)
            {
                lines.AppendLine(hero.Unit.IsAlive
                    ? $"Hero HP  {Mathf.CeilToInt(hero.Unit.Health)} / {hero.Unit.maxHealth:0}"
                    : $"Respawning in {Mathf.CeilToInt(hero.RespawnRemaining)}");
            }
            if (_game.playerSpawner != null)
                lines.AppendLine($"Next wave in {Mathf.CeilToInt(_game.playerSpawner.NextWaveIn)}");
            lines.Append("RMB move/attack · S stop · Y lock cam · Space centre · Scroll zoom");

            GUI.Label(new Rect(16f, 12f, 600f, 80f), lines.ToString(), _label);
        }

        void DrawBanner()
        {
            bool won = _game.Winner == _game.playerTeam;
            DrawRect(new Rect(0f, Screen.height * 0.5f - 60f, Screen.width, 120f), Backing);
            _banner.normal.textColor = won ? Self : Enemy;
            GUI.Label(new Rect(0f, Screen.height * 0.5f - 60f, Screen.width, 80f), won ? "VICTORY" : "DEFEAT", _banner);
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 20f, Screen.width, 30f), "Press R to restart",
                new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = false };
            _label.normal.textColor = Color.white;
            _banner = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
