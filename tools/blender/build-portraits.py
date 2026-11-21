"""
Renders Gildebloon's portraits into Images/ without an open Blender (#25):

    blender --background --factory-startup --python tools/blender/build-portraits.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
scope = {}
for name in ("gildebloon-boss.py", "render-portraits.py"):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        scope = {"REPO": REPO, "__name__": "__portraits__"}
        exec(compile(f.read(), name, "exec"), scope)
print("Gildebloon portraits:", scope.get("result"))
