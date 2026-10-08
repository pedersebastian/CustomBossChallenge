using System.Collections.Generic;
using BTD_Mod_Helper.Api.Data;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Api.ModOptions;
using CustomBossChallenge.Config;

namespace CustomBossChallenge;

/// <summary>
/// The single active custom boss challenge configuration. Mod Helper persists these values and also shows them
/// in its own Mod Settings menu; the Customize panel on the Boss Challenge screen edits the same settings.
/// </summary>
public class Settings : ModSettings
{
    public static readonly ModSettingCategory General = new("General") {order = 0};
    public static readonly ModSettingCategory Boss = new("Boss") {order = 1, icon = VanillaSprites.BossBoostIcon};
    public static readonly ModSettingCategory Bloons = new("Bloons") {order = 2};
    public static readonly ModSettingCategory Skulls = new("Skulls") {order = 3};
    public static readonly ModSettingCategory Rules = new("Start Rules") {order = 4};

    public static readonly ModSettingBool Enabled = new(false)
    {
        category = General,
        displayName = "Enable Custom Boss Challenge",
        description = "When enabled, Boss Challenges (not the weekly Boss Event) use the rules below. " +
                      "Custom games are always unranked, are not saved and do not update your records."
    };

    public static readonly ModSettingInt BossSpeed = Percent(Boss, "Boss Speed", 100, 10, 1000,
        "Speed of the boss bloon, in percent of normal.", VanillaSprites.FasterBossIcon);

    public static readonly ModSettingInt BossHealth = Percent(Boss, "Boss Health", 100, 10, 2000,
        "Health of the boss bloon, in percent of normal.", VanillaSprites.BossBoostIcon);

    public static readonly ModSettingInt BloonSpeed = Percent(Bloons, "Bloon Speed", 100, 25, 400,
        "Speed of regular bloons.");

    public static readonly ModSettingInt MoabSpeed = Percent(Bloons, "MOAB Speed", 100, 25, 400,
        "Speed of MOAB-class bloons.");

    public static readonly ModSettingInt CeramicHealth = Percent(Bloons, "Ceramic Health", 100, 25, 1000,
        "Health of ceramic bloons.");

    public static readonly ModSettingInt MoabHealth = Percent(Bloons, "MOAB Health", 100, 25, 1000,
        "Health of MOAB-class bloons.");

    public static readonly ModSettingInt RegrowRate = Percent(Bloons, "Regrow Rate", 100, 25, 400,
        "How fast regrow bloons regrow.");

    public static readonly ModSettingEnum<SkullMode> SkullMode = new(Config.SkullMode.Vanilla)
    {
        category = Skulls,
        displayName = "Skull Mode",
        description = "Vanilla keeps the normal skulls. Evenly Spaced places 'Skull Count' skulls evenly across " +
                      "the health bar. Custom uses the percentages from 'Custom Skulls'.",
        labelFunction = mode => mode switch
        {
            Config.SkullMode.EvenlySpaced => "Evenly Spaced",
            _ => mode.ToString()
        }
    };

    public static readonly ModSettingInt SkullCount = new(3)
    {
        category = Skulls,
        displayName = "Skull Count",
        description = "Number of skulls when Skull Mode is Evenly Spaced (0 removes all skulls).",
        min = 0,
        max = SkullMath.MaxSkulls,
        slider = true
    };

    public static readonly ModSettingString CustomSkulls = new("75,50,25")
    {
        category = Skulls,
        displayName = "Custom Skulls",
        description = "Comma separated health percentages for skulls when Skull Mode is Custom, e.g. 80,60,40,20."
    };

    public static readonly ModSettingInt StartCash = Rule("Starting Cash", 100000, "Starting cash.");
    public static readonly ModSettingInt StartLives = Rule("Starting Lives", 10000, "Starting lives.");
    public static readonly ModSettingInt MaxLives = Rule("Max Lives", 10000, "Maximum lives.");
    public static readonly ModSettingInt StartRound = Rule("Starting Round", 140, "Round the game starts on.");
    public static readonly ModSettingInt EndRound = Rule("Ending Round", 200, "Round the game ends on.");

    public static readonly ModSettingBool NoMonkeyKnowledge = Toggle("No Monkey Knowledge");
    public static readonly ModSettingBool NoSelling = Toggle("No Selling");
    public static readonly ModSettingBool NoPowers = Toggle("No Powers");
    public static readonly ModSettingBool NoContinues = Toggle("No Continues");
    public static readonly ModSettingBool AllCamo = Toggle("All Camo");
    public static readonly ModSettingBool AllRegen = Toggle("All Regrow");
    public static readonly ModSettingBool AllFortified = Toggle("All Fortified");

    /// <summary>
    /// Every value setting (not <see cref="Enabled"/>), in display order
    /// </summary>
    public static IEnumerable<ModSetting> ValueSettings => new ModSetting[]
    {
        BossSpeed, BossHealth,
        BloonSpeed, MoabSpeed, CeramicHealth, MoabHealth, RegrowRate,
        SkullMode, SkullCount, CustomSkulls,
        StartCash, StartLives, MaxLives, StartRound, EndRound,
        NoMonkeyKnowledge, NoSelling, NoPowers, NoContinues, AllCamo, AllRegen, AllFortified
    };

    public static BossChallengeConfig ToConfig() => new()
    {
        BossSpeed = (int) (long) BossSpeed,
        BossHealth = (int) (long) BossHealth,
        BloonSpeed = (int) (long) BloonSpeed,
        MoabSpeed = (int) (long) MoabSpeed,
        CeramicHealth = (int) (long) CeramicHealth,
        MoabHealth = (int) (long) MoabHealth,
        RegrowRate = (int) (long) RegrowRate,
        SkullMode = SkullMode,
        SkullCount = (int) (long) SkullCount,
        CustomSkulls = CustomSkulls,
        StartCash = (int) (long) StartCash,
        StartLives = (int) (long) StartLives,
        MaxLives = (int) (long) MaxLives,
        StartRound = (int) (long) StartRound,
        EndRound = (int) (long) EndRound,
        NoMonkeyKnowledge = NoMonkeyKnowledge,
        NoSelling = NoSelling,
        NoPowers = NoPowers,
        NoContinues = NoContinues,
        AllCamo = AllCamo,
        AllRegen = AllRegen,
        AllFortified = AllFortified
    };

    /// <summary>
    /// Puts every value setting (but not <see cref="Enabled"/>) back to its default
    /// </summary>
    public static void ResetValues()
    {
        foreach (var setting in ValueSettings)
        {
            setting.SetValue(setting.GetDefaultValue());
        }
    }

    private static ModSettingInt Percent(ModSettingCategory category, string name, int value, int min, int max,
        string description, string? icon = null) => new(value)
    {
        category = category,
        displayName = name,
        description = description + " 100% is unchanged.",
        icon = icon,
        min = min,
        max = max,
        slider = true,
        sliderSuffix = "%"
    };

    private static ModSettingInt Rule(string name, int max, string description) => new(0)
    {
        category = Rules,
        displayName = name,
        description = description + " 0 keeps the normal value.",
        min = 0,
        max = max
    };

    private static ModSettingBool Toggle(string name) => new(false)
    {
        category = Rules,
        displayName = name
    };
}
