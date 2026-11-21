using System;
using System.IO;
using System.Linq;
using CustomBossChallenge.Visuals;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Gildebloon's body and animations as the mod reads them (#25): Bundles/gildebloon-body.gba.gz, made by
/// tools/blender/build-boss.py, and the player that picks which animation plays
/// </summary>
public class GildebloonBodyTests
{
    private static readonly string Root = FindRoot();

    private static readonly Lazy<GildebloonBodyData> Body =
        new(() =>
        {
            using var file = File.OpenRead(Path.Combine(Root, "Bundles", "gildebloon-body.gba.gz"));
            return GildebloonBodyData.Read(file);
        });

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Bundles", "gildebloon-body.gba.gz"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("the repository with Bundles/gildebloon-body.gba.gz");
    }

    private static readonly string[] Animations =
    [
        "Spawn", "Idle", "Hit", "Toll", "Repossession", "Skittish", "VaultBreak", "Death", "Damaged66", "Damaged33",
        "Damaged10", "Dispatch", "Escape", "TarnishHit", "Interest", "VaultUp", "EliteSpawn", "Payout", "IdleCount",
        "IdlePolish", "IdleSmug"
    ];

    [Fact]
    public void TheFileHasEveryAnimationThePlayerKnows()
    {
        var clips = Body.Value.Clips;
        Assert.Equal(Animations.OrderBy(n => n), clips.Keys.OrderBy(n => n));
        foreach (var name in GildebloonClipPlayer.Priority.Keys) Assert.Contains(name, clips.Keys);
        Assert.All(clips.Values, clip => Assert.True(clip.Frames > 0 && clip.Fps > 0 && clip.Tracks.Count > 0, clip.Name));
        Assert.Equal(["Damaged10", "Damaged33", "Damaged66", "Idle"], clips.Values.Where(c => c.Loops).Select(c => c.Name).OrderBy(n => n));
    }

    [Fact]
    public void TheBodyKeepsThePartsTheModMoves()
    {
        var body = Body.Value;
        foreach (var name in new[] {"Gildebloon", "Hull", "VaultDoor", "Dial", "BagLeft", "BagRight", "CoinRing", "EliteBand", "EliteSpikes"})
        {
            Assert.True(body.PartIndex(name) >= 0, $"{name} is missing");
        }

        Assert.True(body.Parts[body.PartIndex("EliteSpikes")].Elite);
        Assert.False(body.Parts[body.PartIndex("Hull")].Elite);
        Assert.True(body.Parts[body.PartIndex("VaultShell")].SeeThrough);
        // Every part's parent comes before it, and its mesh is whole
        for (var i = 0; i < body.Parts.Count; i++)
        {
            var part = body.Parts[i];
            Assert.InRange(part.Parent, -1, i - 1);
            Assert.Equal(part.VertexCount * 12, part.Positions.Length);
            Assert.Equal(part.VertexCount * 8, part.Uvs.Length);
            Assert.Equal(0, part.IndexCount % 3);
            var indices = Enumerable.Range(0, part.IndexCount).Select(k => BitConverter.ToInt32(part.Indices, k * 4));
            Assert.All(indices, index => Assert.InRange(index, 0, Math.Max(0, part.VertexCount - 1)));
        }

        Assert.All(body.Rows, row => Assert.InRange(row.Alpha, 0.01f, 1f));
    }

    [Fact]
    public void ItHoversAboveTheGroundAtRest()
    {
        var body = Body.Value;
        var root = body.Parts[body.PartIndex("Gildebloon")];
        Assert.Equal(-1, root.Parent);
        Assert.True(root.Rest.Position.Y > 0.5f, $"hovering at {root.Rest.Position.Y}");
        // Payout and Escape end with the body gone
        var payout = body.Clips["Payout"];
        Assert.Equal(0, payout.Sample(body.PartIndex("Gildebloon"), payout.Seconds)!.Value.Scale.X, 3);
    }

    [Theory]
    [InlineData(1.0f, "Idle")]
    [InlineData(0.67f, "Idle")]
    [InlineData(0.66f, "Damaged66")]
    [InlineData(0.34f, "Damaged66")]
    [InlineData(0.33f, "Damaged33")]
    [InlineData(0.11f, "Damaged33")]
    [InlineData(0.10f, "Damaged10")]
    [InlineData(0f, "Damaged10")]
    public void TheLoopFollowsItsHealth(float share, string loop) => Assert.Equal(loop, GildebloonClipPlayer.BaseFor(share));

    [Fact]
    public void AnAnimationOnlyCutsInOverOneThatMattersAsMuchOrLess()
    {
        var player = new GildebloonClipPlayer(Body.Value);
        Assert.True(player.Play("Repossession"));
        Assert.False(player.Play("Hit"));
        Assert.Equal("Repossession", player.Current);
        Assert.True(player.Play("Death"));
        Assert.False(player.Play("Repossession"));
        Assert.False(player.Play("Spawn"));
        Assert.False(player.Play("NoSuchAnimation"));
    }

    [Fact]
    public void OneShotsGoBackToTheLoopAndEndingsHold()
    {
        var body = Body.Value;
        var player = new GildebloonClipPlayer(body);
        player.Play("Hit");
        player.Advance(body.Clips["Hit"].Seconds + 0.01f);
        Assert.Null(player.Current);

        player.Play("Death");
        player.Advance(body.Clips["Death"].Seconds + 1);
        Assert.Equal("Death", player.Current);
        Assert.True(player.Finished);
    }

    [Fact]
    public void PayoutFollowsDeath()
    {
        var body = Body.Value;
        var player = new GildebloonClipPlayer(body);
        player.Play("Death");
        player.Queue("Payout");
        Assert.Equal("Death", player.Current);
        player.Advance(body.Clips["Death"].Seconds + 0.01f);
        Assert.Equal("Payout", player.Current);

        // A payout that comes after Death has finished plays at once
        var later = new GildebloonClipPlayer(body);
        later.Play("Death");
        later.Advance(body.Clips["Death"].Seconds + 2);
        later.Queue("Payout");
        Assert.Equal("Payout", later.Current);
    }

    [Fact]
    public void ADamageStateChangesTheLoopWithoutStoppingAnAnimation()
    {
        var player = new GildebloonClipPlayer(Body.Value);
        player.Play("Toll");
        player.SetBase("Damaged33");
        Assert.Equal("Damaged33", player.Base);
        Assert.Equal("Toll", player.Current);
    }

    [Fact]
    public void PosesBlendBetweenAnimations()
    {
        var body = Body.Value;
        var hull = body.PartIndex("Hull");
        var player = new GildebloonClipPlayer(body);
        player.Advance(1);
        var before = player.PoseOf(hull);
        player.Play("Hit");
        // Right as it starts, the pose is still the loop's, then it moves to the animation's
        Assert.Equal(before.Scale.X, player.PoseOf(hull).Scale.X, 3);
        player.Advance(GildebloonClipPlayer.BlendSeconds);
        var hit = body.Clips["Hit"].Sample(hull, GildebloonClipPlayer.BlendSeconds)!.Value;
        Assert.Equal(hit.Scale.X, player.PoseOf(hull).Scale.X, 3);
    }

    [Fact]
    public void ALoopWrapsAround()
    {
        var idle = Body.Value.Clips["Idle"];
        var part = idle.Tracks.Keys.First();
        var start = idle.Sample(part, 0)!.Value;
        var wrapped = idle.Sample(part, idle.Seconds)!.Value;
        Assert.Equal(start.Position.Y, wrapped.Position.Y, 3);
    }
}
