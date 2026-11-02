using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Moba.Online
{
    // Updates the Windows game from GitHub Releases. The menu asks once per run whether the latest release is newer
    // than this build (Application.version, Project Settings > Player > Version); "Update now" downloads its zip, and a
    // small PowerShell script waits for the game to quit, puts the new files in place and starts the game again.
    // Publish a release with "moba release" (scripts/moba.ps1).
    public static class GameUpdater
    {
        public const string Repo = "you-are-not-prepared/moba-arpg-game";
        public const string AssetName = "MobaTest-Windows.zip";
        public const string ReleasesPage = "https://github.com/" + Repo + "/releases";
        const string LatestUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";
        // Another feed for testing, e.g. -update-feed http://127.0.0.1:8765/latest.json (same JSON as GitHub's).
        const string FeedArgument = "-update-feed";
        const int CheckTimeoutSeconds = 10;

        public sealed class Release
        {
            public string Version;
            public string Notes;
            public string DownloadUrl;
            public long Size;
        }

        static Task<Release> _check;

        // Only the Windows game updates itself (not the Editor, other platforms or the game server).
        public static bool Supported =>
            Application.platform == RuntimePlatform.WindowsPlayer && !Net.GameServer.IsServerProcess;

        // The newer release, or null (none, up to date, offline). Asks GitHub once per run.
        public static Task<Release> CheckForUpdate()
        {
            if (!Supported) return Task.FromResult<Release>(null);
            return _check ??= FetchLatest();
        }

        static async Task<Release> FetchLatest()
        {
            string url = FeedUrl() ?? LatestUrl;
            using var request = UnityWebRequest.Get(url);
            request.timeout = CheckTimeoutSeconds;
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "moba-arpg-game/" + Application.version);
            try
            {
                await request.SendWebRequest();
            }
            catch (Exception)
            {
                // Logged below with the request's error.
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                // 404: no release published yet.
                Debug.Log($"[Updater] No update check ({request.responseCode} {request.error}).");
                return null;
            }

            GitHubRelease latest;
            try
            {
                latest = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);
            }
            catch (ArgumentException)
            {
                return null;
            }
            string version = latest?.tag_name?.TrimStart('v', 'V');
            if (!IsNewer(version, Application.version)) return null;
            GitHubAsset asset = Array.Find(latest.assets ?? Array.Empty<GitHubAsset>(), a => a.name == AssetName);
            if (asset == null)
            {
                Debug.LogWarning($"[Updater] Release {latest.tag_name} has no {AssetName}.");
                return null;
            }
            return new Release { Version = version, Notes = latest.body, DownloadUrl = asset.browser_download_url, Size = asset.size };
        }

        static string FeedUrl()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, FeedArgument);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // Versions are MAJOR.MINOR.PATCH; anything else is never offered.
        static bool IsNewer(string candidate, string current) =>
            Version.TryParse(candidate, out var a) && Version.TryParse(current, out var b) && a > b;

        // Downloads the release and hands over to the install script, then quits the game. Returns an error message
        // when it can't (the game keeps running); `progress` gets 0..1 while downloading.
        public static async Task<string> Install(Release release, Action<float> progress)
        {
            // No trailing slash: "D:\" would escape the closing quote on the script's command line.
            string gameDir = Path.GetDirectoryName(Application.dataPath).TrimEnd('\\', '/');
            string exe = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            if (!CanWrite(gameDir))
                return "The game can't update itself in this folder. Move the game folder somewhere you can write to " +
                       "(e.g. Documents), or download the new version from " + ReleasesPage;

            string work = Path.Combine(Application.temporaryCachePath, "update");
            Directory.CreateDirectory(work);
            string zip = Path.Combine(work, AssetName);
            using (var request = new UnityWebRequest(release.DownloadUrl, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
                request.SetRequestHeader("User-Agent", "moba-arpg-game/" + Application.version);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    progress?.Invoke(request.downloadProgress);
                    await Awaitable.NextFrameAsync();
                }
                if (request.result != UnityWebRequest.Result.Success)
                    return $"The download failed ({request.error}). Try again later.";
            }
            progress?.Invoke(1f);

            string script = Path.Combine(work, "install-update.ps1");
            File.WriteAllText(script, InstallScript);
            var start = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
                $"-GamePid {Process.GetCurrentProcess().Id} -Zip \"{zip}\" -GameDir \"{gameDir}\" -Exe \"{exe}\" " +
                $"-Page \"{ReleasesPage}\"")
            {
                // A console window says what's happening while the files are swapped.
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            try
            {
                Process.Start(start);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Updater] Couldn't start the install script: {e.Message}");
                return "Couldn't start the update. Download the new version from " + ReleasesPage;
            }
            Debug.Log($"[Updater] Installing {release.Version}; quitting.");
            Application.Quit();
            return null;
        }

        static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".update-check");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Runs after the game quits. The zip's folders (MobaTest_Data, MonoBleedingEdge, D3D12) belong to the game and
        // are replaced whole, so no old files stay behind; files next to the exe are overwritten. The zip is unpacked
        // inside the game folder first, so moving works even when it's on another drive than the temp folder.
        const string InstallScript = @"param([int]$GamePid, [string]$Zip, [string]$GameDir, [string]$Exe, [string]$Page)
$ErrorActionPreference = 'Stop'
$Host.UI.RawUI.WindowTitle = 'Updating the game'
Write-Host 'Updating the game. It starts again by itself when done; you can leave this window.'
$log = Join-Path (Split-Path -Parent $Zip) 'install-update.log'
$staging = Join-Path $GameDir '.update-staging'
try {
    Wait-Process -Id $GamePid -Timeout 60 -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($Zip, $staging)
    foreach ($item in Get-ChildItem -LiteralPath $staging -Force) {
        $target = Join-Path $GameDir $item.Name
        for ($try = 1; ; $try++) {
            try {
                if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
                Move-Item -LiteralPath $item.FullName -Destination $target
                break
            } catch {
                # Files can stay locked for a moment after the game quits.
                if ($try -ge 10) { throw }
                Start-Sleep -Seconds 1
            }
        }
    }
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $Zip -Force -ErrorAction SilentlyContinue
    ""$(Get-Date -Format s) updated $GameDir"" | Out-File -LiteralPath $log -Append
} catch {
    ""$(Get-Date -Format s) failed: $_"" | Out-File -LiteralPath $log -Append
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(""The update couldn't be installed:`n$_`n`nDownload the new version from $Page"", 'Update failed') | Out-Null
}
$game = Join-Path $GameDir $Exe
if (Test-Path -LiteralPath $game) { Start-Process -FilePath $game -WorkingDirectory $GameDir }
";

        // GitHub's release JSON (only the fields used).
        [Serializable]
        class GitHubRelease
        {
            public string tag_name;
            public string body;
            public GitHubAsset[] assets;
        }

        [Serializable]
        class GitHubAsset
        {
            public string name;
            public string browser_download_url;
            public long size;
        }
    }
}
