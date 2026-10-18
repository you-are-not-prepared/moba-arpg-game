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
        public HeroController hero;
        public CreepSpawner playerSpawner;
        public MobaCamera cameraRig;
        public Items.Shop playerShop;

        bool _paused;

        public Team? Winner { get; private set; }
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
