using Moba.Abilities;
using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Hero passive (Thessa): every spell cast gives a burst of movement speed (and, with talents, attack speed) for a
    // few seconds; casting again refreshes it. Added by HeroDefinition.ApplyTo; ascension talents raise the numbers.
    [RequireComponent(typeof(AbilityCaster))]
    public class TailwindPassive : MonoBehaviour
    {
        [Tooltip("0.25 = 25% faster while Tailwind lasts.")]
        public float moveSpeedBonus = 0.25f;
        public float duration = 2f;
        [Tooltip("0.2 = 20% more attack speed while Tailwind lasts (talents).")]
        public float attackSpeedBonus;

        AbilityCaster _caster;
        NavMeshAgent _agent;
        Attacker _attacker;
        float _remaining;
        float _appliedMove = 1f;
        float _appliedAttack = 1f;

        public bool IsActive => _remaining > 0f;
        public float Remaining => _remaining;

        void Awake()
        {
            _caster = GetComponent<AbilityCaster>();
            _agent = GetComponent<NavMeshAgent>();
            _attacker = GetComponent<Attacker>();
        }

        void OnEnable() => _caster.Casted += OnCasted;

        void OnDisable()
        {
            _caster.Casted -= OnCasted;
            _remaining = 0f;
            SetMultipliers(1f, 1f);
        }

        void OnCasted(int slot)
        {
            _remaining = duration;
            SetMultipliers(1f + moveSpeedBonus, 1f + attackSpeedBonus);
        }

        void Update()
        {
            if (_remaining <= 0f) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) SetMultipliers(1f, 1f);
        }

        // Swaps the previously applied multipliers for new ones, so other speed changes are kept (as Haste does).
        void SetMultipliers(float move, float attack)
        {
            if (_agent != null) _agent.speed = _agent.speed / _appliedMove * move;
            if (_attacker != null) _attacker.attackSpeedMultiplier = _attacker.attackSpeedMultiplier / _appliedAttack * attack;
            _appliedMove = move;
            _appliedAttack = attack;
        }
    }
}
