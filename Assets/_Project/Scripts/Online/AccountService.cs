using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Moba.Online
{
    // The player's account for this run of the game: logging in/out, the remembered session, and keeping
    // options + keybinds in sync with the account. All methods return null on success or a message to show.
    public static class AccountService
    {
        const string TokenPrefsKey = "moba.auth.token";
        // Wait this long after the last options change before uploading (sliders change many times a second).
        const int SettingsUploadDelayMs = 1500;

        static bool s_ApplyingServerSettings;
        static int s_UploadGeneration;

        public static Profile Profile { get; private set; }
        public static bool IsLoggedIn => Profile != null;
        // The player chose "Play offline" this run.
        public static bool PlayingOffline { get; set; }
        // A session from an earlier run that hasn't been checked yet.
        public static bool HasSavedSession => !string.IsNullOrEmpty(Token);

        public static event Action Changed;

        static string Token => PlayerPrefs.GetString(TokenPrefsKey, "");
        // For other backend calls made on the player's behalf (null when logged out).
        public static string SessionToken => IsLoggedIn ? Token : null;

        public static async Task<string> Register(string email, string username, string password)
        {
            string body = JsonUtility.ToJson(new RegisterRequest { email = email, username = username, password = password });
            BackendClient.Response response = await BackendClient.Send("POST", "/auth/register", body);
            return response.Ok ? StartSession(response.Read<AuthResponse>()) : response.ErrorMessage;
        }

        public static async Task<string> Login(string login, string password)
        {
            string body = JsonUtility.ToJson(new LoginRequest { login = login, password = password });
            BackendClient.Response response = await BackendClient.Send("POST", "/auth/login", body);
            return response.Ok ? StartSession(response.Read<AuthResponse>()) : response.ErrorMessage;
        }

        // Logs back in with the session saved by an earlier run. A rejected session is forgotten;
        // an unreachable server keeps it for next time.
        public static async Task<string> Resume()
        {
            if (!HasSavedSession) return "Not logged in.";
            BackendClient.Response response = await BackendClient.Send("GET", "/me", token: Token);
            if (!response.Ok)
            {
                if (response.Status == 401) ForgetSession();
                return response.ErrorMessage;
            }
            SetProfile(response.Read<Profile>());
            return null;
        }

        public static async Task Logout()
        {
            string token = Token;
            ForgetSession();
            Profile = null;
            Changed?.Invoke();
            // Best effort: if the server can't be reached the session simply expires there.
            if (!string.IsNullOrEmpty(token)) await BackendClient.Send("POST", "/auth/logout", token: token);
        }

        static string StartSession(AuthResponse auth)
        {
            if (auth == null || string.IsNullOrEmpty(auth.token) || auth.profile == null)
                return "The server sent an unexpected reply.";
            PlayerPrefs.SetString(TokenPrefsKey, auth.token);
            PlayerPrefs.Save();
            PlayingOffline = false;
            SetProfile(auth.profile);
            return null;
        }

        static void ForgetSession()
        {
            PlayerPrefs.DeleteKey(TokenPrefsKey);
            PlayerPrefs.Save();
        }

        // The account's settings win over this PC's; a brand-new account takes this PC's settings instead.
        static void SetProfile(Profile profile)
        {
            Profile = profile;
            if (profile.settings != null && profile.settings.version > 0) ApplyServerSettings(profile.settings);
            else _ = UploadSettings();
            Changed?.Invoke();
        }

        static void ApplyServerSettings(SyncedSettings settings)
        {
            s_ApplyingServerSettings = true;
            try
            {
                if (settings.game != null) GameSettings.ReplaceWith(settings.game);
                GameInput.Instance.ApplyOverridesJson(settings.keybinds);
            }
            finally
            {
                s_ApplyingServerSettings = false;
            }
        }

        // ---------- Uploading local changes ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            GameSettings.Changed += OnLocalSettingsChanged;
            GameInput.Saved += OnLocalSettingsChanged;
        }

        static void OnLocalSettingsChanged()
        {
            if (!IsLoggedIn || s_ApplyingServerSettings) return;
            _ = UploadSettingsSoon();
        }

        static async Task UploadSettingsSoon()
        {
            int generation = ++s_UploadGeneration;
            await Task.Delay(SettingsUploadDelayMs); // real time, so it also works while the game is paused
            if (generation == s_UploadGeneration) await UploadSettings();
        }

        static async Task UploadSettings()
        {
            if (!IsLoggedIn) return;
            var settings = new SyncedSettings
            {
                version = SyncedSettings.CurrentVersion,
                game = GameSettings.Current,
                keybinds = GameInput.Instance.OverridesJson(),
            };
            BackendClient.Response response =
                await BackendClient.Send("PUT", "/me/settings", JsonUtility.ToJson(settings), Token);
            if (response.Ok) Profile.settings = settings;
            else Debug.LogWarning($"[Account] Couldn't save settings to the account: {response.ErrorMessage}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Profile = null;
            PlayingOffline = false;
            Changed = null;
            s_ApplyingServerSettings = false;
            s_UploadGeneration = 0;
        }
    }
}
