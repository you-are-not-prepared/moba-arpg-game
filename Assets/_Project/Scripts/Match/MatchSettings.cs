using UnityEngine;

namespace Moba
{
    // What champion select chose, carried into the match scene. Ids (not asset references) so nothing depends
    // on assets staying loaded between scenes.
    public static class MatchSettings
    {
        public const string FrontendScene = "Frontend";

        public static string SelectedHeroId;
        public static string LayoutId;
        // The frontend opens straight into champion select (e.g. "Play again" after a match).
        public static bool OpenChampionSelect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SelectedHeroId = null;
            LayoutId = null;
            OpenChampionSelect = false;
        }
    }
}
