// The preview's texts that don't need a page (#297): the tower rules, the Boss Creator's warnings and how Boss Event
// Rounds read

import { describe, expect, it } from "vitest";
import { defaultOptions, type Challenge } from "@cbc/share-code";
import { MOD_TEXTS } from "../src/modTexts";
import { chimpsEventWarning, eventRoundsText } from "../src/preview/eventRounds";
import { towerRuleText, towersWithRules } from "../src/preview/towerRules";
import { warningText } from "../src/preview/warnings";

const rules = (changes: Partial<Challenge>): Challenge => ({ towers: {}, blockedTiers: {}, paragonLocked: [], ...changes }) as Challenge;

describe("the tower rules", () => {
  it("list the towers in the game's order, ones the website doesn't know last", () => {
    expect(towersWithRules(rules({ towers: { NewTower: 1, SniperMonkey: 2 }, paragonLocked: ["DartMonkey"] }))).toEqual([
      "DartMonkey", "SniperMonkey", "NewTower",
    ]);
    expect(towersWithRules(rules({ towers: { ZTower: 1, NewTower: 1 }, blockedTiers: { BoomerangMonkey: [0, 0, 1] } }))).toEqual([
      "BoomerangMonkey", "ZTower", "NewTower",
    ]);
    // Both challenges' towers, each once
    expect(towersWithRules(rules({ towers: { DartMonkey: 1 } }), rules({ towers: { DartMonkey: 2, BombShooter: 0 } }))).toEqual(["DartMonkey", "BombShooter"]);
  });

  it("say what a tower may do", () => {
    expect(towerRuleText(rules({ towers: { DartMonkey: 0 }, paragonLocked: ["DartMonkey"] }), "DartMonkey")).toBe("Not allowed");
    expect(towerRuleText(rules({ towers: { DartMonkey: -1 }, blockedTiers: { DartMonkey: [0, 2, 5] } }), "DartMonkey")).toBe("No limit, upgrades up to 5-3-0");
    expect(towerRuleText(rules({ towers: { DartMonkey: 4 }, paragonLocked: ["DartMonkey"] }), "DartMonkey")).toBe("Max 4, no Paragon");
    expect(towerRuleText(rules({ paragonLocked: ["DartMonkey"] }), "DartMonkey")).toBe("No Paragon");
    expect(towerRuleText(rules({}), "DartMonkey")).toBe("Default");
  });
});

describe("the Boss Creator's warnings", () => {
  it("fill in their numbers, and leave a placeholder without one", () => {
    expect(warningText({ kind: "StartRoundClose", args: [36, 4, 40] })).toBe(
      MOD_TEXTS.StartRoundClose.replace("{0}", "36").replace("{1}", "4").replace("{2}", "40"),
    );
    expect(warningText({ kind: "StartRoundClose", args: [36] })).toBe(MOD_TEXTS.StartRoundClose.replace("{0}", "36"));
    expect(warningText({ kind: "SameTiers", args: ["6-10"] })).toBe(MOD_TEXTS.SameTiers.replace("{0}", "6-10"));
    expect(warningText({ kind: "HardSetup", args: [] })).toBe(MOD_TEXTS.HardSetup);
    expect(warningText({ kind: "ChimpsEventRounds", args: ["Earlier"] })).toBe(MOD_TEXTS.ChimpsEventRoundsEarlier);
    expect(warningText({ kind: "ChimpsEventRounds", args: ["Newest"] })).toBe(MOD_TEXTS.ChimpsEventRoundsNewest);
  });
});

describe("Boss Event Rounds", () => {
  const on = { ...defaultOptions(), BossEventRounds: true };

  it("read as no, yes, or in CHIMPS the events' rounds they play", () => {
    expect(eventRoundsText({ ...on, BossEventRounds: false }, "Clicks")).toBe("No");
    expect(eventRoundsText(on, "Standard")).toBe("Yes");
    expect(eventRoundsText({ ...on, ChimpsRounds: "Own" }, "Clicks")).toBe("Boss's own event rounds");
    expect(eventRoundsText({ ...on, ChimpsRounds: "Earlier" }, "Clicks")).toBe("Earlier CHIMPS events");
    expect(eventRoundsText({ ...on, ChimpsRounds: "Newest" }, "Clicks")).toBe("Newest CHIMPS events");
  });

  it("warn in CHIMPS about the events' rounds, not about the boss's own", () => {
    expect(chimpsEventWarning({ ...on, ChimpsRounds: "Earlier" }, "Clicks")).toBe(MOD_TEXTS.ChimpsEventRoundsEarlier);
    expect(chimpsEventWarning({ ...on, ChimpsRounds: "Newest" }, "Clicks")).toBe(MOD_TEXTS.ChimpsEventRoundsNewest);
    expect(chimpsEventWarning({ ...on, ChimpsRounds: "Own" }, "Clicks")).toBeNull();
    expect(chimpsEventWarning({ ...on, BossEventRounds: false, ChimpsRounds: "Earlier" }, "Clicks")).toBeNull();
    expect(chimpsEventWarning(on, "Standard")).toBeNull();
  });
});
