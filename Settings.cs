using BTD_Mod_Helper.Api.Data;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Api.ModOptions;

namespace CustomBossChallenge;

/// <summary>
/// The mod's page in Mod Helper's Mod Settings. Boss games themselves are set up in the Boss Creator; these are the
/// player's own preferences, the same for every game.
/// <br/>
/// Mod Helper translates the texts itself, with keys of its own in Localization/[Language].json:
/// "CustomBossChallenge-[category] Category", "CustomBossChallenge-[field] Setting Name" and "... Setting Description".
/// The English here must match English.json.
/// </summary>
public class Settings : ModSettings
{
    public const string RankedPracticeName = "Ranked Practice";

    public static readonly ModSettingCategory RankedPractice = new(RankedPracticeName)
    {
        order = 0,
        collapsed = false,
        icon = VanillaSprites.LeaderboardsBtn
    };

    /// <summary>
    /// Shows the ranked event timer, the upgrade tiers and cash used, and the time of every tier when the game is won
    /// (#83). The game keeps the time either way; this only shows it.
    /// </summary>
    public static readonly ModSettingBool ShowTimer = new(false)
    {
        category = RankedPractice,
        displayName = "Show Event Timer",
        description = "Shows the ranked boss events' timer in custom boss games, with the upgrade tiers and cash used. " +
                      "The time is how long the bosses were alive, added up, as the events score it. When the game " +
                      "is won you also see the time of every tier, so a Least Tiers or Least Cash run can be compared " +
                      "on time too. Custom games stay unranked.",
        icon = VanillaSprites.StopWatch
    };
}
