using Moba.Online;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // Post-match analysis: shown right after a match (end screen "Continue"), with the same per-player view as an
    // expanded match history entry. Works offline too, since it's built from the match itself (PostMatch.Last).
    public partial class FrontendController
    {
        VisualElement _postMatchScreen, _postMatchDetails;
        Label _postMatchTitle, _postMatchInfo;

        void SetUpPostMatch(VisualElement root)
        {
            _postMatchScreen = root.Q("postMatchScreen");
            _postMatchDetails = root.Q("postMatchDetails");
            _postMatchTitle = root.Q<Label>("postMatchTitle");
            _postMatchInfo = root.Q<Label>("postMatchInfo");
            root.Q<Button>("postMatchBack").clicked += ShowStart;
        }

        void ShowPostMatch(MatchDetailResponse match)
        {
            ShowOnly(_postMatchScreen);
            _postMatchTitle.text = match.won ? "VICTORY" : "DEFEAT";
            _postMatchTitle.EnableInClassList("post-match__title--win", match.won);
            _postMatchTitle.EnableInClassList("post-match__title--loss", !match.won);

            string info = $"{match.mode} · {MapName(match.mapLayout)} · {match.durationSeconds / 60}:{match.durationSeconds % 60:00}";
            if (match.cheatsUsed) info += " · cheats used (not counted in your stats)";
            _postMatchInfo.text = info;

            _postMatchDetails.Clear();
            var details = new VisualElement();
            details.AddToClassList("match-details");
            details.AddToClassList("match-details--standalone");
            BuildDetails(details, match);
            _postMatchDetails.Add(details);
        }
    }
}
