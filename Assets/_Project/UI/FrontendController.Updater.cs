using Moba.Online;
using UnityEngine.UIElements;

namespace Moba.UI
{
    // The game's version in the corner of the menu, and a bar offering a newer release (see GameUpdater). Both show on
    // the login and start screens only, never during champion select.
    public partial class FrontendController
    {
        const int MaxNoteLines = 6;

        VisualElement _updateBar, _shownScreen;
        Label _updateTitle, _updateNotes, _updateStatus, _versionLabel;
        Button _updateNow, _updateLater;
        GameUpdater.Release _update;
        bool _updateDismissed, _updating;

        void SetUpUpdater(VisualElement root)
        {
            _updateBar = root.Q("updateBar");
            _updateTitle = root.Q<Label>("updateTitle");
            _updateNotes = root.Q<Label>("updateNotes");
            _updateStatus = root.Q<Label>("updateStatus");
            _updateNow = root.Q<Button>("updateNowButton");
            _updateLater = root.Q<Button>("updateLaterButton");
            _versionLabel = root.Q<Label>("versionLabel");
            _versionLabel.text = "v" + UnityEngine.Application.version;

            _updateNow.clicked += () => _ = InstallUpdate();
            _updateLater.clicked += () =>
            {
                _updateDismissed = true;
                RefreshUpdateBar();
            };
            _ = CheckForUpdate();
        }

        async System.Threading.Tasks.Task CheckForUpdate()
        {
            GameUpdater.Release update = await GameUpdater.CheckForUpdate();
            if (this == null || update == null) return;
            _update = update;
            _updateTitle.text = $"Version {update.Version} is out (you have {UnityEngine.Application.version})";
            _updateNotes.text = ShortNotes(update.Notes);
            _updateNotes.EnableInClassList("hidden", string.IsNullOrEmpty(_updateNotes.text));
            RefreshUpdateBar();
        }

        async System.Threading.Tasks.Task InstallUpdate()
        {
            if (_updating || _update == null) return;
            _updating = true;
            _updateNow.SetEnabled(false);
            _updateLater.SetEnabled(false);
            string size = _update.Size > 0 ? $" of {_update.Size / (1024f * 1024f):0} MB" : "";
            string error = await GameUpdater.Install(_update, p =>
            {
                if (_updateStatus != null) SetUpdateStatus($"Downloading… {p * 100f:0}%{size}");
            });
            if (this == null) return;
            if (error == null)
            {
                SetUpdateStatus("Installing. The game restarts by itself.");
                return;
            }
            _updating = false;
            _updateNow.SetEnabled(true);
            _updateLater.SetEnabled(true);
            SetUpdateStatus(error);
        }

        void SetUpdateStatus(string text)
        {
            _updateStatus.text = text;
            _updateStatus.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        void RefreshUpdateBar(VisualElement screen = null)
        {
            if (screen != null) _shownScreen = screen;
            bool onMenu = _shownScreen == _loginScreen || _shownScreen == _startScreen;
            _versionLabel?.EnableInClassList("hidden", !onMenu);
            _updateBar?.EnableInClassList("hidden", _update == null || _updateDismissed || !onMenu);
        }

        // The first lines of the release notes (Markdown headings and the "Full Changelog" link left out); the rest is
        // on the releases page.
        static string ShortNotes(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";
            var lines = new System.Collections.Generic.List<string>();
            foreach (string raw in notes.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("**Full Changelog**")) continue;
                lines.Add(line);
            }
            if (lines.Count <= MaxNoteLines) return string.Join("\n", lines);
            return string.Join("\n", lines.GetRange(0, MaxNoteLines)) + "\n…";
        }
    }
}
