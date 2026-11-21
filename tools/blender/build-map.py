"""
Builds Gildebloon's gold round the map and writes Bundles/gildebloon-map.gba.gz without an open Blender (#25):

    blender --background --factory-startup --python tools/blender/build-map.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
scope = {}
for name in ("gildebloon-boss.py", "gildebloon-map.py", "export-map.py"):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        scope = {"REPO": REPO, "__name__": "__map__"}
        exec(compile(f.read(), name, "exec"), scope)
print("Gildebloon map:", {k: v for k, v in scope.get("result", {}).items() if k != "clips"})
