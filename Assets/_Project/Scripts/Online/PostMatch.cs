using UnityEngine;

namespace Moba.Online
{
    // The match that just ended, carried from the map scene to the menu's post-match analysis screen.
    // Filled by MatchReporter whether or not the player is logged in.
    public static class PostMatch
    {
        public static MatchDetailResponse Last { get; set; }
        // The menu opens on the analysis screen once, right after the match.
        public static bool ShowOnMenu { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Last = null;
            ShowOnMenu = false;
        }
    }
}
