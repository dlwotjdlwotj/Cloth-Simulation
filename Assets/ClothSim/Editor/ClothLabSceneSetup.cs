using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClothLabSceneSetup
{
    public const string ScenePath = "Assets/Scenes/ClothLab.unity";
    public const string WindowsExeName = "ClothSimLab.exe";

    [MenuItem("ClothSim/Setup Lab Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("ClothSim");
        var bootstrap = go.AddComponent<ClothSimBootstrap>();
        bootstrap.clothShader = Resources.Load<ComputeShader>("Cloth");
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("ClothLab scene created: " + ScenePath);
    }

    [MenuItem("ClothSim/Build Windows")]
    public static void BuildWindows()
    {
        EnsurePluginDefines();
        if (!File.Exists(ScenePath))
            CreateScene();

        string dir = Path.GetFullPath("Build");
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, "ClothSimLab.exe");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Build failed: " + report.summary.result + " errors=" + report.summary.totalErrors);

        Debug.Log("Built " + exe);
    }

    [InitializeOnLoadMethod]
    static void EnsurePluginDefines()
    {
        EnsureDefine("UIMGUI_ENABLE_IMGUIZMO_QUAT");
        EnsureDefine("UIMGUI_ENABLE_IMGUIZMO");
    }

    static void EnsureDefine(string define)
    {
        var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
        var parts = symbols.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == define)
                return;
        }

        PlayerSettings.SetScriptingDefineSymbols(target,
            string.IsNullOrEmpty(symbols) ? define : symbols + ";" + define);
    }

    const string BuildRequestPath = "Build/request-build.txt";
    static bool watchingBuildRequest;

    [InitializeOnLoadMethod]
    static void BuildIfRequested()
    {
        if (watchingBuildRequest) return;
        watchingBuildRequest = true;
        EditorApplication.update += PollBuildRequest;
        EditorApplication.delayCall += PollBuildRequest;
    }

    static void PollBuildRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling) return;
        if (!File.Exists(BuildRequestPath)) return;
        File.Delete(BuildRequestPath);
        BuildWindows();
    }

    [InitializeOnLoadMethod]
    static void AutoCreateOnce() // reload hook
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (System.IO.File.Exists(ScenePath)) return;
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            CreateScene();
        };
    }
}


