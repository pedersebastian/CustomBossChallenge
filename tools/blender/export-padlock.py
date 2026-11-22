"""
Writes the padlock of a seized tower, as built by gildebloon-padlock.py, to Bundles/gildebloon-padlock.gba.gz for the
mod (#25; Visuals/GildebloonSeizeLook.cs). Same format as the body (export-boss.py, which this runs): its parts are the
lock, its shackle, its face ($ and banana), the ring and its 16 ticks, and three animations, Lock, Hold (loops) and
Unlock.

    python tools/blender/run-in-blender.py tools/blender/gildebloon-boss.py
    python tools/blender/run-in-blender.py tools/blender/gildebloon-padlock.py
    python tools/blender/run-in-blender.py tools/blender/export-padlock.py
"""
import ast, os
import bpy

EXPORT = {
    "scene": "Gildebloon Padlock",
    "out": os.path.join(REPO, "Bundles", "gildebloon-padlock.gba.gz"),
    "objects": lambda: [o for o in bpy.data.scenes["Gildebloon Padlock"].objects if o.type in ("MESH", "EMPTY")],
    "anims": ast.literal_eval(bpy.data.scenes["Gildebloon Padlock"]["gildebloon_padlock_anims"]),
    "loops": {"Hold"},
    "rest_frame": 16,
}
with open(os.path.join(REPO, "tools", "blender", "export-boss.py"), encoding="utf-8") as f:
    exec(compile(f.read(), "export-boss.py", "exec"))
