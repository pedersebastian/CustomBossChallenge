using System;

namespace CustomBossChallenge.Config;

/// <summary>
/// Pure helpers for the Max Paragons slider, also built in Shared/ so the unit tests and the website's port
/// (web/src/math/paragons.ts) check them against the same cases. The slider goes from 0 to the number of paragons in
/// the game; the last position means no limit, since a limit of every paragon there is never stops anyone.
/// </summary>
public static partial class ParagonMath
{
    /// <summary>
    /// The value of DailyChallengeModel.maxParagons the game reads as no limit
    /// </summary>
    public const int NoLimit = -1;

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
