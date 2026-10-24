using System;
using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Moves a hero to a point in a quick arc (Oren's Blazing Leap), then calls `landed`. The NavMeshAgent stops
    // steering meanwhile and is warped to the landing spot. On a game server the arc reaches clients through the
    // position sync. Orders given during the leap wait until it lands.
    [DisallowMultipleComponent]
    public class Leap : MonoBehaviour
    {
        NavMeshAgent _agent;
        Vector3 _from, _to;
        float _time, _duration, _height;
        Action _landed;

        public static bool IsLeaping(Unit unit) => unit != null && unit.TryGetComponent(out Leap l) && l.enabled;

        public static void Perform(Unit unit, Vector3 to, float duration, float height, Action landed)
        {
            if (unit == null || !unit.IsAlive) return;
            if (!unit.TryGetComponent(out Leap leap)) leap = unit.gameObject.AddComponent<Leap>();
            leap.Begin(to, duration, height, landed);
        }

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        void Begin(Vector3 to, float duration, float height, Action landed)
        {
            _from = transform.position;
            _to = to;
            _duration = Mathf.Max(0.05f, duration);
            _height = height;
            _time = 0f;
            _landed = landed;
            Vector3 facing = to - _from;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
            if (_agent != null && _agent.enabled)
            {
                _agent.ResetPath();
                _agent.updatePosition = false;
            }
            enabled = true;
        }

        void Update()
        {
            _time += Time.deltaTime;
            float p = Mathf.Clamp01(_time / _duration);
            transform.position = Vector3.Lerp(_from, _to, p) + Vector3.up * (Mathf.Sin(p * Mathf.PI) * _height);
            if (p < 1f) return;
            Land();
        }

        void Land()
        {
            enabled = false;
            if (_agent != null && _agent.enabled)
            {
                _agent.updatePosition = true;
                _agent.Warp(_to);
            }
            transform.position = _to;
            Action landed = _landed;
            _landed = null;
            landed?.Invoke();
        }

        void OnDisable()
        {
            if (_agent != null && _agent.enabled) _agent.updatePosition = true;
        }
    }
}
