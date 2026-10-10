using UnityEngine;

namespace Moba
{
    // Stationary defender: shoots creeps first, heroes only when no creeps are in range.
    [RequireComponent(typeof(Unit), typeof(Attacker))]
    public class TowerAI : MonoBehaviour
    {
        Unit _unit;
        Attacker _attacker;
        Unit _target;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
        }

        void Update()
        {
            if (_target == null || !_target.IsTargetable || !_attacker.InRange(_target))
                _target = _unit.FindClosestEnemy(_attacker.range, u => u.kind == UnitKind.Creep)
                          ?? _unit.FindClosestEnemy(_attacker.range);

            if (_target != null) _attacker.TryAttack(_target);
        }
    }
}
