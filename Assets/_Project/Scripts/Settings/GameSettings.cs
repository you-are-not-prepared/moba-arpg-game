using System;
using UnityEngine;

namespace Moba
{
    // Player-facing options (interface sizes, gameplay toggles). Persisted as JSON in PlayerPrefs.
    [Serializable]
    public class GameSettings
    {
        public const float MinScale = 0.5f;
        public const float MaxScale = 1.6f;

        [Header("Interface")]
        public float hudScale = 1f;
        public float heroPanelScale = 1f;
        public float minimapScale = 1f;
        public float statsBarScale = 1f;
        public float healthBarScale = 1f;
        public float popupTextScale = 1f;

        [Header("Gameplay")]
        [Tooltip("Attack move fires instantly at the cursor when its key is pressed (otherwise press, then left-click).")]
        public bool attackMoveQuickCast = true;

        const string PrefsKey = "moba.settings";

        static GameSettings s_Current;
        public static GameSettings Current => s_Current ??= Load();
        public static event Action Changed;

        // Call after changing a field: persists and notifies listeners.
        public static void NotifyChanged()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
            Changed?.Invoke();
        }

        public static void ResetInterface()
        {
            var defaults = new GameSettings();
            GameSettings c = Current;
            c.hudScale = defaults.hudScale;
            c.heroPanelScale = defaults.heroPanelScale;
            c.minimapScale = defaults.minimapScale;
            c.statsBarScale = defaults.statsBarScale;
            c.healthBarScale = defaults.healthBarScale;
            c.popupTextScale = defaults.popupTextScale;
            NotifyChanged();
        }

        static GameSettings Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return new GameSettings();
            try
            {
                return JsonUtility.FromJson<GameSettings>(json) ?? new GameSettings();
            }
            catch (ArgumentException)
            {
                return new GameSettings();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Current = null;
            Changed = null;
        }
    }
}
