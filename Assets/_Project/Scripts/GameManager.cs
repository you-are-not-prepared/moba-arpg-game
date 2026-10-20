using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba
{
    // Ends the match when a core dies; the Restart key (or the HUD button) goes back to champion select for a new
    // match on a freshly rolled map. Owns pausing.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Team playerTeam = Team.Blue;
        // The local player's hero.
        public HeroController hero;
        // Every hero in the match (player and bots), filled by MatchBootstrap.
        [System.NonSerialized] public System.Collections.Generic.List<HeroController> heroes = new();
        public CreepSpawner playerSpawner;
        public MobaCamera cameraRig;
        public Items.Shop playerShop;

        bool _paused;

        public Team? Winner { get; private set; }
        // Raised once, with the winning team, when a core falls.
        public event System.Action<Team> MatchEnded;
        // Set when the cheat menu is used; such matches stay out of the player's stats.
        public bool CheatsUsed { get; set; }
        public bool IsOver => Winner.HasValue;
        public bool IsPaused => _paused;
        public float MatchTime => Time.timeSinceLevelLoad;

        void Awake()
        {
            Instance = this;
            ApplyTimeScale();
        }

        void OnEnable() => Unit.AnyDied += OnUnitDied;
        void OnDisable() => Unit.AnyDied -= OnUnitDied;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnUnitDied(Unit unit, Unit killer)
        {
            if (unit.kind != UnitKind.Core || IsOver) return;
            Winner = unit.team == Team.Blue ? Team.Red : Team.Blue;
            ApplyTimeScale();
            MatchEnded?.Invoke(Winner.Value);
        }

        void Update()
        {
            if (IsOver && !_paused && GameInput.Instance.Restart.WasPressedThisFrame()) Restart();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            ApplyTimeScale();
        }

        // "Play again": champion select with a new map roll.
        public void Restart()
        {
            _paused = false;
            MatchSettings.OpenChampionSelect = true;
            SceneManager.LoadScene(MatchSettings.FrontendScene);
        }

        public void LeaveToMenu()
        {
            _paused = false;
            MatchSettings.OpenChampionSelect = false;
            SceneManager.LoadScene(MatchSettings.FrontendScene);
        }

        void ApplyTimeScale() => Time.timeScale = _paused || IsOver ? 0f : 1f;
    }
}
