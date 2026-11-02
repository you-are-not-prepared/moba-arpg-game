using UnityEngine;

namespace Moba.Abilities
{
    // Runs an action after a delay (game time), e.g. a Burst's hit. Gone if the scene changes first.
    public class Delayed : MonoBehaviour
    {
        float _remaining;
        System.Action _action;

        public static void Run(float seconds, System.Action action)
        {
            var go = new GameObject("Delayed");
            var d = go.AddComponent<Delayed>();
            d._remaining = seconds;
            d._action = action;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining > 0f) return;
            Destroy(gameObject);
            _action?.Invoke();
        }
    }
}
