using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildPlayer
{
    [MenuItem("Build/Build Windows64")]
    public static void BuildWindows64()
    {
        var scenes = new[]
        {
            "Assets/MainScene.unity",
            "Assets/Scenes/SampleScene.unity"
        };

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/DungeonCrawlerCarl.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {report.summary.totalSize} bytes, {report.summary.totalTime}");
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result}");
            EditorApplication.Exit(1);
        }
    }
}
