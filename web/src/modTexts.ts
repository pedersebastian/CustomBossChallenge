// The mod's own texts the website shows (#157), by their Menus/Texts.cs name, so the Studio and the previews say what
// the Boss Creator says. test/mod-texts.test.ts keeps them equal to Localization/English.json; where a text talks about
// something only the game has, like Ctrl+Z, the website keeps its first sentences.

/** The texts, by their Texts.cs name */
export const MOD_TEXTS = {
  BossTypeInfo: "Choose which boss to fight. Tick Elite to fight the much stronger Elite version of the boss.",
  BossSpeedInfo: "How fast the boss moves, in percent of its normal speed. 100 % is the normal speed.",
  BossHealthInfo: "How much health the boss has, in percent of its normal health. Applies to every tier.",
  MaxParagonsInfo:
    "How many paragons you can have at the same time. 0 means no paragons. Drag the slider all the way to the right for no limit.",
  BossTiersInfo:
    "How many tiers of the boss you have to defeat, from 1 to 10. Each tier is usually stronger than the one before, unless Speed or Health per Tier is below 0. You win when the last tier is defeated. Tiers above 5 are copies of tier 5 with more health.",
  SpeedPerTierInfo:
    "Adds this share of the normal speed for every tier after the first, on top of the game's own steps: +20 % makes tier 2 ×1.2, tier 3 ×1.4 and so on. Below 0 makes later tiers slower.",
  HealthPerTierInfo:
    "Adds this share of the normal health for every tier after the first, on top of the game's own steps: +100 % makes tier 2 ×2, tier 3 ×3 and so on. Each tier above 5 already has ×1.5 the health of the one before, so tier 7 has ×2.25 of tier 5. 0 % keeps the normal health.",
  FirstBossRoundInfo:
    "The round the first tier arrives on. Together with Rounds Between Tiers it fills in Spawn Rounds; type in Spawn Rounds instead to give every tier its own round.",
  RoundsBetweenTiersInfo: "How many rounds pass between one tier and the next. 20 is the normal game; 1 sends a new tier every round.",
  SpawnRoundsInfo:
    "The round each tier arrives on, separated by commas, e.g. 40, 60, 80. Missing rounds continue every 20 rounds. The game's last round is moved to after the last tier so every tier can be played.",
  BossEventRoundsInfo:
    "Tick to play the rounds of the official boss events for this boss instead of the normal rounds. In the events the tiers arrive on rounds 40, 60, 80, 100 and 120 with bloons made for them; with other Spawn Rounds those rounds move with the tiers. In Advanced Mode every tier arrives on the round made for its own boss, and the other rounds follow the first tier's boss. In CHIMPS, pick the rounds of the newest CHIMPS boss event (Diamondback, 2026) or the earlier ones (Bloonarius and Dreadbloon), the same for every boss. The newest have no regrow but denser rounds that test your damage; the earlier ones have regrow, camo regrow and DDTs that test your detection. The i next to the dropdown tells more. The dropdown also has the boss's own event rounds, as in the other modes.",
  ChimpsRoundsInfo:
    "Both CHIMPS round sets are much harder than the normal rounds, in different ways: neither is simply harder than the other. Only 9 of their 140 rounds are the same.\n\nNewest CHIMPS events (Diamondback, 2026): no regrow bloons at all. The bloons come in fewer, bigger groups and the rounds last about half as long, so more arrive at once. The pressure comes from MOAB-class bloons and big floods of plain bloons, like 1372 Greens on round 49. It tests your damage, most of all from round 41 to 120.\n\nEarlier CHIMPS events (Bloonarius 2024, Dreadbloon 2025): many regrow and camo regrow bloons, DDTs on rounds 85, 87 and 93 and extra BADs late in the game, spread over longer rounds. It tests your camo detection and popping regrow bloons before they grow back.\n\nThe rounds made for the bosses, rounds 40, 60, 80, 100, 120 and 140 in the events, are nearly the same in both sets. With other Spawn Rounds they move with the tiers.\n\nBoss's own event rounds: the rounds of the boss's events in the other modes, as CHIMPS played them before 2.11, close to the normal rounds. In Advanced Mode every tier arrives on the round made for its own boss.",
  SkullsInfo:
    "Skulls are points on the boss's health bar where it uses its special ability. Vanilla keeps the normal skulls, Evenly Spaced spreads a number of skulls over the health bar and Custom % puts them at the health percentages you choose.",
  SkullCountInfo: "How many evenly spaced skulls the boss has. 0 removes all skulls.",
  SkullHealthInfo:
    "How much of its health the boss has left when it reaches each skull and uses its special ability, separated by commas, e.g. 80, 60, 40, 20. The percentages are the same for every tier.",
  EventRoundsOff: "Off",
  ChimpsRoundsNewest: "Newest CHIMPS events (no regrow, dense)",
  ChimpsRoundsEarlier: "Earlier CHIMPS events (regrow, DDTs)",
  ChimpsRoundsOwn: "Boss's own event rounds (like other modes)",
  ChimpsEventRoundsNewest:
    "Boss Event Rounds in CHIMPS: much harder than the normal rounds, with no regrow but dense floods that test your damage.",
  ChimpsEventRoundsEarlier:
    "Boss Event Rounds in CHIMPS: much harder than the normal rounds, with regrow, camo regrow and DDTs that test your detection.",
} as const;

export type ModText = keyof typeof MOD_TEXTS;
