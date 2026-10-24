using System.IO;
using System.Linq;
using System.Reflection;
using Moba.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Moba.EditorTools
{
    // Game-server support: network components on the generated prefabs and scene objects, the prefab list the
    // NetworkManager loads (Resources/NetworkPrefabs), the Linux dedicated server build used by Docker, and the
    // Windows client build.
    public static partial class GameBuilder
    {
        const string ResourcesDir = Root + "/Resources";
        const string NetworkPrefabsPath = ResourcesDir + "/NetworkPrefabs.asset";
        public const string ServerBuildDir = "Builds/LinuxServer";
        public const string ServerExecutable = "MobaServer.x86_64";
        public const string ClientBuildDir = "Builds/WindowsClient";
        public const string ClientExecutable = "MobaTest.exe";

        // Replicated by the game server: identity, (for moving units) position, team and health. Facing goes through
        // NetworkUnit instead, unsmoothed (no turn rate).
        static void MakeNetworked(GameObject go, bool moving)
        {
            go.AddComponent<NetworkObject>();
            if (moving)
            {
                var transform = go.AddComponent<NetworkTransform>();
                transform.SyncScaleX = transform.SyncScaleY = transform.SyncScaleZ = false;
                transform.SyncRotAngleX = transform.SyncRotAngleY = transform.SyncRotAngleZ = false;
                transform.UseHalfFloatPrecision = true;
                transform.Interpolate = true;
            }
            go.AddComponent<NetworkUnit>();
        }

        // In-scene NetworkObjects (towers, cores, the match) are matched between server and client by an id derived
        // from the saved scene. Objects created before the scene's first save store 0, which builds recompute but
        // the Editor doesn't, so an Editor client can't match them. Reopening the saved scene computes the ids;
        // saving again stores them.
        static void FixSceneNetworkIds(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            MethodInfo validate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (NetworkObject networkObject in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                validate?.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Every prefab the server spawns at runtime: heroes and creeps (towers and cores are placed in the scenes).
        static void CreateNetworkPrefabs()
        {
            EnsureFolder(ResourcesDir);
            AssetDatabase.DeleteAsset(NetworkPrefabsPath);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsDir }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponent<NetworkObject>() != null)
                    list.Add(new NetworkPrefab { Prefab = prefab });
            }
            AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            Debug.Log($"[GameBuilder] Network prefabs: {string.Join(", ", list.PrefabList.Select(p => p.Prefab.name))}");
        }

        // The game server for Docker: a headless Linux build of every scene (it starts in the Frontend scene and
        // runs as the server because UNITY_SERVER is defined in dedicated server builds).
        [MenuItem("MOBA/Build Linux Server")]
        public static void BuildLinuxServerFromMenu()
        {
            if (!BuildLinuxServer(out string message)) EditorUtility.DisplayDialog("Server build failed", message, "OK");
            else Debug.Log(message);
        }

        // Command line (Editor closed):
        //   unity build --target StandaloneLinux64 --execute-method Moba.EditorTools.GameBuilder.BuildLinuxServerBatch
        public static void BuildLinuxServerBatch()
        {
            bool ok = BuildLinuxServer(out string message);
            Debug.Log(message);
            // `unity build --target StandaloneLinux64` made Linux Server the project's active target; switch back so
            // the Editor plays as the normal Windows game next time it opens.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool BuildLinuxServer(out string message)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                message = "Install 'Linux Dedicated Server Build Support' for this Unity version (Unity Hub > Installs > Add modules).";
                return false;
            }
            return Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, ServerBuildDir, ServerExecutable,
                "Server", out message);
        }

        // The game as players run it on Windows, to play PvP against the game server with a second copy next to the
        // Editor (or on another PC). For a quick test it can join straight away:
        //   MobaTest.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -join vesh -name "Player 2"
        [MenuItem("MOBA/Build Windows Client")]
        public static void BuildWindowsClientFromMenu()
        {
            if (!BuildWindowsClient(out string message)) EditorUtility.DisplayDialog("Client build failed", message, "OK");
            else Debug.Log(message);
        }

        public static bool BuildWindowsClient(out string message) =>
            Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, ClientBuildDir, ClientExecutable,
                "Client", out message);

        static bool Build(BuildTarget target, StandaloneBuildSubtarget subtarget, string dir, string executable, string label,
            out string message)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(dir, executable),
                target = target,
                subtarget = (int)subtarget,
                options = BuildOptions.None,
            };
            // Building can leave another target active (Linux Server would make the Editor compile as a dedicated
            // server, UNITY_SERVER); put the target back afterwards.
            BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
            StandaloneBuildSubtarget previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            // Builds talk to the backend running on this PC over plain http (HTTPS comes with online hosting).
            // Allowed in the builds only; the project setting stays as it was.
            InsecureHttpOption previousHttp = PlayerSettings.insecureHttpOption;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // The build saved the project settings with the build's value; save them back.
                PlayerSettings.insecureHttpOption = previousHttp;
                AssetDatabase.SaveAssets();
            }
            if (EditorUserBuildSettings.activeBuildTarget != previousTarget
                || EditorUserBuildSettings.standaloneBuildSubtarget != previousSubtarget)
            {
                EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget);
            }
            bool ok = report.summary.result == BuildResult.Succeeded;
            message = ok
                ? $"[GameBuilder] {label} built to {dir} ({report.summary.totalSize / (1024 * 1024)} MB) in {report.summary.totalTime.TotalSeconds:0}s."
                : $"[GameBuilder] {label} build {report.summary.result}: {report.summary.totalErrors} error(s). See the console.";
            return ok;
        }
    }
}
