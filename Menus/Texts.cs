using BTD_Mod_Helper.Api;

namespace CustomBossChallenge.Menus;

/// <summary>
/// Every text the mod shows, registered as Mod Helper localization keys. Use <see cref="T"/> to get the text in the
/// player's language; translations live in Localization/[Language].json.
/// </summary>
public static class Texts
{
    private static string Key(string name, string english) => ModContent.Localize<CustomBossChallenge>(name, english);

    public static readonly string BossCreator = Key(nameof(BossCreator), "Boss Creator");
    public static readonly string Boss = Key(nameof(Boss), "Boss");
    public static readonly string Play = Key(nameof(Play), "Play");
    public static readonly string Elite = Key(nameof(Elite), "Elite");

    public static readonly string BossType = Key(nameof(BossType), "Boss Type");
    public static readonly string BossTypeInfo = Key(nameof(BossTypeInfo),
        "Choose which boss to fight. Tick Elite to fight the much stronger Elite version of the boss.");

    public static readonly string BossSpeed = Key(nameof(BossSpeed), "Boss Speed");
    public static readonly string BossSpeedInfo = Key(nameof(BossSpeedInfo),
        "How fast the boss moves, in percent of its normal speed. 100 % is the normal speed.");

    public static readonly string BossHealth = Key(nameof(BossHealth), "Boss Health");
    public static readonly string BossHealthInfo = Key(nameof(BossHealthInfo),
        "How much health the boss has, in percent of its normal health. Applies to every tier.");

    public static readonly string AllFortified = Key(nameof(AllFortified), "All Fortified");
    public static readonly string AllFortifiedInfo = Key(nameof(AllFortifiedInfo),
        "Makes every bloon that can be fortified fortified, so leads, ceramics and MOAB-class bloons are much tougher.");

    public static readonly string MaxParagons = Key(nameof(MaxParagons), "Max Paragons");
    public static readonly string MaxParagonsInfo = Key(nameof(MaxParagonsInfo),
        "How many paragons you can have at the same time. 0 means no paragons. Drag the slider all the way to the " +
        "right for no limit.");
    public static readonly string Unlimited = Key(nameof(Unlimited), "Unlimited");

    public static readonly string BossTiers = Key(nameof(BossTiers), "Boss Tiers");
    public static readonly string BossTiersInfo = Key(nameof(BossTiersInfo),
        "How many tiers of the boss you have to defeat, from 1 to 10. Each tier is stronger than the one before. " +
        "You win when the last tier is defeated. Tiers above 5 are copies of tier 5 with more health.");

    public static readonly string SpeedPerTier = Key(nameof(SpeedPerTier), "Speed per Tier");
    public static readonly string SpeedPerTierInfo = Key(nameof(SpeedPerTierInfo),
        "Adds this share of the normal speed for every tier after the first, on top of the game's own steps: +20 % " +
        "makes tier 2 ×1.2, tier 3 ×1.4 and so on. Below 0 makes later tiers slower.");
    public static readonly string HealthPerTier = Key(nameof(HealthPerTier), "Health per Tier");
    public static readonly string HealthPerTierInfo = Key(nameof(HealthPerTierInfo),
        "Adds this share of the normal health for every tier after the first, on top of the game's own steps: " +
        "+100 % makes tier 2 ×2, tier 3 ×3 and so on. Tiers above 5 already get +50 % each. 0 % keeps the normal health.");
    public static readonly string TierPreview = Key(nameof(TierPreview), "Tier Preview");
    public static readonly string TierPreviewInfo = Key(nameof(TierPreviewInfo),
        "Every tier with the round it arrives on, its speed compared to the normal game and its health. Orange is " +
        "more than the normal game, blue is less.");
    public static readonly string PerTier = Key(nameof(PerTier), "{0} % / tier");

    public static readonly string SpawnRounds = Key(nameof(SpawnRounds), "Spawn Rounds");
    public static readonly string SpawnRoundsInfo = Key(nameof(SpawnRoundsInfo),
        "The round each tier arrives on, separated by commas, e.g. 40, 60, 80. Missing rounds continue every 20 " +
        "rounds. The game's last round is moved to after the last tier so every tier can be played.");
    public static readonly string FirstBossRound = Key(nameof(FirstBossRound), "First Boss Round");
    public static readonly string FirstBossRoundInfo = Key(nameof(FirstBossRoundInfo),
        "The round the first tier arrives on. Together with Rounds Between Tiers it fills in Spawn Rounds; type in " +
        "Spawn Rounds instead to give every tier its own round.");
    public static readonly string RoundsBetweenTiers = Key(nameof(RoundsBetweenTiers), "Rounds Between Tiers");
    public static readonly string RoundsBetweenTiersInfo = Key(nameof(RoundsBetweenTiersInfo),
        "How many rounds pass between one tier and the next. 20 is the normal game; 1 sends a new tier every round.");
    public static readonly string OutOfTimeEachTier = Key(nameof(OutOfTimeEachTier), "You must beat each tier within {0} rounds");
    public static readonly string OutOfTimeTier = Key(nameof(OutOfTimeTier), "You must beat tier {0} within {1} rounds");
    public static readonly string Free = Key(nameof(Free), "Free");
    public static readonly string RetryFromRound = Key(nameof(RetryFromRound), "Retry From Round {0}");

    public static readonly string UnfinishedGame = Key(nameof(UnfinishedGame), "Unfinished Game");
    public static readonly string UnfinishedGameQuestion = Key(nameof(UnfinishedGameQuestion),
        "Your custom game {0} ({2} tiers) was left on round {1}. Continue it, or make a new boss? The unfinished " +
        "game is replaced once you play a new one.");
    public static readonly string UnfinishedGameLost = Key(nameof(UnfinishedGameLost),
        "Your custom game {0} ({2} tiers) was left on round {1}, but it can't be continued with this version of BTD6. " +
        "Load its setup to play it again from the start, or make a new boss.");
    public static readonly string UnfinishedGameBroken = Key(nameof(UnfinishedGameBroken),
        "Your unfinished custom game couldn't be read and can't be continued. The file was kept as {0}.");
    public static readonly string LoadGameSetup = Key(nameof(LoadGameSetup), "Load Setup");
    public static readonly string ContinueGame = Key(nameof(ContinueGame), "Continue");
    public static readonly string NewGame = Key(nameof(NewGame), "New Boss");

    public static readonly string Skulls = Key(nameof(Skulls), "Skulls");
    public static readonly string SkullsInfo = Key(nameof(SkullsInfo),
        "Skulls are points on the boss's health bar where it uses its special ability. Vanilla keeps the normal " +
        "skulls, Evenly Spaced spreads a number of skulls over the health bar and Custom % puts them at the health " +
        "percentages you choose.");
    public static readonly string SkullsVanilla = Key(nameof(SkullsVanilla), "Vanilla");
    public static readonly string SkullsEvenlySpaced = Key(nameof(SkullsEvenlySpaced), "Evenly Spaced");
    public static readonly string SkullsCustom = Key(nameof(SkullsCustom), "Custom %");

    public static readonly string SkullCount = Key(nameof(SkullCount), "Skull Count");
    public static readonly string SkullCountInfo = Key(nameof(SkullCountInfo),
        "How many evenly spaced skulls the boss has. 0 removes all skulls.");

    public static readonly string SkullHealth = Key(nameof(SkullHealth), "Skulls at Boss HP %");
    public static readonly string SkullHealthInfo = Key(nameof(SkullHealthInfo),
        "How much of its health the boss has left when it reaches each skull and uses its special ability, " +
        "separated by commas, e.g. 80, 60, 40, 20. The percentages are the same for every tier.");

    public static readonly string SkullPreview = Key(nameof(SkullPreview), "Skull Preview");
    public static readonly string SkullPreviewInfo = Key(nameof(SkullPreviewInfo),
        "Where the skulls sit on the boss's health bar, from full health on the left to none on the right. The boss " +
        "uses its special ability each time its health drops past a skull.");
    public static readonly string NoSkulls = Key(nameof(NoSkulls), "No skulls");

    public static readonly string SavedSetups = Key(nameof(SavedSetups), "Saved Setups");
    public static readonly string SavedSetupsInfo = Key(nameof(SavedSetupsInfo),
        "Keeps your favorite setups in the game. Save stores this whole setup under the challenge's name; a saved " +
        "setup with the same name is replaced. Pick one in the list and press Load to play it again, or Delete to " +
        "remove it. The list also has a few examples to try: load one, change it and save it as your own. Examples " +
        "can't be deleted.");
    public static readonly string SaveSetup = Key(nameof(SaveSetup), "Save");
    public static readonly string DeleteSetup = Key(nameof(DeleteSetup), "Delete");
    public static readonly string OverwriteSetupQuestion = Key(nameof(OverwriteSetupQuestion), "Replace the saved setup {0}?");
    public static readonly string DeleteSetupQuestion = Key(nameof(DeleteSetupQuestion), "Delete the saved setup {0}?");
    public static readonly string LoadSetupQuestion = Key(nameof(LoadSetupQuestion),
        "Load this saved setup? Your current setup will be replaced.");
    public static readonly string ExampleSetups = Key(nameof(ExampleSetups), "Example Setups");
    public static readonly string ExampleLabel = Key(nameof(ExampleLabel), "Example: {0}");
    public static readonly string LoadExampleQuestion = Key(nameof(LoadExampleQuestion),
        "Load this example? Your current setup will be replaced.");
    public static readonly string SetupSaved = Key(nameof(SetupSaved), "Saved as {0}.");
    public static readonly string TooManySetups = Key(nameof(TooManySetups),
        "You can keep up to {0} saved setups. Delete one to save a new one.");
    public static readonly string SetupFileError = Key(nameof(SetupFileError),
        "The saved setups couldn't be changed. Check that UserData/CustomBossChallenge can be written to.");
    public static readonly string SetupUnreadable = Key(nameof(SetupUnreadable),
        "This saved setup is damaged and can't be loaded. You can delete it.");

    public static readonly string ShareCode = Key(nameof(ShareCode), "Share Code");
    public static readonly string ShareCodeInfo = Key(nameof(ShareCodeInfo),
        "Copy puts this whole setup, boss options and Challenge Editor rules, on the clipboard as a code you can " +
        "paste into Discord, Reddit or a message. Paste loads a code someone shared with you.");
    public static readonly string CopyCode = Key(nameof(CopyCode), "Copy");
    public static readonly string PasteCode = Key(nameof(PasteCode), "Paste");
    public static readonly string CodeCopied = Key(nameof(CodeCopied),
        "Code copied to the clipboard ({0} characters). Paste it anywhere to share this boss.");
    public static readonly string CopyLink = Key(nameof(CopyLink), "Copy link");
    public static readonly string LinkCopied = Key(nameof(LinkCopied),
        "Link copied. It opens this setup on the Custom Boss Challenge website, and Paste reads it too.");
    public static readonly string LoadCodeQuestion = Key(nameof(LoadCodeQuestion),
        "Load this setup? Your current setup will be replaced.");
    public static readonly string CodeInvalid = Key(nameof(CodeInvalid),
        "The clipboard doesn't contain a Boss Creator code. Copy the whole code, starting with CBC");
    public static readonly string CodeTooNew = Key(nameof(CodeTooNew),
        "This code comes from a newer version of Custom Boss Challenge. Update the mod to load it.");
    public static readonly string Load = Key(nameof(Load), "Load");
    public static readonly string Ok = Key(nameof(Ok), "OK");
    public static readonly string Cancel = Key(nameof(Cancel), "Cancel");
    public static readonly string TierCount = Key(nameof(TierCount), "{0} tiers");
    public static readonly string HealthCap = Key(nameof(HealthCap),
        "Health stops at {0} per tier, the game's limit: some tiers get less than the settings ask for.");

    public static readonly string UnsupportedMode = Key(nameof(UnsupportedMode),
        "The {0} game mode can't be used for boss games.");
    public static readonly string StartRoundAfterBoss = Key(nameof(StartRoundAfterBoss),
        "The game starts on round {0}, but the first boss tier comes on round {1} and would be skipped. Choose an " +
        "earlier start round or later spawn rounds.");

    public static readonly string BossUnavailable = Key(nameof(BossUnavailable),
        "This boss can't be played: the game has no {0} bloon. A game update may have changed it.");

    public static readonly string BossCreatorOff = Key(nameof(BossCreatorOff),
        "This version of BTD6 changed parts of the game that keep custom games from touching your records, " +
        "achievements and Monkey Money, so the Boss Creator is turned off until Custom Boss Challenge is updated.\n\n" +
        "Missing: {0}");

    /// <summary>
    /// The text for a key in the player's language
    /// </summary>
    public static string T(this string key) => BTD_Mod_Helper.Extensions.StringExt.Localize(key);

    public static string T(this string key, object arg) => string.Format(key.T(), arg);

    public static string T(this string key, object arg0, object arg1) => string.Format(key.T(), arg0, arg1);

    public static string T(this string key, object arg0, object arg1, object arg2) =>
        string.Format(key.T(), arg0, arg1, arg2);
}
