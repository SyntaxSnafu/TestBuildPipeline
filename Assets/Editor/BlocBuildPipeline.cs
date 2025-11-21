using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;
using UnityEngine;

public static class BlocBuildPipeline
{
    public static void BuildFromCLI()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string platformsArg = GetArg(args, "-platforms");  // Example: "Windows,Android"
        var platforms = platformsArg.Split(',').Select(x => x.Trim());

        foreach (var platform in platforms)
        {
            switch (platform)
            {
                case "Windows":
                    Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Game.exe");
                    break;
                case "Android":
                    Build(BuildTarget.Android, "Builds/Android/Game.apk");
                    break;
                case "iOS":
                    Build(BuildTarget.iOS, "Builds/iOS");
                    break;
                default:
                    Debug.LogError($"Unknown platform: {platform}");
                    break;
            }
        }
    }

    private static void Build(BuildTarget target, string path)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildPipeline.GetBuildTargetGroup(target), target);

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled).Select(s => s.path).ToArray();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            target = target,
            locationPathName = path
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception($"Build failed for {target}!");
    }

    private static string GetArg(string[] args, string name)
    {
        int index = System.Array.IndexOf(args, name);
        return (index >= 0 && index + 1 < args.Length) ? args[index + 1] : "";
    }
}