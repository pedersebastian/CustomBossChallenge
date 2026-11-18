using System;

namespace CustomBossChallenge.Config;

/// <summary>
/// Features that are still being made. They are off in released versions, and only show up once
/// <see cref="Gildebloon"/> is turned on here in the code. Kept free of the game's types for the unit tests.
/// </summary>
public static class Experimental
{
    /// <summary>
    /// The hidden switch for the mod's own boss Gildebloon (#25). Off until the boss is finished: then nothing about
    /// it shows anywhere, the Mod Settings option included. Turn it on here to play it while it is being made; the
    /// tests turn it on for themselves.
    /// </summary>
    public static bool Gildebloon { get; set; } = false;

    /// <summary>
    /// Whether the player wants Gildebloon, from Mod Settings. The mod sets this when it loads; the unit tests set it
    /// themselves.
    /// </summary>
    public static Func<bool> GildebloonSetting { get; set; } = () => false;

    /// <summary>
    /// Whether Gildebloon can be picked and played: switched on in the code and wanted in Mod Settings
    /// </summary>
    public static bool GildebloonAvailable => Gildebloon && GildebloonSetting();
}
