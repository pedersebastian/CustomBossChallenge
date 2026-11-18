using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using BTD_Mod_Helper;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon's pile of money as modelled in Blender (#25, tools/blender/gildebloon-pile.py), read from
/// Bundles/gildebloon-pile.mesh.gz (tools/blender/export-pile.py). The game can't load a model from an asset bundle, so
/// the mesh is built here. Its toon look is baked into UVs on a ramp texture made here: x is how lit a corner is, y the
/// material's row, so the bands stay sharp across a face like the game's cartoon shading. Without it the map gets the
/// simple pile <see cref="GildebloonMapLook"/> builds itself.
/// </summary>
public static class GildebloonPileMesh
{
    public const string Resource = "CustomBossChallenge.gildebloon-pile.mesh.gz";

    /// <summary>The ramp's rows, in the order export-pile.py writes them: shade, base and light colours, and where they start</summary>
    private static readonly (string Shade, string Base, string? Light, float Cut, float LightCut)[] Rows =
    [
        ("#d98a00", "#ffc21a", "#fff1a0", 0.42f, 0.93f), // Gold
        ("#e59a00", "#ffd23d", "#fff6c0", 0.42f, 0.93f), // GoldFace, a coin's face
        ("#b86e00", "#e9a10e", null, 0.42f, 1), // GoldInner, the heap under the coins
        ("#9a6a3a", "#d4a96a", "#ecd2a0", 0.42f, 0.93f), // Sack
        ("#5a3418", "#8a5a2e", null, 0.42f, 1), // Rope
        ("#1f6b22", "#36a03a", null, 0.42f, 1), // Dollar
        ("#2a1a06", "#2a1a06", null, 0, 1), // Outline
        ("#fffbe0", "#fffbe0", null, 0, 1) // Sparkle
    ];

    private const int RampWidth = 64;
    private const int RowHeight = 4;

    public static Mesh? Pile { get; private set; }
    public static Mesh? Sparkle { get; private set; }
    public static IReadOnlyList<(Vector3 Position, float Size)> Sparkles { get; private set; } = [];

    private static Material? material;
    private static bool loaded;

    /// <summary>Whether the modelled pile is there to use</summary>
    public static bool Ready => Pile != null && Material != null;

    /// <summary>Reads the pile's meshes; at the main menu, with Gildebloon's look</summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            using var stream = typeof(GildebloonPileMesh).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
            {
                ModHelper.Warning<CustomBossChallenge>($"{Resource} is missing; Gildebloon's pile of money is a simple one");
                return;
            }

            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip);
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "GPM1") throw new InvalidDataException("not a GPM1 pile");
            Pile = ReadMesh(reader);
            Sparkle = ReadMesh(reader);
            var sparkles = new List<(Vector3, float)>();
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                sparkles.Add((new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle()));
            }

            Sparkles = sparkles;
            ModHelper.Msg<CustomBossChallenge>($"Gildebloon's pile of money loaded: {Pile.vertexCount} vertices");
        }
        catch (Exception e)
        {
            Pile = null;
            Sparkle = null;
            ModHelper.Warning<CustomBossChallenge>($"Gildebloon's pile of money could not be loaded: {e.Message}; a simple one instead");
        }
    }

    private static Mesh ReadMesh(BinaryReader reader)
    {
        var name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
        var count = reader.ReadInt32();
        var vertices = new Vector3[count];
        for (var i = 0; i < count; i++) vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var uvs = new Vector2[count];
        for (var i = 0; i < count; i++) uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        var indices = new int[reader.ReadInt32()];
        for (var i = 0; i < indices.Length; i++) indices[i] = reader.ReadInt32();

        var mesh = new Mesh {name = $"Gildebloon {name}"};
        if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = new Il2CppStructArray<Vector3>(vertices);
        mesh.uv = new Il2CppStructArray<Vector2>(uvs);
        mesh.triangles = new Il2CppStructArray<int>(indices);
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return mesh;
    }

    /// <summary>Unlit, with the ramp: the bands are baked, so the map's light doesn't shade it again</summary>
    public static Material? Material
    {
        get
        {
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            material = new Material(shader) {name = "Gildebloon Pile", hideFlags = HideFlags.DontUnloadUnusedAsset};
            var ramp = Ramp();
            material.mainTexture = ramp;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", ramp);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            return material;
        }
    }

    private static Texture2D Ramp()
    {
        var texture = new Texture2D(RampWidth, Rows.Length * RowHeight, TextureFormat.RGBA32, false)
        {
            name = "Gildebloon Pile Ramp",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        for (var row = 0; row < Rows.Length; row++)
        {
            var (shade, @base, light, cut, lightCut) = Rows[row];
            for (var x = 0; x < RampWidth; x++)
            {
                var lit = (x + 0.5f) / RampWidth;
                var colour = Colour(lit >= lightCut && light != null ? light : lit >= cut ? @base : shade);
                for (var y = 0; y < RowHeight; y++) texture.SetPixel(x, row * RowHeight + y, colour);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Color Colour(string hex) =>
        new(Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, Convert.ToInt32(hex.Substring(3, 2), 16) / 255f,
            Convert.ToInt32(hex.Substring(5, 2), 16) / 255f);
}
