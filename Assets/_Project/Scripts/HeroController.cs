using Moba.Abilities;
using Moba.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Moba
{
    // Player hero input (keys come from GameInput and are rebindable):
    //   Move/attack (RMB): ground = move (hold to keep steering), enemy = attack. Stop (S).
    //   Attack move (A): attack the enemy nearest the cursor; if none, walk there and attack the first
    //     enemy that comes within reach. Quick-cast fires at the cursor; otherwise confirm with LMB.
    //   Abilities (Q/W/E/R): Instant casts at once; Point/Unit enter targeting (LMB confirms, RMB cancels).
    //   Level-up modifier (Ctrl) + ability key spends a skill point.
    // Out-of-range casts walk into range first. Respawns at its spawn point after dying.
    // Bots use the same controller with input off (isPlayer = false); BotBrain gives orders through the public API.
    [RequireComponent(typeof(Unit), typeof(Attacker), typeof(NavMeshAgent))]
    [RequireComponent(typeof(AbilityCaster))]
    public class HeroController : MonoBehaviour
    {
        [Tooltip("Set on spawn by MatchBootstrap.")]
        public HeroDefinition definition;
        [Tooltip("Reads mouse/keyboard input and shows targeting indicators. Off for bots.")]
        public bool isPlayer = true;
        // Set when this is the local player's hero on a game server: orders are sent there instead of run here.
        [System.NonSerialized] public Net.NetworkHero Remote;
        bool _present = true;
        public float respawnSeconds = 5f;
        public Transform spawnPoint;
        public Material moveMarkerMaterial;
        public Material attackMoveMarkerMaterial;
        public Material rangeIndicatorMaterial;

        [Header("Attack move")]
        [Tooltip("An enemy within this distance of the cursor is attacked directly.")]
        public float attackMoveCursorRadius = 3f;
        [Tooltip("While attack-moving, enemies within attack range + this distance are engaged.")]
        public float attackMoveAcquireBonus = 2f;

        const float HoldRepathInterval = 0.1f;
        const float AcquireInterval = 0.15f;
        const float ArriveDistance = 0.6f;
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

        bool _attackMoveTargeting;
        bool _attackMoving;
        bool _attackMovePathIssued;
        bool _targetAutoAcquired; // picked up by attack-move scanning (leashed), not chosen by the player
        Vector3 _attackMoveDestination;
        float _nextAcquire;

        RangeIndicator _rangeRing;
        RangeIndicator _areaRing;
        RangeIndicator _attackRing;

        public Unit Unit => _unit;
        public AbilityCaster Abilities => _abilities;
        public float RespawnRemaining { get; private set; }
        // Slot waiting for a target click, or -1.
        public int TargetingSlot => _targetingSlot;
        public bool IsAttackMoveTargeting => _attackMoveTargeting;
        public bool IsAttackMoving => _attackMoving;
        public Unit AttackTarget => _attackTarget;
        public bool HasPendingCast => _pendingCastSlot != NoSlot;

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
            if (!isPlayer) return;
            _rangeRing = RangeIndicator.Create("CastRange", rangeIndicatorMaterial, new Color(0.4f, 0.8f, 1f, 1f), 0.12f);
            _areaRing = RangeIndicator.Create("CastArea", rangeIndicatorMaterial, new Color(1f, 0.85f, 0.3f, 1f), 0.1f);
            _attackRing = RangeIndicator.Create("AttackRange", rangeIndicatorMaterial, new Color(1f, 0.35f, 0.3f, 1f), 0.12f);
        }

        void OnDestroy()
        {
            if (_rangeRing != null) Destroy(_rangeRing.gameObject);
            if (_areaRing != null) Destroy(_areaRing.gameObject);
            if (_attackRing != null) Destroy(_attackRing.gameObject);
        }

        void Update()
        {
            // Connected to a game server: this copy only reads the player's input and sends it (see Remote);
            // the server moves, fights and respawns the hero.
            if (Net.NetSession.IsRemoteClient)
            {
                GameManager game = GameManager.Instance;
                bool blocked = game != null && (game.IsOver || game.IsPaused);
                if (isPlayer && _unit.IsAlive && !blocked)
                {
                    HandleKeys();
                    HandleMouse();
                }
                UpdateIndicators();
                return;
            }

            if (!_unit.IsAlive)
            {
                RespawnRemaining -= Time.deltaTime;
                if (RespawnRemaining <= 0f) Respawn();
                return;
            }

            // Stunned: frozen in place, input ignored; orders resume when it wears off.
            bool stunned = Stun.IsStunned(_unit);
            if (_agent.isOnNavMesh) _agent.isStopped = stunned;
            if (stunned)
            {
                UpdateIndicators();
                return;
            }

            bool inputBlocked = !isPlayer || (GameManager.Instance != null && (GameManager.Instance.IsOver || GameManager.Instance.IsPaused));
            if (!inputBlocked)
            {
                HandleKeys();
                HandleMouse();
            }
            else if (GameManager.Instance != null && GameManager.Instance.IsOver)
            {
                CancelTargeting();
            }

            if (_pendingCastSlot != NoSlot) UpdatePendingCast();
            else UpdateAttack();
            UpdateIndicators();
        }

        // ---------- Public orders (also used by the HUD / minimap) ----------

        public void OrderMove(Vector3 worldPoint, bool showMarker = true)
        {
            if (!_unit.IsAlive) return;
            if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit navHit, 4f, NavMesh.AllAreas)) return;
            if (Remote != null)
            {
                Remote.SendMove(navHit.position);
                if (showMarker && isPlayer) MoveMarker.Spawn(navHit.position, moveMarkerMaterial);
                return;
            }
            ClearOrders();
            _agent.SetDestination(navHit.position);
            if (showMarker && isPlayer) MoveMarker.Spawn(navHit.position, moveMarkerMaterial);
        }

        public void Stop()
        {
            if (Remote != null)
            {
                Remote.SendStop();
                return;
            }
            ClearOrders();
            if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        // Cancels ability or attack-move targeting. Returns true if there was something to cancel.
        public bool CancelTargeting()
        {
            bool had = _targetingSlot != NoSlot || _attackMoveTargeting;
            _targetingSlot = NoSlot;
            _attackMoveTargeting = false;
            return had;
        }

        // Entry point for an ability key or a HUD click on an ability slot. With spell quick cast on, key presses
        // cast straight at the cursor (no indicator); HUD clicks always use normal targeting.
        public void BeginCast(int slot, bool fromHud = false)
        {
            if (Remote != null)
            {
                Hud.Instance?.ShowHudMessage("Spells on the game server come in the next stage.");
                return;
            }
            if (_abilities.GetCastBlock(slot) != CastBlock.None) return;
            AbilityDefinition ability = _abilities.GetSlot(slot).ability;
            CancelTargeting();
            if (ability.targeting == AbilityTargeting.Instant) _abilities.TryCast(slot, transform.position, null);
            else if (GameSettings.Current.spellQuickCast && !fromHud && Mouse.current != null)
                QuickCast(slot, ability, Mouse.current.position.ReadValue());
            else _targetingSlot = slot;
        }

        // Casts at whatever is under `screen`: a valid unit for Unit spells, the ground for Point spells.
        void QuickCast(int slot, AbilityDefinition ability, Vector2 screen)
        {
            if (_camera == null) return;
            int count = Raycast(screen);

            if (ability.targeting == AbilityTargeting.Unit)
            {
                for (int i = 0; i < count; i++)
                {
                    Unit u = _hits[i].collider.GetComponentInParent<Unit>();
                    if (u != null && u.IsVisibleToPlayer && ability.IsValidTarget(_unit, u))
                    {
                        QueueCast(slot, u.transform.position, u);
                        return;
                    }
                }
                Hud.Instance?.ShowHudMessage($"{ability.displayName}: no valid target under the cursor.");
                return;
            }

            if (TryGetGroundPoint(count, out Vector3 point)) QueueCast(slot, point, null);
        }

        // Attack one unit until it dies or can't be seen (bots; the player uses the mouse).
        public void OrderAttack(Unit target)
        {
            if (!_unit.IsAlive || target == null || !target.IsTargetable) return;
            if (Remote != null)
            {
                Remote.SendAttack(target);
                return;
            }
            if (_attackTarget == target && !_attackMoving && _pendingCastSlot == NoSlot) return;
            ClearOrders();
            _attackTarget = target;
        }

        // Cast a slot at a point or unit, walking into range first (bots; the player uses BeginCast).
        public bool OrderCast(int slot, Vector3 point, Unit target)
        {
            if (!_unit.IsAlive || _abilities.GetCastBlock(slot) != CastBlock.None) return false;
            if (_abilities.GetSlot(slot).ability.targeting == AbilityTargeting.Instant)
                return _abilities.TryCast(slot, transform.position, null);
            QueueCast(slot, point, target);
            return true;
        }

        // The player only acts on what their team can see; bots don't use fog of war.
        bool CanSee(Unit u) => !isPlayer || u.IsVisibleToPlayer;

        void ClearOrders()
        {
            _attackTarget = null;
            _pendingCastSlot = NoSlot;
            _attackMoving = false;
        }

        // ---------- Input ----------

        void HandleKeys()
        {
            GameInput input = GameInput.Instance;

            if (input.Stop.WasPressedThisFrame()) Stop();

            bool levelUp = input.LevelUpModifier.IsPressed();
            for (int i = 0; i < AbilityCaster.SlotCount; i++)
            {
                if (!input.Abilities[i].WasPressedThisFrame()) continue;
                if (levelUp) _abilities.TryLevelUp(i);
                else BeginCast(i);
            }

            if (input.AttackMove.WasPressedThisFrame()) BeginAttackMove();
        }

        void BeginAttackMove()
        {
            CancelTargeting();
            if (!GameSettings.Current.attackMoveQuickCast)
            {
                _attackMoveTargeting = true;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null) return;
            Vector2 screen = mouse.position.ReadValue();
            if (Hud.IsPointerOverHud(screen)) return;
            IssueAttackMoveAtScreen(screen);
        }

        void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null) return;
            GameInput input = GameInput.Instance;

            Vector2 screen = mouse.position.ReadValue();
            bool overHud = Hud.IsPointerOverHud(screen);
            bool select = input.Select.WasPressedThisFrame() && !overHud;

            if (select && _targetingSlot != NoSlot)
            {
                ConfirmTarget(screen);
                return;
            }
            if (select && _attackMoveTargeting)
            {
                _attackMoveTargeting = false;
                IssueAttackMoveAtScreen(screen);
                return;
            }

            bool clicked = input.MoveOrAttack.WasPressedThisFrame() && !overHud;
            if (clicked) CancelTargeting();
            bool holding = input.MoveOrAttack.IsPressed() && !input.MoveOrAttack.WasPressedThisFrame()
                           && _attackTarget == null && !_attackMoving && _pendingCastSlot == NoSlot
                           && Time.time >= _nextHoldRepath && !overHud;
            if (!clicked && !holding) return;

            int count = Raycast(screen);

            if (clicked)
            {
                Unit enemy = FirstEnemyHit(count);
                if (enemy != null)
                {
                    OrderAttack(enemy);
                    return;
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
            ClearOrders();
            _pendingCastSlot = slot;
            _pendingCastPoint = point;
            _pendingCastTarget = target;
        }

        // ---------- Attack move ----------

        void IssueAttackMoveAtScreen(Vector2 screen)
        {
            int count = Raycast(screen);
            Unit direct = FirstEnemyHit(count);
            Vector3 point;
            if (direct != null) point = direct.transform.position;
            else if (!TryGetGroundPoint(count, out point)) return;
            IssueAttackMove(point, direct);
        }

        // Attacks `direct` or the enemy nearest `point`; with no enemy there, walks to `point`
        // engaging the closest enemy that comes within reach on the way.
        public void IssueAttackMove(Vector3 point, Unit direct = null)
        {
            if (!_unit.IsAlive) return;
            if (Remote != null)
            {
                Remote.SendAttackMove(point, direct);
                if (isPlayer && NavMesh.SamplePosition(point, out NavMeshHit markHit, 4f, NavMesh.AllAreas))
                    MoveMarker.Spawn(markHit.position, attackMoveMarkerMaterial != null ? attackMoveMarkerMaterial : moveMarkerMaterial);
                CancelTargeting();
                return;
            }
            Unit target = direct != null ? direct : FindEnemyNear(point, attackMoveCursorRadius);
            Vector3 destination = NavMesh.SamplePosition(point, out NavMeshHit navHit, 4f, NavMesh.AllAreas)
                ? navHit.position
                : target != null ? target.transform.position : transform.position;

            CancelTargeting();
            ClearOrders();
            _attackMoving = true;
            _attackMovePathIssued = false;
            _attackMoveDestination = destination;
            _attackTarget = target;
            _targetAutoAcquired = false;
            _nextAcquire = 0f;
            if (isPlayer) MoveMarker.Spawn(destination, attackMoveMarkerMaterial != null ? attackMoveMarkerMaterial : moveMarkerMaterial);
        }

        Unit FindEnemyNear(Vector3 point, float radius)
        {
            Unit best = null;
            float bestDist = float.MaxValue;
            foreach (Unit u in Unit.All)
            {
                if (!u.IsEnemyOf(_unit) || !u.IsTargetable || !CanSee(u)) continue;
                Vector3 d = u.transform.position - point;
                d.y = 0f;
                float dist = d.magnitude - u.radius;
                if (dist <= radius && dist < bestDist)
                {
                    bestDist = dist;
                    best = u;
                }
            }
            return best;
        }

        void ContinueAttackMove()
        {
            Vector3 d = _attackMoveDestination - transform.position;
            d.y = 0f;
            bool reached = d.magnitude <= ArriveDistance;
            bool pathEnded = _attackMovePathIssued && !_agent.pathPending
                             && (!_agent.hasPath || _agent.remainingDistance <= ArriveDistance);
            if (reached || pathEnded)
            {
                _attackMoving = false;
                if (_agent.hasPath) _agent.ResetPath();
                return;
            }

            if (!_attackMovePathIssued || !_agent.hasPath)
            {
                _agent.SetDestination(_attackMoveDestination);
                _attackMovePathIssued = true;
            }
        }

        // ---------- Orders ----------

        void UpdatePendingCast()
        {
            int slot = _pendingCastSlot;
            AbilityDefinition ability = _abilities.GetSlot(slot).ability;
            if (ability == null || (ability.targeting == AbilityTargeting.Unit
                    && (!ability.IsValidTarget(_unit, _pendingCastTarget) || !CanSee(_pendingCastTarget))))
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
            // Targets that die, become untargetable or slip into the fog are dropped.
            if (_attackTarget != null && (!_attackTarget.IsTargetable || !CanSee(_attackTarget)))
            {
                _attackTarget = null;
                if (!_attackMoving)
                {
                    Stop();
                    return;
                }
                _attackMovePathIssued = false; // resume walking to the attack-move destination
            }

            if (_attackTarget != null && _attackMoving && _targetAutoAcquired
                && _unit.EdgeDistanceTo(_attackTarget) > _attacker.range + attackMoveAcquireBonus + 4f)
            {
                _attackTarget = null; // leash: don't chase forever while attack-moving
                _attackMovePathIssued = false;
            }

            if (_attackTarget == null)
            {
                if (!_attackMoving) return;
                if (Time.time >= _nextAcquire)
                {
                    _nextAcquire = Time.time + AcquireInterval;
                    _attackTarget = _unit.FindClosestEnemy(_attacker.range + attackMoveAcquireBonus, CanSee);
                    _targetAutoAcquired = true;
                }
                if (_attackTarget == null)
                {
                    ContinueAttackMove();
                    return;
                }
            }

            // Engaging replaces the walk path; re-issue it once this target is gone
            // (a destroyed target reads as null, so the "no longer targetable" branch above may never run).
            _attackMovePathIssued = false;

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
            bool alive = _unit.IsAlive;

            if (alive && _attackMoveTargeting) _attackRing.Show(transform.position, _attacker.range + _unit.radius);
            else _attackRing.Hide();

            if (_targetingSlot == NoSlot || !alive)
            {
                _rangeRing.Hide();
                _areaRing.Hide();
                return;
            }

            AbilityDefinition ability = _abilities.GetSlot(_targetingSlot).ability;
            if (ability.IndicatorRange > 0f)
            {
                float radius = ability.IndicatorRange + (ability.targeting == AbilityTargeting.Unit ? _unit.radius : 0f);
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

        Unit FirstEnemyHit(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Unit u = _hits[i].collider.GetComponentInParent<Unit>();
                if (u != null && u.IsEnemyOf(_unit) && u.IsTargetable) return u;
            }
            return null;
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
            _present = present;
            // Game-server clients never move the hero themselves, so their agent stays off.
            if (!Net.NetSession.IsRemoteClient) _agent.enabled = present;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = present;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = present;
        }

        // Game-server clients: alive/dead and the respawn timer come from the server (see NetworkHero).
        public void SetRespawnFromServer(bool alive, float remaining)
        {
            RespawnRemaining = remaining;
            if (alive != _present) SetPresent(alive);
        }

        class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
