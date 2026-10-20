using System.Collections.Generic;
using System.Linq;
using Moba.Abilities;
using Moba.Items;
using Moba.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // The expanded part of a match history entry: per player K/D/A, CS, damage (to heroes, total, taken),
    // spells with their mods, and final items. Both teams, the player's team first.
    public partial class FrontendController
    {
        Dictionary<string, InventoryItem> _itemsById;

        void BuildDetails(VisualElement container, MatchDetailResponse detail)
        {
            MatchDetailPlayer me = detail.players.Find(p => p.slot == detail.slot);
            string myTeam = me?.team;
            // Bars compare everyone in this match against its biggest number.
            int maxHeroDamage = Mathf.Max(1, detail.players.Max(p => p.heroDamage));
            int maxTaken = Mathf.Max(1, detail.players.Max(p => p.damageTaken));

            container.Add(DetailsHeader());
            foreach (bool mine in new[] { true, false })
            {
                bool won = mine ? detail.won : !detail.won;
                Label team = Text(mine ? "YOUR TEAM" : "ENEMY TEAM", "match-details__team");
                team.AddToClassList(won ? "match-details__team--win" : "match-details__team--loss");
                team.text += won ? " · VICTORY" : " · DEFEAT";
                container.Add(team);
                foreach (MatchDetailPlayer p in detail.players.Where(p => (p.team == myTeam) == mine))
                    container.Add(DetailsRow(p, p.slot == detail.slot, maxHeroDamage, maxTaken));
            }
            if (detail.players.Any(p => !p.hasDetails))
                container.Add(Text("Damage and items weren't recorded for matches played before they were tracked.",
                    "match-details__status"));
        }

        static VisualElement DetailsHeader()
        {
            VisualElement header = Block("details-row");
            header.AddToClassList("details-row--labels");
            foreach ((string text, string column) in new[]
                     {
                         ("PLAYER", "details-col--player"), ("K / D / A", "details-col--kda"), ("CS", "details-col--cs"),
                         ("DAMAGE TO HEROES", "details-col--damage"), ("TOTAL DAMAGE", "details-col--total"),
                         ("DAMAGE TAKEN", "details-col--damage"), ("SPELLS", "details-col--spells"), ("ITEMS", "details-col--items"),
                     })
            {
                Label label = Text(text, "details-col");
                label.AddToClassList(column);
                header.Add(label);
            }
            return header;
        }

        VisualElement DetailsRow(MatchDetailPlayer p, bool isMe, int maxHeroDamage, int maxTaken)
        {
            VisualElement row = Block("details-row");
            row.EnableInClassList("details-row--me", isMe);

            VisualElement player = Block("details-col--player");
            player.AddToClassList("details-player");
            player.Add(Glyph(p.heroId, "history-mini"));
            VisualElement names = Block(null);
            names.Add(Text($"{HeroName(p.heroId)}  ·  Lv {p.level}", "details-player__hero"));
            names.Add(Text(isMe ? "You" : p.isBot ? "Bot" : p.username ?? "Player", "details-player__name"));
            player.Add(names);
            row.Add(player);

            row.Add(Cell($"{p.kills} / {p.deaths} / {p.assists}", "details-col--kda"));
            row.Add(Cell(p.creepScore.ToString(), "details-col--cs"));
            row.Add(p.hasDetails ? Bar(p.heroDamage, maxHeroDamage, "details-bar--damage") : Cell("-", "details-col--damage"));
            row.Add(Cell(p.hasDetails ? p.totalDamage.ToString("N0") : "-", "details-col--total"));
            row.Add(p.hasDetails ? Bar(p.damageTaken, maxTaken, "details-bar--taken") : Cell("-", "details-col--damage"));

            VisualElement spells = Block("details-col--spells");
            spells.AddToClassList("details-tiles");
            VisualElement items = Block("details-col--items");
            items.AddToClassList("details-tiles");
            if (p.hasDetails && p.build != null)
            {
                foreach (SpellBuild spell in p.build.spells ?? new List<SpellBuild>()) spells.Add(SpellTile(spell));
                foreach (string item in p.build.items ?? new List<string>()) items.Add(ItemTile(item, "details-tile--item"));
            }
            row.Add(spells);
            row.Add(items);
            return row;
        }

        static Label Cell(string text, string column)
        {
            Label label = Text(text, "details-cell");
            label.AddToClassList(column);
            return label;
        }

        // A number with a bar behind it, scaled to the match's biggest value.
        static VisualElement Bar(int value, int max, string colorClass)
        {
            VisualElement cell = Block("details-col--damage");
            cell.AddToClassList("details-bar");
            VisualElement fill = Block("details-bar__fill");
            fill.AddToClassList(colorClass);
            fill.style.width = Length.Percent(100f * value / max);
            cell.Add(fill);
            cell.Add(Text(value.ToString("N0"), "details-bar__value"));
            return cell;
        }

        // Spell icon with its socketed mods as small icons underneath.
        VisualElement SpellTile(SpellBuild spell)
        {
            VisualElement column = Block("details-spell");
            column.Add(ItemTile(spell.id, "details-tile--spell", spell));
            VisualElement mods = Block("details-mods");
            foreach (string mod in spell.mods ?? new List<string>()) mods.Add(ItemTile(mod, "details-tile--mod", spell));
            column.Add(mods);
            return column;
        }

        // `spell` is the spell slot the tile belongs to (for spells and mods), or null for inventory items.
        VisualElement ItemTile(string id, string sizeClass, SpellBuild spell = null)
        {
            VisualElement tile = Block("details-tile");
            tile.AddToClassList(sizeClass);
            InventoryItem item = FindItem(id);
            if (item == null) return tile; // empty slot (or something removed from the game since)
            // Per-item colour is data, so it's set here rather than in USS.
            tile.style.backgroundColor = item.color;
            if (sizeClass != "details-tile--mod") tile.Add(Text(item.glyph, "details-tile__glyph"));

            tile.pickingMode = PickingMode.Position;
            tile.AddToClassList("details-tile--hoverable");
            tile.RegisterCallback<PointerEnterEvent>(_ => ShowItemTooltip(tile, item, spell));
            tile.RegisterCallback<PointerLeaveEvent>(_ => HideItemTooltip(tile));
            return tile;
        }

        // ---------- Tooltip (same wording as the in-game one) ----------

        VisualElement _itemTooltip, _itemTooltipAnchor;
        Label _itemTooltipTitle, _itemTooltipStats, _itemTooltipBody;

        void SetUpItemTooltip(VisualElement root)
        {
            _itemTooltip = root.Q("itemTooltip");
            _itemTooltipTitle = root.Q<Label>("itemTooltipTitle");
            _itemTooltipStats = root.Q<Label>("itemTooltipStats");
            _itemTooltipBody = root.Q<Label>("itemTooltipBody");
            // Re-place once the tooltip knows its height (it changes with the text).
            _itemTooltip.RegisterCallback<GeometryChangedEvent>(_ => PlaceItemTooltip());
        }

        void ShowItemTooltip(VisualElement anchor, InventoryItem item, SpellBuild slot)
        {
            _itemTooltipAnchor = anchor;
            string stats, body = item.description ?? "";
            switch (item)
            {
                case AbilityDefinition spell:
                {
                    // With the mods that were socketed and the rank it had at the end of the match.
                    var modifiers = SpellModifiers.None;
                    var modNames = new List<string>();
                    foreach (string modId in slot?.mods ?? new List<string>())
                    {
                        if (FindItem(modId) is not ModDefinition mod) continue;
                        bool active = mod.Supports(spell);
                        if (active) mod.Apply(ref modifiers);
                        modNames.Add(active ? mod.displayName : $"{mod.displayName} (inactive)");
                    }
                    int rank = slot != null ? slot.rank : 0;
                    string described = spell.Describe(Mathf.Max(1, rank), modifiers);
                    string rankText = rank > 0 ? $"Rank {rank}" : "Not learned";
                    stats = string.IsNullOrEmpty(described) ? $"Spell   ·   {rankText}" : $"Spell   ·   {rankText}   ·   {described}";
                    if (modNames.Count > 0) body = $"{body}\nMods: {string.Join(", ", modNames)}".Trim();
                    break;
                }
                case ModDefinition mod:
                {
                    stats = $"Mod   ·   {mod.StatsText}";
                    var socketedIn = FindItem(slot?.id) as AbilityDefinition;
                    if (socketedIn != null && !mod.Supports(socketedIn))
                        body = $"{body}\nDoesn't work with {socketedIn.displayName}.";
                    break;
                }
                default:
                    stats = string.IsNullOrEmpty(item.StatsText) ? item.KindLabel : $"{item.KindLabel}   ·   {item.StatsText}";
                    break;
            }

            _itemTooltipTitle.text = item.displayName;
            _itemTooltipStats.text = stats;
            _itemTooltipBody.text = body;
            _itemTooltipBody.EnableInClassList("hidden", string.IsNullOrEmpty(body));
            _itemTooltip.RemoveFromClassList("hidden");
            _itemTooltip.BringToFront();
            PlaceItemTooltip();
        }

        void HideItemTooltip(VisualElement anchor)
        {
            if (_itemTooltipAnchor != anchor) return;
            _itemTooltipAnchor = null;
            _itemTooltip.AddToClassList("hidden");
        }

        // Below the hovered tile, or above it when there's no room; never off the side of the screen.
        void PlaceItemTooltip()
        {
            if (_itemTooltipAnchor == null || _itemTooltip.ClassListContains("hidden")) return;
            Rect anchor = _itemTooltipAnchor.worldBound;
            Rect screen = _itemTooltip.parent.worldBound;
            float width = _itemTooltip.resolvedStyle.width;
            float height = _itemTooltip.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height)) return;

            float left = Mathf.Clamp(anchor.xMin, 8f, screen.width - width - 8f);
            float top = anchor.yMax + 6f;
            if (top + height > screen.height - 8f) top = anchor.yMin - height - 6f;
            _itemTooltip.style.left = left - screen.xMin;
            _itemTooltip.style.top = top - screen.yMin;
        }

        // Shop items, spells and mods by asset name, plus every hero's ultimate.
        InventoryItem FindItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_itemsById == null)
            {
                _itemsById = new Dictionary<string, InventoryItem>();
                if (items != null)
                    foreach (InventoryItem item in items.items)
                        if (item != null) _itemsById[item.name] = item;
                foreach (HeroDefinition hero in roster.heroes)
                    if (hero != null && hero.ultimate != null) _itemsById[hero.ultimate.name] = hero.ultimate;
            }
            return _itemsById.TryGetValue(id, out InventoryItem found) ? found : null;
        }
    }
}
