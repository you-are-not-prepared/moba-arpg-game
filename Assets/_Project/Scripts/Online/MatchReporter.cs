using Moba.Abilities;
using Moba.Bots;
using Moba.Items;
using UnityEngine;

namespace Moba.Online
{
    // When a core falls, takes a snapshot of every player (level, K/D/A, CS, damage, build) for the post-match
    // analysis screen. While logged in it also records the match on the account: registered with the server when
    // the match starts, the snapshot sent when it ends. Matches that are left early stay unfinished there.
    [RequireComponent(typeof(GameManager))]
    public class MatchReporter : MonoBehaviour
    {
        GameManager _game;
        string _matchId;
        bool _startPending;
        MatchDetailResponse _pendingReport;

        async void Start()
        {
            _game = GetComponent<GameManager>();
            _game.MatchEnded += OnMatchEnded;
            if (!AccountService.IsLoggedIn || _game.heroes.Count == 0) return;

            var request = new StartMatchRequest { mode = MatchSettings.Mode.ToString(), mapLayout = MapLayoutId() };
            // Slots follow game.heroes (the lineup order MatchBootstrap spawned them in).
            foreach (HeroController hero in _game.heroes)
            {
                request.players.Add(new StartMatchPlayer
                {
                    team = hero.Unit.team.ToString(),
                    heroId = HeroId(hero),
                    isBot = IsBot(hero),
                    isLocal = hero == _game.hero,
                });
            }

            _startPending = true;
            _matchId = await MatchService.Start(request);
            _startPending = false;
            // A match can only end before the server replied if the reply was very slow.
            if (this != null && _pendingReport != null) Report(_pendingReport);
        }

        void OnDestroy()
        {
            if (_game != null) _game.MatchEnded -= OnMatchEnded;
        }

        void OnMatchEnded(Team winner)
        {
            MatchDetailResponse snapshot = Snapshot(winner);
            PostMatch.Last = snapshot;
            if (_startPending) _pendingReport = snapshot;
            else Report(snapshot);
        }

        MatchDetailResponse Snapshot(Team winner)
        {
            var detail = new MatchDetailResponse
            {
                winnerTeam = winner.ToString(),
                mode = MatchSettings.Mode.ToString(),
                mapLayout = MapLayoutId(),
                durationSeconds = Mathf.RoundToInt(_game.MatchTime),
                cheatsUsed = _game.CheatsUsed,
            };
            for (int slot = 0; slot < _game.heroes.Count; slot++)
            {
                HeroController hero = _game.heroes[slot];
                var progression = hero.GetComponent<HeroProgression>();
                bool isLocal = hero == _game.hero;
                if (isLocal)
                {
                    detail.slot = slot;
                    detail.won = hero.Unit.team == winner;
                }
                detail.players.Add(new MatchDetailPlayer
                {
                    slot = slot,
                    team = hero.Unit.team.ToString(),
                    heroId = HeroId(hero),
                    isBot = IsBot(hero),
                    username = isLocal && AccountService.IsLoggedIn ? AccountService.Profile.username : null,
                    level = progression.Level,
                    kills = progression.Kills,
                    deaths = progression.Deaths,
                    assists = progression.Assists,
                    creepScore = progression.CreepScore,
                    hasDetails = true,
                    heroDamage = Mathf.RoundToInt(progression.HeroDamage),
                    totalDamage = Mathf.RoundToInt(progression.TotalDamage),
                    damageTaken = Mathf.RoundToInt(progression.DamageTaken),
                    build = BuildOf(hero),
                });
            }
            return detail;
        }

        async void Report(MatchDetailResponse snapshot)
        {
            if (string.IsNullOrEmpty(_matchId)) return;
            string matchId = _matchId;
            _matchId = null; // once

            var result = new MatchResultRequest { winnerTeam = snapshot.winnerTeam, cheatsUsed = snapshot.cheatsUsed };
            foreach (MatchDetailPlayer p in snapshot.players)
            {
                result.players.Add(new PlayerResult
                {
                    slot = p.slot,
                    level = p.level,
                    kills = p.kills,
                    deaths = p.deaths,
                    assists = p.assists,
                    creepScore = p.creepScore,
                    heroDamage = p.heroDamage,
                    totalDamage = p.totalDamage,
                    damageTaken = p.damageTaken,
                    build = p.build,
                });
            }

            string error = await MatchService.Report(matchId, result);
            if (error != null) Debug.LogWarning($"[Matches] Result not recorded: {error}");
        }

        string MapLayoutId() => TryGetComponent(out MapLayout layout) && layout.info != null ? layout.info.id : "unknown";

        static string HeroId(HeroController hero) => hero.definition != null ? hero.definition.id : "unknown";

        static bool IsBot(HeroController hero) => hero.GetComponent<BotBrain>() != null;

        // Inventory (8 slots) and Q/W/E/R with their rank and mods, by asset name.
        static PlayerBuild BuildOf(HeroController hero)
        {
            var build = new PlayerBuild();
            if (hero.TryGetComponent(out Inventory inventory))
                for (int i = 0; i < Inventory.SlotCount; i++)
                    build.items.Add(IdOf(inventory.Get(i)));

            AbilityCaster caster = hero.Abilities;
            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                var spell = new SpellBuild { id = IdOf(caster.GetSlot(slot).ability), rank = caster.GetSlot(slot).rank };
                for (int socket = 0; socket < caster.SocketCount(slot); socket++)
                    spell.mods.Add(IdOf(caster.GetMod(slot, socket)));
                build.spells.Add(spell);
            }
            return build;
        }

        static string IdOf(InventoryItem item) => item != null ? item.name : "";
    }
}
