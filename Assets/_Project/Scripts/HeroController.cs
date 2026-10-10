using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Moba
{
    // Player hero: right-click ground to move (hold to keep steering), right-click an enemy to attack it,
    // S to stop. Respawns at its spawn point after dying.
    [RequireComponent(typeof(Unit), typeof(Attacker), typeof(NavMeshAgent))]
    public class HeroController : MonoBehaviour
    {
        public float respawnSeconds = 5f;
        public Transform spawnPoint;
        public Material moveMarkerMaterial;

        const float HoldRepathInterval = 0.1f;

        Unit _unit;
        Attacker _attacker;
        NavMeshAgent _agent;
        Camera _camera;
        Unit _attackTarget;
        float _nextHoldRepath;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        public Unit Unit => _unit;
        public float RespawnRemaining { get; private set; }

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
            _agent = GetComponent<NavMeshAgent>();
            _unit.destroyOnDeath = false;
            _unit.Died += OnDied;
        }

        void Start() => _camera = Camera.main;

        void Update()
        {
            if (!_unit.IsAlive)
            {
                RespawnRemaining -= Time.deltaTime;
                if (RespawnRemaining <= 0f) Respawn();
                return;
            }

            if (GameManager.Instance == null || !GameManager.Instance.IsOver) HandleInput();
            UpdateAttack();
        }

        void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.sKey.wasPressedThisFrame) Stop();

            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null) return;

            bool clicked = mouse.rightButton.wasPressedThisFrame;
            bool holding = mouse.rightButton.isPressed && _attackTarget == null && Time.time >= _nextHoldRepath;
            if (!clicked && !holding) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            int count = Physics.RaycastNonAlloc(ray, _hits, 500f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);

            if (clicked)
            {
                for (int i = 0; i < count; i++)
                {
                    Unit u = _hits[i].collider.GetComponentInParent<Unit>();
                    if (u != null && u.IsEnemyOf(_unit) && u.IsTargetable)
                    {
                        _attackTarget = u;
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.GetComponentInParent<Unit>() != null) continue;
                if (!NavMesh.SamplePosition(_hits[i].point, out NavMeshHit navHit, 4f, NavMesh.AllAreas)) return;

                _attackTarget = null;
                _agent.SetDestination(navHit.position);
                _nextHoldRepath = Time.time + HoldRepathInterval;
                if (clicked) MoveMarker.Spawn(navHit.position, moveMarkerMaterial);
                return;
            }
        }

        void UpdateAttack()
        {
            if (_attackTarget == null) return;
            if (!_attackTarget.IsTargetable)
            {
                Stop();
                return;
            }

            if (_attacker.InRange(_attackTarget))
            {
                if (_agent.hasPath) _agent.ResetPath();
                _attacker.TryAttack(_attackTarget);
            }
            else
            {
                _agent.SetDestination(_attackTarget.transform.position);
            }
        }

        void Stop()
        {
            _attackTarget = null;
            if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        void OnDied(Unit _)
        {
            Stop();
            RespawnRemaining = respawnSeconds;
            SetPresent(false);
        }

        void Respawn()
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            _unit.ResetHealth();
            SetPresent(true);
            _agent.Warp(pos);
        }

        void SetPresent(bool present)
        {
            _agent.enabled = present;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = present;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = present;
        }

        class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
