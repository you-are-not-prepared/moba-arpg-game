using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Moba
{
    // Ends the match when a core dies; R (or the HUD button) restarts.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Team playerTeam = Team.Blue;
        public HeroController hero;
        public CreepSpawner playerSpawner;
        public MobaCamera cameraRig;

        public Team? Winner { get; private set; }
        public bool IsOver => Winner.HasValue;
        public float MatchTime => Time.timeSinceLevelLoad;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
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
            Time.timeScale = 0f;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (IsOver && keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
