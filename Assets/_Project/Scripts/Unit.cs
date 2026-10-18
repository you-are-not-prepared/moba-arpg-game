using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    public enum Team { Blue, Red }

    public enum UnitKind { Hero, Creep, Tower, Core }

    // Changes the damage a unit deals (e.g. a hero passive). Registered with Unit.AddDamageModifier.
    public interface IOutgoingDamageModifier
    {
        float ModifyOutgoingDamage(Unit target, float amount);
    }

    // Anything on the map that has a team and health and can be targeted.
    // Pivot is at the unit's feet; distances are measured edge-to-edge on the XZ plane.
    public class Unit : MonoBehaviour
    {
        static readonly List<Unit> s_All = new();
        public static IReadOnlyList<Unit> All => s_All;

        // (victim, killer). The killer is whoever landed the last hit and may be null.
        public static event Action<Unit, Unit> AnyDied;
        public event Action<Unit, Unit> Died;

        public Team team;
        public UnitKind kind;
        public float maxHealth = 100f;
        [Tooltip("Footprint radius, used for edge-to-edge range checks.")]
        public float radius = 0.5f;
        [Tooltip("Height above the pivot where the health bar is drawn.")]
        public float barHeight = 2f;
        [Tooltip("While ALL of these are alive, this unit can't be damaged or targeted (the outer tower protects the inner " +
                 "tower; every lane's inner tower protects the core, so losing any one exposes it).")]
        public Unit[] protectedBy = System.Array.Empty<Unit>();
        public bool destroyOnDeath = true;
        [Tooltip("Vision radius this unit gives its team (fog of war). 0 = none.")]
        public float sightRadius;

        [Header("Bounty (paid to the hero that lands the last hit)")]
        public int goldBounty;
        public float xpBounty;

        readonly List<IOutgoingDamageModifier> _damageModifiers = new();

        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        // False while the unit is hidden in the local player's fog of war (set by FogOfWar).
        public bool IsVisibleToPlayer { get; set; } = true;
        public bool IsTargetable => IsAlive && !IsProtected;

        bool IsProtected
        {
            get
            {
                if (protectedBy == null || protectedBy.Length == 0) return false;
                foreach (Unit p in protectedBy)
                    if (p == null || !p.IsAlive) return false;
                return true;
            }
        }

        public Vector3 AimPoint => transform.position + Vector3.up * (barHeight * 0.5f);

        void Awake() => Health = maxHealth;
        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        public void TakeDamage(float amount, Unit source)
        {
            if (!IsTargetable) return;
            if (source != null) amount = source.ModifyOutgoingDamage(this, amount);
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f) Die(source);
        }

        public void AddDamageModifier(IOutgoingDamageModifier modifier)
        {
            if (!_damageModifiers.Contains(modifier)) _damageModifiers.Add(modifier);
        }

        public void RemoveDamageModifier(IOutgoingDamageModifier modifier) => _damageModifiers.Remove(modifier);

        float ModifyOutgoingDamage(Unit target, float amount)
        {
            foreach (IOutgoingDamageModifier m in _damageModifiers) amount = m.ModifyOutgoingDamage(target, amount);
            return amount;
        }

        public void ResetHealth() => Health = maxHealth;

        public void Heal(float amount)
        {
            if (IsAlive) Health = Mathf.Min(maxHealth, Health + amount);
        }

        // Raises max health and grants the same amount of current health (e.g. on level-up).
        public void AddMaxHealth(float amount)
        {
            maxHealth += amount;
            if (IsAlive) Health += amount;
        }

        void Die(Unit killer)
        {
            Died?.Invoke(this, killer);
            AnyDied?.Invoke(this, killer);
            if (destroyOnDeath) Destroy(gameObject);
        }

        public bool IsEnemyOf(Unit other) => other.team != team;

        public float EdgeDistanceTo(Unit other)
        {
            Vector3 d = other.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude - radius - other.radius;
        }

        // Closest targetable enemy within `range` (edge-to-edge), optionally filtered.
        public Unit FindClosestEnemy(float range, Func<Unit, bool> filter = null)
        {
            Unit best = null;
            float bestDist = range;
            foreach (Unit u in s_All)
            {
                if (u.team == team || !u.IsTargetable) continue;
                if (filter != null && !filter(u)) continue;
                float d = EdgeDistanceTo(u);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
            return best;
        }
    }
}
