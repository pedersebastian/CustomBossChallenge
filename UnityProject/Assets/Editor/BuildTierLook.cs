using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the TierGlow shader into ../Bundles/tierlook.bundle for the mod. Run from the command line (see
/// CONTRIBUTING.md) with the same Unity version as the game:
/// Unity.exe -batchmode -quit -projectPath UnityProject -executeMethod BuildTierLook.Build -logFile -
/// </summary>
public static class BuildTierLook
{
    public const string ShaderPath = "Assets/TierGlow/TierGlow.shader";
    public const string BundleName = "tierlook.bundle";

    [MenuItem("Custom Boss Challenge/Build tierlook.bundle")]
    public static void Build()
    {
        // Every graphics API the game can run on Windows gets its own compiled shader in the bundle
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] {GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan});

        // Without a URP asset, URP takes itself for inactive and strips every variant of URP shaders from the build
        UsePipeline();

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            throw new System.Exception($"{ShaderPath} is missing or doesn't compile");
        }

        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
        Directory.CreateDirectory(output);
        var builds = new[] {new AssetBundleBuild {assetBundleName = BundleName, assetNames = new[] {ShaderPath}}};
        var manifest = BuildPipeline.BuildAssetBundles(output, builds,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new System.Exception("Building the bundle failed");

        var target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Bundles", BundleName));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(output, BundleName), target, true);
        Debug.Log($"BuildTierLook: wrote {target} ({new FileInfo(target).Length} bytes) with Unity {Application.unityVersion}");
    }

    public const string PipelinePath = "Assets/Pipeline/URP.asset";
    public const string RendererPath = "Assets/Pipeline/URPRenderer.asset";

    /// <summary>
    /// Makes URP the project's pipeline, like it is in the game
    /// </summary>
    private static void UsePipeline()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PipelinePath)!);
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }
}
