"""
Builds the padlock of a seized tower and writes Bundles/gildebloon-padlock.gba.gz without an open Blender (#25):

    blender --background --factory-startup --python tools/blender/build-padlock.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
scope = {}
for name in ("gildebloon-boss.py", "gildebloon-padlock.py", "export-padlock.py"):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        scope = {"REPO": REPO, "__name__": "__padlock__"}
        exec(compile(f.read(), name, "exec"), scope)
print("Gildebloon padlock:", {k: v for k, v in scope.get("result", {}).items() if k != "clips"})
