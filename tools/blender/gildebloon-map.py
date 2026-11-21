"""
A proposal for Gildebloon's effect round the map (#25). Every game boss has one: Bloonarius's slime creeps in from the
map's edges (BloonariusAmbientMapFx), Lych has a mist. The mod hides Bloonarius's and so far puts nothing there.
Gildebloon's: molten gold oozing in from the edges, with drips, coins and gold bars stuck in it and sparkles on it,
in the same cartoon style as its body and pile (toon bands, dark outlines).

Builds a scene "Gildebloon Map" in the open Blender: a stand-in map with a track, the ambient effect, the pile of
money where the track starts and Gildebloon on the track. Run tools/blender/gildebloon-boss.py first; this one shows
its body and pile as collection instances, so its animations play here too:

    python tools/blender/run-in-blender.py tools/blender/gildebloon-boss.py
    python tools/blender/run-in-blender.py tools/blender/gildebloon-map.py

Sizes: the game's map is about 300 x 230 units and Gildebloon about 45 wide; here 1 unit is about 17 of the game's.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Euler

SCENE = "Gildebloon Map"
W, H = 18.0, 13.6  # the map
rng = random.Random(2525)

body = bpy.data.collections.get("GildebloonBody")
pile_col = bpy.data.collections.get("GildebloonPile")
assert body and pile_col, "Run tools/blender/gildebloon-boss.py first"


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*c, 1.0)


scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
for o in list(scene.collection.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.curves):
    for d in list(coll):
        if d.users == 0:
            coll.remove(d)
if bpy.context.window is not None:
    bpy.context.window.scene = scene
col = scene.collection

# the body's materials, made by the boss script
GOLD = bpy.data.materials["Gilde_BossGold"]
GOLD_DARK = bpy.data.materials["Gilde_GoldInner"]
GOLD_FACE = bpy.data.materials["Gilde_GoldFace"]
GRASS = bpy.data.materials["Gilde_Grass"]
LINE = bpy.data.materials["Gilde_Outline"]
SHINE = bpy.data.materials["Gilde_Shine"]
COIN = bpy.data.objects["RingCoin0"].data  # the body's coin (the pile has one by the same name)


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


MOLTEN = toon("Gilde_Molten", "#d47a00", "#ffbf1f", "#fff2a6", cuts=(0.35, 0.9))
DIRT = toon("Gilde_Track", "#b98a4c", "#e2bd7f", None, cuts=(0.3, 1))
GRASS_DARK = toon("Gilde_GrassDark", "#4c8a26", "#6fb336", None, cuts=(0.3, 1))


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


def mesh_obj(name, bm, mats, smooth=True, line=None, loc=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    o.location = loc
    col.objects.link(o)
    if line:
        outline(o, line)
    return o


def place(name, me, loc, rot=(0, 0, 0), scale=1.0, line=None):
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    col.objects.link(o)
    if line:
        outline(o, line)
    return o


# ---- the map: grass with a few darker patches, and a winding dirt track
bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
bmesh.ops.scale(bm, vec=(W, H, 1), verts=bm.verts)
mesh_obj("MapGrass", bm, [GRASS], smooth=False)
for i in range(9):
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=24, radius=1)
    bmesh.ops.scale(bm, vec=(rng.uniform(0.6, 1.4), rng.uniform(0.4, 0.9), 1), verts=bm.verts)
    mesh_obj(f"GrassPatch{i}", bm, [GRASS_DARK], smooth=False,
             loc=(rng.uniform(-W / 2 + 2, W / 2 - 2), rng.uniform(-H / 2 + 2, H / 2 - 2), 0.004))

track_pts = [(-W / 2 - 0.5, 2.6), (-5.5, 2.8), (-3.2, 0.6), (-4.6, -2.2), (-1.4, -3.6), (1.6, -1.6),
             (1.2, 1.6), (3.8, 3.4), (6.2, 1.2), (5.0, -2.6), (7.4, -4.2), (W / 2 + 0.5, -3.6)]
cu = bpy.data.curves.new("Gilde_TrackCurve", "CURVE")
cu.dimensions = "2D"
cu.fill_mode = "BOTH"
sp = cu.splines.new("BEZIER")
sp.bezier_points.add(len(track_pts) - 1)
for bp, (x, y) in zip(sp.bezier_points, track_pts):
    bp.co = (x, y, 0)
    bp.handle_left_type = bp.handle_right_type = "AUTO"
# sample the Bezier through a temporary mesh, then build the track as a flat ribbon along it
cu.fill_mode = "NONE"
cu.dimensions = "3D"
cu.resolution_u = 32
tmp = bpy.data.objects.new("tmp", cu)
col.objects.link(tmp)
dg = scene.view_layers[0].depsgraph  # this scene's, also in a background build
me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
bpy.data.objects.remove(tmp)
bpy.data.curves.remove(cu)
line_pts = [v.co.copy() for v in me.vertices]
bpy.data.meshes.remove(me)
bm = bmesh.new()
half = 0.62
left, right = [], []
for i, p in enumerate(line_pts):
    a = line_pts[max(0, i - 1)]
    b = line_pts[min(len(line_pts) - 1, i + 1)]
    t = (b - a).normalized()
    n = Vector((-t.y, t.x, 0))
    left.append(bm.verts.new(p + n * half + Vector((0, 0, 0.01))))
    right.append(bm.verts.new(p - n * half + Vector((0, 0, 0.01))))
for i in range(len(line_pts) - 1):
    bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
track = mesh_obj("Track", bm, [DIRT], smooth=False)


def track_at(u):
    """A point on the track and its direction, u from 0 to 1"""
    i = min(len(line_pts) - 2, int(u * (len(line_pts) - 1)))
    return line_pts[i], (line_pts[i + 1] - line_pts[i]).normalized()


# ---- the ambient effect: molten gold oozing in from the edges
def edge_depth(side, t):
    """How far the gold reaches in at a point along a side (t 0..1), with drips"""
    d = 0.4 + 0.05 * math.sin(t * 9 + side * 2.1)  # a thin, even band: it frames the map, it doesn't cover it
    for c, w, h in DRIPS[side]:
        d += h * math.exp(-((t - c) / w) ** 2)
    return d


DRIPS = {}
for side in range(4):
    n = 3 if side % 2 == 0 else 2
    DRIPS[side] = [((k + 0.5) / n + rng.uniform(-0.1, 0.1), rng.uniform(0.016, 0.022), rng.uniform(0.35, 0.75)) for k in range(n)]

sides = [  # start corner, direction along the side, inward normal, length
    (Vector((-W / 2, H / 2, 0)), Vector((1, 0, 0)), Vector((0, -1, 0)), W),   # top
    (Vector((W / 2, H / 2, 0)), Vector((0, -1, 0)), Vector((-1, 0, 0)), H),  # right
    (Vector((W / 2, -H / 2, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), W),  # bottom
    (Vector((-W / 2, -H / 2, 0)), Vector((0, 1, 0)), Vector((1, 0, 0)), H),  # left
]
ooze = []
for side, (start, along, inward, length) in enumerate(sides):
    bm = bmesh.new()
    steps = int(length * 14)
    rows = 5
    grid = []
    for i in range(steps + 1):
        t = i / steps
        depth = edge_depth(side, t)
        # round off the corners so neighbouring sides meet in a curve
        corner = min(t, 1 - t) * length
        if corner < 0.5:  # round the corners off a little
            depth = max(depth, 0.5 - corner + 0.2)
        row = []
        for j in range(rows + 1):
            f = j / rows
            # a rounded bead of gold: thick at the edge, thin where it runs in
            z = 0.12 * math.sqrt(max(0.0, 1 - f ** 2)) + 0.01
            row.append(bm.verts.new(start + along * (t * length) + inward * (depth * f) + Vector((0, 0, z))))
        grid.append(row)
    for i in range(steps):
        for j in range(rows):
            bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bm.faces[0].normal.z < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    o = mesh_obj(f"GoldOoze{side}", bm, [MOLTEN], line=0.03)
    ooze.append(o)
    # a round bead at the end of each drip
    for c, w, h in DRIPS[side]:
        tip = start + along * (c * length) + inward * (edge_depth(side, c) - 0.05)
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=1)
        bmesh.ops.scale(bm, vec=(0.12, 0.12, 0.05), verts=bm.verts)
        drop = mesh_obj(f"Drop{side}_{c:.2f}", bm, [MOLTEN], line=0.02, loc=tip + Vector((0, 0, 0.02)))
        drop["drip"] = True

# treasure stuck in the gold: coins, a few gold bars, sparkles
bar_bm = bmesh.new()
bmesh.ops.create_cube(bar_bm, size=1)
for v in bar_bm.verts:
    v.co.x *= 0.46 if v.co.z < 0 else 0.36
    v.co.y *= 0.22 if v.co.z < 0 else 0.16
    v.co.z *= 0.14
bar_me = bpy.data.meshes.new("Gilde_MapBar")
bar_bm.to_mesh(bar_me)
bar_bm.free()
bar_me.materials.append(GOLD_FACE)
spark_bm = bmesh.new()
pts4 = []
for k in range(8):
    r = 0.2 if k % 2 == 0 else 0.05
    a = k * math.pi / 4
    pts4.append(spark_bm.verts.new((r * math.cos(a), r * math.sin(a), 0)))
spark_bm.faces.new(pts4)
spark_me = bpy.data.meshes.new("Gilde_MapSpark")
spark_bm.to_mesh(spark_me)
spark_bm.free()
spark_me.materials.append(SHINE)

coins, sparks = [], []
for side, (start, along, inward, length) in enumerate(sides):
    for k in range(int(length * 0.35)):
        t = rng.uniform(0.03, 0.97)
        d = edge_depth(side, t) * rng.uniform(0.15, 0.7)
        p = start + along * (t * length) + inward * d + Vector((0, 0, 0.1 * math.sqrt(max(0.0, 1 - (d / edge_depth(side, t)) ** 2)) + 0.03))
        coins.append(place(f"MapCoin{side}_{k}", COIN, p,
                           (rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), rng.uniform(0, 6.3)), 0.75, line=0.01))
    for k in range(0):  # no gold bars: simpler
        t = rng.uniform(0.15, 0.85)
        p = start + along * (t * length) + inward * 0.3 + Vector((0, 0, 0.16))
        place(f"MapBar{side}_{k}", bar_me, p, (rng.uniform(-0.3, 0.3), rng.uniform(-0.2, 0.2),
                                                math.atan2(along.y, along.x) + rng.uniform(-0.6, 0.6)), 1.0, line=0.015)
    for k in range(int(length * 0.3)):
        t = rng.uniform(0.02, 0.98)
        p = start + along * (t * length) + inward * rng.uniform(0.1, edge_depth(side, t)) + Vector((0, 0, 0.3))
        s = place(f"MapSpark{side}_{k}", spark_me, p, scale=rng.uniform(0.7, 1.3))
        s.visible_shadow = False
        sparks.append(s)

# ---- Gildebloon and its pile, as instances of the boss scene's collections
def instance(name, coll, loc, rot_z=0.0, scale=1.0):
    e = bpy.data.objects.new(name, None)
    e.instance_type = "COLLECTION"
    e.instance_collection = coll
    e.location = loc
    e.rotation_euler = (0, 0, rot_z)
    e.scale = (scale,) * 3
    col.objects.link(e)
    return e


start_p, start_d = track_at(0.06)
pile_root = next(o for o in pile_col.objects if o.parent is None)
pile_on_map = bpy.data.objects.new("PileOnMap", None)
pile_on_map.location = start_p + Vector((0.9, 0, 0.01))
pile_on_map.scale = (1.0,) * 3  # the boss scene sizes the pile next to Gildebloon already
col.objects.link(pile_on_map)
copies = {}
originals = [pile_root] + list(pile_root.children_recursive)  # before copying: a copy starts as the parent's child too
for o in originals:
    c = o.copy()
    c.animation_data_clear()
    c.hide_render = c.hide_viewport = False
    col.objects.link(c)
    copies[o.name] = c
for o in originals:
    c = copies[o.name]
    c.parent = copies[o.parent.name] if o.parent else pile_on_map
boss_u = min((u / 100 for u in range(30, 75)), key=lambda u: track_at(u)[1].y)
boss_p, boss_d = track_at(boss_u)
# the body's front is -Y; turn it to face along the track
instance("GildebloonOnMap", body, boss_p, math.atan2(boss_d.y, boss_d.x) + math.pi / 2)

# ---- light, camera (the game's: orthographic, 60 degrees down), world
sun = bpy.data.objects.new("MapSun", bpy.data.lights.new("Gilde_MapSun", "SUN"))
col.objects.link(sun)
sun.data.energy = 3.2
sun.data.angle = math.radians(3)
sun.rotation_euler = Euler((math.radians(42), 0, math.radians(-35)))
cam = bpy.data.objects.new("MapCamera", bpy.data.cameras.new("Gilde_MapCam"))
col.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = W * 0.99
el, az, dist = math.radians(60), math.radians(-90), 40
target = Vector((0, 0.25, 0))
cam.location = target + Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el))) * dist
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
scene.world = bpy.data.worlds.get("Gilde_World")
scene.view_settings.view_transform = "Standard"
scene.render.engine = "BLENDER_EEVEE"
scene.render.fps = 24
scene.render.resolution_x, scene.render.resolution_y = 1600, 1000

# ---- it lives: the ooze swells, the drips stretch, coins rock, sparkles twinkle (a 2 s loop on Idle's frames)
F0, F1 = 85, 132
scene.frame_start, scene.frame_end = F0, F1
for side, o in enumerate(ooze):
    for f in range(F0, F1 + 1, 8):
        k = (f - F0) / (F1 - F0) * 2 * math.pi
        o.scale = (1, 1, 1 + 0.12 * math.sin(k + side * 1.7))  # each side out of step, the same every build
        o.keyframe_insert("scale", frame=f)
for o in [x for x in col.objects if x.get("drip")]:
    base = o.scale.copy()
    ph = rng.uniform(0, 6.3)
    for f in range(F0, F1 + 1, 6):
        k = (f - F0) / (F1 - F0) * 2 * math.pi
        o.scale = (base.x * (1 + 0.15 * math.sin(k + ph)), base.y * (1 + 0.15 * math.sin(k + ph)), base.z)
        o.keyframe_insert("scale", frame=f)
for c in coins:
    r = c.rotation_euler.copy()
    ph = rng.uniform(0, 6.3)
    for f in range(F0, F1 + 1, 12):
        k = (f - F0) / (F1 - F0) * 2 * math.pi
        c.rotation_euler = (r.x + 0.12 * math.sin(k + ph), r.y, r.z)
        c.keyframe_insert("rotation_euler", frame=f)
for s in sparks:
    base = s.scale.x
    on = rng.randint(F0, F1 - 10)
    for f, k in ((F0, 0.0), (on, 0.0), (on + 4, 1.0), (on + 8, 0.0), (F1, 0.0)):
        s.scale = (base * k,) * 3
        s.keyframe_insert("scale", frame=f)
    s.rotation_euler = (0, 0, 0)
    s.keyframe_insert("rotation_euler", frame=on)
    s.rotation_euler = (0, 0, 0.8)
    s.keyframe_insert("rotation_euler", frame=on + 8)

for o in col.objects:
    o.visible_shadow = False
scene.frame_set(100)
result = {"scene": scene.name, "objects": len(scene.objects), "coins": len(coins), "sparkles": len(sparks)}
