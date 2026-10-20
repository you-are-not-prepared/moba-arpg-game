using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Moba.Online
{
    // Match records on the backend: starting/finishing a match, and the player's history and stats.
    public static class MatchService
    {
        // Returns the new match's id, or null (logged out, offline, or the server refused).
        public static async Task<string> Start(StartMatchRequest request)
        {
            if (!AccountService.IsLoggedIn) return null;
            BackendClient.Response response =
                await BackendClient.Send("POST", "/matches", JsonUtility.ToJson(request), AccountService.SessionToken);
            if (response.Ok) return response.Read<StartMatchResponse>()?.matchId;
            Debug.LogWarning($"[Matches] Match not recorded: {response.ErrorMessage}");
            return null;
        }

        public static async Task<string> Report(string matchId, MatchResultRequest result)
        {
            BackendClient.Response response = await BackendClient.Send("POST", $"/matches/{matchId}/result",
                JsonUtility.ToJson(result), AccountService.SessionToken);
            return response.Ok ? null : response.ErrorMessage;
        }

        public static async Task<(MatchHistoryResponse history, string error)> History(int limit = 20)
        {
            BackendClient.Response response =
                await BackendClient.Send("GET", $"/me/matches?limit={limit}", token: AccountService.SessionToken);
            return response.Ok ? (response.Read<MatchHistoryResponse>(), null) : (null, response.ErrorMessage);
        }

        public static async Task<(PlayerStatsResponse stats, string error)> Stats()
        {
            BackendClient.Response response = await BackendClient.Send("GET", "/me/stats", token: AccountService.SessionToken);
            return response.Ok ? (response.Read<PlayerStatsResponse>(), null) : (null, response.ErrorMessage);
        }
    }

    // Wire format (camelCase JSON, read and written with JsonUtility).

    [Serializable]
    public class StartMatchRequest
    {
        public string mode;
        public string mapLayout;
        public List<StartMatchPlayer> players = new();
    }

    [Serializable]
    public class StartMatchPlayer
    {
        public string team;
        public string heroId;
        public bool isBot;
        public bool isLocal;
    }

    [Serializable]
    public class StartMatchResponse
    {
        public string matchId;
    }

    [Serializable]
    public class MatchResultRequest
    {
        public string winnerTeam;
        public bool cheatsUsed;
        public List<PlayerResult> players = new();
    }

    [Serializable]
    public class PlayerResult
    {
        public int slot;
        public int level;
        public int kills;
        public int deaths;
        public int assists;
        public int creepScore;
    }

    [Serializable]
    public class MatchHistoryResponse
    {
        public List<MatchSummary> matches = new();
    }

    [Serializable]
    public class MatchSummary
    {
        public string matchId;
        public string mode;
        public string mapLayout;
        public string startedAt;
        public int durationSeconds;
        public string winnerTeam;
        public bool won;
        public bool cheatsUsed;
        public int slot;
        public List<MatchSummaryPlayer> players = new();

        public MatchSummaryPlayer Me => players.Find(p => p.slot == slot);
    }

    [Serializable]
    public class MatchSummaryPlayer
    {
        public int slot;
        public string team;
        public string heroId;
        public bool isBot;
        public string username;
        public int level;
        public int kills;
        public int deaths;
        public int assists;
        public int creepScore;
    }

    [Serializable]
    public class PlayerStatsResponse
    {
        public int matches;
        public int wins;
        public int losses;
        public int kills;
        public int deaths;
        public int assists;
        public int creepScore;
        public List<HeroStats> heroes = new();
    }

    [Serializable]
    public class HeroStats
    {
        public string heroId;
        public int matches;
        public int wins;
    }
}
