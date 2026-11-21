"""
Renders Gildebloon's portraits from its Blender body (#25): Images/Gildebloon.png and Images/GildebloonElite.png,
256 x 256 on a see-through background, for the boss panel's icon by the skulls, the Boss Creator and the tier banner.
Run after the boss script:

    python tools/blender/run-in-blender.py tools/blender/gildebloon-boss.py
    python tools/blender/run-in-blender.py tools/blender/render-portraits.py

The body is shown a little from above and from its left, face on, like the game's boss portraits: Gildebloon in its
Idle, Elite at the end of EliteSpawn (its crown up, a mean squint). The stage (grass, shadow, pile, effect coins) is
left out of the picture and everything is put back afterwards.
"""
import math, os
import bpy
from mathutils import Vector

scene = bpy.data.scenes["Gildebloon Boss"]
PORTRAITS = [("Gildebloon", 100), ("GildebloonElite", 876)]
SIZE = 256

window_scene = bpy.context.window.scene if bpy.context.window else None
if bpy.context.window:
    bpy.context.window.scene = scene

# what the picture leaves out, and how to put it back
hidden = [o for o in scene.objects if o.name in ("Ground", "Shadow", "Reach", "Sun") or o.name.startswith(("FxCoin",))]
was_hidden = {o.name: o.hide_render for o in hidden}
pile = scene.objects.get("SpawnPile")
pile_at = pile.location.copy() if pile else None
r = scene.render
saved = (r.resolution_x, r.resolution_y, r.film_transparent, r.filepath, scene.camera, scene.frame_current,
         scene.eevee.taa_render_samples, r.use_stamp)

cam = bpy.data.objects.new("PortraitCamera", bpy.data.cameras.new("Gilde_PortraitCam"))
scene.collection.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = 3.7
el, az, dist = math.radians(28), math.radians(-105), 20  # a little from above and from its left, face on
target = Vector((0, 0, 1.4))
cam.location = target + Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el))) * dist
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
# a light of its own, from the camera's upper left, so the toon bands fall the same way as on the map
light = bpy.data.objects.new("PortraitSun", bpy.data.lights.new("Gilde_PortraitSun", "SUN"))
scene.collection.objects.link(light)
light.data.energy = 3.2
light.rotation_euler = (cam.location + Vector((-6, -2, 8)) - target).to_track_quat("Z", "Y").to_euler()

written = []
try:
    for o in hidden:
        o.hide_render = True
    if pile:
        pile.location = (0, 0, -100)
    scene.camera = cam
    r.resolution_x = r.resolution_y = SIZE
    r.film_transparent = True
    r.use_stamp = False
    scene.eevee.taa_render_samples = 64
    folder = os.path.join(REPO, "Images")
    for name, frame in PORTRAITS:
        scene.frame_set(frame)
        r.filepath = os.path.join(folder, name + ".png")
        bpy.ops.render.render(write_still=True, scene=scene.name)
        written.append(r.filepath)
finally:
    for o in hidden:
        o.hide_render = was_hidden[o.name]
    if pile:
        pile.location = pile_at
    (r.resolution_x, r.resolution_y, r.film_transparent, r.filepath, scene.camera, frame, scene.eevee.taa_render_samples,
     r.use_stamp) = saved
    scene.frame_set(frame)
    for o in (cam, light):
        data, store = o.data, bpy.data.cameras if o.type == "CAMERA" else bpy.data.lights
        bpy.data.objects.remove(o, do_unlink=True)
        if data.users == 0:
            store.remove(data)
    if window_scene is not None:
        bpy.context.window.scene = window_scene

result = {"portraits": written}
