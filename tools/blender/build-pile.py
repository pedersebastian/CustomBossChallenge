"""
Builds Gildebloon's pile of money and writes Bundles/gildebloon-pile.mesh.gz without an open Blender (#25):

    blender --background --factory-startup --python tools/blender/build-pile.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
for name in ("gildebloon-pile.py", "export-pile.py"):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        scope = {"REPO": REPO, "__name__": "__pile__"}
        exec(compile(f.read(), name, "exec"), scope)
print("Gildebloon pile:", scope.get("result"))
