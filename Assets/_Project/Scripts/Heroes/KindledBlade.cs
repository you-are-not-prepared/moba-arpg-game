using System.Collections.Generic;
using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // Hero passive (Oren): every basic attack builds Heat (up to maxStacks, each giving attack speed; lost after
    // decaySeconds without attacking). With full Heat the next attack bursts: fire damage around the target and a
    // burn, then Heat starts over. Added by HeroDefinition.ApplyTo; talents raise the burst damage.
    [RequireComponent(typeof(Attacker))]
    public class KindledBlade : MonoBehaviour
    {
        const float DecaySeconds = 3f;
        const float BurstRadius = 2.5f;
        const float BurnSeconds = 3f;

        public int maxStacks = 5;
        [Tooltip("Attack speed per stack (0.04 = 4%).")]
        public float attackSpeedPerStack = 0.04f;
        public float burstDamage = 50f;
        public Material burstMaterial;

        static readonly List<Unit> s_Targets = new();

        Unit _unit;
        Attacker _attacker;
        int _stacks;
        float _sinceAttack;
        float _applied = 1f;

        public int Stacks => _stacks;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
        }

        void OnEnable() => _attacker.Hit += OnHit;

        void OnDisable()
        {
            _attacker.Hit -= OnHit;
            SetStacks(0);
        }

        void Update()
        {
            if (_stacks == 0) return;
            _sinceAttack += Time.deltaTime;
            if (_sinceAttack >= DecaySeconds || !_unit.IsAlive) SetStacks(0);
        }

        void OnHit(Unit target)
        {
            _sinceAttack = 0f;
            if (_stacks < maxStacks)
            {
                SetStacks(_stacks + 1);
                return;
            }
            Burst(target);
            SetStacks(0);
        }

        void Burst(Unit target)
        {
            Vector3 center = target.transform.position;
            s_Targets.Clear();
            foreach (Unit u in Unit.All)
            {
                if (!u.IsEnemyOf(_unit) || !u.IsTargetable) continue;
                Vector3 d = u.transform.position - center;
                d.y = 0f;
                if (d.magnitude - u.radius <= BurstRadius) s_Targets.Add(u);
            }
            foreach (Unit u in s_Targets)
            {
                u.TakeDamage(burstDamage, _unit);
                if (u.IsAlive) Burning.Apply(u, burstDamage * 0.5f / BurnSeconds, BurnSeconds, _unit);
            }
            s_Targets.Clear();
            SpellExplosion.Spawn(center, BurstRadius, burstMaterial);
        }

        // Swaps the previously applied attack speed for the new stack count, so other changes are kept.
        void SetStacks(int stacks)
        {
            _stacks = stacks;
            float multiplier = 1f + attackSpeedPerStack * stacks;
            _attacker.attackSpeedMultiplier = _attacker.attackSpeedMultiplier / _applied * multiplier;
            _applied = multiplier;
            if (stacks == 0) _sinceAttack = 0f;
        }
    }
}
