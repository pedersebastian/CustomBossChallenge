"""
Builds Gildebloon's body and animations and writes Bundles/gildebloon-body.gba.gz without an open Blender (#25):

    blender --background --factory-startup --python tools/blender/build-boss.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
for name in ("gildebloon-boss.py", "export-boss.py"):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        scope = {"REPO": REPO, "__name__": "__boss__"}
        exec(compile(f.read(), name, "exec"), scope)
print("Gildebloon body:", {k: v for k, v in scope.get("result", {}).items() if k != "clips"})
