using UnityEngine;
using UnityEngine.AI;

namespace Moba
{
    // Timed buff: more movement and attack speed. Optionally refreshed to full duration whenever the unit
    // kills an enemy hero. A glowing ring under the unit shows it's active.
    [DisallowMultipleComponent]
    public class Haste : MonoBehaviour
    {
        Unit _unit;
        NavMeshAgent _agent;
        Attacker _attacker;
        float _duration;
        float _remaining;
        bool _refreshOnHeroKill;
        float _appliedMove = 1f;
        float _appliedAttack = 1f;
        Transform _aura;
        Renderer _auraRenderer;

        public float Remaining => _remaining;

        public static bool IsActive(Unit unit) =>
            unit != null && unit.TryGetComponent(out Haste h) && h.enabled && h._remaining > 0f;

        public static void Apply(Unit unit, float duration, float moveBonus, float attackSpeedBonus, bool refreshOnHeroKill,
            Material auraMaterial)
        {
            if (unit == null || !unit.IsAlive || duration <= 0f) return;
            if (!unit.TryGetComponent(out Haste haste)) haste = unit.gameObject.AddComponent<Haste>();
            haste.Begin(duration, moveBonus, attackSpeedBonus, refreshOnHeroKill, auraMaterial);
        }

        // Game-server clients: show the buff the server reports (`remaining` 0 = none). Only the aura matters
        // here; the server applies the speed.
        public static void SyncFromServer(Unit unit, float remaining, Material auraMaterial)
        {
            bool active = unit.TryGetComponent(out Haste haste) && haste.enabled;
            if (remaining <= 0f)
            {
                if (active) haste._remaining = 0f; // ends next frame
                return;
            }
            if (active) haste._remaining = remaining;
            else Apply(unit, remaining, 0f, 0f, false, auraMaterial);
        }

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _agent = GetComponent<NavMeshAgent>();
            _attacker = GetComponent<Attacker>();
        }

        void OnEnable() => Unit.AnyDied += OnAnyDied;

        void OnDisable()
        {
            Unit.AnyDied -= OnAnyDied;
            SetMultipliers(1f, 1f);
            if (_aura != null) _aura.gameObject.SetActive(false);
        }

        void Begin(float duration, float moveBonus, float attackSpeedBonus, bool refreshOnHeroKill, Material auraMaterial)
        {
            _duration = duration;
            _remaining = duration;
            _refreshOnHeroKill = refreshOnHeroKill;
            SetMultipliers(1f + moveBonus, 1f + attackSpeedBonus);

            if (_aura == null)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "HasteAura";
                Destroy(ring.GetComponent<Collider>());
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                _aura = ring.transform;
                _auraRenderer = ring.GetComponent<Renderer>();
                _auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _auraRenderer.sharedMaterial = auraMaterial;
            _aura.gameObject.SetActive(true);
            enabled = true;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f || !_unit.IsAlive)
            {
                _remaining = 0f;
                enabled = false;
                return;
            }
            float pulse = 2.4f + 0.2f * Mathf.Sin(Time.time * 10f);
            _aura.localScale = new Vector3(pulse, 0.01f, pulse);
            _auraRenderer.enabled = FogOfWar.ShouldShow(_unit.team, transform.position);
        }

        void OnAnyDied(Unit victim, Unit killer)
        {
            if (_refreshOnHeroKill && killer == _unit && victim.kind == UnitKind.Hero && victim.IsEnemyOf(_unit))
                _remaining = _duration;
        }

        // Swaps the previously applied multipliers for new ones, so other speed changes are kept.
        void SetMultipliers(float move, float attack)
        {
            if (_agent != null) _agent.speed = _agent.speed / _appliedMove * move;
            if (_attacker != null) _attacker.attackSpeedMultiplier = _attacker.attackSpeedMultiplier / _appliedAttack * attack;
            _appliedMove = move;
            _appliedAttack = attack;
        }
    }
}
