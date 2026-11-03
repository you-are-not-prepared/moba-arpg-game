using UnityEngine;

namespace Moba.Abilities
{
    // The running aura: a hit around its unit every `interval` seconds until it ends.
    [DisallowMultipleComponent]
    public class Aura : MonoBehaviour
    {
        Unit _unit;
        float _remaining, _interval, _next, _radius, _damage, _angle;
        SpellModifiers _modifiers;

        public static void Apply(Unit unit, float duration, float interval, float radius, float damage, in SpellModifiers m)
        {
            if (unit == null || !unit.IsAlive || duration <= 0f) return;
            if (!unit.TryGetComponent(out Aura aura)) aura = unit.gameObject.AddComponent<Aura>();
            aura._unit = unit;
            aura._remaining = duration;
            aura._interval = Mathf.Max(0.1f, interval);
            aura._next = 0f;
            aura._radius = radius;
            aura._damage = damage;
            aura._modifiers = m;
            aura.enabled = true;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f || _unit == null || !_unit.IsAlive)
            {
                enabled = false;
                return;
            }
            _next -= Time.deltaTime;
            if (_next > 0f) return;
            _next = _interval;
            _angle += 140f;
            Vector3 dir = Quaternion.Euler(0f, _angle, 0f) * Vector3.forward;
            SpellLook.Play(SpellLookKind.Slash, _modifiers.damageType, _unit, transform.position, dir, _radius, 0f);
            SpellArea.Circle(_unit, transform.position, _radius, _damage, _modifiers);
        }
    }
}
