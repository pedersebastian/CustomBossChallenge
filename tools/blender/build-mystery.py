"""
Builds the mystery map effects and writes Bundles/mystery-fx.mesh.gz without an open Blender (#285):

    blender --background --factory-startup --python tools/blender/build-mystery.py

Also saves tools/blender/mystery-fx.blend, the scene to look at or change by hand (mystery-fx.py rebuilds it from
scratch, so a change by hand belongs in the script).
"""
import os
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
scope = {}
for name in ("mystery-fx.py", "export-mystery.py"):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        scope = {"REPO": REPO, "__name__": "__mystery__"}
        exec(compile(f.read(), name, "exec"), scope)
    print(name, scope.get("result"))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "mystery-fx.blend"), copy=True, compress=True)
