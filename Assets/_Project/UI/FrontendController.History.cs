using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Moba.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Match history (start screen, logged in only): the account's totals and its recent finished matches.
    public partial class FrontendController
    {
        const int HistoryLength = 20;

        VisualElement _historyScreen, _historyList;
        Label _historySummary, _historyStatus;
        Button _historyButton;
        bool _historyLoading;

        void SetUpHistory(VisualElement root)
        {
            _historyScreen = root.Q("historyScreen");
            _historyList = root.Q("historyList");
            _historySummary = root.Q<Label>("historySummary");
            _historyStatus = root.Q<Label>("historyStatus");
            _historyButton = root.Q<Button>("historyButton");
            _historyButton.clicked += () => _ = ShowHistory();
            root.Q<Button>("historyBack").clicked += ShowStart;
        }

        async Task ShowHistory()
        {
            if (_historyLoading) return;
            ShowOnly(_historyScreen);
            _historyList.Clear();
            _historySummary.text = "";
            SetHistoryStatus("Loading…");

            _historyLoading = true;
            var (stats, statsError) = await MatchService.Stats();
            var (history, historyError) = await MatchService.History(HistoryLength);
            _historyLoading = false;
            if (this == null) return;

            if (statsError != null || historyError != null)
            {
                SetHistoryStatus(statsError ?? historyError);
                return;
            }
            _historySummary.text = Summary(stats);
            if (history.matches.Count == 0)
            {
                SetHistoryStatus("No finished matches yet. Play one to the end and it shows up here.");
                return;
            }
            SetHistoryStatus(null);
            foreach (MatchSummary match in history.matches) _historyList.Add(HistoryEntry(match));
        }

        // A match row; clicking it opens (and the first time loads) its details underneath.
        VisualElement HistoryEntry(MatchSummary match)
        {
            var entry = new VisualElement();
            entry.AddToClassList("history-entry");
            VisualElement row = HistoryRow(match);
            Label toggle = Text("+", "history-row__toggle");
            row.Add(toggle);
            var details = new VisualElement();
            details.AddToClassList("match-details");
            details.AddToClassList("hidden");
            entry.Add(row);
            entry.Add(details);

            bool loading = false;
            row.RegisterCallback<ClickEvent>(async _ =>
            {
                bool open = details.ClassListContains("hidden");
                details.EnableInClassList("hidden", !open);
                row.EnableInClassList("history-row--open", open);
                toggle.text = open ? "-" : "+";
                if (!open) HideItemTooltip(_itemTooltipAnchor);
                if (!open || loading || details.childCount > 0) return;

                loading = true;
                details.Add(Text("Loading details…", "match-details__status"));
                var (detail, error) = await MatchService.Detail(match.matchId);
                loading = false;
                if (this == null) return;
                details.Clear();
                if (error != null) details.Add(Text(error, "match-details__status"));
                else BuildDetails(details, detail);
            });
            return entry;
        }

        void SetHistoryStatus(string text)
        {
            _historyStatus.text = text ?? "";
            _historyStatus.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        string Summary(PlayerStatsResponse stats)
        {
            if (stats.matches == 0) return "No stats yet (matches where cheats were used don't count).";
            float n = stats.matches;
            string favorite = stats.heroes.Count > 0 ? HeroName(stats.heroes[0].heroId) : "-";
            return $"{Plural(stats.matches, "match", "matches")} · {Plural(stats.wins, "win", "wins")} · " +
                   $"{100f * stats.wins / n:0}% win rate · " +
                   $"average K/D/A {stats.kills / n:0.#} / {stats.deaths / n:0.#} / {stats.assists / n:0.#} · " +
                   $"most played: {favorite}";
        }

        VisualElement HistoryRow(MatchSummary match)
        {
            MatchSummaryPlayer me = match.Me;
            var row = new VisualElement();
            row.AddToClassList("history-row");
            row.AddToClassList(match.won ? "history-row--win" : "history-row--loss");

            VisualElement result = Block("history-row__result");
            result.Add(Text(match.won ? "VICTORY" : "DEFEAT", "history-row__outcome"));
            result.Add(Text($"{match.mode} · {MapName(match.mapLayout)}", "history-row__small"));
            row.Add(result);

            VisualElement hero = Block("history-row__hero");
            hero.Add(Glyph(me?.heroId, "history-glyph"));
            VisualElement heroText = Block(null);
            heroText.Add(Text(HeroName(me?.heroId), "history-row__name"));
            heroText.Add(Text($"Level {me?.level}", "history-row__small"));
            hero.Add(heroText);
            row.Add(hero);

            VisualElement kda = Block("history-row__stat");
            kda.Add(Text(me != null ? $"{me.kills} / {me.deaths} / {me.assists}" : "-", "history-row__big"));
            kda.Add(Text("K / D / A", "history-row__small"));
            row.Add(kda);

            VisualElement cs = Block("history-row__stat");
            cs.Add(Text(me?.creepScore.ToString() ?? "-", "history-row__big"));
            cs.Add(Text("CS", "history-row__small"));
            row.Add(cs);

            // Everyone's heroes: my team, then theirs.
            VisualElement teams = Block("history-row__teams");
            string myTeam = me?.team;
            foreach (MatchSummaryPlayer p in match.players.Where(p => p.team == myTeam)) teams.Add(Mini(p, p.slot == match.slot));
            teams.Add(Text("vs", "history-row__vs"));
            foreach (MatchSummaryPlayer p in match.players.Where(p => p.team != myTeam)) teams.Add(Mini(p, false));
            row.Add(teams);

            VisualElement when = Block("history-row__when");
            when.Add(Text($"{match.durationSeconds / 60}:{match.durationSeconds % 60:00}", "history-row__big"));
            when.Add(Text(FormatDate(match.startedAt), "history-row__small"));
            if (match.cheatsUsed) when.Add(Text("CHEATS USED", "history-tag"));
            row.Add(when);
            return row;
        }

        Label Mini(MatchSummaryPlayer player, bool isMe)
        {
            Label mini = Glyph(player.heroId, "history-mini");
            mini.EnableInClassList("history-mini--you", isMe);
            mini.tooltip = $"{HeroName(player.heroId)} ({(player.isBot ? "bot" : player.username)}) " +
                           $"{player.kills}/{player.deaths}/{player.assists}";
            return mini;
        }

        Label Glyph(string heroId, string className)
        {
            HeroDefinition def = roster.Find(heroId);
            Label glyph = Text(def != null ? def.glyph : "?", className);
            // Hero colour is data, so it's set here rather than in USS.
            glyph.style.backgroundColor = def != null ? def.color : Color.gray;
            return glyph;
        }

        string HeroName(string heroId) => roster.Find(heroId)?.displayName ?? heroId ?? "-";

        string MapName(string layoutId) =>
            layouts != null ? layouts.layouts.Find(l => l.id == layoutId)?.displayName ?? layoutId : layoutId;

        static string FormatDate(string iso) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime utc)
                ? utc.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture)
                : "";

        static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

        static VisualElement Block(string className)
        {
            var block = new VisualElement { pickingMode = PickingMode.Ignore };
            if (className != null) block.AddToClassList(className);
            return block;
        }
    }
}
