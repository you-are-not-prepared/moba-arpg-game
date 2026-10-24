using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // Timed buff (Thessa's Eye of the Storm): while it lasts the unit's spells cost less mana and get shorter
    // cooldowns (through AbilityCaster's temporary multipliers). A glowing ring under the unit shows it's active.
    [DisallowMultipleComponent]
    public class SpellStorm : MonoBehaviour
    {
        Unit _unit;
        AbilityCaster _caster;
        float _remaining;
        Transform _aura;
        Renderer _auraRenderer;

        public float Remaining => _remaining;

        public static bool IsActive(Unit unit) =>
            unit != null && unit.TryGetComponent(out SpellStorm s) && s.enabled && s._remaining > 0f;

        // `cooldownMultiplier` 0.5 = cooldowns half as long; `manaCostMultiplier` 0 = spells are free.
        public static void Apply(Unit unit, float duration, float cooldownMultiplier, float manaCostMultiplier, Material auraMaterial)
        {
            if (unit == null || !unit.IsAlive || duration <= 0f || !unit.TryGetComponent(out AbilityCaster _)) return;
            if (!unit.TryGetComponent(out SpellStorm storm)) storm = unit.gameObject.AddComponent<SpellStorm>();
            storm.Begin(duration, cooldownMultiplier, manaCostMultiplier, auraMaterial);
        }

        // Game-server clients: show the storm the server reports (`remaining` 0 = none). The multipliers apply here
        // too, so the HUD's "not enough mana" agrees with the server.
        public static void SyncFromServer(Unit unit, float remaining, float cooldownMultiplier, float manaCostMultiplier,
            Material auraMaterial)
        {
            bool active = unit.TryGetComponent(out SpellStorm storm) && storm.enabled;
            if (remaining <= 0f)
            {
                if (active) storm._remaining = 0f; // ends next frame
                return;
            }
            if (active) storm._remaining = remaining;
            else Apply(unit, remaining, cooldownMultiplier, manaCostMultiplier, auraMaterial);
        }

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _caster = GetComponent<AbilityCaster>();
        }

        void OnDisable()
        {
            if (_caster != null) _caster.SetTemporaryMultipliers(1f, 1f);
            if (_aura != null) _aura.gameObject.SetActive(false);
        }

        void Begin(float duration, float cooldownMultiplier, float manaCostMultiplier, Material auraMaterial)
        {
            _remaining = duration;
            _caster.SetTemporaryMultipliers(cooldownMultiplier, manaCostMultiplier);

            if (_aura == null)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "StormAura";
                Destroy(ring.GetComponent<Collider>());
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
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
            float pulse = 2.9f + 0.25f * Mathf.Sin(Time.time * 6f);
            _aura.localScale = new Vector3(pulse, 0.01f, pulse);
            _auraRenderer.enabled = FogOfWar.ShouldShow(_unit.team, transform.position);
        }
    }
}
