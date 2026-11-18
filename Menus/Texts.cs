using BTD_Mod_Helper.Api;

namespace CustomBossChallenge.Menus;

/// <summary>
/// Every text the mod shows, registered as Mod Helper localization keys. Use <see cref="T"/> to get the text in the
/// player's language; translations live in Localization/[Language].json.
/// </summary>
public static class Texts
{
    /// <summary>
    /// What every text key of the mod starts with
    /// </summary>
    public const string KeyPrefix = nameof(CustomBossChallenge) + "-";

    private static string Key(string name, string english) => ModContent.Localize<CustomBossChallenge>(name, english);

    public static readonly string BossCreator = Key(nameof(BossCreator), "Boss Creator");
    public static readonly string Boss = Key(nameof(Boss), "Boss");
    public static readonly string Play = Key(nameof(Play), "Play");
    public static readonly string Elite = Key(nameof(Elite), "Elite");

    public static readonly string BossType = Key(nameof(BossType), "Boss Type");
    public static readonly string BossTypeInfo = Key(nameof(BossTypeInfo),
        "Choose which boss to fight. Tick Elite to fight the much stronger Elite version of the boss. Ctrl+Z undoes " +
        "your last change to the boss options or the Challenge Editor rules, also a pasted, loaded or random setup, " +
        "and Ctrl+Y or Ctrl+Shift+Z redoes it. Delete or Backspace puts the option in the yellow frame back to its " +
        "default.");

    public static readonly string DifferentBosses = Key(nameof(DifferentBosses), "Advanced Mode");
    public static readonly string DifferentBossesInfo = Key(nameof(DifferentBossesInfo),
        "Pick the Advanced tab to give every tier a boss of its own, for example Bloonarius on tier 1 and Lych on " +
        "tier 2. Click a tier in the Tier Preview to choose its boss, Elite or not, or Random for a surprise that is " +
        "picked when the game starts. Each tier also gets its own Boss Speed, Boss Health and Skulls there, and " +
        "starts as the Boss Type, which is hidden in Advanced.");
    public static readonly string ModeStandard = Key(nameof(ModeStandard), "Standard");
    public static readonly string ModeAdvanced = Key(nameof(ModeAdvanced), "Advanced");
    public static readonly string TierBossTitle = Key(nameof(TierBossTitle), "Boss for Tier {0}");
    public static readonly string RandomBoss = Key(nameof(RandomBoss), "Random");
    public static readonly string BossLineup = Key(nameof(BossLineup), "Bosses");
    public static readonly string TierArrives = Key(nameof(TierArrives), "Tier {0}: {1}");

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
        "How many tiers of the boss you have to defeat, from 1 to 10. Each tier is usually stronger than the one " +
        "before, unless Speed or Health per Tier is below 0. " +
        "You win when the last tier is defeated. Tiers above 5 are copies of tier 5 with more health.");

    public static readonly string SpeedPerTier = Key(nameof(SpeedPerTier), "Speed per Tier");
    public static readonly string SpeedPerTierInfo = Key(nameof(SpeedPerTierInfo),
        "Adds this share of the normal speed for every tier after the first, on top of the game's own steps: +20 % " +
        "makes tier 2 ×1.2, tier 3 ×1.4 and so on. Below 0 makes later tiers slower.");
    public static readonly string HealthPerTier = Key(nameof(HealthPerTier), "Health per Tier");
    public static readonly string HealthPerTierInfo = Key(nameof(HealthPerTierInfo),
        "Adds this share of the normal health for every tier after the first, on top of the game's own steps: " +
        "+100 % makes tier 2 ×2, tier 3 ×3 and so on. Each tier above 5 already has ×1.5 the health of the one " +
        "before, so tier 7 has ×2.25 of tier 5. 0 % keeps the normal health.");
    public static readonly string TierPreview = Key(nameof(TierPreview), "Tier Preview");
    public static readonly string TierPreviewInfo = Key(nameof(TierPreviewInfo),
        "Every tier with the round it arrives on, its speed compared to the normal game and its health. Orange is " +
        "more than the normal game, blue is less.");
    public static readonly string PerTier = Key(nameof(PerTier), "{0} % / tier");

    public static readonly string BossEventRounds = Key(nameof(BossEventRounds), "Boss Event Rounds");
    public static readonly string BossEventRoundsInfo = Key(nameof(BossEventRoundsInfo),
        "Tick to play the rounds of the official boss events for this boss instead of the normal rounds. In the " +
        "events the tiers arrive on rounds 40, 60, 80, 100 and 120 with bloons made for them; with other Spawn " +
        "Rounds those rounds move with the tiers. In Advanced Mode every tier arrives on the round made for its own " +
        "boss, and the other rounds follow the first tier's boss. In CHIMPS, pick the rounds of the newest CHIMPS " +
        "boss event (Diamondback, 2026) or the earlier ones (Bloonarius and Dreadbloon), the same for every boss. " +
        "The newest have no regrow but denser rounds that test your damage; the earlier ones have regrow, camo " +
        "regrow and DDTs that test your detection. The i next to the dropdown tells more. The dropdown also has the " +
        "boss's own event rounds, as in the other modes.");
    public static readonly string EventRoundsOff = Key(nameof(EventRoundsOff), "Off");
    public static readonly string ChimpsRoundsNewest = Key(nameof(ChimpsRoundsNewest), "Newest CHIMPS events (no regrow, dense)");
    public static readonly string ChimpsRoundsEarlier = Key(nameof(ChimpsRoundsEarlier), "Earlier CHIMPS events (regrow, DDTs)");
    public static readonly string ChimpsRoundsOwn = Key(nameof(ChimpsRoundsOwn), "Boss's own event rounds (like other modes)");
    /// <summary>
    /// What the two CHIMPS round sets throw at the player, behind the i next to the dropdown (#157), and what the boss's
    /// own event rounds are
    /// </summary>
    public static readonly string ChimpsRoundsInfo = Key(nameof(ChimpsRoundsInfo),
        "Both CHIMPS round sets are much harder than the normal rounds, in different ways: neither is simply harder " +
        "than the other. Only 9 of their 140 rounds are the same.\n\nNewest CHIMPS events (Diamondback, 2026): no " +
        "regrow bloons at all. The bloons come in fewer, bigger groups and the rounds last about half as long, so " +
        "more arrive at once. The pressure comes from MOAB-class bloons and big floods of plain bloons, like 1372 " +
        "Greens on round 49. It tests your damage, most of all from round 41 to 120.\n\nEarlier CHIMPS events " +
        "(Bloonarius 2024, Dreadbloon 2025): many regrow and camo regrow bloons, DDTs on rounds 85, 87 and 93 and " +
        "extra BADs late in the game, spread over longer rounds. It tests your camo detection and popping regrow " +
        "bloons before they grow back.\n\nThe rounds made for the bosses, rounds 40, 60, 80, 100, 120 and 140 in the " +
        "events, are nearly the same in both sets. With other Spawn Rounds they move with the tiers.\n\nBoss's own " +
        "event rounds: the rounds of the boss's events in the other modes, as CHIMPS played them before 2.11, close to " +
        "the normal rounds. In Advanced Mode every tier arrives on the round made for its own boss.");
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
        "Keeps your favourite setups in the game. Save stores this whole setup under the challenge's name; if a " +
        "saved setup already has that name, you are asked before it is replaced. Pick one in the list and press " +
        "Load to play it again, or Delete to remove it. The list also has a few examples to try: load one, change " +
        "it and save it as your own. Examples can't be deleted.");
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
    public static readonly string NewerSetupFiles = Key(nameof(NewerSetupFiles),
        "Your Boss Creator setups come from a newer version of Custom Boss Challenge. They are kept as they are; update the mod to use them.");
    public static readonly string SetupsTooNew = Key(nameof(SetupsTooNew),
        "Your saved setups come from a newer version of Custom Boss Challenge. Update the mod to use them.");
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
    public static readonly string CopyLink = Key(nameof(CopyLink), "Copy Link");
    public static readonly string LinkCopied = Key(nameof(LinkCopied),
        "Link copied. It opens this setup on the Custom Boss Challenge website, and Paste reads it too.");
    public static readonly string LoadCodeQuestion = Key(nameof(LoadCodeQuestion),
        "Load this setup? Your current setup will be replaced.");
    public static readonly string CodeInvalid = Key(nameof(CodeInvalid),
        "The clipboard doesn't contain a Boss Creator code. Copy the whole code, starting with CBC.");
    public static readonly string GildebloonOff = Key(nameof(GildebloonOff),
        "This setup has Gildebloon, a boss made by this mod. Turn on Include Gildebloon in Mod Settings to play it.");
    public static readonly string GildebloonRepossession = Key(nameof(GildebloonRepossession),
        "Repossession! Gildebloon took ${0} and stopped {1} towers");
    public static readonly string CodeTooNew = Key(nameof(CodeTooNew),
        "This code comes from a newer version of Custom Boss Challenge. Update the mod to load it.");

    // The Past Events row and window (#146)
    public static readonly string PastEvents = Key(nameof(PastEvents), "Past Events");
    public static readonly string PastEventsInfo = Key(nameof(PastEventsInfo),
        "Play the official boss events again with their own rules: the map, mode, cash, lives, towers and the " +
        "bloon and boss modifiers, normal or Elite. Browse lists every boss event since 2021; search by name, map " +
        "or mode, or pick a boss, year or mode. New events are downloaded when the game starts, unless that is " +
        "turned off in the mod's settings.");
    public static readonly string BrowsePastEvents = Key(nameof(BrowsePastEvents), "Browse Events ({0})");
    public static readonly string PastEventsLoading = Key(nameof(PastEventsLoading), "Loading the events…");
    public static readonly string PastEventsChecking = Key(nameof(PastEventsChecking), "Looking for new events…");
    public static readonly string PastEventsUpToDate = Key(nameof(PastEventsUpToDate), "Up to date, list from {0}");
    public static readonly string PastEventsFromCache = Key(nameof(PastEventsFromCache), "Downloaded list from {0}");
    public static readonly string PastEventsFromMod = Key(nameof(PastEventsFromMod),
        "List from {0}, came with the mod");
    public static readonly string PastEventsDownloadsOff = Key(nameof(PastEventsDownloadsOff),
        "Downloading new events is turned off");
    public static readonly string PastEventsNone = Key(nameof(PastEventsNone), "No events could be loaded.");
    public static readonly string PastEventsTitle = Key(nameof(PastEventsTitle), "Past Boss Events");
    public static readonly string PastEventsSearch = Key(nameof(PastEventsSearch), "Search: name, map, mode…");
    public static readonly string AnyBoss = Key(nameof(AnyBoss), "Every Boss");
    public static readonly string AnyYear = Key(nameof(AnyYear), "Every Year");
    public static readonly string AnyMode = Key(nameof(AnyMode), "Every Mode");
    public static readonly string PastEventsCount = Key(nameof(PastEventsCount), "{0} of {1}");
    public static readonly string PastEventsNoMatch = Key(nameof(PastEventsNoMatch), "No events match the search.");
    public static readonly string PastEventNormal = Key(nameof(PastEventNormal), "Normal");
    public static readonly string PastEventRanked = Key(nameof(PastEventRanked), "Ranked: {0}, Elite: {1}");
    public static readonly string ScoringTime = Key(nameof(ScoringTime), "Time");
    public static readonly string ScoringLeastCash = Key(nameof(ScoringLeastCash), "Least Cash");
    public static readonly string ScoringLeastTiers = Key(nameof(ScoringLeastTiers), "Least Tiers");
    public static readonly string PastEventNoRules = Key(nameof(PastEventNoRules), "No rules saved");
    public static readonly string PastEventReplacedBy = Key(nameof(PastEventReplacedBy), "Replaced by {0}");
    public static readonly string LoadPastEventQuestion = Key(nameof(LoadPastEventQuestion),
        "Load this boss event? Your current setup will be replaced.");
    public static readonly string PastEventScoring = Key(nameof(PastEventScoring),
        "The event was ranked on {0}. The Boss Creator plays it with the same rules, timed like a Game Time " +
        "event.");
    public static readonly string PastEventUnsupported = Key(nameof(PastEventUnsupported),
        "The Boss Creator can't play these rules of the event, so they are left out: {0}");
    public static readonly string PastEventMissingMap = Key(nameof(PastEventMissingMap),
        "This event's map, {0}, isn't in this version of the game, so the event can't be opened.");
    public static readonly string PastEventUnknownBoss = Key(nameof(PastEventUnknownBoss),
        "This event's boss isn't in this version of the mod. Update the mod to play it.");
    public static readonly string PastEventCantOpen = Key(nameof(PastEventCantOpen),
        "There are no rules saved for this event, so it can't be opened.");
    public static readonly string PastEventOpenFailed = Key(nameof(PastEventOpenFailed), "This event couldn't be opened.");
    // How the player's language writes a date (Config/PastEventDates): the twelve month names, comma separated, and the
    // order of {0} day, {1} month and {2} year
    public static readonly string DateMonths = Key(nameof(DateMonths), "Jan,Feb,Mar,Apr,May,Jun,Jul,Aug,Sep,Oct,Nov,Dec");
    public static readonly string DateFormat = Key(nameof(DateFormat), "{0} {1} {2}");
    public static readonly string Load = Key(nameof(Load), "Load");
    public static readonly string Ok = Key(nameof(Ok), "OK");
    public static readonly string Cancel = Key(nameof(Cancel), "Cancel");
    public static readonly string ResetBossOptions = Key(nameof(ResetBossOptions), "Reset Boss Options");
    public static readonly string ResetBossOptionsQuestion = Key(nameof(ResetBossOptionsQuestion),
        "Put every boss option back to its default? The Challenge Editor rules, like the map and starting cash, stay " +
        "as they are. Ctrl+Z undoes it.");
    public static readonly string Reset = Key(nameof(Reset), "Reset");
    public static readonly string UndoButton = Key(nameof(UndoButton), "Undo (Ctrl+Z)");
    public static readonly string RedoButton = Key(nameof(RedoButton), "Redo (Ctrl+Y)");
    public static readonly string TierCount = Key(nameof(TierCount), "{0} tiers");
    public static readonly string HealthCap = Key(nameof(HealthCap),
        "Health stops at {0} per tier, the game's limit: some tiers get less than the settings ask for.");
    public static readonly string StartRoundClose = Key(nameof(StartRoundClose),
        "The game starts on round {0}, only {1} rounds before the first boss on round {2}.");
    public static readonly string TiersClose = Key(nameof(TiersClose),
        "Tiers come only a few rounds apart on rounds {0}, which leaves little time to beat each one.");
    public static readonly string SameTiers = Key(nameof(SameTiers),
        "Tiers {0} end up the same: their speed and health have reached the game's limits.");
    public static readonly string HardSetup = Key(nameof(HardSetup),
        "These rules together make a very hard challenge.");
    public static readonly string ChimpsEventRoundsNewest = Key(nameof(ChimpsEventRoundsNewest),
        "Boss Event Rounds in CHIMPS: much harder than the normal rounds, with no regrow but dense floods that test " +
        "your damage.");
    public static readonly string ChimpsEventRoundsEarlier = Key(nameof(ChimpsEventRoundsEarlier),
        "Boss Event Rounds in CHIMPS: much harder than the normal rounds, with regrow, camo regrow and DDTs that " +
        "test your detection.");

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

    public static readonly string TierTime = Key(nameof(TierTime), "Tier {0}");
    public static readonly string TotalTime = Key(nameof(TotalTime), "Total");
    public static readonly string PenaltyTime = Key(nameof(PenaltyTime), "Penalty");
    public static readonly string TiersUsed = Key(nameof(TiersUsed), "Upgrade Tiers Used");
    public static readonly string CashUsed = Key(nameof(CashUsed), "Cash Used");
    public static readonly string RankedTime = Key(nameof(RankedTime), "Time");

    public static readonly string RulesNoChanges = Key(nameof(RulesNoChanges), "No other changes");

    /// <summary>
    /// The name of a slider's row in the Rules panel's Modifiers list (#141), like the Boss Creator's
    /// </summary>
    public static string ModifierLabel(Config.SummaryLine line) => line.Kind switch
    {
        Config.RuleLine.Tiers => BossTiers.T(),
        Config.RuleLine.SpeedPerTier => SpeedPerTier.T(),
        Config.RuleLine.HealthPerTier => HealthPerTier.T(),
        _ => SkullCount.T()
    };

    /// <summary>
    /// The value of a slider's row, written like the game's own rows, e.g. "130%"
    /// </summary>
    public static string ModifierValue(Config.SummaryLine line) => line.Kind switch
    {
        Config.RuleLine.SpeedPerTier or Config.RuleLine.HealthPerTier => $"{Signed((int) line.Args[0])}%",
        Config.RuleLine.Skulls => $"{line.Args[1]}",
        _ => $"{line.Args[0]}"
    };

    private static string Signed(int value) => value > 0 ? $"+{value}" : $"{value}";

    /// <summary>
    /// The line the Tier Preview shows for a setup warning (issue #70)
    /// </summary>
    public static string Warning(Config.Warning warning) => warning.Kind switch
    {
        Config.SetupWarning.StartRoundClose => StartRoundClose.T(warning.Args[0], warning.Args[1], warning.Args[2]),
        // The limit is on the boss model and leaves room for Boss Health, which is applied on top like in the game
        Config.SetupWarning.HealthCapped => HealthCap.T(TierStrip.FormatHealth((double) warning.Args[0])),
        Config.SetupWarning.TiersClose => TiersClose.T(warning.Args[0]),
        Config.SetupWarning.SameTiers => SameTiers.T(warning.Args[0]),
        Config.SetupWarning.ChimpsEventRounds => warning.Args is [Config.ChimpsRounds.Earlier]
            ? ChimpsEventRoundsEarlier.T()
            : ChimpsEventRoundsNewest.T(),
        _ => HardSetup.T()
    };

    /// <summary>
    /// A date the way the player's language writes it, e.g. "17 Oct 2026" or "17. okt. 2026"
    /// </summary>
    public static string Date(System.DateTime date) => Config.PastEventDates.Format(date, DateFormat.T(), DateMonths.T());

    /// <summary>
    /// The text for a key in the player's language
    /// </summary>
    public static string T(this string key) => BTD_Mod_Helper.Extensions.StringExt.Localize(key);

    public static string T(this string key, object arg) => string.Format(key.T(), arg);

    public static string T(this string key, object arg0, object arg1) => string.Format(key.T(), arg0, arg1);

    public static string T(this string key, object arg0, object arg1, object arg2) =>
        string.Format(key.T(), arg0, arg1, arg2);
}
