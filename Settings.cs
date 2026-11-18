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
    /// Shows what the ranked boss events score (#83): the bosses' time, the penalty, the upgrade tiers and cash used,
    /// and the time of every tier when the game is won. The game keeps all of it either way; this only shows it.
    /// </summary>
    public static readonly ModSettingBool ShowRankedStats = new(false)
    {
        category = RankedPractice,
        displayName = "Show Ranked Stats",
        description = "Shows what the ranked boss events score in custom boss games: the time the bosses were " +
                      "alive, added up, the penalty for a round without a boss that drags on, and the upgrade tiers " +
                      "and cash used. When the game is won you also see the time of every tier, so a Least Tiers or " +
                      "Least Cash run can be compared on time too. Custom games stay unranked.",
        icon = VanillaSprites.StopWatch
    };

    public const string PastEventsName = "Past Events";

    public static readonly ModSettingCategory PastEvents = new(PastEventsName)
    {
        order = 1,
        collapsed = false,
        icon = VanillaSprites.BossChallenge
    };

    /// <summary>
    /// Whether the Past Events list asks the website for new boss events at the game's start (#146). Off, the list has
    /// the events the mod's release brought, and those downloaded before.
    /// </summary>
    public static readonly ModSettingBool DownloadPastEvents = new(true)
    {
        category = PastEvents,
        displayName = "Download New Boss Events",
        description = "When the game starts, the Boss Creator's Past Events list downloads the newest list of " +
                      "official boss events from the mod's website (pedersebastian.github.io/CustomBossChallenge), " +
                      "so new events show up without an update of the mod. No personal data is sent. Off, the list " +
                      "has the events the mod came with, and the ones it downloaded before.",
        icon = VanillaSprites.BossChallenge
    };

    public const string NewBossesName = "New Bosses";

    /// <summary>
    /// Bosses made by the mod (#25). The category and its options are hidden until a boss is switched on in the code
    /// (<see cref="Config.Experimental"/>), so nothing of an unfinished boss shows up in a released version.
    /// </summary>
    public static readonly ModSettingCategory NewBosses = new(NewBossesName)
    {
        order = 2,
        collapsed = false,
        icon = VanillaSprites.GoldenBloonIcon,
        modifyCategory = category => category.gameObject.SetActive(Config.Experimental.Gildebloon)
    };

    /// <summary>
    /// Whether the mod's own boss Gildebloon can be picked in the Boss Creator, by Random and for tiers (#25)
    /// </summary>
    public static readonly ModSettingBool IncludeGildebloon = new(false)
    {
        category = NewBosses,
        displayName = "Include Gildebloon",
        description = "Adds Gildebloon, a boss made by this mod, to the Boss Creator: a golden vault that steals your " +
                      "cash, hides behind a shield made of it and pays it back when it is beaten. Off, it can't be " +
                      "picked, Random never chooses it, and setups and codes with it ask you to turn this on first.",
        icon = VanillaSprites.GoldenBloonIcon,
        modifyOption = option => option.gameObject.SetActive(Config.Experimental.Gildebloon)
    };
}
