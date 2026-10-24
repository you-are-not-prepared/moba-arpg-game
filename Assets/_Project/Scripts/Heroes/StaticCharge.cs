using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // Hero passive (Corvin): every `every`-th spell he casts is charged, and its first hit jumps to `jumps` more enemies
    // for `fraction` of the damage (see SpellProjectile). Added by HeroDefinition.ApplyTo; talents raise the numbers.
    [RequireComponent(typeof(AbilityCaster))]
    public class StaticCharge : MonoBehaviour
    {
        public int every = 4;
        public int jumps = 1;
        [Tooltip("0.5 = each jump deals 50% of the hit's damage.")]
        public float fraction = 0.5f;
        public float range = 5f;
        public Material boltMaterial;

        AbilityCaster _caster;
        int _casts;

        // Whether the spell being cast right now is the charged one (AbilityCaster raises Casted after the spell runs).
        public bool NextIsCharged => (_casts + 1) % Mathf.Max(1, every) == 0;
        public int Casts => _casts;

        void Awake() => _caster = GetComponent<AbilityCaster>();
        void OnEnable() => _caster.Casted += OnCasted;
        void OnDisable() => _caster.Casted -= OnCasted;

        void OnCasted(int slot) => _casts++;
    }
}
