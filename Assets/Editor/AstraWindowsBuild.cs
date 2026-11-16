using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Builds the configured ASTRA scene into its Windows player folder.</summary>
public static class AstraWindowsBuild
{
    private const string OutputPath = "Builds/Windows/ASTRA-Mission-Control-v3.exe";

    [MenuItem("ASTRA/Build Windows Player")]
    public static void Build()
    {
        PlayerSettings.productName = "ASTRA Junior Astronaut Trainer";
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes are listed in Build Settings.");

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.AutoRunPlayer
        });

        Debug.Log($"ASTRA Windows build: {report.summary.result} ({report.summary.totalSize / (1024 * 1024)} MB) at {OutputPath}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"ASTRA Windows build failed: {report.summary.result}");
    }

    [MenuItem("ASTRA/Rebuild Scene and Build Windows Player")]
    public static void RebuildSceneAndBuild()
    {
        AstraSceneBuilder.BuildScene();
        Build();
    }
}
