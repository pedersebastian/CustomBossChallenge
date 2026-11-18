"""
Gildebloon's pile of money (#25) in a BTD6-like cartoon style: toon bands, dark outlines, chunky shapes. Builds it in
the open Blender, with a preview stage (grass, sun, camera):

    python tools/blender/run-in-blender.py tools/blender/gildebloon-pile.py
    python tools/blender/run-in-blender.py tools/blender/export-pile.py      writes Bundles/gildebloon-pile.mesh.gz

That needs Blender's MCP add-on (Blender Lab) running, which listens on localhost:9876. Without an open Blender:

    blender --background --factory-startup --python tools/blender/build-pile.py The colours and bands here are
the same as Visuals/GildebloonPileMesh.cs uses in the game.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

PILE = "GildebloonMoneyPile"
rng = random.Random(25)


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*c, 1.0)


# ---- clean up an earlier run and the default cube
for o in list(bpy.data.objects):
    if o.name == "Cube" or o.get("gilde"):
        bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
    for d in list(coll):
        if d.users == 0:
            coll.remove(d)

scene = bpy.context.scene
col = bpy.data.collections.get(PILE) or bpy.data.collections.new(PILE)
if col.name not in scene.collection.children:
    scene.collection.children.link(col)


def link(obj):
    obj["gilde"] = True
    col.objects.link(obj)
    return obj


# ---- materials
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


def flat(name, colour):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs[0].default_value = lin(colour)
    nt.links.new(em.outputs[0], out.inputs[0])
    m.diffuse_color = lin(colour)
    return m


GOLD = toon("Gilde_Gold", "#d98a00", "#ffc21a", "#fff1a0")
GOLD_DARK = toon("Gilde_GoldInner", "#b86e00", "#e9a10e", None)
SACK = toon("Gilde_Sack", "#9a6a3a", "#d4a96a", "#ecd2a0")
ROPE = toon("Gilde_Rope", "#5a3418", "#8a5a2e", None)
GREEN = toon("Gilde_Dollar", "#1f6b22", "#36a03a", None)
GRASS = toon("Gilde_Grass", "#5a9a2e", "#84c441", None, cuts=(0.42, 1))
GOLD_FACE = toon("Gilde_GoldFace", "#e59a00", "#ffd23d", "#fff6c0")
SPARK = flat("Gilde_Sparkle", "#fffbe0")
LINE = flat("Gilde_Outline", "#2a1a06")
LINE.use_backface_culling = True
if hasattr(LINE, "use_backface_culling_shadow"):
    LINE.use_backface_culling_shadow = True
# the shell must not shade the object it outlines
_nt = LINE.node_tree
_lp = _nt.nodes.new("ShaderNodeLightPath")
_tr = _nt.nodes.new("ShaderNodeBsdfTransparent")
_mix = _nt.nodes.new("ShaderNodeMixShader")
_em = [n for n in _nt.nodes if n.type == "EMISSION"][0]
_out = [n for n in _nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
_nt.links.new(_lp.outputs["Is Shadow Ray"], _mix.inputs[0])
_nt.links.new(_em.outputs[0], _mix.inputs[1])
_nt.links.new(_tr.outputs[0], _mix.inputs[2])
_nt.links.new(_mix.outputs[0], _out.inputs[0])
if hasattr(LINE, "use_transparent_shadow"):
    LINE.use_transparent_shadow = True


def outline(obj, width):
    mats = obj.data.materials
    names = [m.name for m in mats if m]
    if LINE.name in names:
        n = names.index(LINE.name)
    else:
        n = len(mats)
        for _ in range(n):
            mats.append(LINE)
    mod = obj.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = width
    mod.offset = 1
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = n
    mod.material_offset_rim = n


def smooth(mesh):
    for p in mesh.polygons:
        p.use_smooth = True


# ---- meshes
def text_mesh(body, size, depth):
    cu = bpy.data.curves.new("tmp", "FONT")
    cu.body = body
    cu.size = size
    cu.extrude = depth
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    cu.bevel_depth = depth * 0.5
    cu.bevel_resolution = 0
    cu.resolution_u = 4
    tmp = bpy.data.objects.new("tmp", cu)
    scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    return me


def coin_mesh():
    r, t = 0.13, 0.034
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=r, radius2=r, depth=t)
    caps = [f for f in bm.faces if abs(f.normal.z) > 0.9]
    res = bmesh.ops.inset_individual(bm, faces=caps, thickness=0.024, depth=-0.005)
    for f in caps:
        f.material_index = 1
    me = bpy.data.meshes.new("Gilde_Coin")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(GOLD)
    me.materials.append(GOLD_FACE)
    return me


def mound_mesh():
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=0.9)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01], context="VERTS")
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        v.co.z *= 0.5 / 0.9 * (1 + 0.05 * math.sin(a * 5) + 0.04 * math.sin(a * 9 + 1))
        v.co.x *= 0.98
    me = bpy.data.meshes.new("Gilde_Mound")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(GOLD_DARK)
    smooth(me)
    return me


def sack_mesh():
    prof = [(0.0, 0.0), (0.26, 0.015), (0.40, 0.09), (0.47, 0.24), (0.45, 0.40), (0.36, 0.53),
            (0.21, 0.63), (0.115, 0.70), (0.105, 0.75), (0.16, 0.80), (0.25, 0.87), (0.22, 0.905),
            (0.12, 0.86), (0.0, 0.83)]
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0, z)) for r, z in prof]
    edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
    bmesh.ops.spin(bm, geom=verts + edges, cent=(0, 0, 0), axis=(0, 0, 1), angle=math.tau, steps=36,
                   use_merge=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    for v in bm.verts:  # a crinkled top and a slightly lumpy body
        a = math.atan2(v.co.y, v.co.x)
        if v.co.z > 0.76:
            v.co.z += 0.035 * math.sin(a * 7)
            v.co.x *= 1 + 0.08 * math.sin(a * 7 + 1.3)
            v.co.y *= 1 + 0.08 * math.sin(a * 7 + 1.3)
        elif v.co.z > 0.05:
            k = 1 + 0.03 * math.sin(a * 3 + v.co.z * 6)
            v.co.x *= k
            v.co.y *= k
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Gilde_Sack")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(SACK)
    smooth(me)
    return me


def rope_mesh():
    bm = bmesh.new()
    seg, ring = 32, 10
    R, r = 0.118, 0.028
    grid = []
    for i in range(seg):
        u = i / seg * math.tau
        row = []
        for j in range(ring):
            w = j / ring * math.tau
            rr = R + r * math.cos(w)
            row.append(bm.verts.new((rr * math.cos(u), rr * math.sin(u), r * math.sin(w))))
        grid.append(row)
    for i in range(seg):
        for j in range(ring):
            bm.faces.new((grid[i][j], grid[(i + 1) % seg][j], grid[(i + 1) % seg][(j + 1) % ring], grid[i][(j + 1) % ring]))
    # the knot's two loose ends
    me = bpy.data.meshes.new("Gilde_Rope")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(ROPE)
    smooth(me)
    return me


def bar_mesh():
    bm = bmesh.new()
    lo, hi, h = (0.20, 0.085), (0.155, 0.055), 0.085
    vs = [bm.verts.new((sx * lo[0], sy * lo[1], 0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    vs += [bm.verts.new((sx * hi[0], sy * hi[1], h)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([vs[i] for i in f])
    me = bpy.data.meshes.new("Gilde_Bar")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(GOLD)
    return me


def sparkle_mesh():
    bm = bmesh.new()
    pts = []
    for i in range(8):
        a = i / 8 * math.tau
        r = 0.16 if i % 2 == 0 else 0.035
        pts.append(bm.verts.new((r * math.cos(a), r * math.sin(a), 0)))
    c = bm.verts.new((0, 0, 0))
    for i in range(8):
        bm.faces.new((c, pts[i], pts[(i + 1) % 8]))
    me = bpy.data.meshes.new("Gilde_Sparkle")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(SPARK)
    return me


# ---- the pile
root = link(bpy.data.objects.new(PILE, None))
root.empty_display_size = 1.2


def part(name, me, loc, rot=(0, 0, 0), scale=1.0, line=None):
    o = link(bpy.data.objects.new(name, me))
    o.parent = root
    o.location = loc
    o.rotation_euler = rot if isinstance(rot, Euler) else Euler(rot)
    o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    if line:
        outline(o, line)
    return o


def mound_z(x, y):
    r2 = x * x + y * y
    return 0.5 * math.sqrt(max(0.0, 1 - r2))


part("Mound", mound_mesh(), (0, 0, 0), line=0.02)

coin = coin_mesh()
up = Vector((0, 0, 1))
for i in range(240):
    d = (math.sqrt(rng.random()) * 0.97) if i < 170 else rng.uniform(0.8, 1.0)
    a = rng.random() * math.tau
    x, y = math.cos(a) * d, math.sin(a) * d
    z = mound_z(x, y)
    n = Vector((x, y, z / 0.25)).normalized()
    q = up.rotation_difference(n)
    tilt = Euler((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0, math.tau)))
    rot = (q.to_matrix() @ tilt.to_matrix()).to_euler()
    part(f"Coin.{i:03}", coin, Vector((x, y, z)) + n * 0.012, rot, line=0.007)

# coins that rolled off onto the ground, a couple standing on edge
for i in range(26):
    a = rng.random() * math.tau
    d = rng.uniform(1.02, 1.55)
    x, y = math.cos(a) * d, math.sin(a) * d
    if i % 9 == 4:
        rot = (math.radians(rng.uniform(70, 85)), 0, rng.uniform(0, math.tau))
        z = 0.12
    else:
        rot = (rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), rng.uniform(0, math.tau))
        z = 0.017
    part(f"LooseCoin.{i:02}", coin, (x, y, z), rot, line=0.007)

# little stacks of coins
for s, (sx, sy, count) in enumerate(((1.05, -0.75, 5), (1.25, -0.45, 3), (-1.15, -0.7, 4))):
    for k in range(count):
        part(f"Stack{s}.{k}", coin, (sx + rng.uniform(-0.012, 0.012), sy + rng.uniform(-0.012, 0.012), 0.017 + k * 0.036),
             (0, 0, rng.uniform(0, math.tau)), line=0.007)

# gold bars, two side by side and one on top
bar = bar_mesh()
for name, loc, rz in (("Bar.0", (-0.55, -1.05, 0), 0.35), ("Bar.1", (-0.25, -1.17, 0), 0.35),
                      ("Bar.2", (-0.4, -1.11, 0.085), 0.35 + math.pi / 2 * 0.0)):
    o = part(name, bar, loc, (0, 0, rz), line=0.008)
    bev = o.modifiers.new("Bevel", "BEVEL")
    bev.width = 0.01
    bev.segments = 2
    o.modifiers.move(len(o.modifiers) - 1, 0)

CAM_AZ = math.radians(-90)  # the $ on the bags faces the bottom of the game's screen (-Y here)
# money bags leaning on the heap
sack, rope = sack_mesh(), rope_mesh()
dollar = text_mesh("$", 0.42, 0.02)
dollar.materials.append(GREEN)
for i, (x, y, size, turn) in enumerate(((-0.78, 0.25, 1.0, 0.0), (0.72, -0.15, 0.85, 0.0), (0.05, 0.72, 0.8, 0.0))):
    d = math.hypot(x, y)
    away = math.atan2(y, x)
    tilt = math.radians(14)
    base = Vector((x, y, max(0.0, mound_z(x * 0.7, y * 0.7) - 0.18)))
    face = math.atan2(-y, -x) * 0 + away  # the $ faces away from the heap's middle
    rot = Euler((0, tilt, away))
    s = part(f"Sack.{i}", sack, base, rot, size, line=0.016)
    sub = s.modifiers.new("Smooth", "SUBSURF")
    sub.levels = 2
    sub.render_levels = 2
    s.modifiers.move(len(s.modifiers) - 1, 0)
    r = part(f"Rope.{i}", rope, (0, 0, 0.725), (0, 0, 0), 1.0, line=0.01)
    r.parent = s
    # the $ printed on the bag, towards the camera's side
    ang = CAM_AZ - away + turn
    t = part(f"SackDollar.{i}", dollar, (0.445 * math.cos(ang), 0.445 * math.sin(ang), 0.36),
             (math.pi / 2 - 0.3, 0, ang + math.pi / 2), 1.0, line=0.008)
    t.parent = s

# ---- stage: grass, sun, camera
for o in list(bpy.data.objects):
    if o.get("stage"):
        bpy.data.objects.remove(o, do_unlink=True)
gm = bpy.data.meshes.new("Gilde_Ground")
bm = bmesh.new()
bmesh.ops.create_circle(bm, cap_ends=True, segments=64, radius=6)
bm.to_mesh(gm)
bm.free()
gm.materials.append(GRASS)
ground = bpy.data.objects.new("Ground", gm)
ground["stage"] = True
scene.collection.objects.link(ground)

light = bpy.data.objects.get("Light")
if light is None or light.type != "LIGHT":
    light = bpy.data.objects.new("Light", bpy.data.lights.new("Light", "SUN"))
    scene.collection.objects.link(light)
light.data.type = "SUN"
light.data.energy = 3.2
light.data.angle = math.radians(3)
light.rotation_euler = Euler((math.radians(42), 0, math.radians(-35)))
light.location = (0, 0, 6)

cam = bpy.data.objects.get("Camera")
if cam is None or cam.type != "CAMERA":
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    scene.collection.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = 4.6
el, az, dist = math.radians(48), math.radians(-62), 14
target = Vector((0.05, -0.1, 0.3))
cam.location = target + Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el))) * dist
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam

# sparkles facing the camera
spark = sparkle_mesh()
for i, (x, y, z, sc) in enumerate(((0.1, -0.1, 0.78, 1.0), (-0.25, -0.35, 0.9, 0.7), (0.55, -0.55, 0.45, 0.8),
                                   (-0.3, -1.05, 0.32, 0.6), (0.9, 0.1, 0.95, 0.55))):
    o = part(f"Sparkle.{i}", spark, (x, y, z), (0, 0, 0), sc)
    o.visible_shadow = False
    c = o.constraints.new("TRACK_TO")
    c.target = cam
    c.track_axis = "TRACK_Z"
    c.up_axis = "UP_Y"

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
if bg:
    bg.inputs[0].default_value = lin("#9fd3f0")
    bg.inputs[1].default_value = 0.25

scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 1200
scene.render.film_transparent = False

result = {"objects": len(col.objects)}
