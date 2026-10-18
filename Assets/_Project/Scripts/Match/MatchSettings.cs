using System.Collections.Generic;

namespace Moba
{
    public enum MatchMode
    {
        // Versus bots: every other slot is filled by a bot with a random hero.
        PvE,
        // Versus players. Online play doesn't exist yet, so this starts a match with only the local player.
        PvP,
    }

    // One player in the match lineup: the local player or a bot.
    public struct LobbySlot
    {
        public Team team;
        public string heroId;
        public bool isBot;
        public bool isLocal;
    }

    // What the frontend chose, carried into the match scene. Ids (not asset references) so nothing depends
    // on assets staying loaded between scenes.
    public static class MatchSettings
    {
        public const string FrontendScene = "Frontend";
        // Players per team (3v3).
        public const int TeamSize = 3;

        public static MatchMode Mode = MatchMode.PvE;
        public static string SelectedHeroId;
        public static string LayoutId;
        // Everyone in the match, the local player included. Empty when a map scene is played directly.
        public static readonly List<LobbySlot> Lineup = new();
        // The frontend opens straight into champion select (e.g. "Play again" after a match).
        public static bool OpenChampionSelect;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mode = MatchMode.PvE;
            SelectedHeroId = null;
            LayoutId = null;
            Lineup.Clear();
            OpenChampionSelect = false;
        }
    }
}
