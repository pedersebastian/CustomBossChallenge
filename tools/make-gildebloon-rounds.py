"""
Makes Gildebloon's own rounds, "Gold Rush" (#25), from the game's default rounds:

    python tools/make-gildebloon-rounds.py            writes Config/RoundSets/gildebloon.json
    python tools/make-gildebloon-rounds.py --check    fails if the file isn't what this makes

The game's rounds and what each bloon is worth (its RBE) come from tools/data, written by the in-game RoundDataTest
(CBC_ROUND_DATA=<repo>/tools/data). Every round stays close to the default round's RBE and length, so the match is as
hard as a normal one; only what comes changes:

- Rounds 1-10 are the default rounds, so the start is fair.
- From round 11 plain bloons turn to metal: a share of the red to pink groups becomes Lead or Zebra, black and white
  become Zebra or Purple, the more so the later the round. From round 45 some Lead comes camo, from round 60 fortified;
  from round 81 some Ceramics come fortified ("armoured gold").
- From round 40 the MOAB-class bloons come as cash convoys: their groups come in half the time.
- The rounds before the default boss rounds (39, 59, 79, 99, 119) are heists: a short, dense rush of metal and, later,
  MOAB-class bloons, worth what the default round is worth.
- Every round is then scaled back to within 10 % of the default round's RBE.

The boss rounds themselves (40, 60, 80, 100, 120) and every round after 140 are the game's own: the boss comes on top,
and freeplay goes on as usual.
"""
import json
import math
import os
import random
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "tools", "data")
OUT = os.path.join(ROOT, "Config", "RoundSets", "gildebloon.json")

KEEP_UNTIL = 10
HEISTS = [39, 59, 79, 99, 119]
BOSS_ROUNDS = [40, 60, 80, 100, 120]
TOLERANCE = 0.10
TARGET = 0.06

PLAIN = ["Red", "Blue", "Green", "Yellow", "Pink"]
SPLIT = ["Black", "White"]
MOAB_CLASS = ["Moab", "Bfb", "Zomg", "Ddt", "Bad"]
MODIFIERS = ["Regrow", "Fortified", "Camo"]


def split_id(bloon):
    """'LeadRegrowFortifiedCamo' -> ('Lead', {'Regrow', 'Fortified', 'Camo'})"""
    for base in sorted(PLAIN + SPLIT + MOAB_CLASS + ["Lead", "Zebra", "Purple", "Rainbow", "Ceramic"], key=len, reverse=True):
        if bloon.startswith(base):
            rest = bloon[len(base):]
            found = {m for m in MODIFIERS if m in rest}
            return base, found
    return bloon, set()


def join_id(base, modifiers, rbe):
    """The game's id for a base bloon with modifiers, in the game's order, or the plainest one the game has"""
    for drop in ([], ["Camo"], ["Fortified"], ["Regrow"], ["Regrow", "Fortified", "Camo"]):
        kept = [m for m in MODIFIERS if m in modifiers and m not in drop]
        bloon = base + "".join(kept)
        if bloon in rbe:
            return bloon
    return base


def worth(round_, rbe):
    return sum(rbe[g["bloon"]] * g["count"] for g in round_["bloonGroups"])


def length(round_):
    return max((g["start"] + g["duration"] for g in round_["bloonGroups"]), default=0)


def metal_share(number):
    """How much of the plain bloons turns to metal: a quarter at round 11, three fifths from round 60"""
    return min(0.6, 0.25 + (number - 11) * 0.35 / 49)


def regroup(group, base, modifiers, rbe):
    """The group as <base> bloons worth about the same"""
    bloon = join_id(base, modifiers, rbe)
    count = max(1, round(rbe[group["bloon"]] * group["count"] / rbe[bloon]))
    return dict(group, bloon=bloon, count=count)


def gold_rush(number, default, rbe):
    rng = random.Random(number * 7919)
    groups = []
    share = metal_share(number)
    for group in default["bloonGroups"]:
        base, modifiers = split_id(group["bloon"])
        new = dict(group)
        if base in PLAIN and rng.random() < share:
            new = regroup(group, "Lead" if rng.random() < 0.55 else "Zebra", modifiers, rbe)
        elif base in SPLIT and rng.random() < share:
            new = regroup(group, "Zebra" if rng.random() < 0.6 else "Purple", modifiers, rbe)
        elif base == "Lead":
            extra = set(modifiers)
            if number >= 45 and rng.random() < 0.35:
                extra.add("Camo")
            if number >= 60 and rng.random() < 0.5:
                extra.add("Fortified")
            new = regroup(group, "Lead", extra, rbe)
        elif base == "Ceramic" and number >= 81 and rng.random() < 0.4:
            new = regroup(group, "Ceramic", set(modifiers) | {"Fortified"}, rbe)

        # Cash convoys: MOAB-class bloons close together
        if number >= 40 and split_id(new["bloon"])[0] in MOAB_CLASS and new["count"] > 1:
            new["duration"] = round(new["duration"] * 0.5, 3)
        groups.append(new)
    return {"roundNumber": number, "addToRound": False, "bloonGroups": groups}


def heist(number, default, rbe):
    """A short, dense rush worth what the default round is worth: metal first, MOAB-class later"""
    total = worth(default, rbe)
    seconds = max(10.0, min(24.0, length(default) * 0.7))
    if number < 60:
        mix = [("Lead", 0.4, 0.0), ("Zebra", 0.3, 0.15), ("PurpleRegrow", 0.3, 0.3)]
    elif number < 80:
        mix = [("LeadFortified", 0.1, 0.0), ("CeramicFortified", 0.3, 0.15), ("MoabFortified", 0.6, 0.35)]
    elif number < 100:
        mix = [("CeramicFortified", 0.2, 0.0), ("Ddt", 0.3, 0.2), ("BfbFortified", 0.5, 0.4)]
    else:
        mix = [("DdtFortified", 0.3, 0.0), ("ZomgFortified", 0.5, 0.25), ("BfbFortified", 0.2, 0.5)]
    groups = []
    for bloon, part, start in mix:
        count = max(1, round(total * part / rbe[bloon]))
        groups.append({"bloon": bloon, "count": count, "start": round(seconds * start, 3),
                       "duration": round(seconds * (0.6 if count > 1 else 0.0), 3)})
    return {"roundNumber": number, "addToRound": False, "bloonGroups": groups}


def fit(round_, target, rbe):
    """Scales the round's groups until its RBE is within TARGET of the default round's"""
    for _ in range(40):
        now = worth(round_, rbe)
        if target <= 0 or abs(now - target) <= target * TARGET:
            return round_
        groups = round_["bloonGroups"]
        factor = target / now
        for group in groups:
            group["count"] = max(1, round(group["count"] * factor))
        if abs(worth(round_, rbe) - now) < 1:
            break
    return round_


def make(default_rounds, rbe):
    rounds = []
    for default in default_rounds:
        number = default["roundNumber"]
        if number <= KEEP_UNTIL or number in BOSS_ROUNDS or number > 140:
            made = json.loads(json.dumps(default))
        elif number in HEISTS:
            made = heist(number, default, rbe)
        else:
            made = gold_rush(number, default, rbe)
        rounds.append(fit(made, worth(default, rbe), rbe))
    return {"scale": 1, "rounds": rounds}


def write(data):
    lines = ["{", '  "scale": 1,', '  "rounds": [']
    for i, round_ in enumerate(data["rounds"]):
        lines.append("    " + json.dumps(round_, separators=(",", ":")) + ("," if i < len(data["rounds"]) - 1 else ""))
    lines += ["  ]", "}", ""]
    return "\n".join(lines)


def main():
    with open(os.path.join(DATA, "defaultroundset.json"), encoding="utf-8") as f:
        default_rounds = json.load(f)["rounds"][:140]
    with open(os.path.join(DATA, "bloonrbe.json"), encoding="utf-8") as f:
        rbe = json.load(f)
    text = write(make(default_rounds, rbe))
    if "--check" in sys.argv:
        with open(OUT, encoding="utf-8") as f:
            if f.read().replace("\r\n", "\n") != text:
                sys.exit(f"{OUT} isn't what tools/make-gildebloon-rounds.py makes; run it again")
        print("gildebloon.json is up to date")
        return
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
