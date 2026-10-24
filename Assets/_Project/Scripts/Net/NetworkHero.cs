using Moba.Abilities;
using Moba.Items;
using Moba.UI;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Moba.Net
{
    // The cheat menu's buttons, sent to a game server that allows cheats.
    public enum CheatAction : byte { Level, Gold, NoManaCost, NoCooldowns }

    // A hero on the game server.
    //  - Server -> clients: which hero it is, level, XP, gold, K/D/A, CS, damage, the respawn timer, its kit
    //    (inventory, spells, mods, ranks, skill points), cooldowns, mana, ascension talents and timed buffs.
    //  - Owning client -> server: the player's orders (move, attack, attack move, stop, cast, learn a rank),
    //    shop, inventory and ascension choices, and cheats when the server allows them. The server checks them
    //    and runs them through the same code offline play uses.
    // A player who leaves keeps their hero in the match (owned by the server, standing still); logging back in to
    // the same server hands it back to them (see GameServer).
    [RequireComponent(typeof(HeroController))]
    public class NetworkHero : NetworkBehaviour
    {
        // _kit layout: inventory slots, the spell in each slot, mods (slot * MaxModSockets + socket), ranks, skill points.
        const int KitSpells = Inventory.SlotCount;
        const int KitMods = KitSpells + AbilityCaster.SlotCount;
        const int KitRanks = KitMods + AbilityCaster.SlotCount * AbilityCaster.MaxModSockets;
        const int KitSkillPoints = KitRanks + AbilityCaster.SlotCount;
        const int KitSize = KitSkillPoints + 1;

        // After a drag and drop the client shows its own result at once; the server's copy takes over after this.
        const float PredictionSeconds = 0.5f;
        // Timers are sent as the server time they end at; smaller drifts aren't worth a message.
        const float TimerTolerance = 0.05f;

        readonly NetworkVariable<FixedString32Bytes> _heroId = new();
        readonly NetworkVariable<FixedString64Bytes> _playerName = new();
        readonly NetworkVariable<bool> _isBot = new();
        readonly NetworkVariable<int> _level = new(1);
        readonly NetworkVariable<float> _xp = new();
        readonly NetworkVariable<int> _gold = new();
        readonly NetworkVariable<int> _kills = new();
        readonly NetworkVariable<int> _deaths = new();
        readonly NetworkVariable<int> _assists = new();
        readonly NetworkVariable<int> _creepScore = new();
        // Damage dealt to heroes, dealt in total, and taken (whole numbers; for the scoreboard and match analysis).
        readonly NetworkVariable<int> _heroDamage = new();
        readonly NetworkVariable<int> _totalDamage = new();
        readonly NetworkVariable<int> _damageTaken = new();
        readonly NetworkVariable<float> _respawn = new();
        readonly NetworkVariable<float> _mana = new();
        readonly NetworkVariable<float> _maxMana = new();
        readonly NetworkVariable<float> _hasteEnd = new();
        readonly NetworkVariable<float> _shieldEnd = new();
        // Cheat toggles: 1 = no mana cost, 2 = no cooldowns.
        readonly NetworkVariable<byte> _cheatToggles = new();
        NetworkList<short> _kit;
        // Per slot: the server time the cooldown ends (0 = ready), then its full length.
        NetworkList<float> _cooldowns;
        // The option picked at each ascension tier.
        NetworkList<byte> _talents;

        HeroController _hero;
        HeroProgression _progression;
        AbilityCaster _caster;
        Inventory _inventory;
        HeroAscension _ascension;
        NetworkTransform _netTransform;
        float _predictUntil;

        public string HeroId => _heroId.Value.ToString();
        public string PlayerName => _playerName.Value.ToString();
        public bool IsBot => _isBot.Value;
        // Server only: the player's backend account ("" for bots and players who weren't logged in).
        public string AccountId { get; set; } = "";

        float ServerNow => NetworkManager.ServerTime.TimeAsFloat;

        void Awake()
        {
            _hero = GetComponent<HeroController>();
            _progression = GetComponent<HeroProgression>();
            _caster = GetComponent<AbilityCaster>();
            _inventory = GetComponent<Inventory>();
            _netTransform = GetComponent<NetworkTransform>();
            _kit = new NetworkList<short>();
            _cooldowns = new NetworkList<float>();
            _talents = new NetworkList<byte>();
        }

        string _initHeroId = "", _initPlayerName = "";
        bool _initIsBot;

        // Server, before Spawn(): applied in OnNetworkSpawn, once the network variables can be written.
        public void Init(string heroId, string playerName, bool isBot)
        {
            _initHeroId = heroId ?? "";
            _initPlayerName = playerName ?? "";
            _initIsBot = isBot;
        }

        // Server: the name everyone sees (the account's username once the server knows it).
        public void SetPlayerName(string playerName) => _playerName.Value = Truncate(playerName);

        static FixedString64Bytes Truncate(string text)
        {
            text ??= "";
            if (text.Length > 30) text = text[..30];
            return new FixedString64Bytes(text);
        }

        public override void OnNetworkSpawn()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.heroes.Contains(_hero)) game.heroes.Add(_hero);
            if (IsServer)
            {
                _heroId.Value = _initHeroId;
                _playerName.Value = Truncate(_initPlayerName);
                _isBot.Value = _initIsBot;
                _hero.isPlayer = false; // no local input on the server
                _ascension = GetComponent<HeroAscension>();
                for (int i = 0; i < KitSize; i++) _kit.Add(NetItems.None);
                for (int i = 0; i < AbilityCaster.SlotCount * 2; i++) _cooldowns.Add(0f);
                _progression.BountyEarned += SendBounty;
                _hero.Respawned += TeleportOnClients;
                PushState();
                return;
            }

            // Client: set the hero up like the server did (passive, ultimate, ascension tree), mirror its state,
            // and take input if it's ours.
            HeroRoster roster = FindFirstObjectByType<MatchBootstrap>()?.roster;
            HeroDefinition definition = roster != null ? roster.Find(HeroId) : null;
            if (definition != null)
            {
                definition.ApplyTo(_hero);
                _hero.name = IsBot ? $"{definition.displayName} (bot)" : definition.displayName;
            }
            _ascension = GetComponent<HeroAscension>();
            _hero.isPlayer = false;
            if (IsOwner) TakeControl();
            ApplyState();
        }

        // Client: this hero is ours (spawned for us, or handed back when we rejoin).
        void TakeControl()
        {
            _hero.isPlayer = true;
            _hero.Remote = this;
            GameManager game = GameManager.Instance;
            if (game == null) return;
            game.hero = _hero;
            if (game.cameraRig != null)
            {
                game.cameraRig.hero = _hero;
                game.cameraRig.transform.position = transform.position;
            }
        }

        public override void OnGainedOwnership()
        {
            if (!IsServer) TakeControl();
        }

        // Server: its player left; the hero stays in the match and stops where it is.
        public override void OnLostOwnership()
        {
            if (IsServer && OwnerClientId == NetworkManager.ServerClientId) _hero.Stop();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                _progression.BountyEarned -= SendBounty;
                _hero.Respawned -= TeleportOnClients;
            }
            GameManager game = GameManager.Instance;
            if (game != null) game.heroes.Remove(_hero);
        }

        // Server: a respawn moves the hero to the fountain in one step. Clients must jump too: the position sync
        // smooths ordinary moves, which would slide the hero across the map.
        void TeleportOnClients()
        {
            if (_netTransform != null) _netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        // Server: the gold and XP pop-ups, for the player who earned them.
        void SendBounty(int gold, float xp, Vector3 worldPos)
        {
            if (OwnerClientId == NetworkManager.ServerClientId) return; // a bot, or a player who left
            var target = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
            BountyClientRpc(gold, xp, worldPos, target);
        }

        [ClientRpc]
        void BountyClientRpc(int gold, float xp, Vector3 worldPos, ClientRpcParams target = default) =>
            _progression.RaiseBountyFromServer(gold, xp, worldPos);

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) PushState();
            else ApplyState();
        }

        // ---------- Server -> clients ----------

        void PushState()
        {
            _level.Value = _progression.Level;
            _xp.Value = _progression.Xp;
            _gold.Value = _progression.Gold;
            _kills.Value = _progression.Kills;
            _deaths.Value = _progression.Deaths;
            _assists.Value = _progression.Assists;
            _creepScore.Value = _progression.CreepScore;
            _heroDamage.Value = Mathf.RoundToInt(_progression.HeroDamage);
            _totalDamage.Value = Mathf.RoundToInt(_progression.TotalDamage);
            _damageTaken.Value = Mathf.RoundToInt(_progression.DamageTaken);
            _respawn.Value = _hero.Unit.IsAlive ? 0f : Mathf.Max(0f, _hero.RespawnRemaining);
            _mana.Value = Mathf.Round(_caster.Mana);
            _maxMana.Value = _caster.maxMana;
            _cheatToggles.Value = (byte)((_caster.NoManaCost ? 1 : 0) | (_caster.NoCooldowns ? 2 : 0));

            for (int i = 0; i < Inventory.SlotCount; i++) WriteKit(i, NetItems.Id(_inventory.Get(i)));
            float now = ServerNow;
            for (int s = 0; s < AbilityCaster.SlotCount; s++)
            {
                AbilityCaster.Slot slot = _caster.GetSlot(s);
                WriteKit(KitSpells + s, NetItems.Id(slot.ability));
                for (int k = 0; k < AbilityCaster.MaxModSockets; k++)
                    WriteKit(KitMods + s * AbilityCaster.MaxModSockets + k, NetItems.Id(slot.mods[k]));
                WriteKit(KitRanks + s, (short)slot.rank);

                float end = EndTime(slot.cooldownRemaining, _cooldowns[s * 2], now);
                if (_cooldowns[s * 2] != end) _cooldowns[s * 2] = end;
                if (_cooldowns[s * 2 + 1] != slot.cooldownTotal) _cooldowns[s * 2 + 1] = slot.cooldownTotal;
            }
            WriteKit(KitSkillPoints, (short)_caster.SkillPoints);

            if (_ascension != null)
                for (int i = _talents.Count; i < _ascension.PickedOptions.Count; i++) _talents.Add((byte)_ascension.PickedOptions[i]);

            _hasteEnd.Value = EndTime(TryGetComponent(out Haste haste) && haste.enabled ? haste.Remaining : 0f, _hasteEnd.Value, now);
            _shieldEnd.Value = EndTime(TryGetComponent(out ProjectileShield shield) && shield.enabled ? shield.Remaining : 0f,
                _shieldEnd.Value, now);
        }

        void WriteKit(int index, short value)
        {
            if (_kit[index] != value) _kit[index] = value;
        }

        // The server time a timer with `remaining` seconds ends at (0 = not running), keeping `current` when close.
        static float EndTime(float remaining, float current, float now)
        {
            if (remaining <= 0f) return 0f;
            float end = now + remaining;
            return Mathf.Abs(end - current) > TimerTolerance ? end : current;
        }

        // ---------- Clients: show the server's state ----------

        void ApplyState()
        {
            _progression.SetFromServer(_level.Value, _xp.Value, _gold.Value, _kills.Value, _deaths.Value,
                _assists.Value, _creepScore.Value);
            _progression.SetDamageFromServer(_heroDamage.Value, _totalDamage.Value, _damageTaken.Value);
            _hero.SetRespawnFromServer(_hero.Unit.IsAlive, _respawn.Value);
            _caster.SetManaFromServer(_mana.Value, _maxMana.Value);
            _caster.NoManaCost = (_cheatToggles.Value & 1) != 0;
            bool noCooldowns = (_cheatToggles.Value & 2) != 0;
            if (_caster.NoCooldowns != noCooldowns) _caster.NoCooldowns = noCooldowns;

            float now = ServerNow;
            if (_cooldowns.Count == AbilityCaster.SlotCount * 2)
                for (int s = 0; s < AbilityCaster.SlotCount; s++)
                {
                    float end = _cooldowns[s * 2];
                    _caster.SetCooldownFromServer(s, end > 0f ? Mathf.Max(0f, end - now) : 0f, _cooldowns[s * 2 + 1]);
                }

            if (_kit.Count == KitSize && Time.unscaledTime >= _predictUntil) ApplyKit();

            // Talents after the level, which the choices depend on.
            if (_ascension != null)
                while (_ascension.Picked.Count < _talents.Count) _ascension.ChooseFromServer(_talents[_ascension.Picked.Count]);

            Haste.SyncFromServer(_hero.Unit, Remaining(_hasteEnd.Value, now), FindSpell<HasteSpell>()?.auraMaterial);
            ShieldWallSpell wall = FindSpell<ShieldWallSpell>();
            if (wall != null)
                ProjectileShield.SyncFromServer(_hero.Unit, Remaining(_shieldEnd.Value, now), wall.radius, wall.arcDegrees,
                    wall.height, wall.wallMaterial);
        }

        void ApplyKit()
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                InventoryItem item = NetItems.Get(_kit[i]);
                if (_inventory.Get(i) != item) _inventory.Set(i, item);
            }
            for (int s = 0; s < AbilityCaster.SlotCount; s++)
            {
                var spell = NetItems.Get(_kit[KitSpells + s]) as AbilityDefinition;
                if (_caster.GetSlot(s).ability != spell) _caster.SetAbility(s, spell);
                for (int k = 0; k < AbilityCaster.MaxModSockets; k++)
                {
                    var mod = NetItems.Get(_kit[KitMods + s * AbilityCaster.MaxModSockets + k]) as ModDefinition;
                    if (_caster.GetMod(s, k) != mod) _caster.SetMod(s, k, mod);
                }
                _caster.SetRankFromServer(s, _kit[KitRanks + s]);
            }
            _caster.SkillPoints = _kit[KitSkillPoints];
        }

        static float Remaining(float end, float now) => end > 0f ? Mathf.Max(0f, end - now) : 0f;

        // The equipped spell of type T (timed ultimates: their look on the client comes from the spell).
        T FindSpell<T>() where T : AbilityDefinition
        {
            for (int s = 0; s < AbilityCaster.SlotCount; s++)
                if (_caster.GetSlot(s).ability is T spell) return spell;
            return null;
        }

        // ---------- Orders (owning client -> server) ----------

        public void SendMove(Vector3 point) => MoveServerRpc(point);
        public void SendAttack(Unit target) { if (TryRef(target, out NetworkObjectReference r)) AttackServerRpc(r); }
        public void SendAttackMove(Vector3 point, Unit direct)
        {
            bool hasTarget = TryRef(direct, out NetworkObjectReference r);
            AttackMoveServerRpc(point, hasTarget, r);
        }
        public void SendStop() => StopServerRpc();

        public void SendCast(int slot, Vector3 point, Unit target)
        {
            bool hasTarget = TryRef(target, out NetworkObjectReference r);
            CastServerRpc((byte)slot, point, hasTarget, r);
        }

        public void SendLevelUp(int slot) => LevelUpServerRpc((byte)slot);

        public void SendBuy(InventoryItem item)
        {
            short id = NetItems.Id(item);
            if (id != NetItems.None) BuyServerRpc(id);
        }

        public void SendSell(int inventorySlot) => SellServerRpc((byte)inventorySlot);

        // The client already made the move on its copy (see Hud); the server's result replaces it shortly.
        public void SendLoadout(LoadoutAction action, int a, int b, int c)
        {
            _predictUntil = Time.unscaledTime + PredictionSeconds;
            LoadoutServerRpc((byte)action, (sbyte)a, (sbyte)b, (sbyte)c);
        }

        public void SendAscend(int tier, int option) => AscendServerRpc((byte)tier, (byte)option);

        public void SendCheat(CheatAction cheat) => CheatServerRpc((byte)cheat);

        [ServerRpc]
        void MoveServerRpc(Vector3 point) => _hero.OrderMove(point, showMarker: false);

        [ServerRpc]
        void AttackServerRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out NetworkObject obj) && obj.TryGetComponent(out Unit unit) && unit.IsEnemyOf(_hero.Unit))
                _hero.OrderAttack(unit);
        }

        [ServerRpc]
        void AttackMoveServerRpc(Vector3 point, bool hasTarget, NetworkObjectReference target)
        {
            Unit direct = null;
            if (hasTarget && target.TryGet(out NetworkObject obj) && obj.TryGetComponent(out Unit unit) && unit.IsEnemyOf(_hero.Unit))
                direct = unit;
            _hero.IssueAttackMove(point, direct);
        }

        [ServerRpc]
        void StopServerRpc() => _hero.Stop();

        // Walks into range first if needed; the spell's own checks (target, mana, cooldown) apply.
        [ServerRpc]
        void CastServerRpc(byte slot, Vector3 point, bool hasTarget, NetworkObjectReference target)
        {
            if (slot >= AbilityCaster.SlotCount) return;
            Unit unit = null;
            if (hasTarget && target.TryGet(out NetworkObject obj)) obj.TryGetComponent(out unit);
            _hero.OrderCast(slot, point, unit);
        }

        [ServerRpc]
        void LevelUpServerRpc(byte slot)
        {
            if (slot < AbilityCaster.SlotCount) _caster.TryLevelUp(slot);
        }

        // Only what the team's shop sells (ultimates have ids too, but aren't for sale).
        [ServerRpc]
        void BuyServerRpc(short itemId)
        {
            InventoryItem item = NetItems.Get(itemId);
            Shop shop = Shop.ForTeam(_hero.Unit.team);
            if (item == null || shop == null || shop.catalog == null || !shop.catalog.items.Contains(item)) return;
            shop.TryBuy(_hero.Unit, _progression, _inventory, item);
        }

        [ServerRpc]
        void SellServerRpc(byte inventorySlot)
        {
            Shop shop = Shop.ForTeam(_hero.Unit.team);
            if (shop != null && inventorySlot < Inventory.SlotCount) shop.TrySell(_hero.Unit, _progression, _inventory, inventorySlot);
        }

        [ServerRpc]
        void LoadoutServerRpc(byte action, sbyte a, sbyte b, sbyte c) =>
            Loadout.Perform(_inventory, _caster, (LoadoutAction)action, a, b, c);

        // `tier` guards against a double click choosing the next tier too.
        [ServerRpc]
        void AscendServerRpc(byte tier, byte option)
        {
            if (_ascension != null && _ascension.PendingTier == tier) _ascension.Choose(option);
        }

        [ServerRpc]
        void CheatServerRpc(byte cheat)
        {
            if (!NetworkMatch.CheatsAllowed) return;
            switch ((CheatAction)cheat)
            {
                case CheatAction.Level: _progression.GainLevel(); break;
                case CheatAction.Gold: _progression.AddGold(Hud.CheatGold); break;
                case CheatAction.NoManaCost: _caster.NoManaCost = !_caster.NoManaCost; break;
                case CheatAction.NoCooldowns: _caster.NoCooldowns = !_caster.NoCooldowns; break;
                default: return;
            }
            if (GameManager.Instance != null) GameManager.Instance.CheatsUsed = true;
        }

        static bool TryRef(Unit unit, out NetworkObjectReference reference)
        {
            reference = default;
            if (unit == null || !unit.TryGetComponent(out NetworkObject obj) || !obj.IsSpawned) return false;
            reference = obj;
            return true;
        }
    }
}
