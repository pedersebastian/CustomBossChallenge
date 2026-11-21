using System;

namespace CustomBossChallenge.Config;

/// <summary>
/// The values the Boss Health sliders stop at (#250), Boss Health and a tier's own health in Advanced Mode, in
/// percent: from 5 % to 500 % in steps of 5, then on to 5000 % in steps of 10. A slider position is the number of the
/// step, so dragging, the arrows and the keyboard all move from one value to the next. The website has the same steps
/// (web/packages/share-code/src/math/healthSteps.ts).
/// </summary>
public static class HealthSteps
{
    public const int Min = (int) (ChallengeLimits.MinBossHealth * 100 + 0.5f);
    public const int Max = (int) (ChallengeLimits.MaxBossHealth * 100 + 0.5f);

    /// <summary>Up to this value the steps are <see cref="FineStep"/>, above it <see cref="Step"/></summary>
    public const int FineUntil = 500;

    public const int FineStep = 5;
    public const int Step = 10;

    /// <summary>The position of <see cref="FineUntil"/>, the last fine step</summary>
    private const int FinePositions = (FineUntil - Min) / FineStep;

    /// <summary>The last position, <see cref="Max"/></summary>
    public const int LastPosition = FinePositions + (Max - FineUntil) / Step;

    /// <summary>The value at a slider position, held to the slider</summary>
    public static int ValueAt(int position)
    {
        position = Math.Clamp(position, 0, LastPosition);
        return position <= FinePositions
            ? Min + position * FineStep
            : FineUntil + (position - FinePositions) * Step;
    }

    /// <summary>The position of the step nearest to <paramref name="percent"/>; halfway between two, the higher one</summary>
    public static int PositionOf(double percent)
    {
        if (double.IsNaN(percent) || percent <= Min) return 0;
        if (percent >= Max) return LastPosition;
        // Above the last fine step the gap to the first coarse step (510) is a coarse one too
        return percent <= FineUntil
            ? (int) Math.Floor((percent - Min) / FineStep + 0.5)
            : Math.Min(FinePositions + (int) Math.Floor((percent - FineUntil) / Step + 0.5), LastPosition);
    }

    /// <summary>The step nearest to <paramref name="percent"/>, from <see cref="Min"/> to <see cref="Max"/></summary>
    public static int Snap(double percent) => ValueAt(PositionOf(percent));
}
