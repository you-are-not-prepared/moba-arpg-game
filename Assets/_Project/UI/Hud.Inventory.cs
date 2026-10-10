using System.Collections.Generic;
using Moba.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Inventory grid (2x4, drag to move/swap, right-click to sell while the shop is open) and the shop window.
    public partial class Hud
    {
        const float ShopMessageSeconds = 3f;
        const string ShopDefaultHint =
            "Click an item to buy it. With the shop open, right-click an inventory item to sell it. Drag items to rearrange.";

        Inventory _inventory;
        Shop _shop;

        VisualElement _inventoryGrid;
        Label _inventoryGold;
        readonly ItemSlotView[] _itemSlots = new ItemSlotView[Inventory.SlotCount];
        int _hoveredItem = -1;

        VisualElement _shopWindow;
        Label _shopStatus, _shopHint;
        ScrollView _shopList;
        readonly List<ShopItemView> _shopItems = new();
        float _shopMessageUntil;

        ItemTile _dragGhost;
        int _dragFrom = -1;
        int _dropTarget = -1;

        public bool IsShopOpen => _shopWindow != null && !_shopWindow.ClassListContains("hidden");

        void SetUpInventoryAndShop()
        {
            _inventoryGrid = _root.Q("inventoryGrid");
            _inventoryGold = _root.Q<Label>("inventoryGold");
            _inventoryGrid.Clear();
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                int slot = i;
                var view = new ItemSlotView();
                view.Tile.Root.AddManipulator(new ItemDragManipulator(this, slot));
                view.Tile.Root.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    TrySellSlot(slot);
                    evt.StopPropagation();
                });
                view.Tile.Root.RegisterCallback<PointerEnterEvent>(_ => _hoveredItem = slot);
                view.Tile.Root.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredItem == slot) _hoveredItem = -1;
                });
                _inventoryGrid.Add(view.Tile.Root);
                _itemSlots[i] = view;
            }

            Button shopButton = _root.Q<Button>("shopButton");
            shopButton.focusable = false;
            shopButton.clicked += ToggleShop;

            _shopWindow = _root.Q("shopWindow");
            _shopStatus = _root.Q<Label>("shopStatus");
            _shopHint = _root.Q<Label>("shopHint");
            _shopList = _root.Q<ScrollView>("shopList");
            Button close = _root.Q<Button>("shopClose");
            close.focusable = false;
            close.clicked += CloseShop;
            _shopHint.text = ShopDefaultHint;

            _dragGhost = new ItemTile("item-ghost");
            _dragGhost.Root.pickingMode = PickingMode.Ignore;
            _dragGhost.Root.usageHints = UsageHints.DynamicTransform;
            _dragGhost.Root.AddToClassList("hidden");
            _root.Add(_dragGhost.Root);

            _dragFrom = -1;
            _dropTarget = -1;
            _hoveredItem = -1;
        }

        void BindInventory()
        {
            _inventory = _hero.GetComponent<Inventory>();
            _shop = _game.playerShop != null ? _game.playerShop : Shop.ForTeam(_game.playerTeam);
            BuildShopList();
        }

        void UpdateInventoryAndShop()
        {
            if (!_game.IsOver && GameInput.Instance.OpenShop.WasPressedThisFrame()) ToggleShop();
            if (_game.IsOver && IsShopOpen) CloseShop();

            if (_inventory != null)
                for (int i = 0; i < _itemSlots.Length; i++)
                    _itemSlots[i].Show(_inventory.Get(i), i == _dragFrom, i == _dropTarget && i != _dragFrom);
            if (_progression != null) SetText(_inventoryGold, $"{_progression.Gold}g");

            if (IsShopOpen) RefreshShop();
        }

        // ---------- Shop ----------

        public void ToggleShop()
        {
            if (IsShopOpen) CloseShop();
            else OpenShop();
        }

        public void OpenShop()
        {
            _shopWindow.RemoveFromClassList("hidden");
            _shopHint.text = ShopDefaultHint;
            _shopMessageUntil = 0f;
        }

        public void CloseShop() => _shopWindow.AddToClassList("hidden");

        // Esc closes the shop before it cancels targeting or opens the options menu.
        public bool CloseShopIfOpen()
        {
            if (!IsShopOpen) return false;
            CloseShop();
            return true;
        }

        void BuildShopList()
        {
            _shopList.Clear();
            _shopItems.Clear();
            if (_shop == null || _shop.catalog == null) return;

            foreach (ItemDefinition item in _shop.catalog.items)
            {
                if (item == null) continue;
                var view = new ShopItemView(item);
                view.Root.RegisterCallback<ClickEvent>(_ => TryBuy(item));
                _shopList.Add(view.Root);
                _shopItems.Add(view);
            }
        }

        void RefreshShop()
        {
            bool canUse = _shop != null && _shop.CanUse(_hero.Unit);
            SetText(_shopStatus, _shop == null
                ? "No shop for your team."
                : canUse ? "In base: you can buy and sell." : "Return to your base to buy or sell.");
            _shopStatus.EnableInClassList("shop__status--locked", !canUse);

            if (_shop != null && _progression != null && _inventory != null)
            {
                foreach (ShopItemView view in _shopItems)
                {
                    ShopResult result = _shop.CanBuy(_hero.Unit, _progression, _inventory, view.Item);
                    view.Root.EnableInClassList("shop-item--unaffordable", _progression.Gold < view.Item.cost);
                    view.Root.EnableInClassList("shop-item--unavailable", result != ShopResult.Ok);
                }
            }

            if (_shopMessageUntil > 0f && Time.unscaledTime > _shopMessageUntil)
            {
                _shopMessageUntil = 0f;
                _shopHint.text = ShopDefaultHint;
            }
        }

        void TryBuy(ItemDefinition item)
        {
            if (_shop == null || _progression == null || _inventory == null) return;
            ShopResult result = _shop.TryBuy(_hero.Unit, _progression, _inventory, item);
            ShowShopMessage(result switch
            {
                ShopResult.Ok => $"Bought {item.displayName}.",
                ShopResult.NotEnoughGold => $"Not enough gold: {item.displayName} needs {item.cost - _progression.Gold}g more.",
                ShopResult.InventoryFull => "Inventory full. Sell an item first.",
                ShopResult.NotInShopRange => "Return to your base to buy.",
                _ => "",
            });
        }

        void TrySellSlot(int slot)
        {
            if (_shop == null || _inventory == null || _progression == null) return;
            ItemDefinition item = _inventory.Get(slot);
            if (item == null) return;
            if (!IsShopOpen)
            {
                ShowShopMessage("Open the shop to sell items.");
                return;
            }
            int price = _shop.SellPrice(item);
            ShopResult result = _shop.TrySell(_hero.Unit, _progression, _inventory, slot);
            ShowShopMessage(result == ShopResult.Ok
                ? $"Sold {item.displayName} for {price}g."
                : "Return to your base to sell.");
        }

        void ShowShopMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _shopHint.text = message;
            _shopMessageUntil = Time.unscaledTime + ShopMessageSeconds;
        }

        // ---------- Item tooltip ----------

        bool ShowItemTooltip()
        {
            if (_hoveredItem < 0 || _dragFrom >= 0 || _inventory == null) return false;
            ItemDefinition item = _inventory.Get(_hoveredItem);
            if (item == null) return false;

            _tooltip.RemoveFromClassList("hidden");
            string sell = _shop != null ? $"Sells for {_shop.SellPrice(item)}g" : "";
            SetText(_tooltipTitle, item.displayName);
            SetText(_tooltipStats, string.IsNullOrEmpty(item.StatsText) ? sell : $"{item.StatsText}   ·   {sell}");
            string action = IsShopOpen ? "Right-click to sell." : "Open the shop in base to sell.";
            SetText(_tooltipBody, string.IsNullOrEmpty(item.description) ? action : $"{item.description}\n{action}");
            return true;
        }

        // ---------- Drag and drop (called by ItemDragManipulator) ----------

        bool CanDrag(int slot) => _inventory != null && _inventory.Get(slot) != null;

        void BeginItemDrag(int slot)
        {
            _dragFrom = slot;
            _dropTarget = -1;
            _dragGhost.Show(_inventory.Get(slot));
            _dragGhost.Root.style.scale = new Scale(Vector2.one * _heroPanelScale);
            _dragGhost.Root.RemoveFromClassList("hidden");
            _dragGhost.Root.BringToFront();
        }

        void UpdateItemDrag(Vector2 panelPosition)
        {
            _dragGhost.Root.style.translate = new Translate(panelPosition.x - 23f, panelPosition.y - 23f);
            _dropTarget = SlotAt(panelPosition);
        }

        void EndItemDrag(Vector2 panelPosition)
        {
            int to = SlotAt(panelPosition);
            if (_dragFrom >= 0 && to >= 0 && to != _dragFrom) _inventory.Move(_dragFrom, to);
            CancelItemDrag();
        }

        void CancelItemDrag()
        {
            _dragFrom = -1;
            _dropTarget = -1;
            _dragGhost.Root.AddToClassList("hidden");
        }

        int SlotAt(Vector2 panelPosition)
        {
            for (int i = 0; i < _itemSlots.Length; i++)
                if (_itemSlots[i].Tile.Root.worldBound.Contains(panelPosition)) return i;
            return -1;
        }

        // ---------- Views ----------

        // Icon (if the item has one) or the item's colour with its glyph.
        sealed class ItemTile
        {
            public readonly VisualElement Root;
            readonly VisualElement _icon;
            readonly Label _glyph;
            ItemDefinition _shown;
            bool _initialized;

            public ItemTile(string rootClass)
            {
                Root = new VisualElement();
                Root.AddToClassList(rootClass);
                _icon = new VisualElement { pickingMode = PickingMode.Ignore };
                _icon.AddToClassList("item-slot__icon");
                _glyph = new Label { pickingMode = PickingMode.Ignore };
                _glyph.AddToClassList("item-slot__glyph");
                Root.Add(_icon);
                Root.Add(_glyph);
            }

            public void Show(ItemDefinition item)
            {
                if (_initialized && item == _shown) return;
                _initialized = true;
                _shown = item;
                bool hasIcon = item != null && item.icon != null;
                _icon.style.backgroundImage = hasIcon ? new StyleBackground(item.icon) : new StyleBackground(StyleKeyword.None);
                _glyph.text = item != null && !hasIcon ? item.glyph : "";
                // Per-item colour is data, not style; clear it to fall back to the USS slot colour.
                Root.style.backgroundColor = item != null && !hasIcon ? new StyleColor(item.color) : new StyleColor(StyleKeyword.Null);
            }
        }

        sealed class ItemSlotView
        {
            public readonly ItemTile Tile = new("item-slot");

            public void Show(ItemDefinition item, bool dragSource, bool dropTarget)
            {
                Tile.Show(item);
                Tile.Root.EnableInClassList("item-slot--filled", item != null);
                Tile.Root.EnableInClassList("item-slot--drag-source", dragSource);
                Tile.Root.EnableInClassList("item-slot--drop-target", dropTarget);
            }
        }

        sealed class ShopItemView
        {
            public readonly ItemDefinition Item;
            public readonly VisualElement Root;

            public ShopItemView(ItemDefinition item)
            {
                Item = item;
                Root = new VisualElement();
                Root.AddToClassList("shop-item");

                var tile = new ItemTile("shop-item__tile");
                tile.Root.pickingMode = PickingMode.Ignore;
                tile.Show(item);
                Root.Add(tile.Root);

                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.AddToClassList("shop-item__info");
                var name = new Label(item.displayName) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("shop-item__name");
                var stats = new Label(item.StatsText) { pickingMode = PickingMode.Ignore };
                stats.AddToClassList("shop-item__stats");
                info.Add(name);
                info.Add(stats);
                Root.Add(info);

                var cost = new Label($"{item.cost}g") { pickingMode = PickingMode.Ignore };
                cost.AddToClassList("shop-item__cost");
                Root.Add(cost);

                if (!string.IsNullOrEmpty(item.description)) Root.tooltip = item.description;
            }
        }

        // Left-drag an inventory slot onto another slot to move/swap. A short press without moving is not a drag.
        sealed class ItemDragManipulator : PointerManipulator
        {
            const float DragThresholdSqr = 36f;

            readonly Hud _hud;
            readonly int _slot;
            bool _pressed;
            bool _dragging;
            Vector2 _start;
            int _pointerId = -1;

            public ItemDragManipulator(Hud hud, int slot)
            {
                _hud = hud;
                _slot = slot;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            void OnPointerDown(PointerDownEvent evt)
            {
                if (!CanStartManipulation(evt) || !_hud.CanDrag(_slot)) return;
                _pressed = true;
                _dragging = false;
                _start = evt.position;
                _pointerId = evt.pointerId;
                target.CapturePointer(_pointerId);
                evt.StopPropagation();
            }

            void OnPointerMove(PointerMoveEvent evt)
            {
                if (!_pressed || !target.HasPointerCapture(evt.pointerId)) return;
                Vector2 position = evt.position;
                if (!_dragging && (position - _start).sqrMagnitude > DragThresholdSqr)
                {
                    _dragging = true;
                    _hud.BeginItemDrag(_slot);
                }
                if (_dragging) _hud.UpdateItemDrag(position);
                evt.StopPropagation();
            }

            void OnPointerUp(PointerUpEvent evt)
            {
                if (!_pressed || !CanStopManipulation(evt)) return;
                bool wasDragging = _dragging;
                _pressed = false;
                _dragging = false;
                if (wasDragging) _hud.EndItemDrag(evt.position);
                if (target.HasPointerCapture(_pointerId)) target.ReleasePointer(_pointerId);
                evt.StopPropagation();
            }

            void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (_dragging) _hud.CancelItemDrag();
                _pressed = false;
                _dragging = false;
            }
        }
    }
}
