using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Moba.Online
{
    // Minimal JSON-over-HTTP client for the moba-backend API. Every call resolves (never throws); check Ok.
    public static class BackendClient
    {
        // Override with PlayerPrefs "moba.backend.url" (e.g. a test server) without rebuilding.
        public const string DefaultUrl = "http://localhost:8080";
        const string UrlPrefsKey = "moba.backend.url";
        const int TimeoutSeconds = 10;

        public static string BaseUrl => PlayerPrefs.GetString(UrlPrefsKey, DefaultUrl).TrimEnd('/');

        public readonly struct Response
        {
            public readonly long Status;
            public readonly string Body;
            // True when the server couldn't be reached at all (no status code).
            public readonly bool Unreachable;

            public Response(long status, string body, bool unreachable)
            {
                Status = status;
                Body = body;
                Unreachable = unreachable;
            }

            public bool Ok => !Unreachable && Status >= 200 && Status < 300;

            public T Read<T>() => string.IsNullOrEmpty(Body) ? default : JsonUtility.FromJson<T>(Body);

            // A message for the player: the server's own message when it sent one.
            public string ErrorMessage
            {
                get
                {
                    if (Unreachable) return "Can't reach the game server. Check that it's running, or play offline.";
                    ApiError error = null;
                    try
                    {
                        error = string.IsNullOrEmpty(Body) ? null : JsonUtility.FromJson<ApiError>(Body);
                    }
                    catch (ArgumentException)
                    {
                        // Not JSON (e.g. a proxy's HTML error page).
                    }
                    if (!string.IsNullOrEmpty(error?.message)) return error.message;
                    return Status == 401 ? "Your session has expired. Please log in again." : $"Server error ({Status}).";
                }
            }

            public string ErrorCode
            {
                get
                {
                    try
                    {
                        return string.IsNullOrEmpty(Body) ? null : JsonUtility.FromJson<ApiError>(Body)?.error;
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }
            }
        }

        public static async Task<Response> Send(string method, string path, string jsonBody = null, string token = null)
        {
            using var request = new UnityWebRequest(BaseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);

            string failure = null;
            try
            {
                await request.SendWebRequest();
            }
            catch (Exception e)
            {
                // E.g. plain http:// blocked by Player Settings ("Allow downloads over HTTP").
                failure = e.Message;
            }

            if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.InProgress
                || request.responseCode == 0)
            {
                Debug.LogWarning($"[Backend] {method} {BaseUrl}{path} failed: {failure ?? request.error ?? "no response"}");
                return new Response(0, null, unreachable: true);
            }
            return new Response(request.responseCode, request.downloadHandler?.text, unreachable: false);
        }
    }

    // Wire format of the backend (camelCase JSON, read with JsonUtility).

    [Serializable]
    public class ApiError
    {
        public string error;
        public string message;
    }

    [Serializable]
    public class AuthResponse
    {
        public string token;
        public string expiresAt;
        public Profile profile;
    }

    [Serializable]
    public class Profile
    {
        public string accountId;
        public string email;
        public string username;
        public int level;
        public int xp;
        public int mmr;
        public SyncedSettings settings;
    }

    // What the account stores of the local setup. version 0 = nothing saved yet.
    [Serializable]
    public class SyncedSettings
    {
        public const int CurrentVersion = 1;

        public int version;
        public GameSettings game;
        // Input System binding overrides (its own JSON format), as a string.
        public string keybinds;
    }

    [Serializable]
    class RegisterRequest
    {
        public string email;
        public string username;
        public string password;
    }

    [Serializable]
    class LoginRequest
    {
        public string login;
        public string password;
    }
}
