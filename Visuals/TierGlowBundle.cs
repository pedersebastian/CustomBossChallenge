using System;
using System.IO;
using System.Linq;
using BTD_Mod_Helper;
using MelonLoader.Utils;
using UnityEngine;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The mod's own glow shader, built with the game's Unity version into Bundles/tierlook.bundle (see UnityProject/ and
/// CONTRIBUTING.md). It draws a rim along the boss's outline and pulses by itself. If the bundle can't be loaded or
/// the game can't run the shader, e.g. after the game moved to a newer Unity, the glow falls back to the game's own
/// shaders and only this part of the look is lost.
/// <br/>
/// Mod Helper loads the bundle when the mod starts; if it didn't, it is loaded from a copy in
/// UserData/CustomBossChallenge with the calls the game uses for its own bundles.
/// </summary>
public static class TierGlowBundle
{
    public const string BundleFile = "tierlook.bundle";
    public const string ShaderName = "CustomBossChallenge/TierGlow";
    public const string ShaderAsset = "Assets/TierGlow/TierGlow.shader";

    private static bool loaded;
    private static Shader? shader;

    /// <summary>
    /// Kept loaded for as long as the game runs; unloading it would take the shader with it
    /// </summary>
    private static AssetBundle? bundle;

    /// <summary>
    /// Why the shader isn't used, or null when it is
    /// </summary>
    public static string? Problem { get; private set; }

    /// <summary>
    /// The glow shader, or null to use the fallback
    /// </summary>
    public static Shader? Shader
    {
        get
        {
            if (!loaded) Load();
            return shader;
        }
    }

    private static void Load()
    {
        loaded = true;
        try
        {
            var bytes = Bytes();
            if (bytes == null)
            {
                Problem = $"{BundleFile} is missing";
            }
            else
            {
                bundle = Loaded() ?? FromFile(bytes);
                var found = bundle == null ? null : LoadShader(bundle);
                if (bundle == null) Problem = $"{BundleFile} could not be loaded";
                else if (found == null) Problem = $"{ShaderAsset} is not in {BundleFile}";
                else if (!found.isSupported) Problem = $"{ShaderName} is not supported on this graphics card or Unity version";
                else
                {
                    found.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    shader = found;
                }
            }
        }
        catch (Exception e)
        {
            Problem = $"{BundleFile} could not be loaded: {e.Message}";
        }

        if (Problem != null) ModHelper.Warning<CustomBossChallenge>($"{Problem}; tiers above 5 glow with the game's own shader instead");
        else ModHelper.Msg<CustomBossChallenge>($"{ShaderName} loaded");
    }

    /// <summary>
    /// The bundle as Mod Helper loaded it when the mod started: it loads every bundle a mod carries, but looking one up
    /// through Mod Helper fails in this version of the game, so it is found among Unity's loaded bundles
    /// </summary>
    private static AssetBundle? Loaded()
    {
        foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles_Native())
        {
            if (loaded == null) continue;
            foreach (var name in loaded.GetAllAssetNames())
            {
                if (string.Equals(name, ShaderAsset, StringComparison.OrdinalIgnoreCase)) return loaded;
            }
        }

        return null;
    }

    /// <summary>
    /// The shader from the bundle. The game has dropped the synchronous loading calls and its interop fails for the
    /// ones that take a name, so this goes through MelonLoader's wrapper, which calls the game's asynchronous loading
    /// (the one it uses for its own bundles) itself; asking for the result finishes it right away.
    /// </summary>
    private static Shader? LoadShader(AssetBundle bundle) =>
        new Il2CppAssetBundle(bundle.Pointer).LoadAssetAsync<Shader>(ShaderAsset).asset?.TryCast<Shader>();

    /// <summary>
    /// Loads the bundle from a copy in UserData/CustomBossChallenge. Loading from memory fails in the game's interop,
    /// and the game only has the asynchronous loading calls; asking for the result finishes them right away.
    /// </summary>
    private static AssetBundle? FromFile(byte[] bytes)
    {
        var path = Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", BundleFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
        return AssetBundle.LoadFromFileAsync(path).assetBundle;
    }

    private static byte[]? Bytes()
    {
        var assembly = typeof(TierGlowBundle).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(resource => resource.EndsWith(BundleFile));
        if (name == null) return null;

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
