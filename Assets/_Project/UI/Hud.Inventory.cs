using System.Collections.Generic;
using Moba.Abilities;
using Moba.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Inventory grid (2x4), shop window, and drag and drop between inventory slots, spell slots and mod sockets:
    //   inventory -> inventory: move/swap         inventory -> spell slot: equip spell / socket mod
    //   spell -> inventory: unequip (or swap)      spell -> spell slot: swap spells
    //   mod socket -> inventory: unsocket          mod socket -> spell slot: move the mod
    // Right-click an inventory item to sell it while the shop is open.
    public partial class Hud
    {
        const float ShopMessageSeconds = 3f;
        const float HudMessageSeconds = 2.5f;
        const string ShopDefaultHint =
            "Click to buy. Drag spells and mods from your inventory onto a spell slot. " +
            "With the shop open, right-click an inventory item to sell it.";

        enum DragKind { None, Inventory, Spell, Mod }

        struct DragSource
        {
            public DragKind Kind;
            public int Index;  // inventory slot or spell slot
            public int Socket; // mod socket (Mod only)
        }

        Inventory _inventory;
        Shop _shop;

        VisualElement _inventoryGrid;
        Label _inventoryGold;
        readonly ItemSlotView[] _itemSlots = new ItemSlotView[Inventory.SlotCount];
        int _hoveredItem = -1;
        int _hoveredSocketSlot = -1;
        int _hoveredSocket = -1;

        VisualElement _shopWindow;
        Label _shopStatus, _shopHint;
        ScrollView _shopList;
        readonly List<ShopItemView> _shopItems = new();
        float _shopMessageUntil;

        Label _hudMessage;
        float _hudMessageUntil;

        ItemTile _dragGhost;
        DragSource _dragFrom;
        int _dropInventory = -1;
        int _dropSpell = -1;

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
                VisualElement element = view.Tile.Root;
                element.AddManipulator(new DragManipulator(this, new DragSource { Kind = DragKind.Inventory, Index = slot }, null));
                element.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    TrySellSlot(slot);
                    evt.StopPropagation();
                });
                element.RegisterCallback<PointerEnterEvent>(_ => _hoveredItem = slot);
                element.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredItem == slot) _hoveredItem = -1;
                });
                _inventoryGrid.Add(element);
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

            _hudMessage = _root.Q<Label>("hudMessage");

            _dragGhost = new ItemTile("item-ghost");
            _dragGhost.Root.pickingMode = PickingMode.Ignore;
            _dragGhost.Root.usageHints = UsageHints.DynamicTransform;
            _dragGhost.Root.AddToClassList("hidden");
            _root.Add(_dragGhost.Root);

            _dragFrom = default;
            _dropInventory = -1;
            _dropSpell = -1;
            _hoveredItem = -1;
            _hoveredSocketSlot = -1;
        }

        // Spell icon: click casts, drag moves the spell. Each mod socket: drag moves the mod; hover shows it.
        void RegisterSpellSlotDragAndDrop(SlotView view, int slot)
        {
            view.Frame.AddManipulator(new DragManipulator(this, new DragSource { Kind = DragKind.Spell, Index = slot },
                () => _hero?.BeginCast(slot)));

            for (int s = 0; s < view.Sockets.Length; s++)
            {
                int socket = s;
                VisualElement element = view.Sockets[s];
                element.AddManipulator(new DragManipulator(this,
                    new DragSource { Kind = DragKind.Mod, Index = slot, Socket = socket }, null));
                element.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    _hoveredSocketSlot = slot;
                    _hoveredSocket = socket;
                });
                element.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredSocketSlot == slot && _hoveredSocket == socket) _hoveredSocketSlot = -1;
                });
            }
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
            {
                bool draggingItem = _dragFrom.Kind == DragKind.Inventory;
                for (int i = 0; i < _itemSlots.Length; i++)
                    _itemSlots[i].Show(_inventory.Get(i), draggingItem && i == _dragFrom.Index, i == _dropInventory);
            }
            for (int i = 0; i < _slots.Length; i++) _slots[i].SetDropTarget(i == _dropSpell);
            if (_progression != null) SetText(_inventoryGold, $"{_progression.Gold}g");

            if (IsShopOpen) RefreshShop();
            if (_hudMessageUntil > 0f && Time.unscaledTime > _hudMessageUntil)
            {
                _hudMessageUntil = 0f;
                _hudMessage.AddToClassList("hidden");
            }
        }

        void ShowHudMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _hudMessage.text = message;
            _hudMessage.RemoveFromClassList("hidden");
            _hudMessageUntil = Time.unscaledTime + HudMessageSeconds;
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

            AddShopSection<ItemDefinition>("ITEMS");
            AddShopSection<AbilityDefinition>("SPELLS");
            AddShopSection<ModDefinition>("MODS");
        }

        void AddShopSection<T>(string title) where T : InventoryItem
        {
            bool any = false;
            foreach (InventoryItem item in _shop.catalog.items)
            {
                if (item is not T) continue;
                if (!any)
                {
                    var header = new Label(title) { pickingMode = PickingMode.Ignore };
                    header.AddToClassList("shop__section");
                    _shopList.Add(header);
                    any = true;
                }
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

        void TryBuy(InventoryItem item)
        {
            if (_shop == null || _progression == null || _inventory == null) return;
            ShopResult result = _shop.TryBuy(_hero.Unit, _progression, _inventory, item);
            ShowShopMessage(result switch
            {
                ShopResult.Ok => item is ItemDefinition
                    ? $"Bought {item.displayName}."
                    : $"Bought {item.displayName}. Drag it onto a spell slot to equip it.",
                ShopResult.NotEnoughGold => $"Not enough gold: {item.displayName} needs {item.cost - _progression.Gold}g more.",
                ShopResult.InventoryFull => "Inventory full. Sell or equip something first.",
                ShopResult.NotInShopRange => "Return to your base to buy.",
                _ => "",
            });
        }

        void TrySellSlot(int slot)
        {
            if (_shop == null || _inventory == null || _progression == null) return;
            InventoryItem item = _inventory.Get(slot);
            if (item == null) return;
            if (!IsShopOpen)
            {
                ShowHudMessage("Open the shop to sell items.");
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

        // ---------- Tooltips ----------

        bool ShowItemTooltip()
        {
            if (_hoveredItem < 0 || _dragFrom.Kind != DragKind.None || _inventory == null) return false;
            InventoryItem item = _inventory.Get(_hoveredItem);
            if (item == null) return false;

            var lines = new List<string> { item.KindLabel };
            if (!string.IsNullOrEmpty(item.StatsText)) lines.Add(item.StatsText);
            if (_shop != null) lines.Add($"Sells for {_shop.SellPrice(item)}g");

            string hint = item is ItemDefinition ? "" : "Drag onto a spell slot to equip.\n";
            hint += IsShopOpen ? "Right-click to sell." : "Open the shop in base to sell.";

            _tooltip.RemoveFromClassList("hidden");
            SetText(_tooltipTitle, item.displayName);
            SetText(_tooltipStats, string.Join("   ·   ", lines));
            SetText(_tooltipBody, string.IsNullOrEmpty(item.description) ? hint : $"{item.description}\n{hint}");
            return true;
        }

        bool ShowSocketTooltip()
        {
            if (_hoveredSocketSlot < 0 || _dragFrom.Kind != DragKind.None || _abilities == null) return false;
            ModDefinition mod = _abilities.GetMod(_hoveredSocketSlot, _hoveredSocket);
            string key = GameInput.Display(GameInput.Instance.Abilities[_hoveredSocketSlot]);

            _tooltip.RemoveFromClassList("hidden");
            if (mod == null)
            {
                SetText(_tooltipTitle, $"[{key}] Empty mod socket");
                SetText(_tooltipStats, "Mod");
                SetText(_tooltipBody, "Drag a mod from your inventory onto this spell slot to socket it.");
                return true;
            }

            bool active = _abilities.IsModActive(_hoveredSocketSlot, _hoveredSocket);
            SetText(_tooltipTitle, $"[{key}] {mod.displayName}{(active ? "" : " (inactive)")}");
            SetText(_tooltipStats, $"Mod   ·   {mod.StatsText}");
            string state = active ? "" : "Doesn't work with the spell in this slot.\n";
            SetText(_tooltipBody, $"{mod.description}\n{state}Drag to your inventory to remove it.");
            return true;
        }

        // ---------- Drag and drop (driven by DragManipulator) ----------

        InventoryItem ItemAt(DragSource source) => source.Kind switch
        {
            DragKind.Inventory => _inventory != null ? _inventory.Get(source.Index) : null,
            DragKind.Spell => _abilities != null ? _abilities.GetSlot(source.Index).ability : null,
            DragKind.Mod => _abilities != null ? _abilities.GetMod(source.Index, source.Socket) : null,
            _ => null,
        };

        bool CanDrag(DragSource source) => ItemAt(source) != null && _inventory != null && _abilities != null;

        void BeginDrag(DragSource source)
        {
            _dragFrom = source;
            _dropInventory = -1;
            _dropSpell = -1;
            _dragGhost.Show(ItemAt(source));
            _dragGhost.Root.style.scale = new Scale(Vector2.one * _heroPanelScale);
            _dragGhost.Root.RemoveFromClassList("hidden");
            _dragGhost.Root.BringToFront();
        }

        void UpdateDrag(Vector2 panelPosition)
        {
            _dragGhost.Root.style.translate = new Translate(panelPosition.x - 23f, panelPosition.y - 23f);
            FindDropTarget(panelPosition, out _dropInventory, out _dropSpell);
        }

        void EndDrag(Vector2 panelPosition)
        {
            FindDropTarget(panelPosition, out int inventorySlot, out int spellSlot);
            DragSource from = _dragFrom;
            CancelDrag();

            string error = null;
            if (inventorySlot >= 0)
            {
                error = from.Kind switch
                {
                    DragKind.Inventory => Move(from.Index, inventorySlot),
                    DragKind.Spell => Loadout.UnequipSpell(_inventory, _abilities, from.Index, inventorySlot),
                    DragKind.Mod => Loadout.UnequipMod(_inventory, _abilities, from.Index, from.Socket, inventorySlot),
                    _ => null,
                };
            }
            else if (spellSlot >= 0)
            {
                error = from.Kind switch
                {
                    DragKind.Inventory => Loadout.EquipFromInventory(_inventory, _abilities, from.Index, spellSlot),
                    DragKind.Spell => Loadout.MoveSpell(_abilities, from.Index, spellSlot),
                    DragKind.Mod => Loadout.MoveMod(_abilities, from.Index, from.Socket, spellSlot),
                    _ => null,
                };
            }
            ShowHudMessage(error);
        }

        string Move(int from, int to)
        {
            _inventory.Move(from, to);
            return null;
        }

        void CancelDrag()
        {
            _dragFrom = default;
            _dropInventory = -1;
            _dropSpell = -1;
            _dragGhost.Root.AddToClassList("hidden");
        }

        void FindDropTarget(Vector2 panelPosition, out int inventorySlot, out int spellSlot)
        {
            inventorySlot = -1;
            spellSlot = -1;
            for (int i = 0; i < _itemSlots.Length; i++)
            {
                if (!_itemSlots[i].Tile.Root.worldBound.Contains(panelPosition)) continue;
                inventorySlot = i;
                return;
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].DropArea.worldBound.Contains(panelPosition)) continue;
                spellSlot = i;
                return;
            }
        }

        // ---------- Views ----------

        // Icon (if the item has one) or the item's colour with its glyph.
        sealed class ItemTile
        {
            public readonly VisualElement Root;
            readonly VisualElement _icon;
            readonly Label _glyph;
            InventoryItem _shown;
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

            public void Show(InventoryItem item)
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

            public void Show(InventoryItem item, bool dragSource, bool dropTarget)
            {
                Tile.Show(item);
                Tile.Root.EnableInClassList("item-slot--filled", item != null);
                Tile.Root.EnableInClassList("item-slot--drag-source", dragSource);
                Tile.Root.EnableInClassList("item-slot--drop-target", dropTarget);
            }
        }

        sealed class ShopItemView
        {
            public readonly InventoryItem Item;
            public readonly VisualElement Root;

            public ShopItemView(InventoryItem item)
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

        // Left-drag to move. A press released without moving counts as a click (onClick, if any).
        sealed class DragManipulator : PointerManipulator
        {
            const float DragThresholdSqr = 36f;

            readonly Hud _hud;
            readonly DragSource _source;
            readonly System.Action _onClick;
            bool _pressed;
            bool _dragging;
            Vector2 _start;
            int _pointerId = -1;

            public DragManipulator(Hud hud, DragSource source, System.Action onClick)
            {
                _hud = hud;
                _source = source;
                _onClick = onClick;
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
                if (!CanStartManipulation(evt)) return;
                if (_onClick == null && !_hud.CanDrag(_source)) return;
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
                if (!_dragging && (position - _start).sqrMagnitude > DragThresholdSqr && _hud.CanDrag(_source))
                {
                    _dragging = true;
                    _hud.BeginDrag(_source);
                }
                if (_dragging) _hud.UpdateDrag(position);
                evt.StopPropagation();
            }

            void OnPointerUp(PointerUpEvent evt)
            {
                if (!_pressed || !CanStopManipulation(evt)) return;
                bool wasDragging = _dragging;
                _pressed = false;
                _dragging = false;
                if (target.HasPointerCapture(_pointerId)) target.ReleasePointer(_pointerId);
                if (wasDragging) _hud.EndDrag(evt.position);
                else _onClick?.Invoke();
                evt.StopPropagation();
            }

            void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (_dragging) _hud.CancelDrag();
                _pressed = false;
                _dragging = false;
            }
        }
    }
}
