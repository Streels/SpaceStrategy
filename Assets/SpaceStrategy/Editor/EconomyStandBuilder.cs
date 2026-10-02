using System;
using System.IO;
using SpaceStrategy.Domain.Economy;
using SpaceStrategy.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceStrategy.Editor
{
    public static class EconomyStandBuilder
    {
        public const string ScenePath = "Assets/SpaceStrategy/Scenes/EconomyStand.unity";
        [MenuItem("Space Strategy/Create economy stand v0.3")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MakeScene(NewSceneMode.Single);
        }
        private static void MakeScene(NewSceneMode mode)
        {
            Directory.CreateDirectory("Assets/SpaceStrategy/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            var camera = new GameObject("Economy Stand Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .041f, .062f);
            camera.tag = "MainCamera";
            new GameObject("Economy Stand v0.3").AddComponent<EconomyStandController>();
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.Refresh();
            if (mode == NewSceneMode.Additive) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("ECONOMY_STAND_SCENE " + ScenePath);
        }
        [MenuItem("Space Strategy/Run economy checks v0.3")]
        public static void RunChecks() { Debug.Log(EconomyChecks.Run()); }
        [MenuItem("Space Strategy/Build Windows economy stand v0.3")]
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--economy-output");
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/SpaceStrategyEconomy_v0.3/SpaceStrategy.exe"));
            BuildToPath(output);
        }
        public static void BuildToPath(string output)
        {
            var standalone = UnityEditor.Build.NamedBuildTarget.Standalone;
            string company = PlayerSettings.companyName, product = PlayerSettings.productName;
            int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
            var screenMode = PlayerSettings.fullScreenMode;
            bool background = PlayerSettings.runInBackground;
            var backend = PlayerSettings.GetScriptingBackend(standalone);
            try
            {
                RunChecks();
                if (!File.Exists(ScenePath)) MakeScene(NewSceneMode.Additive);
                PlayerSettings.companyName = "SpaceStrategy";
                PlayerSettings.productName = "Space Strategy — Economy Stand 03";
                PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 1000;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64, locationPathName = output, options = BuildOptions.Development });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Economy build failed: " + report.summary.result);
                Debug.Log("ECONOMY_BUILD_SUCCESS " + output);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
            finally
            {
                PlayerSettings.companyName = company; PlayerSettings.productName = product;
                PlayerSettings.defaultScreenWidth = width; PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.fullScreenMode = screenMode; PlayerSettings.runInBackground = background;
                PlayerSettings.SetScriptingBackend(standalone, backend);
            }
        }
    }
}
