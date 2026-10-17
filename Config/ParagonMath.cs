using System;
using System.Linq;
using Il2CppAssets.Scripts.Unity;

namespace CustomBossChallenge.Config;

/// <summary>
/// Pure helpers for the Max Paragons slider. The slider goes from 0 to the number of paragons in the game; the last
/// position means no limit, since a limit of every paragon there is never stops anyone.
/// </summary>
public static class ParagonMath
{
    /// <summary>
    /// The value of DailyChallengeModel.maxParagons the game reads as no limit
    /// </summary>
    public const int NoLimit = -1;

    /// <summary>
    /// Used when the game model isn't loaded yet
    /// </summary>
    public const int FallbackAvailable = 14;

    private static int available;

    /// <summary>
    /// How many different paragons the game has
    /// </summary>
    public static int Available
    {
        get
        {
            if (available > 0) return available;

            var towers = Game.instance?.model?.towers;
            if (towers == null) return FallbackAvailable;

            available = towers.Where(t => t.isParagon).Select(t => t.baseId).Distinct().Count();
            if (available == 0) available = FallbackAvailable;
            return available;
        }
    }

    public static bool IsUnlimited(int maxParagons, int availableParagons) =>
        maxParagons < 0 || maxParagons >= availableParagons;

    /// <summary>
    /// The slider position for a model's maxParagons; no limit, a limit of every paragon and broken values are all
    /// shown as the last position
    /// </summary>
    public static int ToSlider(int maxParagons, int availableParagons) =>
        IsUnlimited(maxParagons, availableParagons) ? availableParagons : maxParagons;

    /// <summary>
    /// The maxParagons value for a slider position
    /// </summary>
    public static int FromSlider(int position, int availableParagons) =>
        position >= availableParagons ? NoLimit : Math.Max(position, 0);
}
