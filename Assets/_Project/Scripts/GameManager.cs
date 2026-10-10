using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Moba
{
    // Ends the match when a core dies; R restarts.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Team playerTeam = Team.Blue;
        public HeroController hero;
        public CreepSpawner playerSpawner;

        public Team? Winner { get; private set; }
        public bool IsOver => Winner.HasValue;

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

        void OnUnitDied(Unit unit)
        {
            if (unit.kind != UnitKind.Core || IsOver) return;
            Winner = unit.team == Team.Blue ? Team.Red : Team.Blue;
            Time.timeScale = 0f;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (IsOver && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
