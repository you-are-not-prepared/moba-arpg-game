using Moba.Abilities;
using UnityEngine;

namespace Moba
{
    // Hero passives that react to the hero's own spell hits (SpellEvents.Hit). Added by HeroDefinition.ApplyTo; talents
    // raise the numbers.
    //   Chilling Touch (Ilsa): hits slow the target, and slowed enemies take more damage from the hero.
    //   Verdant Bond (Maela): hits heal the nearest allied hero for part of the damage dealt.
    [RequireComponent(typeof(Unit))]
    public class SpellHitPassive : MonoBehaviour, IOutgoingDamageModifier
    {
        [Header("Chilling Touch")]
        [Tooltip("0.2 = spell hits slow by 20%. 0 = no slow.")]
        public float slow;
        public float slowSeconds = 1.5f;
        [Tooltip("0.1 = slowed enemies take 10% more damage from this hero.")]
        public float slowedDamageBonus;

        [Header("Verdant Bond")]
        [Tooltip("0.15 = the nearest allied hero heals 15% of the spell damage dealt. 0 = none.")]
        public float healFraction;
        public float healRange = 8f;

        Unit _unit;

        void Awake() => _unit = GetComponent<Unit>();

        void OnEnable()
        {
            SpellEvents.Hit += OnSpellHit;
            _unit.AddDamageModifier(this);
        }

        void OnDisable()
        {
            SpellEvents.Hit -= OnSpellHit;
            _unit.RemoveDamageModifier(this);
        }

        void OnSpellHit(Unit caster, Unit target, float damage)
        {
            if (caster != _unit) return;
            if (slow > 0f && target.IsAlive) Slow.Apply(target, slow, slowSeconds);
            if (healFraction > 0f) NearestAlly()?.Heal(damage * healFraction);
        }

        Unit NearestAlly()
        {
            Unit best = null;
            float bestDistance = healRange;
            foreach (Unit u in Unit.All)
            {
                if (u == _unit || u.kind != UnitKind.Hero || u.team != _unit.team || !u.IsAlive) continue;
                float d = _unit.EdgeDistanceTo(u);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = u;
                }
            }
            return best;
        }

        public float ModifyOutgoingDamage(Unit target, float amount) =>
            slowedDamageBonus > 0f && Slow.IsSlowed(target) ? amount * (1f + slowedDamageBonus) : amount;
    }
}
