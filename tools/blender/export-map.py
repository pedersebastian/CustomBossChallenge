"""
Writes the gold oozing in round the map's edges, as built by gildebloon-map.py, to Bundles/gildebloon-map.gba.gz for
the mod (#25): Gildebloon's effect round the map, where every game boss has one (Bloonarius's slime). Same format as
the body (export-boss.py, which this runs): its parts are the ooze, its drops, the coins, gold bars and sparkles in it,
and one looping animation, "Ooze" (it swells, the drops stretch, coins rock, sparkles twinkle), on the map scene's
frames 85-132. Only the effect: the stand-in map, its track, the pile and Gildebloon on it stay out.

    python tools/blender/run-in-blender.py tools/blender/gildebloon-boss.py
    python tools/blender/run-in-blender.py tools/blender/gildebloon-map.py
    python tools/blender/run-in-blender.py tools/blender/export-map.py

The map is W x H = 18 x 13.6 of its units, centred on its origin; the mod stretches it over the game's map.
"""
import os
import bpy

PREFIXES = ("GoldOoze", "Drop", "MapCoin", "MapBar", "MapSpark")
EXPORT = {
    "scene": "Gildebloon Map",
    "out": os.path.join(REPO, "Bundles", "gildebloon-map.gba.gz"),
    "objects": lambda: [o for o in bpy.data.scenes["Gildebloon Map"].objects if o.name.startswith(PREFIXES)],
    "anims": [("Ooze", 85, 132)],
    "loops": {"Ooze"},
    "rest_frame": 85,
}
with open(os.path.join(REPO, "tools", "blender", "export-boss.py"), encoding="utf-8") as f:
    exec(compile(f.read(), "export-boss.py", "exec"))
