using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    public enum Team { Blue, Red }

    public enum UnitKind { Hero, Creep, Tower, Core }

    // Anything on the map that has a team and health and can be targeted.
    // Pivot is at the unit's feet; distances are measured edge-to-edge on the XZ plane.
    public class Unit : MonoBehaviour
    {
        static readonly List<Unit> s_All = new();
        public static IReadOnlyList<Unit> All => s_All;

        public static event Action<Unit> AnyDied;
        public event Action<Unit> Died;

        public Team team;
        public UnitKind kind;
        public float maxHealth = 100f;
        [Tooltip("Footprint radius, used for edge-to-edge range checks.")]
        public float radius = 0.5f;
        [Tooltip("Height above the pivot where the health bar is drawn.")]
        public float barHeight = 2f;
        [Tooltip("While this unit is alive, this one can't be damaged or targeted (e.g. outer tower protects inner tower).")]
        public Unit protectedBy;
        public bool destroyOnDeath = true;

        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        public bool IsTargetable => IsAlive && (protectedBy == null || !protectedBy.IsAlive);
        public Vector3 AimPoint => transform.position + Vector3.up * (barHeight * 0.5f);

        void Awake() => Health = maxHealth;
        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        public void TakeDamage(float amount, Unit source)
        {
            if (!IsTargetable) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f) Die();
        }

        public void ResetHealth() => Health = maxHealth;

        void Die()
        {
            Died?.Invoke(this);
            AnyDied?.Invoke(this);
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
