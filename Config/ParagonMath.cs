using System.Linq;
using Il2CppAssets.Scripts.Unity;

namespace CustomBossChallenge.Config;

/// <summary>
/// How many paragons the game has. The slider's math is in ParagonMath.Slider.cs, which is built without the game.
/// </summary>
public static partial class ParagonMath
{
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
}
