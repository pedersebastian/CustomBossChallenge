"""
Renders a contact sheet of Gildebloon's animations (#25): a row per animation in tools/blender/gildebloon-boss.py's
ANIMS, three moments of each (early, middle, late), its name stamped on each picture. Run after the boss script:

    python tools/blender/run-in-blender.py tools/blender/render-sheet.py

It writes gildebloon-sheet.png in the system's temp folder and sends its path back. To render only some animations,
set ONLY before the file runs (e.g. through the MCP add-on: ONLY = ["Dispatch", "Escape"]; then this file's code).

The boss scene is made the active one first: its body is also shown as a collection instance in the map scene, and
shared objects animate in the active scene's evaluation.
"""
import ast, os, tempfile
import bpy
import numpy as np

scene = bpy.data.scenes["Gildebloon Boss"]
anims = ast.literal_eval(scene["gildebloon_anims"])
only = globals().get("ONLY")
if only:
    anims = [a for a in anims if a[0] in only]
FRACTIONS = (0.15, 0.5, 0.85)
W, H = 360, 270

window_scene = bpy.context.window.scene if bpy.context.window else None
if bpy.context.window:
    bpy.context.window.scene = scene
r = scene.render
saved = (r.resolution_x, r.resolution_y, r.use_stamp, r.filepath, scene.eevee.taa_render_samples, scene.frame_current)
r.resolution_x, r.resolution_y = W, H
r.use_stamp = True
for k in dir(r):
    if k.startswith("use_stamp_") and k not in ("use_stamp_note", "use_stamp_labels"):
        try:
            setattr(r, k, False)
        except (AttributeError, TypeError):
            pass
r.use_stamp_note = True
r.use_stamp_labels = False
r.stamp_font_size = 14
scene.eevee.taa_render_samples = 16

folder = os.path.join(tempfile.gettempdir(), "gildebloon-sheet")
os.makedirs(folder, exist_ok=True)
sheet = np.ones((len(anims) * H, len(FRACTIONS) * W, 4), dtype=np.float32)
try:
    for row, (name, a0, a1) in enumerate(anims):
        for column, fraction in enumerate(FRACTIONS):
            frame = round(a0 + (a1 - a0) * fraction)
            scene.frame_set(frame)
            r.stamp_note_text = f"{name}  f{frame}"
            path = os.path.join(folder, f"{frame:04d}.png")
            r.filepath = path
            bpy.ops.render.render(write_still=True, scene=scene.name)
            image = bpy.data.images.load(path)
            pixels = np.array(image.pixels[:], dtype=np.float32).reshape(H, W, 4)
            bpy.data.images.remove(image)
            y0 = (len(anims) - 1 - row) * H  # Blender's images start at the bottom
            sheet[y0:y0 + H, column * W:(column + 1) * W] = pixels
finally:
    r.resolution_x, r.resolution_y, r.use_stamp, r.filepath, scene.eevee.taa_render_samples, frame = saved
    scene.frame_set(frame)
    if window_scene is not None:
        bpy.context.window.scene = window_scene

out = bpy.data.images.new("gildebloon-sheet", sheet.shape[1], sheet.shape[0], alpha=True)
out.pixels = sheet.ravel()
out.filepath_raw = os.path.join(tempfile.gettempdir(), "gildebloon-sheet.png")
out.file_format = "PNG"
out.save()
bpy.data.images.remove(out)
result = {"sheet": os.path.join(tempfile.gettempdir(), "gildebloon-sheet.png"), "animations": len(anims)}
