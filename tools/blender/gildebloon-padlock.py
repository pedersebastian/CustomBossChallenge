"""
The padlock Gildebloon hangs over every tower it seizes at a skull (#25), in the game's cartoon style like its body:
a gold padlock with an iron shackle, a green $ on it (a Banana Farm's has a banana), and a ring of 16 gold ticks round
it that the mod takes away one by one as the tower's time runs out (Visuals/GildebloonSeizeLook.cs). Before
(2026-11-22) it was a flat box seen from above that read as a crate.

It stands up, its face towards -y; the mod turns it to face the game's camera. Three animations:
  Lock   (1-16)   drops in and the shackle snaps shut
  Hold   (20-67)  sways a little (loops)
  Unlock (70-88)  the shackle pops up and swings open, the lock jumps and is gone (the mod bursts it into coins)

Builds the scene "Gildebloon Padlock" with the boss's materials, so run the boss script first:

    python tools/blender/run-in-blender.py tools/blender/gildebloon-boss.py
    python tools/blender/run-in-blender.py tools/blender/gildebloon-padlock.py

tools/blender/build-padlock.py does both and writes Bundles/gildebloon-padlock.gba.gz (export-padlock.py).
"""
import bpy, bmesh, math
from mathutils import Matrix, Vector

SCENE = "Gildebloon Padlock"
TICKS = 16

GOLD = bpy.data.materials["Gilde_BossGold"]
GOLD_FACE = bpy.data.materials["Gilde_GoldFace"]
IRON = bpy.data.materials["Gilde_Iron"]
GREEN = bpy.data.materials["Gilde_Dollar"]
SLOT = bpy.data.materials["Gilde_Slot"]
LINE = bpy.data.materials["Gilde_Outline"]


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*c, 1.0)


def toon(name, shade, base, light, cuts=(0.42, 0.93)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    cr.interpolation = "CONSTANT"
    cr.elements[0].position = 0.0
    cr.elements[0].color = lin(shade)
    cr.elements[1].position = cuts[0]
    cr.elements[1].color = lin(base)
    if light:
        cr.elements.new(cuts[1]).color = lin(light)
    nt.links.new(dif.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    m.diffuse_color = lin(base)
    return m


BANANA = toon("Gilde_Banana", "#c98f00", "#ffdd3a", "#fff4a0")
STEM = toon("Gilde_BananaStem", "#4a2c10", "#7a4d1e", None)

scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
for o in list(scene.collection.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
if bpy.context.window is not None:
    bpy.context.window.scene = scene
scene.frame_start, scene.frame_end = 1, 88
scene.render.fps = 24
col = scene.collection


def outline(o, width):
    mats = o.data.materials
    names = [m.name for m in mats if m]
    if LINE.name in names:
        n = names.index(LINE.name)
    else:
        n = len(mats)
        for _ in range(n):
            mats.append(LINE)
    mod = o.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = width
    mod.offset = 1
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = n
    mod.material_offset_rim = n


def finish(bm, name, mats, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = smooth
    return me


def obj(name, me, parent=None, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0, line=None):
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    col.objects.link(o)
    if parent is not None:
        o.parent = parent
    if line:
        outline(o, line)
    return o


def empty(name, parent=None, loc=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.3
    o.location = loc
    col.objects.link(o)
    if parent is not None:
        o.parent = parent
    return o


def rounded_box(name, size, radius, mat):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=radius, segments=4, affect="EDGES", profile=0.5)
    return finish(bm, name, [mat])


def tube(name, points, radii, mat, segr=10):
    """A tube along points, as thick as radii there (caps closed)"""
    bm = bmesh.new()
    rings = []
    for i, (p, r) in enumerate(zip(points, radii)):
        a = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        a.normalize()
        side = a.cross(Vector((0, 1, 0)))
        if side.length < 1e-4:
            side = a.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(a)
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * j / segr) + up * math.sin(2 * math.pi * j / segr)) * r)
                      for j in range(segr)])
    for i in range(len(rings) - 1):
        for j in range(segr):
            bm.faces.new((rings[i][j], rings[i][(j + 1) % segr], rings[i + 1][(j + 1) % segr], rings[i + 1][j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(bm, name, [mat])


def text_mesh(body, size, depth, mat):
    cu = bpy.data.curves.new("tmp", "FONT")
    cu.body = body
    cu.size = size
    cu.extrude = depth
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    tmp = bpy.data.objects.new("tmp", cu)
    col.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    me.materials.append(mat)
    return me


# ---- the padlock: its root (what the animations drop and sway), the body, the shackle, the face
root = empty("PadlockRoot")
body = obj("LockBody", rounded_box("Gilde_LockBody", (1.0, 0.42, 0.8), 0.12, GOLD), root, line=0.035)
# a lighter plate on its face, the keyhole in it
obj("LockPlate", rounded_box("Gilde_LockPlate", (0.78, 0.05, 0.6), 0.08, GOLD_FACE), body, loc=(0, -0.205, -0.02), line=0.015)
hole = bmesh.new()
bmesh.ops.create_circle(hole, cap_ends=True, segments=20, radius=0.07)
hole.faces.new([hole.verts.new(v) for v in ((-0.035, -0.04, 0), (-0.05, -0.17, 0), (0.05, -0.17, 0), (0.035, -0.04, 0))])
for f in hole.faces:  # drawn in the xy plane facing +z, then stood up to face -y
    if f.normal.z < 0:
        f.normal_flip()
bmesh.ops.rotate(hole, verts=hole.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "X"))
obj("LockKeyhole", finish(hole, "Gilde_LockKeyhole", [SLOT], smooth=False), body, loc=(0, -0.235, -0.1))
# rivets in its corners
rivet = bmesh.new()
bmesh.ops.create_uvsphere(rivet, u_segments=10, v_segments=6, radius=0.035)
rivet_me = finish(rivet, "Gilde_LockRivet", [GOLD_FACE])
for i, (x, z) in enumerate(((-0.4, 0.3), (0.4, 0.3), (-0.4, -0.32), (0.4, -0.32))):
    obj(f"LockRivet{i}", rivet_me, body, loc=(x, -0.22, z), line=0.008)
# what it holds: a $ for a tower, a banana for a Banana Farm (the mod shows one)
obj("LockDollar", text_mesh("$", 0.42, 0.04, GREEN), body, loc=(0, -0.24, 0.13), rot=(math.pi / 2, 0, 0), line=0.014)
arc = [Vector((0.2 * math.cos(a), 0, 0.2 * math.sin(a) + 0.24)) for a in
       [math.radians(d) for d in range(200, 341, 10)]]
banana = obj("LockBanana", tube("Gilde_LockBanana", arc, [0.02, 0.045, 0.06, 0.068, 0.072, 0.074, 0.075, 0.075, 0.075, 0.074,
                                                          0.072, 0.068, 0.06, 0.045, 0.02], BANANA), body,
             loc=(0, -0.26, 0.03), line=0.014)
obj("LockBananaStem", tube("Gilde_LockBananaStem", [Vector((0.19, 0, 0.17)), Vector((0.25, 0, 0.21))], [0.025, 0.02], STEM),
    banana, line=0.01)

# the shackle: an iron arch over the top, turning on its left leg when it swings open
pivot = empty("ShackleHinge", root, loc=(-0.3, 0, 0.32))
legs = [Vector((0.6, 0, 0)), Vector((0.6, 0, 0.18))]
arch = [Vector((0.3 + 0.3 * math.cos(a), 0, 0.18 + 0.3 * math.sin(a))) for a in
        [math.radians(d) for d in range(0, 181, 12)]]
left = [Vector((0, 0, 0.18)), Vector((0, 0, 0))]
path = legs + arch[1:-1] + left
obj("Shackle", tube("Gilde_Shackle", path, [0.075] * len(path), IRON, segr=12), pivot, line=0.025)

# ---- the time it has left: a dark ring round it with 16 gold ticks on it, the first at the top going clockwise
ring = obj("TickRing", tube("Gilde_TickRing", [Vector((0.95 * math.cos(a), 0, 0.95 * math.sin(a))) for a in
                                               [2 * math.pi * k / 48 for k in range(49)]], [0.075] * 49, IRON, segr=8),
           root, loc=(0, 0.05, 0), line=0.015)
tick = rounded_box("Gilde_Tick", (0.24, 0.1, 0.13), 0.035, GOLD_FACE)
for k in range(TICKS):
    a = math.pi / 2 - 2 * math.pi * (k + 0.5) / TICKS  # clockwise from the top, seen from the front (-y)
    t = obj(f"Tick{k:02}", tick, root, loc=(0.95 * math.cos(a), -0.02, 0.95 * math.sin(a)), line=0.01)
    t.rotation_euler = (0, -(a + math.pi / 2), 0)


# ---- animations
def key(o, f, loc=None, rot=None, scale=None):
    if loc is not None:
        o.location = loc
        o.keyframe_insert("location", frame=f)
    if rot is not None:
        o.rotation_euler = rot
        o.keyframe_insert("rotation_euler", frame=f)
    if scale is not None:
        o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
        o.keyframe_insert("scale", frame=f)


HINGE = (-0.3, 0, 0.32)
UP = (-0.3, 0, 0.5)

# Lock: drops in from above, squashes on landing, the shackle snaps down
key(root, 1, loc=(0, 0, 1.2), rot=(0, 0, 0), scale=0.3)
key(root, 6, loc=(0, 0, -0.08), scale=(1.15, 1.15, 0.85))
key(root, 9, loc=(0, 0, 0.05), scale=(0.95, 0.95, 1.06))
key(root, 12, loc=(0, 0, 0), scale=1)
key(root, 16, loc=(0, 0, 0), rot=(0, 0, 0), scale=1)
key(pivot, 1, loc=UP, rot=(0, 0, 0))
key(pivot, 8, loc=UP)
key(pivot, 10, loc=(-0.3, 0, 0.29))
key(pivot, 12, loc=HINGE)
key(pivot, 16, loc=HINGE, rot=(0, 0, 0))

# Hold: a gentle sway and bob (loops: same pose at both ends)
for f, sway, bob in ((20, 0, 0), (32, 4, 0.03), (44, 0, 0), (56, -4, 0.03), (67, 0, 0)):
    key(root, f, loc=(0, 0, bob), rot=(0, math.radians(sway), 0), scale=1)
key(pivot, 20, loc=HINGE, rot=(0, 0, 0))
key(pivot, 67, loc=HINGE, rot=(0, 0, 0))

# Unlock: the shackle pops up and swings open round its left leg, the lock jumps and shrinks away
key(root, 70, loc=(0, 0, 0), rot=(0, 0, 0), scale=1)
key(pivot, 70, loc=HINGE, rot=(0, 0, 0))
key(pivot, 74, loc=UP, rot=(0, 0, 0))
key(pivot, 80, loc=UP, rot=(0, 0, math.radians(-110)))
key(root, 76, loc=(0, 0, 0.15), scale=1.1)
key(root, 82, loc=(0, 0, 0.35), rot=(0, math.radians(-12), 0), scale=0.8)
key(root, 88, loc=(0, 0, 0.5), rot=(0, math.radians(-20), 0), scale=0)
key(pivot, 88, loc=UP, rot=(0, 0, math.radians(-110)))

ANIMS = [("Lock", 1, 16), ("Hold", 20, 67), ("Unlock", 70, 88)]
scene["gildebloon_padlock_anims"] = repr(ANIMS)

# a camera like the game's, looking at its face a little from above, for a look in Blender
cam = bpy.data.objects.get("PadlockCamera") or bpy.data.objects.new("PadlockCamera", bpy.data.cameras.new("Gilde_PadlockCam"))
if cam.name not in col.objects:
    col.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = 3.0
cam.location = (0, -10, 1.5)
cam.rotation_euler = (math.radians(82), 0, 0)
scene.camera = cam
scene.frame_set(16)

result = {"padlock": [o.name for o in col.objects if o.type == "MESH"]}
