using System;
using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The look of tiers above 5
/// </summary>
public class TierLookTests
{
    private static readonly string[] GameBosses = {"Bloonarius", "Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos", "Diamondback"};

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    public void Tiers_up_to_5_keep_the_games_look(int tier)
    {
        Assert.False(TierLook.HasLook(tier));
        Assert.Equal(0, TierLook.GlowAlpha(tier));
        Assert.Equal(0, TierLook.AuraAlpha(tier));
        Assert.Equal(1, TierLook.Scale(tier));
        Assert.Equal(1, TierLook.Pulse(tier, 0.7));
        Assert.Equal(new TierColor(1, 1, 1), TierLook.Color("Bloonarius", tier));
    }

    [Fact]
    public void Every_boss_of_the_game_has_a_theme_of_its_own() =>
        Assert.Equal(GameBosses.OrderBy(b => b), TierLook.Bosses.OrderBy(b => b));

    [Fact]
    public void Each_tier_of_a_boss_has_its_own_colour()
    {
        foreach (var boss in GameBosses)
        {
            var colors = Enumerable.Range(6, TierLook.Steps).Select(tier => TierLook.Color(boss, tier)).ToList();
            for (var i = 0; i < colors.Count; i++)
            {
                for (var j = i + 1; j < colors.Count; j++)
                {
                    Assert.True(Distance(colors[i], colors[j]) > 0.25, $"{boss} tiers {i + 6} and {j + 6} look alike");
                }
            }
        }
    }

    [Fact]
    public void Bosses_differ_at_the_same_tier()
    {
        for (var tier = 6; tier <= 10; tier++)
        {
            var themes = GameBosses.Select(boss => TierLook.Color(boss, tier)).ToList();
            Assert.True(themes.Distinct().Count() >= GameBosses.Length - 1, $"Tier {tier} colours repeat across bosses");
        }

        // Taken as a whole, no two bosses share a theme
        foreach (var a in GameBosses)
        {
            foreach (var b in GameBosses.Where(b => string.CompareOrdinal(a, b) < 0))
            {
                var total = Enumerable.Range(6, TierLook.Steps).Sum(tier => Distance(TierLook.Color(a, tier), TierLook.Color(b, tier)));
                Assert.True(total > 0.8, $"{a} and {b} have nearly the same theme");
            }
        }
    }

    [Fact]
    public void Every_effect_gets_stronger_with_each_tier()
    {
        for (var tier = 6; tier <= 10; tier++)
        {
            Assert.True(TierLook.GlowAlpha(tier) > TierLook.GlowAlpha(tier - 1), $"Glow at tier {tier}");
            Assert.True(TierLook.AuraAlpha(tier) > TierLook.AuraAlpha(tier - 1), $"Aura at tier {tier}");
            Assert.True(TierLook.AuraSize(tier) > TierLook.AuraSize(tier - 1), $"Aura size at tier {tier}");
            Assert.True(TierLook.Scale(tier) > TierLook.Scale(tier - 1), $"Scale at tier {tier}");
            Assert.True(TierLook.PulseAmplitude(tier) > TierLook.PulseAmplitude(tier - 1), $"Pulse at tier {tier}");
            Assert.True(TierLook.PulsePeriod(tier) < TierLook.PulsePeriod(tier - 1), $"Pulse speed at tier {tier}");
        }
    }

    [Fact]
    public void Strengths_stay_in_range()
    {
        for (var tier = 6; tier <= 10; tier++)
        {
            for (var t = 0.0; t < 5; t += 0.05)
            {
                var pulse = TierLook.Pulse(tier, t);
                Assert.InRange(TierLook.GlowAlpha(tier) * pulse, 0, 1);
                Assert.InRange(TierLook.AuraAlpha(tier) * pulse, 0, 1);
            }
        }

        Assert.Equal(1.15f, TierLook.Scale(10), 4);
        Assert.Equal(TierLook.Scale(10), TierLook.Scale(12));
    }

    [Fact]
    public void A_boss_the_game_adds_later_still_gets_a_look()
    {
        Assert.Equal(TierLook.Steps, TierLook.Theme("SomeNewBoss").Count);
        Assert.NotEqual(new TierColor(1, 1, 1), TierLook.Color("SomeNewBoss", 7));
    }

    [Theory]
    [InlineData("#9BFF2E")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void Hex_codes_round_trip(string hex) => Assert.Equal(hex, TierColor.FromHex(hex).Hex);

    private static double Distance(TierColor a, TierColor b) =>
        Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
}
