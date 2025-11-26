#if UNITY_EDITOR

using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace com.bloc.BuildPipeline.Editor
{
    public static class BlocBuildPipeline
    {
        public static void BuildFromCLI()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string platformsArg = GetArg(args, "-platforms");  // Example: "Windows,Android"
            var platforms = platformsArg.Split(',').Select(x => x.Trim());

            string projectName = PlayerSettings.productName;
            foreach (var platform in platforms)
            {
                switch (platform)
                {
                    case "Windows":
                        string windowsPath = $"Builds/Windows/{projectName}.exe";
                        Build(BuildTarget.StandaloneWindows64, windowsPath);
                        break;
                    case "Android":
                        string androidPath = $"Builds/Android/{projectName}.apk";
                        Build(BuildTarget.Android, androidPath);
                        break;
                    case "iOS":
                        Build(BuildTarget.iOS, "Builds/iOS");
                        break;
                    case "WebGL":
                        Build(BuildTarget.WebGL, "Builds/WebGL");
                        break;
                    default:
                        Debug.LogError($"Unknown platform: {platform}");
                        break;
                }
            }
        }

        private static void Build(BuildTarget target, string path)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildPipeline.GetBuildTargetGroup(target), target);

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToArray();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = path
            };

            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Build failed for {target}!");
        }

        private static string GetArg(string[] args, string name)
        {
            int index = System.Array.IndexOf(args, name);
            return (index >= 0 && index + 1 < args.Length) ? args[index + 1] : "";
        }
    }
}

#endif