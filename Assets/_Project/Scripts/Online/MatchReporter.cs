using Moba.Abilities;
using Moba.Bots;
using Moba.Items;
using UnityEngine;

namespace Moba.Online
{
    // Records the match on the player's account: registers it with the server when it starts and sends the
    // final numbers when a core falls. Does nothing while playing offline. Matches that are left early stay
    // unfinished on the server and don't count.
    [RequireComponent(typeof(GameManager))]
    public class MatchReporter : MonoBehaviour
    {
        GameManager _game;
        string _matchId;
        bool _startPending;
        Team? _pendingWinner;

        async void Start()
        {
            _game = GetComponent<GameManager>();
            _game.MatchEnded += OnMatchEnded;
            if (!AccountService.IsLoggedIn || _game.heroes.Count == 0) return;

            var request = new StartMatchRequest
            {
                mode = MatchSettings.Mode.ToString(),
                mapLayout = TryGetComponent(out MapLayout layout) && layout.info != null ? layout.info.id : "unknown",
            };
            // Slots follow game.heroes (the lineup order MatchBootstrap spawned them in).
            foreach (HeroController hero in _game.heroes)
            {
                request.players.Add(new StartMatchPlayer
                {
                    team = hero.Unit.team.ToString(),
                    heroId = hero.definition != null ? hero.definition.id : "unknown",
                    isBot = hero.GetComponent<BotBrain>() != null,
                    isLocal = hero == _game.hero,
                });
            }

            _startPending = true;
            _matchId = await MatchService.Start(request);
            _startPending = false;
            // A match can only end before the server replied if the reply was very slow.
            if (this != null && _pendingWinner.HasValue) Report(_pendingWinner.Value);
        }

        void OnDestroy()
        {
            if (_game != null) _game.MatchEnded -= OnMatchEnded;
        }

        void OnMatchEnded(Team winner)
        {
            if (_startPending) _pendingWinner = winner;
            else Report(winner);
        }

        async void Report(Team winner)
        {
            if (string.IsNullOrEmpty(_matchId)) return;
            string matchId = _matchId;
            _matchId = null; // once

            var result = new MatchResultRequest { winnerTeam = winner.ToString(), cheatsUsed = _game.CheatsUsed };
            for (int slot = 0; slot < _game.heroes.Count; slot++)
            {
                HeroController hero = _game.heroes[slot];
                var progression = hero.GetComponent<HeroProgression>();
                result.players.Add(new PlayerResult
                {
                    slot = slot,
                    level = progression.Level,
                    kills = progression.Kills,
                    deaths = progression.Deaths,
                    assists = progression.Assists,
                    creepScore = progression.CreepScore,
                    heroDamage = Mathf.RoundToInt(progression.HeroDamage),
                    totalDamage = Mathf.RoundToInt(progression.TotalDamage),
                    damageTaken = Mathf.RoundToInt(progression.DamageTaken),
                    build = BuildOf(hero),
                });
            }

            string error = await MatchService.Report(matchId, result);
            if (error != null) Debug.LogWarning($"[Matches] Result not recorded: {error}");
        }

        // Inventory (8 slots) and Q/W/E/R with their mods, by asset name.
        static PlayerBuild BuildOf(HeroController hero)
        {
            var build = new PlayerBuild();
            if (hero.TryGetComponent(out Inventory inventory))
                for (int i = 0; i < Inventory.SlotCount; i++)
                    build.items.Add(IdOf(inventory.Get(i)));

            AbilityCaster caster = hero.Abilities;
            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                var spell = new SpellBuild { id = IdOf(caster.GetSlot(slot).ability) };
                for (int socket = 0; socket < caster.SocketCount(slot); socket++)
                    spell.mods.Add(IdOf(caster.GetMod(slot, socket)));
                build.spells.Add(spell);
            }
            return build;
        }

        static string IdOf(InventoryItem item) => item != null ? item.name : "";
    }
}
