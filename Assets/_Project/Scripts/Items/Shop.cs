using System.Collections.Generic;
using UnityEngine;

namespace Moba.Items
{
    public enum ShopResult { Ok, NotInShopRange, NotEnoughGold, InventoryFull, NoItem }

    // A team's shop. Buying and selling only work inside its radius (or while dead, as in the fountain).
    public class Shop : MonoBehaviour
    {
        static readonly List<Shop> s_All = new();

        public Team team;
        public ItemCatalog catalog;
        [Tooltip("Heroes within this distance (XZ) of the shop can buy and sell.")]
        public float radius = 14f;
        [Range(0f, 1f)] public float sellRefund = 0.7f;
        [Tooltip("Fountain: allied heroes inside the radius regain this fraction of max health and mana per second.")]
        public float regenFractionPerSecond = 0.1f;

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        void Update()
        {
            // On a game-server client the server heals; health and mana arrive from there.
            if (regenFractionPerSecond <= 0f || Net.NetSession.IsRemoteClient) return;
            float fraction = regenFractionPerSecond * Time.deltaTime;
            foreach (Unit u in Unit.All)
            {
                if (u.kind != UnitKind.Hero || !u.IsAlive || !CanUse(u)) continue;
                u.Heal(u.maxHealth * fraction);
                if (u.TryGetComponent(out Abilities.AbilityCaster caster)) caster.RestoreMana(caster.maxMana * fraction);
            }
        }

        public static Shop ForTeam(Team team)
        {
            foreach (Shop s in s_All)
                if (s.team == team) return s;
            return null;
        }

        public bool CanUse(Unit hero)
        {
            if (hero == null || hero.team != team) return false;
            if (!hero.IsAlive) return true;
            Vector3 d = hero.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude <= radius;
        }

        public int SellPrice(InventoryItem item) => item != null ? Mathf.FloorToInt(item.cost * sellRefund) : 0;

        public ShopResult CanBuy(Unit hero, HeroProgression wallet, Inventory inventory, InventoryItem item)
        {
            if (item == null) return ShopResult.NoItem;
            if (!CanUse(hero)) return ShopResult.NotInShopRange;
            if (inventory.IsFull) return ShopResult.InventoryFull;
            if (wallet.Gold < item.cost) return ShopResult.NotEnoughGold;
            return ShopResult.Ok;
        }

        public ShopResult TryBuy(Unit hero, HeroProgression wallet, Inventory inventory, InventoryItem item)
        {
            ShopResult result = CanBuy(hero, wallet, inventory, item);
            if (result != ShopResult.Ok) return result;
            wallet.TrySpendGold(item.cost);
            inventory.TryAdd(item, out _);
            return ShopResult.Ok;
        }

        public ShopResult TrySell(Unit hero, HeroProgression wallet, Inventory inventory, int slot)
        {
            InventoryItem item = inventory.Get(slot);
            if (item == null) return ShopResult.NoItem;
            if (!CanUse(hero)) return ShopResult.NotInShopRange;
            inventory.RemoveAt(slot);
            wallet.AddGold(SellPrice(item));
            return ShopResult.Ok;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = team == Team.Blue ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
