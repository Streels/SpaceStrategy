using System;
using System.IO;
using SpaceStrategy.Presentation;
using SpaceStrategy.Verification;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceStrategy.Editor
{
    public static class PrototypeBuilder
    {
        public const string ScenePath = "Assets/SpaceStrategy/Scenes/LogisticsPrototype.unity";

        [MenuItem("Space Strategy/Create prototype scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/SpaceStrategy/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Strategic Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.025f, 0.043f, 0.066f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.tag = "MainCamera";
            new GameObject("Logistics Prototype").AddComponent<PrototypeController>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Prototype scene created: " + ScenePath);
        }

        [MenuItem("Space Strategy/Run simulation checks")]
        public static void RunChecks() { Debug.Log(SimulationChecks.Run()); }

        [MenuItem("Space Strategy/Build Windows prototype")]
        public static void Build()
        {
            try
            {
                RunChecks();
                if (!File.Exists(ScenePath)) CreateScene();
                else EditorSceneManager.OpenScene(ScenePath);
                PlayerSettings.companyName = "SpaceStrategy";
                PlayerSettings.productName = "Space Strategy — Logistics Prototype";
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.runInBackground = true;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "--prototype-output");
                string output = index >= 0 && index + 1 < args.Length ? args[index + 1]
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/SpaceStrategyPrototype/SpaceStrategy.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + report.summary.result);
                Debug.Log("PROTOTYPE_BUILD_SUCCESS " + output + " | " + report.summary.totalSize + " bytes");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
    }
}
