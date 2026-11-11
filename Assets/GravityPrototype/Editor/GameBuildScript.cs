using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GravityPrototype.Editor
{
    /// <summary>
    /// Windows 发行包构建：菜单场景排第一，开发用的 PhysicsSandbox 不进包。
    /// 也作为 batchmode 入口，用于 CI/命令行出包。
    /// </summary>
    public static class GameBuildScript
    {
        private const string OutputDir = "Builds/GravityFrame-win64";
        private const string ExeName = "GravityFrame.exe";
        private const string SandboxScene = "PhysicsSandbox.unity";

        [MenuItem("Tools/Gravity Prototype/Build Windows Player", false, 60)]
        public static void BuildWindowsFromMenu()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            Build();
        }

        /// <summary>batchmode 入口：-executeMethod GravityPrototype.Editor.GameBuildScript.BuildWindowsForCi</summary>
        public static void BuildWindowsForCi()
        {
            bool ok = Build();
            if (!ok)
            {
                EditorApplication.Exit(1);
            }
        }

        private static bool Build()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !path.EndsWith(SandboxScene, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("Build Settings 里没有启用的场景。");
                return false;
            }

            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, "
                + $"errors={summary.totalErrors}, time={summary.totalTime}, scenes={scenes.Length}, first={scenes[0]}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
