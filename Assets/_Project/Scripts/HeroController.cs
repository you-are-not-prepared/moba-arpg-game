using Moba.Abilities;
using Moba.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Moba
{
    // Player hero input:
    //   RMB ground = move (hold to keep steering), RMB enemy = attack, S = stop.
    //   Q/W/E/R = cast (Instant casts at once; Point/Unit enter targeting: LMB confirms, RMB/Esc cancels).
    //   Ctrl+Q/W/E/R = spend a skill point on that ability.
    // Out-of-range casts walk into range first. Respawns at its spawn point after dying.
    [RequireComponent(typeof(Unit), typeof(Attacker), typeof(NavMeshAgent))]
    [RequireComponent(typeof(AbilityCaster))]
    public class HeroController : MonoBehaviour
    {
        public float respawnSeconds = 5f;
        public Transform spawnPoint;
        public Material moveMarkerMaterial;
        public Material rangeIndicatorMaterial;

        const float HoldRepathInterval = 0.1f;
        const int NoSlot = -1;

        Unit _unit;
        Attacker _attacker;
        NavMeshAgent _agent;
        AbilityCaster _abilities;
        Camera _camera;
        Unit _attackTarget;
        float _nextHoldRepath;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        int _targetingSlot = NoSlot;
        int _pendingCastSlot = NoSlot;
        Vector3 _pendingCastPoint;
        Unit _pendingCastTarget;

        RangeIndicator _rangeRing;
        RangeIndicator _areaRing;

        public Unit Unit => _unit;
        public AbilityCaster Abilities => _abilities;
        public float RespawnRemaining { get; private set; }
        // Slot waiting for a target click, or -1.
        public int TargetingSlot => _targetingSlot;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
            _agent = GetComponent<NavMeshAgent>();
            _abilities = GetComponent<AbilityCaster>();
            _unit.destroyOnDeath = false;
            _unit.Died += OnDied;
        }

        void Start()
        {
            _camera = Camera.main;
            _rangeRing = RangeIndicator.Create("CastRange", rangeIndicatorMaterial, new Color(0.4f, 0.8f, 1f, 1f), 0.12f);
            _areaRing = RangeIndicator.Create("CastArea", rangeIndicatorMaterial, new Color(1f, 0.85f, 0.3f, 1f), 0.1f);
        }

        void OnDestroy()
        {
            if (_rangeRing != null) Destroy(_rangeRing.gameObject);
            if (_areaRing != null) Destroy(_areaRing.gameObject);
        }

        void Update()
        {
            if (!_unit.IsAlive)
            {
                RespawnRemaining -= Time.deltaTime;
                if (RespawnRemaining <= 0f) Respawn();
                return;
            }

            if (GameManager.Instance == null || !GameManager.Instance.IsOver)
            {
                HandleAbilityKeys();
                HandleMouse();
            }
            else
            {
                CancelTargeting();
            }

            if (_pendingCastSlot != NoSlot) UpdatePendingCast();
            else UpdateAttack();
            UpdateIndicators();
        }

        // ---------- Public orders (also used by the minimap) ----------

        public void OrderMove(Vector3 worldPoint, bool showMarker = true)
        {
            if (!_unit.IsAlive) return;
            if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit navHit, 4f, NavMesh.AllAreas)) return;
            _attackTarget = null;
            _pendingCastSlot = NoSlot;
            _agent.SetDestination(navHit.position);
            if (showMarker) MoveMarker.Spawn(navHit.position, moveMarkerMaterial);
        }

        public void Stop()
        {
            _attackTarget = null;
            _pendingCastSlot = NoSlot;
            if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        // ---------- Input ----------

        void HandleAbilityKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.sKey.wasPressedThisFrame) Stop();
            if (keyboard.escapeKey.wasPressedThisFrame) CancelTargeting();

            bool ctrl = keyboard.ctrlKey.isPressed;
            for (int i = 0; i < AbilityCaster.SlotCount; i++)
            {
                if (!SlotKey(keyboard, i).wasPressedThisFrame) continue;
                if (ctrl) _abilities.TryLevelUp(i);
                else BeginCast(i);
            }
        }

        static UnityEngine.InputSystem.Controls.KeyControl SlotKey(Keyboard k, int slot) => slot switch
        {
            0 => k.qKey,
            1 => k.wKey,
            2 => k.eKey,
            _ => k.rKey,
        };

        // Entry point for a key press or a HUD click on an ability slot.
        public void BeginCast(int slot)
        {
            if (_abilities.GetCastBlock(slot) != CastBlock.None) return;
            AbilityDefinition ability = _abilities.GetSlot(slot).ability;
            if (ability.targeting == AbilityTargeting.Instant)
            {
                CancelTargeting();
                _abilities.TryCast(slot, transform.position, null);
            }
            else
            {
                _targetingSlot = slot;
            }
        }

        void CancelTargeting() => _targetingSlot = NoSlot;

        void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null) return;

            Vector2 screen = mouse.position.ReadValue();
            bool overHud = Hud.IsPointerOverHud(screen);

            if (_targetingSlot != NoSlot && mouse.leftButton.wasPressedThisFrame && !overHud)
            {
                ConfirmTarget(screen);
                return;
            }

            bool clicked = mouse.rightButton.wasPressedThisFrame && !overHud;
            if (clicked) CancelTargeting();
            bool holding = mouse.rightButton.isPressed && !mouse.rightButton.wasPressedThisFrame && _attackTarget == null
                           && _pendingCastSlot == NoSlot && Time.time >= _nextHoldRepath && !overHud;
            if (!clicked && !holding) return;

            int count = Raycast(screen);

            if (clicked)
            {
                for (int i = 0; i < count; i++)
                {
                    Unit u = _hits[i].collider.GetComponentInParent<Unit>();
                    if (u != null && u.IsEnemyOf(_unit) && u.IsTargetable)
                    {
                        _pendingCastSlot = NoSlot;
                        _attackTarget = u;
                        return;
                    }
                }
            }

            if (TryGetGroundPoint(count, out Vector3 ground))
            {
                OrderMove(ground, showMarker: clicked);
                _nextHoldRepath = Time.time + HoldRepathInterval;
            }
        }

        void ConfirmTarget(Vector2 screen)
        {
            int slot = _targetingSlot;
            AbilityDefinition ability = _abilities.GetSlot(slot).ability;
            int count = Raycast(screen);

            if (ability.targeting == AbilityTargeting.Unit)
            {
                for (int i = 0; i < count; i++)
                {
                    Unit u = _hits[i].collider.GetComponentInParent<Unit>();
                    if (u != null && ability.IsValidTarget(_unit, u))
                    {
                        QueueCast(slot, u.transform.position, u);
                        return;
                    }
                }
                return; // no valid unit under the cursor: stay in targeting mode
            }

            if (TryGetGroundPoint(count, out Vector3 point)) QueueCast(slot, point, null);
        }

        void QueueCast(int slot, Vector3 point, Unit target)
        {
            CancelTargeting();
            _attackTarget = null;
            _pendingCastSlot = slot;
            _pendingCastPoint = point;
            _pendingCastTarget = target;
        }

        // ---------- Orders ----------

        void UpdatePendingCast()
        {
            int slot = _pendingCastSlot;
            AbilityDefinition ability = _abilities.GetSlot(slot).ability;
            if (ability == null || (ability.targeting == AbilityTargeting.Unit && !ability.IsValidTarget(_unit, _pendingCastTarget)))
            {
                _pendingCastSlot = NoSlot;
                return;
            }

            Vector3 point = _pendingCastTarget != null ? _pendingCastTarget.transform.position : _pendingCastPoint;
            if (_abilities.InCastRange(slot, point, _pendingCastTarget))
            {
                if (_agent.hasPath) _agent.ResetPath();
                _abilities.TryCast(slot, point, _pendingCastTarget);
                _pendingCastSlot = NoSlot;
            }
            else
            {
                _agent.SetDestination(point);
            }
        }

        void UpdateAttack()
        {
            if (_attackTarget == null) return;
            if (!_attackTarget.IsTargetable)
            {
                Stop();
                return;
            }

            if (_attacker.InRange(_attackTarget))
            {
                if (_agent.hasPath) _agent.ResetPath();
                _attacker.TryAttack(_attackTarget);
            }
            else
            {
                _agent.SetDestination(_attackTarget.transform.position);
            }
        }

        void UpdateIndicators()
        {
            if (_rangeRing == null) return;
            if (_targetingSlot == NoSlot || !_unit.IsAlive)
            {
                _rangeRing.Hide();
                _areaRing.Hide();
                return;
            }

            AbilityDefinition ability = _abilities.GetSlot(_targetingSlot).ability;
            if (ability.range > 0f)
            {
                float radius = ability.range + (ability.targeting == AbilityTargeting.Unit ? _unit.radius : 0f);
                _rangeRing.Show(transform.position, radius);
            }
            else
            {
                _rangeRing.Hide();
            }

            Mouse mouse = Mouse.current;
            if (ability.targeting == AbilityTargeting.Point && ability.areaRadius > 0f && mouse != null
                && TryGetGroundPoint(Raycast(mouse.position.ReadValue()), out Vector3 cursor))
                _areaRing.Show(cursor, ability.areaRadius);
            else
                _areaRing.Hide();
        }

        // ---------- Raycast helpers ----------

        int Raycast(Vector2 screen)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            int count = Physics.RaycastNonAlloc(ray, _hits, 500f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);
            return count;
        }

        bool TryGetGroundPoint(int hitCount, out Vector3 point)
        {
            for (int i = 0; i < hitCount; i++)
            {
                if (_hits[i].collider.GetComponentInParent<Unit>() != null) continue;
                point = _hits[i].point;
                return true;
            }
            point = default;
            return false;
        }

        // ---------- Death ----------

        void OnDied(Unit victim, Unit killer)
        {
            Stop();
            CancelTargeting();
            RespawnRemaining = respawnSeconds;
            SetPresent(false);
        }

        void Respawn()
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            _unit.ResetHealth();
            _abilities.RestoreMana();
            SetPresent(true);
            _agent.Warp(pos);
        }

        void SetPresent(bool present)
        {
            _agent.enabled = present;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = present;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = present;
        }

        class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
