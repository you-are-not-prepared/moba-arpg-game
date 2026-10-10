using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Walks its lane toward the enemy base, fighting the closest enemy that comes within aggro range.
    [RequireComponent(typeof(Unit), typeof(Attacker), typeof(NavMeshAgent))]
    public class CreepAI : MonoBehaviour
    {
        public float aggroRange = 6f;
        [Tooltip("Drop the current target once it gets this much farther than aggroRange.")]
        public float leashExtra = 3f;
        public float retargetInterval = 0.25f;

        Unit _unit;
        Attacker _attacker;
        NavMeshAgent _agent;
        Vector3[] _lane;
        int _waypoint;
        Unit _target;
        float _nextRetarget;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
            _agent = GetComponent<NavMeshAgent>();
        }

        public void Init(Vector3[] lane)
        {
            _lane = lane;
            _waypoint = 0;
        }

        void Update()
        {
            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + retargetInterval;
                if (_target == null || !_target.IsTargetable || _unit.EdgeDistanceTo(_target) > aggroRange + leashExtra)
                    _target = _unit.FindClosestEnemy(aggroRange);
            }

            if (_target != null && _target.IsTargetable)
            {
                if (_attacker.InRange(_target))
                {
                    if (_agent.hasPath) _agent.ResetPath();
                    _attacker.TryAttack(_target);
                }
                else
                {
                    _agent.SetDestination(_target.transform.position);
                }
                return;
            }

            FollowLane();
        }

        void FollowLane()
        {
            if (_lane == null || _lane.Length == 0) return;

            // Skip waypoints we've already passed (e.g. after being pulled sideways by a fight).
            Vector3 pos = transform.position;
            while (_waypoint < _lane.Length - 1)
            {
                Vector3 segment = _lane[_waypoint + 1] - _lane[_waypoint];
                bool passed = Vector3.Dot(segment, pos - _lane[_waypoint]) > 0f;
                bool reached = (Flat(pos) - Flat(_lane[_waypoint])).sqrMagnitude < 4f;
                if (!passed && !reached) break;
                _waypoint++;
            }

            Vector3 goal = _lane[_waypoint];
            if (!_agent.hasPath || (Flat(_agent.destination) - Flat(goal)).sqrMagnitude > 1f)
                _agent.SetDestination(goal);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
