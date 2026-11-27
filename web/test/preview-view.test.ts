// @vitest-environment happy-dom
// What the setup preview puts on the page (#297): its text, the arrows and the words screen readers get for them, the
// ARIA attributes, the Advanced Mode skulls that follow the tier card pointed at, the die for Random and the timeline's
// marks. Mutation testing found these rendered by other tests but not checked.

import { afterEach, describe, expect, it, vi } from "vitest";
import { decode, defaultOptions, type BossOptions, type Challenge } from "@cbc/share-code";
import { EXAMPLES, GAME } from "../src/data/types";
import { skulls, tierCards } from "../src/preview/compute";
import { dice } from "../src/preview/portrait";
import { bossTitle, breakable, renderSetup, renderSkulls, renderTiers, skullSummary, tierPortrait } from "../src/preview/render";
import { labelRows, renderTimeline, tierLine } from "../src/preview/timeline";

async function example(name: string): Promise<{ options: BossOptions; challenge: Challenge }> {
  const result = await decode(EXAMPLES.find((entry) => entry.name === name)!.code);
  if (!result.ok) throw new Error(result.reason);
  return { options: result.options, challenge: result.challenge };
}

const texts = (root: Element, selector: string): (string | null)[] => [...root.querySelectorAll(selector)].map((e) => e.textContent);
const attributes = (root: Element, selector: string, name: string): (string | null)[] =>
  [...root.querySelectorAll(selector)].map((e) => e.getAttribute(name));

afterEach(() => {
  document.body.replaceChildren();
  vi.useRealTimers();
});

describe("the title line", () => {
  it("names the boss, Elite or not, or Advanced Mode", () => {
    expect(bossTitle({ ...defaultOptions(), Boss: "Lych", Elite: true })).toBe("Elite Lych");
    expect(bossTitle({ ...defaultOptions(), Boss: "Lych", Elite: false })).toBe("Lych");
    expect(bossTitle({ ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Vortex" })).toBe("Advanced Mode");
  });

  it("gives the boss, the tiers, the map and the difficulty, and the mode only when it isn't Standard", async () => {
    const { options, challenge } = await example("Marathon Lych");
    const setup = renderSetup(options, challenge);
    expect(setup.querySelector(".setup-name")?.textContent).toBe("Marathon Lych");
    expect(setup.querySelector(".setup-sub")?.textContent).toBe("Elite Lych · 10 tiers · Haunted · Medium");

    const one = renderSetup({ ...options, Elite: false, Tiers: 1 }, { ...challenge, name: "", mode: "Clicks" });
    // Without a name of its own, the setup is called after its boss
    expect(one.querySelector(".setup-name")?.textContent).toBe("Lych");
    expect(one.querySelector(".setup-sub")?.textContent).toBe("Lych · 1 tier · Haunted · Medium · Clicks");
  });

  it("shows a map, difficulty or mode the website doesn't know by its id", async () => {
    const { options, challenge } = await example("Marathon Lych");
    const setup = renderSetup(options, { ...challenge, map: "NewMap", difficulty: "Brutal", mode: "NewMode" });
    expect(setup.querySelector(".setup-sub")?.textContent).toBe("Elite Lych · 10 tiers · NewMap · Brutal · NewMode");
  });

  it("says when the game's Play would refuse the setup, and only then", async () => {
    const { options, challenge } = await example("Speed Demon Vortex");
    expect(renderSetup(options, challenge).querySelector(".warning")).toBeNull();
    const late = renderSetup(options, { ...challenge, round: 50 });
    expect(late.querySelector(".warning")?.textContent).toBe(
      "In the game, Play refuses this setup: The game starts on round 50, but the first boss tier comes on round 40 and would be skipped.",
    );
  });
});

describe("the boss badge", () => {
  it("is a decoration with the boss's portrait, marked Elite, or a star in Advanced Mode", async () => {
    const { challenge } = await example("Speed Demon Vortex");
    const elite = renderSetup({ ...defaultOptions(), Boss: "Lych", Elite: true, Tiers: 5 }, challenge, true).querySelector(".boss-badge")!;
    expect(elite.getAttribute("aria-hidden")).toBe("true");
    expect(elite.className).toBe("boss-badge elite");
    expect(elite.getAttribute("style")).toBeNull();

    const normal = renderSetup({ ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 5 }, challenge, true).querySelector(".boss-badge")!;
    expect(normal.className).toBe("boss-badge");

    // Elite on a boss per tier is each tier's own, not the badge's
    const mixed = renderSetup({ ...defaultOptions(), Elite: true, Tiers: 2, TierBosses: "Lych,Vortex" }, challenge, true).querySelector(".boss-badge")!;
    expect(mixed.className).toBe("boss-badge");
    expect(mixed.textContent).toBe("★");
  });

  it("glows like the last tier with more than 5 tiers", async () => {
    const { challenge } = await example("Speed Demon Vortex");
    const badge = renderSetup({ ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 8 }, challenge, true).querySelector(".boss-badge")!;
    expect(badge.className).toBe("boss-badge tier-look");
    expect(badge.getAttribute("style")).toContain("--tier-color:");
  });
});

describe("the Tier Preview's cards", () => {
  it("are a list, each card read out in words with its numbers hidden from screen readers", async () => {
    const { options, challenge } = await example("Glass Cannon");
    const tiers = renderTiers(options, challenge);
    expect(tiers.querySelector("h4")?.textContent).toBe("Tier Preview");
    expect(tiers.querySelector(".tiers")?.getAttribute("role")).toBe("list");
    const card = tiers.querySelector(".tier")!;
    expect(card.getAttribute("role")).toBe("listitem");
    // Only an Advanced Mode card can take the focus
    expect(card.hasAttribute("tabindex")).toBe(false);
    expect(card.className).toBe("tier");
    expect(card.querySelector(".sr-only")?.textContent).toBe(
      "Tier 1, round 40, speed ×3, faster than normal, health 2k, weaker than normal",
    );
    expect(texts(card, '[aria-hidden="true"]')).toEqual(["T1", "R40", "×3▲", "2k▼"]);
    expect(attributes(card, '[aria-hidden="true"]', "class")).toEqual(["tier-label", "tier-round", "tier-speed harder", "tier-health easier"]);
    expect(attributes(card, ".tier-speed, .tier-health", "title")).toEqual(["Speed compared with the normal game", "Health"]);
  });

  it("show ▲ and say faster and tougher above the normal game, ▼ slower and weaker below it, and nothing at it", async () => {
    const { options, challenge } = await example("Glass Cannon");
    const card = (bossSpeed: number, bossHealth: number): Element => renderTiers(options, { ...challenge, bossSpeed, bossHealth }).querySelector(".tier")!;

    const slower = card(0.5, 2);
    expect(slower.querySelector(".sr-only")?.textContent).toContain("speed ×0.5, slower than normal, health");
    expect(slower.querySelector(".sr-only")?.textContent).toMatch(/tougher than normal$/);
    expect(slower.querySelector(".tier-speed")?.className).toBe("tier-speed easier");
    expect(slower.querySelector(".tier-speed")?.textContent).toBe("×0.5▼");
    expect(slower.querySelector(".tier-health")?.className).toBe("tier-health harder");
    expect(slower.querySelector(".tier-health")?.textContent).toMatch(/▲$/);

    const normal = card(1, 1);
    expect(normal.querySelector(".sr-only")?.textContent).toBe("Tier 1, round 40, speed ×1, normal speed, health 20k, normal health");
    expect(normal.querySelector(".tier-speed")?.className).toBe("tier-speed same");
    expect(normal.querySelector(".tier-speed")?.textContent).toBe("×1");
    expect(normal.querySelector(".tier-health")?.textContent).toBe("20k");
  });

  it("call a change too small to show the same as the normal game", async () => {
    const { options, challenge } = await example("Glass Cannon");
    // Boss Speed is a float in the game: 1.00005 isn't exactly 1, but no faster to see
    for (const bossSpeed of [1.00005, 0.99995]) {
      const card = renderTiers(options, { ...challenge, bossSpeed, bossHealth: 1 }).querySelector(".tier")!;
      expect(card.querySelector(".tier-speed")?.className, String(bossSpeed)).toBe("tier-speed same");
    }
    for (const bossSpeed of [1.0002, 0.9998]) {
      const card = renderTiers(options, { ...challenge, bossSpeed, bossHealth: 1 }).querySelector(".tier")!;
      expect(card.querySelector(".tier-speed")?.className, String(bossSpeed)).not.toBe("tier-speed same");
    }
  });

  it("glow from tier 6 on", async () => {
    const { options, challenge } = await example("Marathon Lych");
    const cards = [...renderTiers(options, challenge).querySelectorAll(".tier")];
    expect(cards.map((card) => card.className)).toEqual([...Array(5).fill("tier"), ...Array(5).fill("tier tier-look")]);
    expect(cards[4].getAttribute("style")).toBeNull();
    expect(cards[5].getAttribute("style")).toContain("--tier-color:");
  });

  it("in Advanced Mode name each tier's boss and skulls, and can be focused", async () => {
    const { options, challenge } = await example("Boss Gauntlet");
    const cards = [...renderTiers(options, challenge).querySelectorAll(".tier")];
    expect(cards.every((card) => card.getAttribute("tabindex") === "0")).toBe(true);
    expect(cards[1].querySelector(".sr-only")?.textContent).toBe(
      "Tier 2, Lych, round 60, speed ×1, normal speed, health 53k, normal health, 5 skulls",
    );
    expect(texts(cards[1], ".tier-skulls")).toEqual(["☠ 5"]);
    expect(attributes(cards[1], ".tier-boss, .tier-skulls", "title")).toEqual(["This tier's boss", "Skulls of this tier's boss"]);
    expect(attributes(cards[1], ".tier-boss, .tier-skulls", "aria-hidden")).toEqual(["true", "true"]);
    // A long name may break where its parts meet
    expect(cards[5].querySelector(".tier-boss")?.textContent).toBe("Blasta­popoulos");
  });

  it("in Advanced Mode say when a Random tier's skulls aren't known yet", () => {
    const options = { ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Random" };
    const cards = [...renderTiers(options, { bossSpeed: 1, bossHealth: 1 } as Challenge).querySelectorAll(".tier")];
    expect(cards[1].querySelector(".sr-only")?.textContent).toMatch(/^Tier 2, Random, round 60, .*, skulls unknown$/);
    expect(texts(cards[1], ".tier-skulls")).toEqual(["☠ ?"]);
    expect(cards[1].querySelector(".tier-boss svg.dice")).not.toBeNull();
    expect(cards[0].querySelector(".tier-boss svg.dice")).toBeNull();
  });

  it("show only the notes they're given, even with tiers held to the health limit", () => {
    const options = { ...defaultOptions(), Elite: true, Tiers: 10, HealthPerTier: 200 };
    const challenge = { bossSpeed: 1, bossHealth: 1 } as Challenge;
    expect(renderTiers(options, challenge).querySelectorAll(".note")).toHaveLength(1);
    expect(renderTiers(options, challenge, []).querySelectorAll(".note")).toHaveLength(0);
    const given = renderTiers(defaultOptions(), challenge, ["One", "Two"]);
    expect(texts(given, ".note")).toEqual(["One", "Two"]);
    expect(attributes(given, ".note", "class")).toEqual(["note harder", "note harder"]);
    // Above 100 % Boss Health the limit is the game's, so each tier's model gets less
    const doubled = renderTiers(options, { bossSpeed: 1, bossHealth: 2 } as Challenge);
    expect(doubled.querySelector(".note")?.textContent).toContain("Health stops at 2B per tier");
    // Without a tier at the limit and without notes, there is none
    expect(renderTiers(defaultOptions(), challenge).querySelectorAll(".note")).toHaveLength(0);
  });
});

describe("long boss names", () => {
  it("may break only where their parts meet", () => {
    expect(breakable("Blastapopoulos")).toBe("Blasta­popoulos");
    expect(breakable("Diamondback")).toBe("Diamond­back");
    expect(breakable("Dreadbloon")).toBe("Dread­bloon");
    expect(breakable("Bloonarius")).toBe("Bloon­arius");
    expect(breakable("Elite Lych")).toBe("Elite Lych");
  });
});

describe("the Random die", () => {
  it("is a drawing of a five, hidden from screen readers", () => {
    const svg = dice(24);
    expect(svg.namespaceURI).toBe("http://www.w3.org/2000/svg");
    expect(svg.getAttribute("viewBox")).toBe("0 0 20 20");
    expect(svg.getAttribute("class")).toBe("portrait dice");
    expect(svg.getAttribute("width")).toBe("24");
    expect(svg.getAttribute("height")).toBe("24");
    expect(svg.getAttribute("aria-hidden")).toBe("true");
    expect(svg.getAttribute("focusable")).toBe("false");

    const body = svg.querySelector("rect")!;
    expect([...svg.children].every((child) => child.namespaceURI === "http://www.w3.org/2000/svg")).toBe(true);
    expect(Object.fromEntries(["x", "y", "width", "height", "rx", "class"].map((name) => [name, body.getAttribute(name)]))).toEqual({
      x: "2.5", y: "2.5", width: "15", height: "15", rx: "3.5", class: "dice-body",
    });
    const pips = [...svg.querySelectorAll("circle")];
    expect(pips.map((pip) => `${pip.getAttribute("cx")},${pip.getAttribute("cy")}`)).toEqual(["6.5,6.5", "13.5,6.5", "10,10", "6.5,13.5", "13.5,13.5"]);
    expect(pips.every((pip) => pip.getAttribute("r") === "1.5" && pip.getAttribute("class") === "dice-pip")).toBe(true);
  });

  it("stands in for a Random tier's portrait, Elite or not", () => {
    const options = { ...defaultOptions(), Tiers: 2, TierBosses: "Lych,RandomElite" };
    expect(tierPortrait(options, 2, "Random Elite").getAttribute("class")).toBe("portrait dice");
    expect(tierPortrait(options, 1, "Lych").getAttribute("src")).toBe("./bosses/Lych.webp");
  });
});

describe("the Skull Preview", () => {
  it("says where the skulls are, in a picture with words and in labels on the bar", () => {
    const block = renderSkulls({ ...defaultOptions(), Boss: "Vortex", Elite: false, Tiers: 5, SkullMode: "Vanilla" });
    // Every tier of the boss has the same skulls, so the heading names no tier
    expect(block.querySelector("h4")?.textContent).toBe("Skulls (Vanilla)");
    const bar = block.querySelector(".skull-bar")!;
    expect(bar.getAttribute("role")).toBe("img");
    expect(bar.getAttribute("aria-label")).toBe("Health bar with skulls at 75%, 50%, 25% of the boss's health");
    expect(texts(bar, ".skull span")).toEqual(["75%", "50%", "25%"]);
    expect([...bar.querySelectorAll<HTMLElement>(".skull")].map((skull) => skull.style.left)).toEqual(["25%", "50%", "75%"]);
    expect(skullSummary(block)).toBe("Skulls (Vanilla). Health bar with skulls at 75%, 50%, 25% of the boss's health");
  });

  it("says when there are no skulls, or when a Random tier's aren't known", async () => {
    const none = renderSkulls((await example("No Skulls, No Mercy")).options);
    expect(none.querySelector(".skull-bar")).toBeNull();
    expect(none.querySelector("p.muted")?.textContent).toBe("No skulls: the boss never triggers its skull effect.");
    expect(skullSummary(none)).toBe(`${none.querySelector("h4")?.textContent}. No skulls: the boss never triggers its skull effect.`);

    const random = renderSkulls({ ...defaultOptions(), Tiers: 2, TierBosses: "Random,Lych" });
    expect(random.querySelector("h4")?.textContent).toBe("Skulls (Vanilla, tier 1)");
    expect(random.querySelector("p.muted")?.textContent).toBe("A Random tier gets the skulls of the boss it gets when the game starts.");
  });
});

describe("the skulls in Advanced Mode", () => {
  const skullBlock = (root: Element): Element => [...root.querySelectorAll(".block")].find((b) => b.querySelector("h4")?.textContent?.startsWith("Skulls"))!;

  it("follow the tier card pointed at", async () => {
    vi.useFakeTimers();
    const { options, challenge } = await example("Boss Gauntlet");
    const setup = renderSetup(options, challenge);
    document.body.append(setup);
    const first = skullBlock(setup);
    expect(first.querySelector("h4")?.textContent).toBe("Skulls (Vanilla, tier 1)");
    const cards = [...setup.querySelectorAll(".tier")];

    // Pointing at the tier already shown changes nothing
    cards[0].dispatchEvent(new Event("mouseenter"));
    expect(skullBlock(setup)).toBe(first);

    cards[1].dispatchEvent(new Event("mouseenter"));
    const second = skullBlock(setup);
    expect(second).not.toBe(first);
    expect(first.isConnected).toBe(false);
    expect(second.querySelector("h4")?.textContent).toBe("Skulls (Vanilla, tier 2)");
    expect(second.querySelectorAll(".skull")).toHaveLength(5);
    // The block keeps its place, between the timeline and the strength chart
    expect(second.previousElementSibling?.classList.contains("timeline")).toBe(true);

    cards[1].dispatchEvent(new Event("mouseenter"));
    expect(skullBlock(setup)).toBe(second);
    // Pointing isn't read out: the screen reader follows the focus, not the mouse
    vi.advanceTimersByTime(100);
    expect(document.querySelector('[role="status"]')?.textContent ?? "").toBe("");
  });

  it("follow the focus too, and say what they show then", async () => {
    vi.useFakeTimers();
    const { options, challenge } = await example("Boss Gauntlet");
    const setup = renderSetup(options, challenge);
    document.body.append(setup);
    setup.querySelectorAll(".tier")[5].dispatchEvent(new Event("focus"));
    expect(skullBlock(setup).querySelector("h4")?.textContent).toBe("Skulls (Vanilla, tier 6)");
    vi.advanceTimersByTime(100);
    expect(document.querySelector('[role="status"]')?.textContent).toBe(
      "Skulls (Vanilla, tier 6). Health bar with skulls at 80%, 60%, 40%, 20% of the boss's health",
    );
  });

  it("name the skull mode of the tier shown", () => {
    const options = { ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Vortex#4" };
    expect(renderSkulls(options, 1).querySelector("h4")?.textContent).toBe("Skulls (Vanilla, tier 1)");
    expect(renderSkulls(options, 2).querySelector("h4")?.textContent).toBe("Skulls (Evenly Spaced, tier 2)");
  });

  it("stay put with one boss for every tier", async () => {
    const { options, challenge } = await example("Speed Demon Vortex");
    const setup = renderSetup(options, challenge);
    const first = skullBlock(setup);
    setup.querySelectorAll(".tier")[2].dispatchEvent(new Event("mouseenter"));
    setup.querySelectorAll(".tier")[2].dispatchEvent(new Event("focus"));
    expect(skullBlock(setup)).toBe(first);
  });
});

describe("the timeline's marks", () => {
  const marks = (block: Element) =>
    [...block.querySelectorAll<HTMLElement>(".timeline-mark")].map((mark) => ({
      kind: mark.className,
      text: [...mark.querySelectorAll(".timeline-label > *")].map((part) => part.textContent),
      left: mark.style.left,
      row: mark.style.getPropertyValue("--row"),
    }));

  it("go from the start round, when the code sets one, to the end", () => {
    const options = { ...defaultOptions(), Tiers: 2, SpawnRounds: "20, 22" };
    const block = renderTimeline(options, { round: 2 } as Challenge);
    expect(block.querySelector("h4")?.textContent).toBe("Timeline");
    // Rounds 20 and 22 are too close for their labels to share a row
    expect(marks(block)).toEqual([
      { kind: "timeline-mark start", text: ["Start", "R2"], left: "0%", row: "0" },
      { kind: "timeline-mark tier", text: ["T1", "R20"], left: "45%", row: "0" },
      { kind: "timeline-mark tier", text: ["T2", "R22"], left: "50%", row: "1" },
      { kind: "timeline-mark end", text: ["End", "R42"], left: "100%", row: "0" },
    ]);
    expect(block.querySelector<HTMLElement>(".timeline-bar")?.style.marginBottom).toBe("80px");
    expect(block.querySelector("p.muted")).toBeNull();
  });

  it("start at round 1 when the difficulty decides the start", () => {
    const block = renderTimeline({ ...defaultOptions(), Tiers: 1, SpawnRounds: "21" }, { round: -1 } as Challenge);
    expect(marks(block)).toEqual([
      { kind: "timeline-mark tier", text: ["T1", "R21"], left: "50%", row: "0" },
      { kind: "timeline-mark end", text: ["End", "R41"], left: "100%", row: "0" },
    ]);
    expect(block.querySelector<HTMLElement>(".timeline-bar")?.style.marginBottom).toBe("40px");
    expect(texts(block, "li")).toEqual(["Tier 1: round 21, Bloonarius", "The game ends on round 41"]);
  });

  it("put a label on the same row once the one before has room", () => {
    // Exactly a label's width apart is enough
    expect(labelRows([0, 0.25], 0.25)).toEqual([0, 0]);
    expect(labelRows([0, 0.24], 0.25)).toEqual([0, 1]);
  });

  it("say which event round a tier plays, and that tiers above 5 have none", () => {
    expect(tierLine({ tier: 2, round: 60, boss: "Lych", eventRound: 60 })).toBe("Tier 2: round 60, Lych (plays the event's round 60)");
    expect(tierLine({ tier: 6, round: 140, boss: "Lych", eventRound: null })).toBe("Tier 6: round 140, Lych");

    const options = { ...defaultOptions(), Tiers: 6, BossEventRounds: true };
    expect(renderTimeline(options, { round: -1 } as Challenge).querySelector("p.muted")?.textContent).toBe(
      "Tiers above 5 have no event round of their own.",
    );
    // Without event rounds, or with 5 tiers, there is nothing to say
    expect(renderTimeline({ ...options, BossEventRounds: false }, { round: -1 } as Challenge).querySelector("p.muted")).toBeNull();
    expect(renderTimeline({ ...options, Tiers: 5 }, { round: -1 } as Challenge).querySelector("p.muted")).toBeNull();
  });
});

describe("the Tier Preview's numbers at the edges", () => {
  it("hold a tier to the game's health limit only above it", () => {
    const model = GAME.bosses.Lych.normal[0];
    const health = model.health;
    try {
      const options = { ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 1 };
      const challenge = { bossSpeed: 1, bossHealth: 1 } as Challenge;
      model.health = 2_000_000_000;
      expect(tierCards(options, challenge)[0]).toMatchObject({ capped: false, healthValue: 2_000_000_000 });
      model.health = 2_000_000_001;
      expect(tierCards(options, challenge)[0]).toMatchObject({ capped: true, healthValue: 2_000_000_000 });
    } finally {
      model.health = health;
    }
  });

  it("show the skulls of tier 1 for a tier below it, and of the last tier above it", () => {
    const options = { ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Vortex" };
    expect(skulls(options, 0)).toEqual(skulls(options, 1));
    expect(skulls(options, 0).tier).toBe(1);
    expect(skulls(options, 9).tier).toBe(2);
  });

  it("have no skulls for a boss the website doesn't know", () => {
    expect(skulls({ ...defaultOptions(), Boss: "NewBoss", Tiers: 5 })).toEqual({ percents: [], tier: null });
  });

  it("name a tier for the skulls only when the boss's tiers differ", () => {
    expect(skulls({ ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 5 }).tier).toBeNull();

    const models = GAME.bosses.Lych.normal;
    const kept = models[1].skulls;
    try {
      models[1].skulls = [0.5];
      const shown = skulls({ ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 5 });
      expect(shown.tier).toBe(1);
      expect(shown.percents).toEqual(models[0].skulls.map(Math.fround).sort((a, b) => b - a));
      // The same number of skulls in other places differs too
      models[1].skulls = models[0].skulls.map((value) => value / 2);
      expect(skulls({ ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 5 }).tier).toBe(1);
      // Only tiers up to the setup's own count
      expect(skulls({ ...defaultOptions(), Boss: "Lych", Elite: false, Tiers: 1 }).tier).toBeNull();
    } finally {
      models[1].skulls = kept;
    }
  });
});

describe("the rules list", () => {
  const rules = (root: Element): [string | null, string | null][] =>
    [...root.querySelectorAll(".rules .rule")].map((rule) => [rule.querySelector("dt")!.textContent, rule.querySelector("dd")!.textContent]);

  it("gives Speed and Health per Tier with their sign, and leaves rules at their default out", async () => {
    const { options, challenge } = await example("Speed Demon Vortex");
    const shown = (changes: Partial<BossOptions>, rules_: Partial<Challenge> = {}) =>
      new Map(rules(renderSetup({ ...options, ...changes }, { ...challenge, ...rules_ })));

    const up = shown({ SpeedPerTier: 10, HealthPerTier: 25 });
    expect(up.get("Speed per Tier")).toBe("+10 %");
    expect(up.get("Health per Tier")).toBe("+25 %");
    const down = shown({ SpeedPerTier: -10, HealthPerTier: -25 });
    expect(down.get("Speed per Tier")).toBe("-10 %");
    expect(down.get("Health per Tier")).toBe("-25 %");
    const none = shown({ SpeedPerTier: 0, HealthPerTier: 0, BossEventRounds: false }, { maxLives: -1, round: -1, maxTowers: -1, leastCash: -1, leastTiers: -1 });
    expect([...none.keys()]).toEqual(["Spawn rounds", "Game ends", "Boss Speed", "Boss Health", "Max Paragons", "Starting cash", "Lives"]);
    expect(none.get("Spawn rounds")).toBe("40, 60, 80, 100, 120");
    expect(none.get("Game ends")).toBe("Round 140");

    const all = shown({ BossEventRounds: true }, { maxLives: 300, round: 3, maxTowers: 20, leastCash: 0, leastTiers: 2, cash: -1, lives: 150 });
    expect(all.get("Boss Event Rounds")).toBe("Yes");
    expect(all.get("Max lives")).toBe("300");
    expect(all.get("Start round")).toBe("3");
    expect(all.get("Max towers")).toBe("20");
    expect(all.get("Least cash")).toBe("0");
    expect(all.get("Least tiers")).toBe("2");
    expect(all.get("Starting cash")).toBe("Default");
    expect(all.get("Lives")).toBe("150");
  });

  it("puts every modifier in a chip of its own, and the tower rules in a list that opens", async () => {
    const { options, challenge } = await example("Speed Demon Vortex");
    const flags = {
      allCamo: true, allRegen: true, allFortified: true, disableMK: true, disableSelling: true, disablePowers: true,
      noContinues: true, disableDoubleCash: true, disableFastTrack: true,
    };
    const setup = renderSetup(options, { ...challenge, ...flags, noInstaReward: GAME.defaultChallenge.noInstaReward as boolean });
    expect(texts(setup, ".chips .chip")).toEqual([
      "All Camo", "All Regrow", "All Fortified", "No Monkey Knowledge", "No Selling", "No Powers", "No Continues", "No Double Cash", "No Fast Track",
    ]);
    const plain = renderSetup(options, { ...challenge, towers: {}, blockedTiers: {}, paragonLocked: [] });
    expect(plain.querySelector(".chips")).toBeNull();
    expect(plain.querySelector(".rules ~ details.towers")).toBeNull();

    const towers = renderSetup(options, { ...challenge, towers: { DartMonkey: 2 }, blockedTiers: {}, paragonLocked: ["BombShooter"] });
    expect(towers.querySelector(".rules ~ details.towers summary")?.textContent).toBe("Tower rules (2)");
    expect(texts(towers, ".rules ~ details.towers li")).toEqual(["Dart Monkey: Max 2", "Bomb Shooter: No Paragon"]);
  });
});

describe("the strength chart", () => {
  it("names its rows by tier, and in Advanced Mode by the tier's boss too", async () => {
    const one = await example("Speed Demon Vortex");
    const rows = texts(renderSetup(one.options, one.challenge), ".compare-table tbody th");
    expect(rows).toEqual(["T1", "T2", "T3", "T4", "T5"]);
  });
});

describe("the difficulty stars", () => {
  it("are a picture with words, and the i opens and closes what they mean", async () => {
    const { options, challenge } = await example("Speed Demon Vortex");
    const block = renderSetup(options, challenge).querySelector(".difficulty")!;
    const stars = Number(block.getAttribute("data-stars"));
    expect(block.querySelector(".difficulty-stars")?.getAttribute("role")).toBe("img");
    expect(block.querySelector(".difficulty-stars")?.getAttribute("aria-label")).toMatch(new RegExp(`: ${stars} of 10 stars, an estimate$`));
    expect(block.querySelectorAll(".star.on")).toHaveLength(stars);
    expect(block.querySelectorAll(".star")).toHaveLength(10);

    const button = block.querySelector<HTMLButtonElement>("button.studio-info")!;
    const panel = block.querySelector<HTMLElement>(".difficulty-text")!;
    expect(button.getAttribute("aria-controls")).toBe(panel.id);
    expect([panel.hidden, button.getAttribute("aria-expanded")]).toEqual([true, "false"]);
    button.click();
    expect([panel.hidden, button.getAttribute("aria-expanded")]).toEqual([false, "true"]);
    button.click();
    expect([panel.hidden, button.getAttribute("aria-expanded")]).toEqual([true, "false"]);
  });
});
