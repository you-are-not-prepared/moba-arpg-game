using Moba.Abilities;
using Moba.Items;
using UnityEngine;

namespace Moba.Bots
{
    // Simple hero AI. Several times a second it:
    //   - spends skill points (ultimate first) and, while at its shop, buys a spell for Q (with fullShopping: spells
    //     for every slot, mods and items, finishing its shopping before it leaves base, and walking home to shop
    //     once it has saved up recallGold);
    //   - retreats to base below retreatHealth and stays until healed;
    //   - fights an enemy hero in reach when it isn't losing the trade, otherwise last-hits creeps, then hits
    //     structures that its own creeps are tanking;
    //   - never walks into enemy tower range unless allied creeps are there to take the shots, and stays out of the
    //     enemy fountain until the enemy core can be hit;
    //   - with nothing to do, walks its lane toward the enemy base.
    // It gives orders through HeroController, exactly like the player's input would.
    [RequireComponent(typeof(HeroController))]
    public class BotBrain : MonoBehaviour
    {
        public float thinkInterval = 0.2f;
        [Range(0f, 1f)] public float retreatHealth = 0.3f;
        [Range(0f, 1f)] public float returnHealth = 0.9f;
        [Tooltip("Enemy creeps and structures within this edge distance are considered.")]
        public float acquireRange = 8f;
        [Tooltip("Enemy heroes within this edge distance are fought or fled from.")]
        public float heroRange = 9f;
        [Tooltip("With this much gold (and something to buy), the bot walks home to shop.")]
        public int recallGold = 1500;
        [Tooltip("Buy spells for every slot, mods and items. Off for now: bots buy one spell (the shop's first, Fireball) " +
                 "for Q and nothing else, until items and prices are balanced.")]
        public bool fullShopping;

        HeroController _hero;
        Unit _unit;
        Attacker _attacker;
        AbilityCaster _abilities;
        HeroProgression _progression;
        Inventory _inventory;
        HeroAscension _ascension;
        Shop _shop;
        Shop _enemyShop;
        Vector3[] _lane;
        int _waypoint;
        bool _retreating;
        float _nextThink;
        Vector3 _lastMove;

        float HealthFraction => _unit.Health / _unit.maxHealth;
        float ManaFraction => _abilities.maxMana > 0f ? _abilities.Mana / _abilities.maxMana : 1f;

        // Lane from this bot's base to the enemy base.
        public void Init(Vector3[] lane)
        {
            _lane = lane;
            _waypoint = 0;
        }

        void Awake()
        {
            _hero = GetComponent<HeroController>();
            _unit = GetComponent<Unit>();
            _attacker = GetComponent<Attacker>();
            _abilities = GetComponent<AbilityCaster>();
            _progression = GetComponent<HeroProgression>();
            _inventory = GetComponent<Inventory>();
        }

        void Start()
        {
            _shop = Shop.ForTeam(_unit.team);
            _ascension = GetComponent<HeroAscension>();
            _enemyShop = Shop.ForTeam(_unit.team == Team.Blue ? Team.Red : Team.Blue);
            _nextThink = Time.time + Random.Range(0f, thinkInterval);
        }

        void Update()
        {
            if (Time.time < _nextThink) return;
            _nextThink = Time.time + thinkInterval;
            if (GameManager.Instance != null && GameManager.Instance.IsOver) return;

            SpendSkillPoints();
            // Ascension: bots pick at random.
            if (_ascension != null && _ascension.HasPendingChoice)
                _ascension.Choose(Random.Range(0, _ascension.Options(_ascension.PendingTier).Count));
            if (_shop != null && _shop.CanUse(_unit)) BuyThings();
            if (!_unit.IsAlive)
            {
                _retreating = false;
                _waypoint = 0;
                return;
            }
            if (Stun.IsStunned(_unit)) return;
            Think();
        }

        // ---------- Decisions ----------

        void Think()
        {
            Unit enemyHero = _unit.FindClosestEnemy(heroRange, u => u.kind == UnitKind.Hero);

            if (HealthFraction < retreatHealth) _retreating = true;
            // Go home to spend a pile of gold when no enemy hero is around.
            if (!_retreating && enemyHero == null && _progression != null && _progression.Gold >= recallGold && WantsToShop())
                _retreating = true;
            if (_retreating)
            {
                bool healed = HealthFraction >= returnHealth && ManaFraction >= 0.5f;
                if (_shop == null || (_shop.CanUse(_unit) && healed))
                {
                    _retreating = false;
                    _waypoint = 0;
                }
                else
                {
                    UseEscapeAbilities(enemyHero);
                    MoveTo(_shop.transform.position);
                    return;
                }
            }

            // Finish shopping before leaving base (one purchase per think).
            if (enemyHero == null && _shop != null && _shop.CanUse(_unit) && WantsToShop())
            {
                Hold();
                return;
            }

            if (UseAbilities(enemyHero) || _hero.HasPendingCast) return;

            Unit target = ChooseTarget(enemyHero);
            if (target != null)
            {
                _hero.OrderAttack(target);
                return;
            }
            Advance();
        }

        Unit ChooseTarget(Unit enemyHero)
        {
            if (enemyHero != null && WantsToFight(enemyHero)) return enemyHero;

            // Keep hitting the current target while it's still a sensible one (avoids flip-flopping).
            Unit current = _hero.AttackTarget;
            if (current != null && current.IsTargetable && current.kind != UnitKind.Hero
                && _unit.EdgeDistanceTo(current) <= acquireRange + 2f && !IsUnsafe(current.transform.position)
                && (current.kind == UnitKind.Creep || StructureIsSafe(current)))
                return current;

            // Creeps: a last hit first, otherwise the weakest one.
            Unit lastHit = null, weakest = null;
            foreach (Unit u in Unit.All)
            {
                if (u.kind != UnitKind.Creep || !u.IsTargetable || !u.IsEnemyOf(_unit)) continue;
                if (_unit.EdgeDistanceTo(u) > acquireRange || IsUnsafe(u.transform.position)) continue;
                if (u.Health <= _attacker.TotalDamage && (lastHit == null || u.Health < lastHit.Health)) lastHit = u;
                if (weakest == null || u.Health < weakest.Health) weakest = u;
            }
            if (lastHit != null) return lastHit;
            if (weakest != null) return weakest;

            // Structures, once they can be hurt and our creeps are soaking the tower's shots.
            return _unit.FindClosestEnemy(acquireRange,
                u => (u.kind == UnitKind.Tower || u.kind == UnitKind.Core) && StructureIsSafe(u));
        }

        bool WantsToFight(Unit enemy)
        {
            if (IsUnsafe(enemy.transform.position)) return false;
            float theirs = enemy.Health / enemy.maxHealth;
            return HealthFraction >= theirs - 0.15f || theirs < 0.35f;
        }

        // Walks the lane, waiting at the edge of enemy tower range until allied creeps go in first.
        void Advance()
        {
            if (_lane == null || _lane.Length == 0) return;
            Vector3 pos = transform.position;
            while (_waypoint < _lane.Length - 1)
            {
                Vector3 segment = _lane[_waypoint + 1] - _lane[_waypoint];
                bool passed = Vector3.Dot(segment, pos - _lane[_waypoint]) > 0f;
                bool reached = (Flat(pos) - Flat(_lane[_waypoint])).sqrMagnitude < 4f;
                if (!passed && !reached) break;
                _waypoint++;
            }

            Vector3 goal = _lane[_waypoint];
            Vector3 dir = Flat(goal - pos);
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;

            if (IsUnsafe(pos))
                MoveTo(pos - dir * 4f); // back off
            else if (IsUnsafe(pos + dir * 3f))
                Hold();
            else
                MoveTo(goal);
        }

        // ---------- Abilities ----------

        // Returns true if a cast was ordered this think.
        bool UseAbilities(Unit enemyHero)
        {
            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                if (_abilities.GetCastBlock(slot) != CastBlock.None) continue;
                AbilityDefinition ability = _abilities.GetSlot(slot).ability;
                switch (ability)
                {
                    case ProjectileSpell spell:
                    {
                        Unit target = enemyHero != null && _unit.EdgeDistanceTo(enemyHero) <= spell.maxDistance * 0.85f
                            ? enemyHero
                            : null;
                        // Basic spells also clear creeps while mana is plentiful.
                        if (target == null && slot != AbilityCaster.UltimateSlot && ManaFraction > 0.5f)
                            target = _unit.FindClosestEnemy(spell.maxDistance * 0.6f,
                                u => u.kind == UnitKind.Creep && !IsUnsafe(u.transform.position));
                        if (target != null && _hero.OrderCast(slot, target.transform.position, null)) return true;
                        break;
                    }
                    case CooldownResetSpell:
                        if (enemyHero != null && AnyBasicOnCooldown(2f) && _hero.OrderCast(slot, default, null)) return true;
                        break;
                    case ShieldWallSpell:
                    case SpellStormSpell:
                        if (enemyHero != null && _hero.OrderCast(slot, default, null)) return true;
                        break;
                    case HasteSpell:
                        if (enemyHero != null && enemyHero.Health / enemyHero.maxHealth < 0.5f
                            && _hero.OrderCast(slot, default, null)) return true;
                        break;
                    default:
                        if (ability != null && enemyHero != null && ability.targeting == AbilityTargeting.Unit
                            && ability.IsValidTarget(_unit, enemyHero)
                            && _hero.OrderCast(slot, enemyHero.transform.position, enemyHero)) return true;
                        break;
                }
            }
            return false;
        }

        void UseEscapeAbilities(Unit enemyHero)
        {
            if (enemyHero == null) return;
            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                if (_abilities.GetCastBlock(slot) != CastBlock.None) continue;
                AbilityDefinition ability = _abilities.GetSlot(slot).ability;
                if (ability is HasteSpell || ability is ShieldWallSpell) _hero.OrderCast(slot, default, null);
            }
        }

        bool AnyBasicOnCooldown(float minRemaining)
        {
            for (int i = 0; i < AbilityCaster.SlotCount; i++)
                if (i != AbilityCaster.UltimateSlot && _abilities.GetSlot(i).cooldownRemaining > minRemaining) return true;
            return false;
        }

        void SpendSkillPoints()
        {
            while (_abilities.SkillPoints > 0)
            {
                if (_abilities.TryLevelUp(AbilityCaster.UltimateSlot)) continue;
                int best = -1;
                for (int i = 0; i < AbilityCaster.SlotCount; i++)
                {
                    if (i == AbilityCaster.UltimateSlot || !_abilities.CanLevelUp(i)) continue;
                    if (best < 0 || _abilities.GetSlot(i).rank < _abilities.GetSlot(best).rank) best = i;
                }
                if (best < 0 || !_abilities.TryLevelUp(best)) return;
            }
        }

        // ---------- Shopping (one purchase per think) ----------

        void BuyThings()
        {
            InventoryItem item = NextPurchase(out int spellSlot);
            if (item != null && Buy(item, out int inventorySlot) && spellSlot >= 0)
                Loadout.EquipFromInventory(_inventory, _abilities, inventorySlot, spellSlot);
        }

        bool WantsToShop() => _shop != null && NextPurchase(out _) != null;

        // The next affordable thing worth buying, and the spell slot it goes into (-1 for equipment). In order:
        // a spell for every empty basic slot, mods for those spells (ones the spell lacks first), then the best
        // item, keeping one inventory slot free for spells and mods passing through.
        InventoryItem NextPurchase(out int spellSlot)
        {
            spellSlot = -1;
            if (_shop.catalog == null || _inventory == null || _progression == null || _inventory.IsFull) return null;
            if (!fullShopping) return FirstSpell(out spellSlot);

            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                if (_abilities.IsSlotLocked(slot) || _abilities.GetSlot(slot).ability != null) continue;
                AbilityDefinition spell = PickRandom<AbilityDefinition>(a => !a.IsPassive);
                if (spell == null) break;
                spellSlot = slot;
                return spell;
            }

            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                AbilityDefinition spell = _abilities.GetSlot(slot).ability;
                if (spell == null || _abilities.IsSlotLocked(slot) || _abilities.FirstFreeSocket(slot) < 0) continue;
                ModDefinition mod = PickRandom<ModDefinition>(m => m.Supports(spell) && !HasMod(slot, m))
                                    ?? PickRandom<ModDefinition>(m => m.Supports(spell));
                if (mod == null) continue;
                spellSlot = slot;
                return mod;
            }

            if (EmptyInventorySlots() < 2) return null;
            ItemDefinition best = null;
            foreach (InventoryItem item in _shop.catalog.items)
                if (item is ItemDefinition equipment && equipment.cost <= _progression.Gold
                    && (best == null || equipment.cost > best.cost || (equipment.cost == best.cost && equipment.attackDamage > best.attackDamage)))
                    best = equipment;
            return best;
        }

        // Without fullShopping: the shop's first spell for the first basic slot, once.
        InventoryItem FirstSpell(out int spellSlot)
        {
            spellSlot = -1;
            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                if (_abilities.IsSlotLocked(slot)) continue;
                if (_abilities.GetSlot(slot).ability != null) return null;
                if (spellSlot < 0) spellSlot = slot;
            }
            foreach (InventoryItem item in _shop.catalog.items)
                if (item is AbilityDefinition spell && !spell.IsPassive && spell.cost <= _progression.Gold) return spell;
            spellSlot = -1;
            return null;
        }

        bool Buy(InventoryItem item, out int inventorySlot)
        {
            inventorySlot = -1;
            if (_shop.TryBuy(_unit, _progression, _inventory, item) != ShopResult.Ok) return false;
            for (int i = Inventory.SlotCount - 1; i >= 0; i--)
                if (_inventory.Get(i) == item)
                {
                    inventorySlot = i;
                    break;
                }
            return inventorySlot >= 0;
        }

        T PickRandom<T>(System.Func<T, bool> filter) where T : InventoryItem
        {
            T chosen = null;
            int seen = 0;
            foreach (InventoryItem item in _shop.catalog.items)
            {
                if (item is not T typed || typed.cost > _progression.Gold || !filter(typed)) continue;
                if (Random.Range(0, ++seen) == 0) chosen = typed; // reservoir sampling
            }
            return chosen;
        }

        bool HasMod(int slot, ModDefinition mod)
        {
            for (int s = 0; s < AbilityCaster.MaxModSockets; s++)
                if (_abilities.GetMod(slot, s) == mod) return true;
            return false;
        }

        int EmptyInventorySlots()
        {
            int n = 0;
            for (int i = 0; i < Inventory.SlotCount; i++)
                if (_inventory.Get(i) == null) n++;
            return n;
        }

        // ---------- Tower safety ----------

        // Somewhere a bot shouldn't walk or chase into.
        bool IsUnsafe(Vector3 point) => InTowerDanger(point) || InEnemyBase(point);

        // The enemy fountain, where their heroes heal. Off limits until their core can be hit.
        bool InEnemyBase(Vector3 point)
        {
            if (_enemyShop == null || FlatDistance(point, _enemyShop.transform.position) > _enemyShop.radius) return false;
            foreach (Unit u in Unit.All)
                if (u.kind == UnitKind.Core && u.IsEnemyOf(_unit) && u.IsTargetable) return false;
            return true;
        }

        // Inside an enemy tower's reach with no allied creep there to draw its fire.
        bool InTowerDanger(Vector3 point)
        {
            foreach (Unit tower in Unit.All)
            {
                if (tower.kind != UnitKind.Tower || !tower.IsAlive || !tower.IsEnemyOf(_unit)) continue;
                if (!tower.TryGetComponent(out Attacker towerAttack)) continue;
                float reach = towerAttack.range + tower.radius + _unit.radius + 1.5f;
                if (FlatDistance(point, tower.transform.position) > reach) continue;
                if (!AlliedCreepNear(tower.transform.position, towerAttack.range + tower.radius)) return true;
            }
            return false;
        }

        // A tower is only hit while allied creeps are inside its range; anything else (the core) while no tower covers it.
        bool StructureIsSafe(Unit structure)
        {
            if (structure.kind == UnitKind.Tower && structure.TryGetComponent(out Attacker towerAttack))
                return AlliedCreepNear(structure.transform.position, towerAttack.range + structure.radius);
            return !IsUnsafe(structure.transform.position);
        }

        bool AlliedCreepNear(Vector3 point, float radius)
        {
            foreach (Unit u in Unit.All)
                if (u.kind == UnitKind.Creep && u.team == _unit.team && u.IsAlive
                    && FlatDistance(u.transform.position, point) <= radius) return true;
            return false;
        }

        // ---------- Movement ----------

        void MoveTo(Vector3 point)
        {
            bool sameGoal = (Flat(point) - Flat(_lastMove)).sqrMagnitude < 1f;
            if (sameGoal && _hero.AttackTarget == null && !_hero.HasPendingCast && IsMoving()) return;
            _lastMove = point;
            _hero.OrderMove(point, showMarker: false);
        }

        void Hold()
        {
            if (_hero.AttackTarget != null || IsMoving()) _hero.Stop();
        }

        bool IsMoving() => TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.enabled && agent.hasPath;

        static float FlatDistance(Vector3 a, Vector3 b) => (Flat(a) - Flat(b)).magnitude;

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
