using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // No turn rate: the unit faces its next path corner immediately instead of rotating over time.
    // Pair with a high NavMeshAgent acceleration so velocity also changes direction instantly.
    [RequireComponent(typeof(NavMeshAgent))]
    public class InstantTurn : MonoBehaviour
    {
        NavMeshAgent _agent;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
        }

        void LateUpdate()
        {
            if (!_agent.enabled || !_agent.hasPath) return;
            Vector3 dir = _agent.steeringTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
