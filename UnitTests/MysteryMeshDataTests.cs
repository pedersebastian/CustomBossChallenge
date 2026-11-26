using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using CustomBossChallenge.Visuals;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The mystery map effects modelled in Blender (#285): Bundles/mystery-fx.mesh.gz as tools/blender/export-mystery.py
/// writes it, read the way the mod reads it
/// </summary>
public class MysteryMeshDataTests
{
    private static string RepoFile(params string[] path)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine([folder.FullName, ..path]))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException($"{string.Join('/', path)} not found above {AppContext.BaseDirectory}");
        return Path.Combine([folder.FullName, ..path]);
    }

    private static readonly Lazy<MysteryMeshData> Data = new(() =>
    {
        using var file = File.OpenRead(RepoFile("Bundles", "mystery-fx.mesh.gz"));
        return MysteryMeshData.Read(file);
    });

    [Fact]
    public void It_has_the_parts_the_mod_shows()
    {
        var data = Data.Value;
        Assert.Equal(["Die", "Glow", "Haze", "Twinkle"], data.Parts.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.False(data.Parts["Die"].SeeThrough);
        Assert.True(data.Parts["Glow"].SeeThrough);
        Assert.True(data.Parts["Haze"].SeeThrough);
        Assert.True(data.Parts["Twinkle"].SeeThrough);
        foreach (var part in data.Parts.Values)
        {
            Assert.True(part.VertexCount > 0, part.Name);
            Assert.True(part.IndexCount > 0 && part.IndexCount % 3 == 0, part.Name);
        }
    }

    [Fact]
    public void Three_dice_and_pips_round_the_edges()
    {
        var data = Data.Value;
        Assert.Equal(3, data.Points.Count(point => point.Name.StartsWith("DieSpot")));
        var pips = data.Points.Where(point => point.Name.StartsWith("PipSpot")).ToList();
        Assert.InRange(pips.Count, 8, 24);
        // Each pip lies inside the haze's band, near an edge of the 18 x 13.6 frame, not in the middle of the screen
        foreach (var (name, position, size) in pips)
        {
            var fromEdge = Math.Min(9 - Math.Abs(position.X), 6.8f - Math.Abs(position.Z));
            Assert.InRange(fromEdge, 0, 1.7f);
            Assert.True(size > 0, name);
        }
    }

    [Fact]
    public void A_die_is_one_unit_round_its_middle_with_a_light_for_each_way_it_lies()
    {
        var data = Data.Value;
        var die = data.Parts["Die"];
        // The die and its outline shell
        Assert.InRange(die.Extent(), 0.5f, 0.6f);
        Assert.Equal(24, data.Orientations.Count);
        Assert.Equal(24, die.Steps.Length);
        Assert.All(data.Orientations, rotation => Assert.Equal(1, rotation.Length(), 3));
        // All 24 ways are different
        for (var a = 0; a < 24; a++)
        {
            for (var b = a + 1; b < 24; b++) Assert.True(Math.Abs(Quaternion.Dot(data.Orientations[a], data.Orientations[b])) < 0.99f);
        }
        // The light differs from way to way: what is on top is lit, not always the same face
        Assert.True(die.Steps.Select(step => string.Join(",", step)).Distinct().Count() > 1);
    }

    [Fact]
    public void The_nearest_way_to_lie_is_found_for_a_rotation_a_little_off()
    {
        var data = Data.Value;
        for (var i = 0; i < data.Orientations.Count; i++)
        {
            var off = data.Orientations[i] * Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1, 2, 3)), 0.2f);
            Assert.Equal(i, data.NearestOrientation(off));
            // q and -q are the same rotation
            Assert.Equal(i, data.NearestOrientation(-off));
        }
    }

    [Fact]
    public void Uvs_stay_in_the_ramp_and_on_their_row()
    {
        var data = Data.Value;
        foreach (var part in data.Parts.Values)
        {
            var uvs = part.UvsFor(part.Steps.Length - 1);
            Assert.Equal(part.VertexCount * 8, uvs.Length);
            for (var i = 0; i < part.VertexCount; i++)
            {
                var x = BitConverter.ToSingle(uvs, i * 8);
                var y = BitConverter.ToSingle(uvs, i * 8 + 4);
                Assert.InRange(x, 0, 1);
                Assert.InRange(y, 0, 1);
                var row = (int) (y * data.Rows.Count);
                Assert.Equal(row + 0.5f, y * data.Rows.Count, 3);
                Assert.Equal(part.SeeThrough, data.Rows[row].Kind == MysteryMeshData.Row.Fading);
            }
        }
    }

    [Fact]
    public void The_ramp_has_the_dies_colours_and_see_through_rows_fade_out()
    {
        var data = Data.Value;
        const int width = 64, height = 2;
        var pixels = data.RampPixels(width, height);
        Assert.Equal(width * height * data.Rows.Count * 4, pixels.Length);

        byte[] At(int row, int x) => pixels.AsSpan(((row * height) * width + x) * 4, 4).ToArray();
        for (var row = 0; row < data.Rows.Count; row++)
        {
            if (data.Rows[row].Kind == MysteryMeshData.Row.Toon)
            {
                Assert.Equal(255, At(row, 0)[3]);
                Assert.Equal(255, At(row, width - 1)[3]);
            }
            else
            {
                // Strongest where it starts (the screen's edge, the glow's middle), gone at the end
                Assert.True(At(row, 0)[3] > 50, $"row {row}");
                Assert.True(At(row, width - 1)[3] < 3, $"row {row}");
                Assert.True(At(row, width / 2)[3] < At(row, 0)[3]);
            }
        }

        // The haze is subtle: never more than about a third opaque
        var haze = data.Rows[(int) (data.Parts["Haze"].RowYs[0] * data.Rows.Count)];
        Assert.InRange(haze.Alpha, 0.1f, 0.4f);
        // The die is light: ivory, as the Random icon's
        var dieRows = data.Parts["Die"].RowYs.Distinct().Select(y => data.Rows[(int) (y * data.Rows.Count)]).ToList();
        Assert.Equal(3, dieRows.Count); // ivory, pips and outline
        Assert.Contains(dieRows, row => row.Base.All(channel => channel > 200));
        Assert.Contains(dieRows, row => row.Base.All(channel => channel < 80));
    }

    [Fact]
    public void A_file_of_another_kind_is_refused()
    {
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(memory, CompressionLevel.Fastest, true)) gzip.Write("GPM1...."u8);
        memory.Position = 0;
        Assert.Throws<InvalidDataException>(() => MysteryMeshData.Read(memory));
    }

    [Fact]
    public void A_file_cut_short_is_refused()
    {
        var bytes = File.ReadAllBytes(RepoFile("Bundles", "mystery-fx.mesh.gz"));
        using var whole = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        whole.CopyTo(raw);
        using var cut = new MemoryStream();
        using (var gzip = new GZipStream(cut, CompressionLevel.Fastest, true)) gzip.Write(raw.ToArray().AsSpan(0, (int) raw.Length / 2));
        cut.Position = 0;
        Assert.ThrowsAny<IOException>(() => MysteryMeshData.Read(cut));
    }
}
