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
        private const string WebGLOutputDir = "Builds/WebGL";
        private const string ExeName = "GravityFrame.exe";
        private const string SandboxScene = "PhysicsSandbox.unity";

        [MenuItem("Tools/Gravity Prototype/Build Windows Player", false, 60)]
        public static void BuildWindowsFromMenu()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            Build();
        }

        [MenuItem("Tools/Gravity Prototype/Build WebGL Player", false, 61)]
        public static void BuildWebGLFromMenu()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            BuildWebGL();
        }

        /// <summary>batchmode 入口：-executeMethod GravityPrototype.Editor.GameBuildScript.BuildWebGLForCi</summary>
        public static void BuildWebGLForCi()
        {
            bool ok = BuildWebGL();
            if (!ok) EditorApplication.Exit(1);
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

        private static string[] CollectScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !path.EndsWith(SandboxScene, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static bool BuildWebGL()
        {
            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("Build Settings 里没有启用的场景。");
                return false;
            }

            // GitHub Pages 不会给 .unityweb 加 Content-Encoding，压缩必须关掉，否则浏览器解不开。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;

            Directory.CreateDirectory(WebGLOutputDir);
            var webOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WebGLOutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport webReport = BuildPipeline.BuildPlayer(webOptions);
            BuildSummary webSummary = webReport.summary;
            Debug.Log($"WebGL build {webSummary.result}: {webSummary.totalSize / (1024 * 1024)} MB, "
                + $"errors={webSummary.totalErrors}, time={webSummary.totalTime}, scenes={scenes.Length}");
            return webSummary.result == BuildResult.Succeeded;
        }

        private static bool Build()
        {
            string[] scenes = CollectScenes();
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
