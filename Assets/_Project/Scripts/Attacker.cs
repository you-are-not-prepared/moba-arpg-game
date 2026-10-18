using UnityEngine;

namespace Moba
{
    // Basic auto-attack: melee (instant hit) or ranged (homing projectile).
    [RequireComponent(typeof(Unit))]
    public class Attacker : MonoBehaviour
    {
        [Tooltip("Base damage per attack (grows with level for heroes).")]
        public float damage = 10f;
        // Flat damage from items and other bonuses; set by Inventory.
        [System.NonSerialized] public float bonusDamage;
        [Tooltip("Edge-to-edge distance at which the attack can be started.")]
        public float range = 1f;
        public float attacksPerSecond = 1f;
        [Tooltip("0 = melee (instant hit). Otherwise the speed of a homing projectile.")]
        public float projectileSpeed;
        public float projectileSize = 0.3f;
        public Material projectileMaterial;
        public Vector3 projectileOrigin = new(0f, 1f, 0f);
        public bool faceTarget = true;

        Unit _self;
        float _nextAttackTime;

        public float TotalDamage => damage + bonusDamage;

        void Awake() => _self = GetComponent<Unit>();

        public bool InRange(Unit target) => _self.EdgeDistanceTo(target) <= range;

        public bool TryAttack(Unit target)
        {
            if (target == null || !target.IsTargetable || !InRange(target) || Stun.IsStunned(_self)) return false;

            if (faceTarget)
            {
                Vector3 dir = target.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            }

            if (Time.time < _nextAttackTime) return false;
            _nextAttackTime = Time.time + 1f / attacksPerSecond;

            if (projectileSpeed <= 0f)
                target.TakeDamage(TotalDamage, _self);
            else
                Projectile.Launch(transform.TransformPoint(projectileOrigin), _self, target, TotalDamage,
                    projectileSpeed, projectileSize, projectileMaterial);
            return true;
        }
    }
}
