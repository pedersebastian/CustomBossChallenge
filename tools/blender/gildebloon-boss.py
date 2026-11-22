"""
A proposal for Gildebloon's own body and its animations (#25), in the same cartoon style as the pile of money
(tools/blender/gildebloon-pile.py): toon bands and dark outlines. Builds it in the open Blender, in a scene of its
own ("Gildebloon Boss"), so nothing else in the file is touched:

    python tools/blender/run-in-blender.py tools/blender/gildebloon-boss.py

The body keeps the part names the mod already moves (docs/gildebloon.md, "Its look"): Hull, VaultDoor, Dial,
BagLeft, BagRight and CoinRing. New parts: Eyes (EyeLeft, EyeRight), Mouth (a coin slot), VaultShell (the Vault
shown as a shield) and the effect coins FxCoin.N.

Every part is rigid, so each animation is only a position, rotation and scale per part per frame. That can be
exported as numbers (like the pile's mesh) and played by Visuals/GildebloonLook.cs, since nothing can come from an
asset bundle. The timeline has one marker per animation; ANIMS below lists them.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Euler, Matrix

SCENE = "Gildebloon Boss"
FPS = 24
rng = random.Random(25)

# name, first frame, last frame, what it is for
ANIMS = [
    ("Spawn", 1, 84, "rises out of the pile of money, shakes the coins off, opens its eyes"),
    ("Idle", 85, 132, "hovers along the track: bob, coin ring, dial, bags (loops)"),
    ("Hit", 133, 144, "takes damage: squash, squint, a coin knocked loose"),
    ("Toll", 145, 180, "steals cash: coins fly into the coin slot, the bags swell"),
    ("Repossession", 181, 264, "a skull: shakes, the door bursts open, coins spray, its reach rings out, door slams"),
    ("Skittish", 265, 288, "lost 10 % within a second: winds up and dashes ahead"),
    ("VaultBreak", 289, 336, "its Vault (shield of cash) shimmers and shatters"),
    ("Death", 337, 432, "wobbles, door flies off, bags burst, it deflates and pays out in a shower of coins"),
    ("Damaged66", 433, 480, "below 66 % health: dents in the hull, a wince now and then (loops)"),
    ("Damaged33", 481, 528, "below 33 %: cracks, the door rattling ajar, sweat (loops)"),
    ("Damaged10", 529, 576, "below 10 %: coins trickle out of the cracks, it sags and trembles (loops)"),
    ("Dispatch", 577, 624, "sends a Tax Collector: the door opens and a gold MOAB pops out onto the track ahead"),
    ("Escape", 625, 696, "leaks (it beats you): grins, stuffs coins into its bags and dives out"),
    ("TarnishHit", 697, 724, "hit by an Alchemist or Glue Gunner: tarnish blotches flash on the gold, a wince"),
    ("Interest", 725, 756, "Elite's Interest: the Vault pulses once and a few coins drop in"),
    ("VaultUp", 757, 792, "its Vault builds up: coins rise from the bags into a shield"),
    ("EliteSpawn", 793, 876, "Elite: rises out of the pile like Spawn, then its crown of spikes shoots up"),
    ("Payout", 877, 924, "beaten: the coins it leaves fountain up and out; the mod's screen flies them on to the cash"),
    ("IdleCount", 925, 972, "idle: flips a coin out of the door and catches it"),
    ("IdlePolish", 973, 1020, "idle: polishes its shine with a banknote"),
    ("IdleSmug", 1021, 1068, "idle, after a big Toll: leans back, chuckles, bags bouncing"),
]


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*c, 1.0)


# ---- its own scene, emptied on a rerun; in a background build (no window) the context's scene, renamed, so what
# evaluates through the context (the $ text, the exporter) sees it
if bpy.context.window is not None:
    scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
else:
    scene = bpy.context.scene
    scene.name = SCENE
for o in list(scene.collection.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.actions):
    for d in list(coll):
        if d.users == 0:
            coll.remove(d)
if bpy.context.window is not None:
    bpy.context.window.scene = scene
scene.timeline_markers.clear()
col = bpy.data.collections.get("GildebloonBody") or bpy.data.collections.new("GildebloonBody")
if col.name not in scene.collection.children:
    scene.collection.children.link(col)


def to_stage(o):
    """Moves a part of the stage (and its children) out of the body's collection"""
    for x in [o] + list(o.children_recursive):
        if x.name in col.objects:
            col.objects.unlink(x)
        if x.name not in scene.collection.objects:
            scene.collection.objects.link(x)
    return o


def link(obj, parent=None):
    col.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


# ---- materials: the same toon bands as the pile
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


def glass(name, colour, alpha):
    """See-through gold for the Vault: brighter at the rim"""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs[0].default_value = lin(colour)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    fr = nt.nodes.new("ShaderNodeLayerWeight")
    fr.inputs[0].default_value = 0.35
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs[3].default_value = alpha
    mr.inputs[4].default_value = 0.85
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(fr.outputs["Facing"], mr.inputs[0])
    nt.links.new(mr.outputs[0], mix.inputs[0])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    if hasattr(m, "surface_render_method"):
        m.surface_render_method = "BLENDED"
    m.diffuse_color = lin(colour)
    return m


GOLD = toon("Gilde_BossGold", "#e08600", "#ffc61a", "#fff3b0", cuts=(0.3, 0.95))  # the pale band only a shine
GOLD_DARK = toon("Gilde_GoldInner", "#b86e00", "#e9a10e", None)
GOLD_FACE = toon("Gilde_GoldFace", "#e59a00", "#ffd23d", "#fff6c0")
IRON = toon("Gilde_Iron", "#3a3f48", "#6b7380", "#aab2bd")
SACK = toon("Gilde_Sack", "#9a6a3a", "#d4a96a", "#ecd2a0")
ROPE = toon("Gilde_Rope", "#5a3418", "#8a5a2e", None)
GREEN = toon("Gilde_Dollar", "#1f6b22", "#36a03a", None)
GRASS = toon("Gilde_Grass", "#5a9a2e", "#84c441", None, cuts=(0.42, 1))
WHITE = toon("Gilde_EyeWhite", "#c9d2dc", "#ffffff", None, cuts=(0.3, 1))
BLACK = flat("Gilde_Pupil", "#120a02")
SLOT = flat("Gilde_Slot", "#2a1404")
SHINE = flat("Gilde_Shine", "#ffffff")


def blob_shadow(name):
    """BTD6 bloons have a soft round shadow under them rather than a cast one"""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    gr = nt.nodes.new("ShaderNodeTexGradient")
    gr.gradient_type = "SPHERICAL"
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs[4].default_value = 0.62
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs[0].default_value = lin("#1d3a0c")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(tc.outputs["Object"], gr.inputs[0])
    nt.links.new(gr.outputs[1], mr.inputs[0])
    nt.links.new(mr.outputs[0], mix.inputs[0])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    if hasattr(m, "surface_render_method"):
        m.surface_render_method = "BLENDED"
    return m


SHADOW = blob_shadow("Gilde_BlobShadow")
RING = flat("Gilde_Reach", "#ffe066")
VAULT = glass("Gilde_Vault", "#ffd23d", 0.08)
LINE = flat("Gilde_Outline", "#2a1a06")
LINE.use_backface_culling = True


OUTLINE_SCALE = 2.0  # at the game's size the outlines must be twice what reads in Blender's close-ups


def outline(obj, width):
    mats = obj.data.materials
    names = [m.name for m in mats if m]
    if LINE.name in names:  # a mesh shared by many parts gets its outline material once
        n = names.index(LINE.name)
    else:
        n = len(mats)
        for _ in range(n):
            mats.append(LINE)
    mod = obj.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = width * OUTLINE_SCALE
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
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return me


def ellipsoid(name, size, mat, seg=48, rings=24):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    return finish(bm, name, [mat])


def disc(name, r, depth, mat, seg=40):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=depth)
    return finish(bm, name, [mat], smooth=False)


def torus(name, major, minor, mat, seg=48, segr=10, squash=(1, 1)):
    bm = bmesh.new()
    rows = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        row = []
        for j in range(segr):
            b = 2 * math.pi * j / segr
            r = major + minor * math.cos(b)
            row.append(bm.verts.new((r * math.cos(a) * squash[0], r * math.sin(a) * squash[1], minor * math.sin(b))))
        rows.append(row)
    for i in range(seg):
        for j in range(segr):
            bm.faces.new((rows[i][j], rows[(i + 1) % seg][j], rows[(i + 1) % seg][(j + 1) % segr],
                          rows[i][(j + 1) % segr]))
    return finish(bm, name, [mat])


def box(name, size, mat):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    return finish(bm, name, [mat], smooth=False)


def coin_mesh():
    r, t = 0.2, 0.05  # at the game's size smaller ones read as specks
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=r, radius2=r, depth=t)
    caps = [f for f in bm.faces if abs(f.normal.z) > 0.9]
    bmesh.ops.inset_individual(bm, faces=caps, thickness=0.037, depth=-0.007)
    for f in caps:
        f.material_index = 1
    return finish(bm, "Gilde_Coin", [GOLD, GOLD_FACE], smooth=False)


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


def obj(name, me, parent=None, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0, line=None):
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    if line:
        outline(o, line)
    return link(o, parent)


def empty(name, parent=None, loc=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.3
    o.location = loc
    return link(o, parent)


# ---- the body. Front is -Y (towards the bottom of the game's screen), up is +Z; the hull is about 2 wide.
HULL = Vector((1.0, 1.25, 0.8))
HOVER = 1.25
root = empty("Gildebloon", loc=(0, 0, HOVER))
hull = obj("Hull", ellipsoid("Gilde_Hull", HULL, GOLD), root, line=0.055)
obj("Seam", torus("Gilde_Seam", 1.0, 0.045, GOLD_DARK, squash=(1.0, 1.25)), hull, line=0.012)
merid = torus("Gilde_Meridian", 1.0, 0.035, GOLD_DARK, squash=(1.0, 0.8))
obj("SeamMeridian", merid, hull, rot=(math.pi / 2, 0, math.pi / 2), scale=(1.0, 1.0, 1.25), line=0.01)
rivet = ellipsoid("Gilde_Rivet", (0.05, 0.05, 0.05), IRON, 10, 6)
for i in range(18):
    a = 2 * math.pi * (i + 0.5) / 18
    if abs(a - 1.5 * math.pi) < 0.45:  # room for the coin slot on the band at the front
        continue
    obj(f"Rivet{i}", rivet, hull, loc=(1.035 * math.cos(a), 1.035 * 1.25 * math.sin(a), 0.06))
    obj(f"RivetLow{i}", rivet, hull, loc=(1.035 * math.cos(a), 1.035 * 1.25 * math.sin(a), -0.06))

# the vault door on top (the game looks down on it), hinged at its back edge
TOP = HULL.z - 0.04
obj("DoorFrame", torus("Gilde_DoorFrame", 0.52, 0.06, IRON), hull, loc=(0, 0, TOP), line=0.012)
door_me = disc("Gilde_Door", 0.5, 0.09, GOLD_FACE)
bmesh_tmp = bmesh.new()
bmesh_tmp.from_mesh(door_me)
bmesh.ops.translate(bmesh_tmp, vec=(0, -0.5, 0), verts=bmesh_tmp.verts)  # the hinge is the object's origin
bmesh_tmp.to_mesh(door_me)
bmesh_tmp.free()
door = obj("VaultDoor", door_me, hull, loc=(0, 0.5, TOP + 0.03), line=0.025)
obj("DoorBolts", torus("Gilde_DoorBolts", 0.38, 0.025, IRON, seg=32, segr=6), door, loc=(0, -0.5, 0.05))
dial = obj("Dial", disc("Gilde_Dial", 0.16, 0.08, IRON), door, loc=(0, -0.5, 0.09), line=0.01)
spoke = box("Gilde_Spoke", (0.42, 0.045, 0.045), IRON)
for i in range(3):
    obj(f"Spoke{i}", spoke, dial, loc=(0, 0, 0.05), rot=(0, 0, i * math.pi / 3), line=0.008)
obj("DialKnob", ellipsoid("Gilde_Knob", (0.07, 0.07, 0.05), GOLD_FACE, 12, 8), dial, loc=(0, 0, 0.07))

# a face: two eyes and a coin slot for a mouth, on the front
EYES_AT = (0, -0.8, 0.62)  # high on the front, just short of the door, so they show from above also sideways
eyes = empty("Eyes", hull, loc=EYES_AT)
eyes.rotation_euler = (-1.08, 0, 0)  # lying on the hull there, turned up towards the game's camera
eyes.scale = (1.35,) * 3
eye_me = ellipsoid("Gilde_Eye", (0.2, 0.09, 0.24), WHITE, 24, 12)
pupil_me = ellipsoid("Gilde_PupilMesh", (0.085, 0.05, 0.1), BLACK, 16, 8)
brow_me = box("Gilde_Brow", (0.32, 0.06, 0.09), BLACK)
glint_me = ellipsoid("Gilde_Glint", (0.03, 0.012, 0.036), SHINE, 10, 6)
x_me = box("Gilde_XBar", (0.42, 0.05, 0.1), BLACK)
for side, sx in (("Left", -1), ("Right", 1)):
    e = obj(f"Eye{side}", eye_me, eyes, loc=(0.34 * sx, 0, 0), rot=(0, 0, -0.12 * sx), line=0.02)
    pu = obj(f"Pupil{side}", pupil_me, e, loc=(-0.03 * sx, -0.07, -0.02))
    obj(f"Glint{side}", glint_me, pu, loc=(0.03, -0.045, 0.045))
    obj(f"Brow{side}", brow_me, e, loc=(0.02 * sx, -0.05, 0.26), rot=(0, -0.38 * sx, 0))  # inner ends down: cross
    for k, r in enumerate((0.8, -0.8)):  # crossed eyes for its death, hidden until then
        obj(f"EyeX{side}{k}", x_me, e, loc=(0, -0.09, 0), rot=(0, r, 0))
mouth = obj("Mouth", box("Gilde_SlotMesh", (0.5, 0.1, 0.09), SLOT), hull, loc=(0, -1.29, 0.03),
            rot=(-0.15, 0, 0))
obj("MouthFrame", box("Gilde_SlotFrame", (0.66, 0.08, 0.2), IRON), mouth, loc=(0, 0.025, 0), line=0.015)

# the shine every bloon has, up on the hull towards the light
gloss_at = Vector((-0.62, -0.5, 0.8 * math.sqrt(1 - 0.62 ** 2 - (0.5 / 1.25) ** 2)))
gloss_n = Vector((gloss_at.x, gloss_at.y / 1.25 ** 2, gloss_at.z / 0.8 ** 2)).normalized()
gloss = obj("Gloss", ellipsoid("Gilde_Gloss", (0.2, 0.08, 0.012), SHINE, 20, 8), hull, loc=gloss_at + gloss_n * 0.005)
gloss.rotation_euler = gloss_n.to_track_quat("Z", "Y").to_euler()
gloss.rotation_euler.rotate_axis("Z", 0.7)

# money bags on its sides, the $ towards the camera
dollar = text_mesh("$", 0.72, 0.04, GREEN)
bag_body = ellipsoid("Gilde_Bag", (0.42, 0.4, 0.46), SACK, 24, 14)
neck = bmesh.new()
bmesh.ops.create_cone(neck, cap_ends=True, segments=16, radius1=0.12, radius2=0.2, depth=0.2)
neck_me = finish(neck, "Gilde_BagNeck", [SACK])
rope = torus("Gilde_BagRope", 0.13, 0.035, ROPE, seg=20, segr=6)
for side, sx in (("Left", -1), ("Right", 1)):
    b = empty(f"Bag{side}", hull, loc=(1.12 * sx, 0.05, -0.12))
    obj(f"Bag{side}Body", bag_body, b, line=0.03)
    obj(f"Bag{side}Neck", neck_me, b, loc=(0, 0, 0.5), line=0.01)
    obj(f"Bag{side}Rope", rope, b, loc=(0, 0, 0.43), line=0.008)
    obj(f"Bag{side}Dollar", dollar, b, loc=(0.05 * sx, -0.42, 0.04), rot=(math.pi / 2 - 0.35, 0, 0), line=0.012)

# a ring of coins circling it
coin = coin_mesh()
ring = empty("CoinRing", root, loc=(0, 0, -0.15))
for i in range(8):
    a = 2 * math.pi * i / 8
    obj(f"RingCoin{i}", coin, ring, loc=(1.6 * math.cos(a), 1.6 * 1.15 * math.sin(a), 0.08 * math.sin(3 * a)),
        rot=(math.pi / 2, 0, a + math.pi / 2), scale=1.15, line=0.008)

# the Vault as a shield round it, hidden until VaultBreak
shell = obj("VaultShell", bpy.data.meshes.new("Gilde_VaultShell"), root)
sbm = bmesh.new()
bmesh.ops.create_icosphere(sbm, subdivisions=3, radius=1)
bmesh.ops.scale(sbm, vec=(1.85, 2.05, 1.45), verts=sbm.verts)
sbm.to_mesh(shell.data)
sbm.free()
shell.data.materials.append(VAULT)
shell.visible_shadow = False

# ---- parts for the damage states, Tarnish, Elite, Dispatch and the idle polish; each group hidden by scale 0
DENT = toon("Gilde_Dent", "#a8620a", "#d98d12", None)
TARNISH = toon("Gilde_Tarnish", "#4f6b47", "#7d9a6f", None)
SWEAT = toon("Gilde_Sweat", "#6fa6d6", "#cdeaff", "#ffffff", cuts=(0.3, 0.8))
RED = toon("Gilde_EliteRed", "#7a0c0c", "#d42a2a", "#ff8a7a")


def on_hull(x, y):
    """A point on the hull's upper surface over (x, y), in the hull's space, and the surface's normal there"""
    z = HULL.z * math.sqrt(max(0.0, 1 - (x / HULL.x) ** 2 - (y / HULL.y) ** 2))
    p = Vector((x, y, z))
    return p, Vector((x / HULL.x ** 2, y / HULL.y ** 2, z / HULL.z ** 2)).normalized()


def decal(name, me, parent, x, y, spin=0.0, lift=0.004):
    p, n = on_hull(x, y)
    o = obj(name, me, parent, loc=p + n * lift)
    o.rotation_euler = n.to_track_quat("Z", "Y").to_euler()
    o.rotation_euler.rotate_axis("Z", spin)
    return o


def crack(name, parent, pts):
    """A zigzag crack along points on the hull, as short dark strips lying on it"""
    g = empty(name, parent)
    for k in range(len(pts) - 1):
        a, na = on_hull(*pts[k])
        b, nb = on_hull(*pts[k + 1])
        n = (na + nb).normalized()
        d = b - a
        x = (d - n * d.dot(n)).normalized()
        y = n.cross(x)
        m = Matrix((x, y, n)).transposed()
        seg = obj(f"{name}_{k}", box("Gilde_CrackSeg", (d.length + 0.04, 0.06, 0.016), SLOT), g,
                  loc=(a + b) / 2 + n * 0.008)
        seg.rotation_euler = m.to_euler()
    return g


dents = empty("Dents", hull)
dent_me = ellipsoid("Gilde_DentMesh", (0.16, 0.11, 0.012), DENT, 16, 6)
for i, (x, y, sp) in enumerate(((-0.55, -0.62, 0.4), (0.5, -0.5, -0.3), (0.62, 0.35, 1.1), (-0.3, 0.75, 0.2))):
    decal(f"Dent{i}", dent_me, dents, x, y, sp)
cracks = empty("Cracks", hull)
CRACKS = [[(-0.72, -0.3), (-0.58, -0.42), (-0.64, -0.58), (-0.48, -0.7), (-0.52, -0.86)],
          [(0.36, -0.6), (0.52, -0.5), (0.46, -0.34), (0.64, -0.24), (0.6, -0.08)],
          [(-0.3, 0.55), (-0.1, 0.64), (-0.18, 0.82), (0.02, 0.9)]]
for i, pts in enumerate(CRACKS):
    crack(f"Crack{i}", cracks, pts)
sweat = obj("SweatDrop", ellipsoid("Gilde_SweatMesh", (0.07, 0.05, 0.1), SWEAT, 12, 8), hull,
            loc=(0.62, -0.78, 0.5), line=0.01)
tarnish = empty("Tarnish", hull)
tar_me = ellipsoid("Gilde_TarnishMesh", (0.2, 0.14, 0.014), TARNISH, 16, 6)
for i, (x, y, sp) in enumerate(((-0.3, -0.55, 0.5), (0.35, -0.65, -0.2), (0.6, 0.05, 0.9), (-0.62, 0.1, 0.1),
                                (0.1, 0.55, 1.3))):
    decal(f"Tarnish{i}", tar_me, tarnish, x, y, sp, lift=0.006)
# ---- what towers put on it, in its own shape (#25, 2026-11-22). The game draws these on a boss from its overlay class,
# and Gildebloon's is Bloonarius's: glue, acid, fire and laser shock shaped for Bloonarius's long body, which no scaling
# lays tight on this round one. These are the same effects, made on the hull; each group is shown by the mod while the
# game's own is on (GildebloonLook), and hidden in Blender's renders.
import random as _random

GLUE_YELLOW = toon("Gilde_GlueYellow", "#93a300", "#e3f04a", "#fbffb8", cuts=(0.35, 0.9))  # lime, to show on the gold
GLUE_GREEN = toon("Gilde_GlueGreen", "#3f8a17", "#86cf38", "#cdf284", cuts=(0.35, 0.9))
GLUE_PINK = toon("Gilde_GluePink", "#b01f6c", "#ff5aad", "#ffb6da", cuts=(0.35, 0.9))
ACID = toon("Gilde_Acid", "#3e1f57", "#7a3db0", "#b886ea", cuts=(0.35, 0.9))
BUBBLE = toon("Gilde_Bubble", "#b9c7d6", "#f2f7ff", "#ffffff", cuts=(0.3, 0.85))


def hull_dir(v):
    """Where a direction from the hull's middle meets its surface, and the surface's normal there"""
    k = math.sqrt((v.x / HULL.x) ** 2 + (v.y / HULL.y) ** 2 + (v.z / HULL.z) ** 2)
    s = v / k
    return s, Vector((s.x / HULL.x ** 2, s.y / HULL.y ** 2, s.z / HULL.z ** 2)).normalized()


def hug_splat(name, mat, centre, radius, thick, seed, stretch=(1.0, 1.0), lift=0.012, rings=7, seg=28):
    """A blob of goo lying on the hull round the direction `centre`: a dome bent onto the surface, with a wavy edge"""
    r_ = _random.Random(seed)
    c, n = hull_dir(Vector(centre))
    t1 = n.cross(Vector((0, 0, 1)))
    if t1.length < 1e-3:
        t1 = Vector((1, 0, 0))
    t1.normalize()
    t2 = n.cross(t1).normalized()
    ph = [r_.uniform(0, 6.3) for _ in range(3)]
    bm = bmesh.new()
    tip = bm.verts.new(c + n * (lift + thick))
    prev = None
    rows = []
    for i in range(1, rings + 1):
        rho = i / rings
        row = []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            edge = 1 + 0.16 * math.sin(3 * a + ph[0]) + 0.09 * math.sin(5 * a + ph[1]) + 0.05 * math.sin(8 * a + ph[2])
            d = radius * rho * edge
            q = c + t1 * (math.cos(a) * d * stretch[0]) + t2 * (math.sin(a) * d * stretch[1])
            s, sn = hull_dir(q)
            h = lift + thick * max(0.0, 1 - rho ** 2) ** 0.6
            row.append(bm.verts.new(s + sn * h))
        rows.append(row)
    for j in range(seg):
        bm.faces.new((tip, rows[0][j], rows[0][(j + 1) % seg]))
    for i in range(rings - 1):
        for j in range(seg):
            bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][(j + 1) % seg], rows[i][(j + 1) % seg]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = finish(bm, name, [mat])
    # facing out: the first face's normal away from the hull's middle
    f = me.polygons[0]
    if f.normal.dot(f.center) < 0:
        for p in me.polygons:
            p.flip()
    return me


GLUE_SPOTS = [  # (direction from the hull's middle, radius, stretch): a coat over its top running down its sides
    ((0.0, -0.15, 1.0), 0.62, (1.0, 1.1)), ((0.55, 0.45, 0.7), 0.42, (1.0, 1.0)), ((-0.6, 0.35, 0.65), 0.45, (1.0, 1.0)),
    ((0.45, -0.75, 0.45), 0.36, (1.0, 1.0)), ((-0.5, -0.7, 0.4), 0.34, (1.0, 1.0)), ((0.0, 0.9, 0.5), 0.4, (1.2, 1.0)),
    ((0.95, -0.05, 0.25), 0.3, (1.0, 1.0)), ((-0.95, 0.0, 0.2), 0.3, (1.0, 1.0)),
]
GLUE_DRIPS = [((0.62, -0.62, 0.05), 0.16), ((-0.75, -0.35, 0.0), 0.14), ((0.15, 1.05, 0.05), 0.15), ((-0.35, 0.95, -0.05), 0.12),
              ((0.98, 0.35, -0.05), 0.13)]


def glue_coat(group, key, mat, seed):
    # a glob over the vault door too, as the game's glue covers a whole boss
    obj(f"{key}DoorGlob", hug_splat(f"Gilde_{key}DoorGlob", mat, (0.05, -0.1, 1.0), 0.34, 0.05, seed + 90, (1.0, 0.9), lift=0.11),
        group, line=0.014)
    for i, (d, r, st) in enumerate(GLUE_SPOTS):
        obj(f"{key}Splat{i}", hug_splat(f"Gilde_{key}Splat{i}", mat, d, r, 0.055, seed + i, st), group, line=0.014)
    for i, (d, r) in enumerate(GLUE_DRIPS):
        obj(f"{key}Drip{i}", hug_splat(f"Gilde_{key}Drip{i}", mat, d, r, 0.05, seed + 40 + i, (0.7, 1.5)), group, line=0.012)


def bubbles(group, key, spots, size):
    bub = ellipsoid(f"Gilde_{key}Bubble", (size, size, size), BUBBLE, 12, 8)
    for i, d in enumerate(spots):
        s, n = hull_dir(Vector(d))
        obj(f"{key}Bubble{i}", bub, group, loc=s + n * (0.07 + size * 0.5), scale=0.7 + 0.15 * (i % 3), line=0.008)


coat_groups = {}
for key, mat, seed in (("GlueBasic", GLUE_YELLOW, 11), ("GlueDissolve", GLUE_GREEN, 23), ("GlueStronger", GLUE_PINK, 37),
                       ("GlueStrongerDot", GLUE_PINK, 37)):
    g = empty("Overlay" + key, hull)
    glue_coat(g, key, mat, seed)
    coat_groups[key] = g
bubbles(coat_groups["GlueStrongerDot"], "GlueStrongerDot",
        [(0.1, -0.2, 1.0), (-0.3, 0.2, 0.95), (0.4, 0.3, 0.85), (-0.15, -0.55, 0.8), (0.55, -0.35, 0.75), (-0.5, 0.55, 0.7)], 0.06)

# Unstable Concoction: purple acid eating in, in smaller spots, bubbling
acid = empty("OverlayUnstableConcoction", hull)
_r = _random.Random(57)
for i in range(13):
    d = (_r.uniform(-0.9, 0.9), _r.uniform(-0.95, 0.95), _r.uniform(0.15, 1.0))
    obj(f"AcidSplat{i}", hug_splat(f"Gilde_AcidSplat{i}", ACID, d, _r.uniform(0.16, 0.28), 0.035, 300 + i), acid, line=0.01)
bubbles(acid, "Acid", [(0.2, 0.1, 1.0), (-0.45, -0.3, 0.8), (0.6, -0.5, 0.6), (-0.2, 0.7, 0.7), (0.7, 0.5, 0.5)], 0.05)
coat_groups["UnstableConcoction"] = acid


def hug_poly(name, mat, centre, shape, size, toward, lift=0.012):
    """A flat shape lying bent on the hull round the direction `centre`: `shape` is its outline, +y pointing `toward`
    along the surface, fanned from its middle"""
    c, n = hull_dir(Vector(centre))
    d = Vector(toward)
    tv = d - n * d.dot(n)
    if tv.length < 1e-3:
        tv = n.cross(Vector((1, 0, 0)))
    tv.normalize()
    tu = tv.cross(n).normalized()
    bm = bmesh.new()
    mid = Vector((sum(p[0] for p in shape) / len(shape), sum(p[1] for p in shape) / len(shape)))

    def put(x, y):
        q = c + tu * (x * size) + tv * (y * size)
        s, sn = hull_dir(q)
        return bm.verts.new(s + sn * lift)

    m = put(*mid)
    ring = [put(x, y) for x, y in shape]
    for i in range(len(ring)):
        bm.faces.new((m, ring[i], ring[(i + 1) % len(ring)]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = finish(bm, name, [mat], smooth=False)
    f = me.polygons[0]
    if f.normal.dot(f.center) < 0:
        for p in me.polygons:
            p.flip()
    return me


# Fire: like the game's, flat flame tongues lying on it, orange with a yellow heart and no outline, trailing back
# (+y, away from its face), and red flames licking up round its rim (2026-11-22: flames standing out of it as cones
# didn't read as fire)
FIRE_ORANGE = flat("Gilde_FireOrange", "#ff4d12")  # redder than the gold, to show on it
FIRE_YELLOW = flat("Gilde_FireYellow", "#ffe85a")
FIRE_RED = flat("Gilde_FireRed", "#e8321c")
TONGUE = [(-0.45, -0.35), (-0.55, 0.15), (-0.32, 0.45), (-0.42, 0.95), (-0.12, 0.7), (-0.05, 1.35), (0.15, 0.75),
          (0.38, 1.05), (0.33, 0.5), (0.55, 0.2), (0.42, -0.3), (0.0, -0.45)]
fire = empty("OverlayFire", hull)
TONGUES = [((0.55, 0.15, 0.8), 0.78, 0.35), ((-0.55, 0.2, 0.8), 0.8, -0.35), ((0.05, 0.75, 0.65), 0.72, 0.0),
           ((0.72, -0.5, 0.5), 0.6, 0.5), ((-0.74, -0.45, 0.5), 0.62, -0.5)]
for i, (d, size, lean) in enumerate(TONGUES):
    toward = (lean, 1.0, 0.25)  # back along it, leaning out to the side it is on
    obj(f"FireTongue{i}", hug_poly(f"Gilde_FireTongue{i}", FIRE_ORANGE, d, TONGUE, size, toward, lift=0.022), fire)
    obj(f"FireHeart{i}", hug_poly(f"Gilde_FireHeart{i}", FIRE_YELLOW, d, [(x * 0.55, y * 0.55 - 0.08) for x, y in TONGUE],
                                  size, toward, lift=0.036), fire)
for i in range(10):  # red flames licking up round its rim, not over its face (its front is -y)
    a = math.radians(-115 + i * (230 / 9))
    d = (math.sin(a), math.cos(a), -0.12)  # low on its side, licking up below the orange ones
    obj(f"FireRim{i}", hug_poly(f"Gilde_FireRim{i}", FIRE_RED, d, TONGUE, 0.26 + 0.05 * (i % 3), (0, 0, 1), lift=0.01), fire)
coat_groups["Fire"] = fire

# Laser Shock: like the game's, broad white lightning lying on it with a red edge
SHOCK_RED = flat("Gilde_ShockRed", "#ff2a2a")
SHOCK_WHITE = flat("Gilde_ShockWhite", "#ffffff")


def ribbon(name, mat, pts, width, lift):
    """A flat band along points on the hull"""
    bm = bmesh.new()
    rows = []
    for i, p in enumerate(pts):
        s, n = hull_dir(p)
        a = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        side = a.cross(n).normalized() * (width / 2)
        rows.append([bm.verts.new(hull_dir(s + side)[0] + n * lift), bm.verts.new(hull_dir(s - side)[0] + n * lift)])
    for (a1, a2), (b1, b2) in zip(rows, rows[1:]):
        bm.faces.new((a1, b1, b2, a2))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = finish(bm, name, [mat], smooth=False)
    f = me.polygons[0]
    if f.normal.dot(f.center) < 0:
        for p in me.polygons:
            p.flip()
    return me


shock = empty("OverlayLaserShock", hull)
BOLTS = [  # round the door, which would cover them: down its left side, its right side and across its back
    [(-0.55, -0.75, 0.45), (-0.8, -0.35, 0.5), (-0.5, -0.1, 0.8), (-0.85, 0.2, 0.5), (-0.55, 0.45, 0.7), (-0.7, 0.8, 0.35)],
    [(0.6, -0.7, 0.45), (0.85, -0.3, 0.45), (0.55, 0.0, 0.8), (0.88, 0.25, 0.45), (0.6, 0.55, 0.65), (0.75, 0.85, 0.3)],
    [(-0.45, 0.95, 0.45), (-0.15, 0.7, 0.75), (0.05, 1.05, 0.4), (0.3, 0.72, 0.75), (0.5, 0.95, 0.4)]]
for i, path in enumerate(BOLTS):
    # sharp corners: each corner gets its own short straight run
    pts = []
    for a, b in zip(path, path[1:]):
        for k in range(4):
            pts.append(Vector(a).lerp(Vector(b), k / 4))
    pts.append(Vector(path[-1]))
    obj(f"ShockEdge{i}", ribbon(f"Gilde_ShockEdge{i}", SHOCK_RED, pts, 0.3, 0.016), shock)
    obj(f"ShockBolt{i}", ribbon(f"Gilde_ShockBolt{i}", SHOCK_WHITE, pts, 0.19, 0.024), shock)
coat_groups["LaserShock"] = shock

# Not in the renders of its body (the sheet, the portraits): the mod shows them
for g in coat_groups.values():
    for o in [g] + list(g.children_recursive):
        o.hide_render = True
        o.visible_shadow = False

# Elite: a red band and a crown of spikes round the door
elite_band = empty("EliteBand", hull)
band_z = 0.3
band_r = math.sqrt(1 - (band_z / HULL.z) ** 2)
obj("EliteBandRing", torus("Gilde_EliteBand", band_r * 1.01, 0.055, RED, squash=(1.0, 1.25)), elite_band,
    loc=(0, 0, band_z), line=0.012)
elite_spikes = empty("EliteSpikes", hull)
spike_bm = bmesh.new()
bmesh.ops.create_cone(spike_bm, cap_ends=True, segments=10, radius1=0.14, radius2=0.0, depth=0.72)
bmesh.ops.translate(spike_bm, vec=(0, 0, 0.36), verts=spike_bm.verts)  # its base at the origin
spike_me = finish(spike_bm, "Gilde_Spike", [RED], smooth=False)
tip_me = ellipsoid("Gilde_SpikeTip", (0.075, 0.075, 0.075), GOLD_FACE, 12, 8)
for i, deg in enumerate((-20, 35, 90, 145, 200)):  # a crown of five round the back and sides; none over its face
    a = math.radians(deg)
    p_, n_ = on_hull(0.7 * math.cos(a), 0.7 * 1.1 * math.sin(a))
    up = (n_ + Vector((0, 0, 1.2))).normalized()  # more upright than the hull, like a crown's points
    spk = obj(f"EliteSpike{i}", spike_me, elite_spikes, loc=p_ - n_ * 0.03, line=0.014)
    spk.rotation_euler = up.to_track_quat("Z", "Y").to_euler()
    obj(f"EliteSpikeTip{i}", tip_me, spk, loc=(0, 0, 0.72), line=0.01)
# Dispatch: a small gold MOAB, the Tax Collector, flying off on its own (not a child of the body)
proxy = empty("CollectorProxy")
obj("CollectorHull", ellipsoid("Gilde_CollectorHull", (0.34, 0.6, 0.28), GOLD, 24, 12), proxy, line=0.025)
fin = box("Gilde_CollectorFin", (0.05, 0.22, 0.2), GOLD_DARK)
for i, (x, z) in enumerate(((0.0, 0.26), (0.0, -0.26), (0.26, 0.0), (-0.26, 0.0))):
    fo = obj(f"CollectorFin{i}", fin, proxy, loc=(x, 0.58, z), line=0.01)
    if x != 0:
        fo.rotation_euler = (0, math.pi / 2, 0)
obj("CollectorDoor", disc("Gilde_CollectorDoor", 0.14, 0.04, GOLD_FACE), proxy, loc=(0, -0.05, 0.27), line=0.01)
# a big green $ on its back, so it reads as the thief it is from the game's camera above (2026-11-22: it read as a
# gold MOAB), and a dark band round its middle like a MOAB's
# on its back behind the door, lying on the hull (its surface there tilts about 15 degrees back) and standing out of it
obj("CollectorDollar", text_mesh("$", 0.44, 0.035, GREEN), proxy, loc=(0, 0.31, 0.272), rot=(-0.26, 0, math.pi / 2), line=0.014)
obj("CollectorBand", torus("Gilde_CollectorBand", 0.335, 0.022, GOLD_DARK, 32, 6, squash=(1, 0.83)), proxy,
    loc=(0, 0.12, 0), rot=(math.pi / 2, 0, 0), line=0.008)
for o in [proxy] + list(proxy.children_recursive):
    o.visible_shadow = False
# idle polish: a banknote it rubs its shine with
bill = obj("PolishBill", box("Gilde_PolishBill", (0.52, 0.25, 0.04), GREEN), hull, line=0.014)
obj("PolishBillBand", box("Gilde_PolishBand", (0.09, 0.26, 0.046), SACK), bill)

# ---- the stage: grass, the pile it rises from, its reach on the ground, sun, camera
gbm = bmesh.new()
bmesh.ops.create_circle(gbm, cap_ends=True, segments=64, radius=8)
to_stage(obj("Ground", finish(gbm, "Gilde_Ground", [GRASS], smooth=False)))
mound = bmesh.new()
bmesh.ops.create_uvsphere(mound, u_segments=32, v_segments=16, radius=1)
bmesh.ops.delete(mound, geom=[v for v in mound.verts if v.co.z < -0.01], context="VERTS")
bmesh.ops.scale(mound, vec=(1.7, 1.7, 0.55), verts=mound.verts)
REAL_PILE = bpy.data.collections.get("GildebloonMoneyPile")
pile_coins = []
if REAL_PILE is not None:
    # as wide next to Gildebloon as in the game: 26 of the game's units against its 45
    pile = empty("SpawnPile")
    pile.instance_type = "COLLECTION"
    pile.instance_collection = REAL_PILE
    pile.scale = (0.6,) * 3
    mound.free()
else:
    pile = obj("SpawnPile", finish(mound, "Gilde_Mound", [GOLD_DARK]), line=0.02)
for i in range(70 if REAL_PILE is None else 0):
    a, d = rng.uniform(0, 2 * math.pi), math.sqrt(rng.random()) * 1.6
    x, y = d * math.cos(a), d * math.sin(a)
    z = 0.55 * math.sqrt(max(0.0, 1 - (x / 1.7) ** 2 - (y / 1.7) ** 2))
    pile_coins.append(obj(f"PileCoin{i}", coin, pile, loc=(x, y, z / 0.55 * 1.0 * 0.55 + 0.01),
                          rot=(rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0, 6.3)), line=0.006))
# the pile in a collection of its own too, for the map scene (tools/blender/gildebloon-map.py)
pile_col = bpy.data.collections.get("GildebloonPile") or bpy.data.collections.new("GildebloonPile")
if pile_col.name not in scene.collection.children:
    scene.collection.children.link(pile_col)
for x in [pile] + list(pile.children_recursive):
    col.objects.unlink(x)
    pile_col.objects.link(x)
for o in col.objects:
    o.visible_shadow = False
sbm2 = bmesh.new()
bmesh.ops.create_circle(sbm2, cap_ends=True, segments=48, radius=1)
# a vertex in the middle: the game's shadow fades by each corner's distance from it (export-boss.py)
bmesh.ops.poke(sbm2, faces=list(sbm2.faces))
SHADOW_SIZE = (1.55, 1.75, 1)
shadow = obj("Shadow", finish(sbm2, "Gilde_ShadowDisc", [SHADOW], smooth=False), loc=(0, 0, 0.012),
             scale=SHADOW_SIZE)
shadow.visible_shadow = False
follow = shadow.constraints.new("COPY_LOCATION")
follow.target = root
follow.use_z = False
reach = obj("Reach", torus("Gilde_ReachRing", 1.0, 0.05, RING, seg=64, segr=6), loc=(0, 0, 0.03))
reach.visible_shadow = False

sun = to_stage(link(bpy.data.objects.new("Sun", bpy.data.lights.new("Gilde_Sun", "SUN"))))
sun.data.energy = 3.2
sun.data.angle = math.radians(3)
sun.rotation_euler = Euler((math.radians(42), 0, math.radians(-35)))
cam = to_stage(link(bpy.data.objects.new("Camera", bpy.data.cameras.new("Gilde_Cam"))))
cam.data.type = "ORTHO"
cam.data.ortho_scale = 7.2
el, az, dist = math.radians(60), math.radians(-90), 20  # the game's camera looks down at 60 degrees
target = Vector((0, 0, 0.9))
cam.location = target + Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el))) * dist
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
world = bpy.data.worlds.get("Gilde_World") or bpy.data.worlds.new("Gilde_World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = lin("#9fd3f0")
world.node_tree.nodes["Background"].inputs[1].default_value = 0.25
scene.view_settings.view_transform = "Standard"
scene.render.engine = "BLENDER_EEVEE"
scene.render.fps = FPS
scene.render.resolution_x, scene.render.resolution_y = 960, 720

# effect coins, hidden (scale 0) when not flying
fx = [obj(f"FxCoin{i}", coin, scale=0.0, line=0.006) for i in range(28)]
for c in fx:
    c.visible_shadow = False

# ---- keyframe helpers
def key(o, f, loc=None, rot=None, scale=None, interp=None):
    if loc is not None:
        o.location = loc
        o.keyframe_insert("location", frame=f)
    if rot is not None:
        o.rotation_euler = rot
        o.keyframe_insert("rotation_euler", frame=f)
    if scale is not None:
        o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
        o.keyframe_insert("scale", frame=f)


def show(o, f, visible):
    o.hide_render = not visible
    o.hide_viewport = not visible
    o.keyframe_insert("hide_render", frame=f)
    o.keyframe_insert("hide_viewport", frame=f)


def rest(f):
    """Every animation starts from the same pose, so each can be played on its own"""
    key(root, f, (0, 0, HOVER), (0, 0, 0), 1)
    key(hull, f, (0, 0, 0), (0, 0, 0), 1)
    key(door, f, rot=(0, 0, 0))
    key(eyes, f, EYES_AT, scale=1.35)
    for side in ("Left", "Right"):
        key(bpy.data.objects[f"Eye{side}"], f, scale=1)
        key(bpy.data.objects[f"Pupil{side}"], f, scale=1)
        key(bpy.data.objects[f"Bag{side}"], f, rot=(0, 0, 0), scale=1)
        for k in range(2):
            key(bpy.data.objects[f"EyeX{side}{k}"], f, scale=0)
    key(mouth, f, scale=1)
    key(door, f, (0, 0.5, TOP + 0.03), scale=1)
    key(proxy, f, scale=0)
    for g in (dents, cracks, sweat, tarnish, elite_band, elite_spikes, bill):
        key(g, f, scale=0)
    key(gloss, f, scale=1)
    for side, sx in (("Left", -1), ("Right", 1)):
        key(bpy.data.objects[f"Pupil{side}"], f, (-0.03 * sx, -0.07, -0.02))
    key(ring, f, scale=1)
    key(shell, f, scale=0)
    key(reach, f, scale=0)
    key(shadow, f, scale=SHADOW_SIZE)
    for c in fx:
        key(c, f, scale=0)


def eyes_to(f, open_=1.0, pupil=1.0):
    for side in ("Left", "Right"):
        key(bpy.data.objects[f"Eye{side}"], f, scale=(1, 1, open_))
        key(bpy.data.objects[f"Pupil{side}"], f, scale=pupil)


def fly(c, f0, f1, p0, v, spin, land=True):
    """A coin thrown from p0 with speed v (units/s); it lands on the grass and lies there"""
    key(c, f0 - 1, p0, scale=0)
    g = -14.0
    p, landed = Vector(p0), False
    for f in range(f0, f1 + 1, 2):
        t = (f - f0) / FPS
        if not landed:
            p = Vector(p0) + Vector(v) * t + Vector((0, 0, 0.5 * g * t * t))
            if land and p.z < 0.03 and t > 0.1:
                p.z, landed = 0.03, True
        r = (0, 0, spin * t) if landed else (spin * t, spin * 0.7 * t, spin * 0.3 * t)
        key(c, f, tuple(p), r, 1.0)
    key(c, f1 + 1, scale=0)


def shake(o, f0, f1, amount, every=2, axis_scale=(1, 1, 0.4)):
    for f in range(f0, f1 + 1, every):
        a = amount * (f - f0) / max(1, f1 - f0) if amount > 0 else -amount
        key(o, f, rot=(rng.uniform(-a, a) * axis_scale[0], rng.uniform(-a, a) * axis_scale[1],
                       rng.uniform(-a, a) * axis_scale[2]))


# ---- the animations
for name, f0, f1, _ in ANIMS:
    scene.timeline_markers.new(name, frame=f0)
    rest(f0)

# Spawn: under the pile -> the pile trembles -> it bursts up, coins fly off -> overshoot, squash, settle, eyes open
def spawn(s0):
    """Spawn from frame 1 + s0"""
    key(root, s0 + 1, (0, 0, -1.3))
    key(shadow, s0 + 1, scale=0)
    key(shadow, s0 + 38, scale=0)
    key(shadow, s0 + 50, scale=SHADOW_SIZE)
    eyes_to(s0 + 1, 0.05)
    PS = pile.scale.x  # the pile's own size; its trembling is relative to it
    for f in range(1, 31, 3):
        key(pile, s0 + f, scale=(PS, PS, PS * (1 + 0.06 * math.sin(f * 1.7) * f / 30)))
    key(pile, s0 + 33, scale=(PS * 1.05, PS * 1.05, PS * 1.15))
    key(pile, s0 + 40, scale=(PS, PS, PS * 0.9))
    key(pile, s0 + 48, scale=PS)
    key(root, s0 + 30, (0, 0, -1.1))
    key(root, s0 + 46, (0, 0, HOVER + 0.55))
    key(hull, s0 + 46, scale=(0.9, 0.9, 1.15))
    key(root, s0 + 58, (0, 0, HOVER - 0.12))
    key(hull, s0 + 58, scale=(1.12, 1.12, 0.85))
    key(root, s0 + 68, (0, 0, HOVER))
    key(hull, s0 + 68, scale=1)
    for i, c in enumerate(fx[:16]):
        a = 2 * math.pi * i / 16 + rng.uniform(-0.2, 0.2)
        fly(c, s0 + 34 + i % 4, s0 + 80, (0.4 * math.cos(a), 0.4 * math.sin(a), 0.7),
            (2.6 * math.cos(a), 2.6 * math.sin(a), rng.uniform(4.5, 6.5)), rng.uniform(8, 14))
    eyes_to(s0 + 62, 0.05)
    eyes_to(s0 + 66, 1.15)
    eyes_to(s0 + 70, 1.0)
    key(ring, s0 + 40, scale=0.2)
    key(ring, s0 + 66, scale=1.1)
    key(ring, s0 + 74, scale=1)
    for o in [pile] + pile_coins:  # hiding a parent doesn't hide its children in a render
        show(o, s0 + 1, True)
        show(o, s0 + 85, False)


spawn(0)
key(dial, 1, rot=(0, 0, 0))

# Idle: bob, sway; the ring and dial turn all the time (below)
for f in range(85, 133, 6):
    t = (f - 85) / 48 * 2 * math.pi
    key(root, f, (0, 0, HOVER + 0.07 * math.sin(t)), (0.03 * math.sin(t + 1), 0.04 * math.sin(t), 0))
    for side, sx in (("Left", -1), ("Right", 1)):
        key(bpy.data.objects[f"Bag{side}"], f, rot=(0.08 * math.sin(t + sx), 0.06 * sx * math.sin(t), 0))
eyes_to(118, 1.0)
eyes_to(120, 0.1)  # a blink
eyes_to(123, 1.0)

# Hit: squash, knocked back, squint, a coin knocked loose
key(hull, 134, scale=(1.12, 1.12, 0.82))
key(root, 134, (0, 0.12, HOVER - 0.05), (0.12, 0, 0))
eyes_to(134, 0.25, 0.8)
key(hull, 138, scale=(0.96, 0.96, 1.06))
key(root, 140, (0, 0.04, HOVER), (0, 0, 0))
key(hull, 143, scale=1)
eyes_to(142, 1.0)
fly(fx[16], 134, 144, (0.6, -0.3, HOVER + 0.6), (1.8, -1.2, 3.5), 12, land=False)

# Toll: the slot opens wide, coins fly in from the map, the bags swell, pleased eyes
key(mouth, 150, scale=(1.5, 1, 2.4))
eyes_to(150, 0.55, 1.2)
for i, c in enumerate(fx[:10]):
    a = math.radians(-90) + rng.uniform(-1.1, 1.1)
    start = Vector((3.0 * math.cos(a), 3.0 * math.sin(a), 0.3))
    end = Vector((0, -1.25, HOVER - 0.2))
    fs = 148 + i * 2
    key(c, fs - 1, tuple(start), scale=0)
    for k in range(0, 13, 2):
        t = k / 12
        p = start.lerp(end, t) + Vector((0, 0, 1.4 * math.sin(math.pi * t)))
        key(c, fs + k, tuple(p), (t * 9, t * 5, 0), 1.0 if t < 1 else 0.3)
    key(c, fs + 13, scale=0)
for side in ("Left", "Right"):
    b = bpy.data.objects[f"Bag{side}"]
    key(b, 162, scale=1.0)
    key(b, 168, scale=1.2)
    key(b, 174, scale=1.1)
key(hull, 168, scale=(1.04, 1.04, 0.97))
key(hull, 176, scale=1)
key(mouth, 172, scale=1)
eyes_to(176, 1.0)

# Repossession (a skull): shakes, wide eyes, the door bursts open, coins spray, its reach rings out, the door slams
eyes_to(184, 1.35, 0.6)
shake(root, 182, 196, 0.12)
key(root, 196, (0, 0, HOVER + 0.3), (0, 0, 0))
key(door, 196, rot=(0, 0, 0))
key(door, 200, rot=(math.radians(-115), 0, 0))
key(dial, 196, rot=(0, 0, math.radians(720 + 300)))
for i, c in enumerate(fx):
    a = 2 * math.pi * i / len(fx) + rng.uniform(-0.15, 0.15)
    fly(c, 199 + i % 6, 250, (0, -0.1, HOVER + 1.2), (3.4 * math.cos(a), 3.4 * math.sin(a), rng.uniform(5, 8)),
        rng.uniform(8, 16))
key(reach, 198, scale=0.4)
key(reach, 222, scale=4.2)
key(reach, 232, scale=4.4)
key(reach, 236, scale=0)
for side in ("Left", "Right"):
    b = bpy.data.objects[f"Bag{side}"]
    key(b, 204, scale=1.0)
    key(b, 214, scale=1.25)
    key(b, 240, scale=1.15)
key(door, 236, rot=(math.radians(-115), 0, 0))
key(door, 240, rot=(math.radians(8), 0, 0))
key(door, 243, rot=(0, 0, 0))
key(hull, 240, scale=(1.1, 1.1, 0.86))
key(root, 240, (0, 0, HOVER - 0.08))
key(hull, 248, scale=1)
key(root, 252, (0, 0, HOVER))
eyes_to(244, 0.6, 1.0)
eyes_to(256, 1.0)

# Skittish: winds up backwards, stretches and darts ahead along the track (-Y), settles
key(root, 271, (0, 0.35, HOVER - 0.05), (math.radians(-14), 0, 0))
key(hull, 271, scale=(1.08, 0.88, 1.02))
eyes_to(271, 1.25, 0.7)
key(root, 276, (0, -2.0, HOVER), (math.radians(12), 0, 0))
key(hull, 276, scale=(0.9, 1.28, 0.95))
key(root, 282, (0, -2.4, HOVER), (0, 0, 0))
key(hull, 282, scale=1)
key(ring, 271, scale=0.85)
key(ring, 280, scale=1.1)
key(ring, 286, scale=1)
eyes_to(284, 1.0)

# VaultBreak: the shield builds, shimmers, takes the last hit, swells and shatters
key(shell, 290, scale=0.0)
key(shell, 298, scale=1.08)
key(shell, 302, scale=1.0)
shell.rotation_euler = (0, 0, 0)
shell.keyframe_insert("rotation_euler", frame=290)
for f in range(304, 318, 2):
    key(shell, f, scale=1.0 + 0.03 * ((f // 2) % 2))
key(shell, 318, scale=1.0, rot=(0, 0, math.radians(40)))
key(shell, 322, scale=1.3)
key(shell, 324, scale=0.0)
key(hull, 322, scale=(1.08, 1.08, 0.9))
eyes_to(322, 0.3, 0.8)
key(hull, 330, scale=1)
eyes_to(332, 1.0)
for i, c in enumerate(fx[:14]):
    a = 2 * math.pi * i / 14
    fly(c, 322, 336, (1.9 * math.cos(a), 2.05 * math.sin(a), HOVER), (2.5 * math.cos(a), 2.5 * math.sin(a), 2.5),
        10, land=False)

# Death: wobbles harder and harder, crossed eyes, door flies off, bags burst, it deflates and drops, coins rain
eyes_to(340, 1.4, 0.5)
shake(root, 340, 372, 0.25)
key(hull, 340, scale=1)
for f in range(344, 373, 4):
    s = 1 + 0.06 * ((f // 4) % 2) * (f - 340) / 32
    key(hull, f, scale=(s, s, 2 - s))
key(root, 372, (0, 0, HOVER + 0.15), (0, 0, 0))
eyes_to(372, 1.0, 0.0)  # the pupils go, the crossed eyes (children of the eyes) show
for side in ("Left", "Right"):
    for k in range(2):
        key(bpy.data.objects[f"EyeX{side}{k}"], 371, scale=0)
        key(bpy.data.objects[f"EyeX{side}{k}"], 373, scale=1)
    b = bpy.data.objects[f"Bag{side}"]
    key(b, 372, scale=1.0)
    key(b, 376, scale=1.45)
    key(b, 379, scale=0.0)
key(door, 372, (0, 0.5, TOP + 0.03), (0, 0, 0))
for k, f in enumerate(range(374, 405, 3)):
    t = (f - 373) / FPS
    key(door, f, (0, 0.5 + 2.2 * t, TOP + 0.03 + 7 * t - 7 * t * t), (math.radians(-260) * t * 2, math.radians(90) * t, 0))
key(door, 405, scale=1)
key(door, 410, scale=0)
key(door, 336, scale=1)
key(door, 432, scale=0)  # rest() puts it back at the next animation
key(hull, 376, scale=(1.15, 1.15, 0.9))
key(hull, 392, scale=(0.55, 0.55, 0.3))
key(root, 392, (0, 0, 0.35))
key(hull, 402, scale=0.0)
key(shadow, 376, scale=SHADOW_SIZE)
key(shadow, 392, scale=(0.7, 0.8, 1))
key(shadow, 402, scale=0)
key(ring, 372, scale=1.0)
key(ring, 380, scale=1.6)
key(ring, 384, scale=0.0)
for i, c in enumerate(fx):
    a = 2 * math.pi * i / len(fx) + rng.uniform(-0.2, 0.2)
    sp = rng.uniform(0.6, 1.9)  # the payout lands in a heap where it fell
    fly(c, 375 + i % 5, 431, (0, 0, HOVER + 0.5), (sp * math.cos(a), sp * math.sin(a), rng.uniform(6, 9)),
        rng.uniform(8, 16))
# the door's fall resets at the next play; put it back on the hull for frame 1 again
key(door, 1, (0, 0.5, TOP + 0.03), scale=1)

def bob(f0, f1, depth=0.07, base=HOVER, sway=0.03):
    """Idle's hover over f0..f1, around a height"""
    for f in range(f0, f1 + 1, 6):
        t = (f - f0) / (f1 - f0) * 2 * math.pi
        key(root, f, (0, 0, base + depth * math.sin(t)), (sway * math.sin(t + 1), 0.04 * math.sin(t), 0))


def hull_point(x, y, lift=0.0):
    """Where a point on the hull is in the world while Gildebloon hovers at rest"""
    pt, n = on_hull(x, y)
    return Vector((0, 0, HOVER)) + pt + n * lift


# Damaged66: dents, a wince now and then; loops like Idle
f0, f1 = 433, 480
bob(f0, f1)
key(dents, f0, scale=1)
eyes_to(f0, 0.9)
eyes_to(455, 0.9)
eyes_to(458, 0.35, 0.85)  # a wince
key(hull, 455, scale=1)
key(hull, 458, scale=(1.05, 1.05, 0.93))
key(hull, 464, scale=1)
eyes_to(464, 0.9)

# Damaged33: cracks, the door rattling ajar, sweat running down; it hangs lower
f0, f1 = 481, 528
bob(f0, f1, 0.05, HOVER - 0.1, 0.05)
for g in (dents, cracks, sweat):
    key(g, f0, scale=1)
eyes_to(f0, 0.75, 0.9)
for side, sx in (("Left", -1), ("Right", 1)):
    key(bpy.data.objects[f"Pupil{side}"], f0, (0.0 * sx, -0.07, -0.05))  # looking worried, down
for f in range(f0, f1 + 1, 4):
    key(door, f, rot=(math.radians(-10 - 6 * ((f // 4) % 2)), 0, 0))
key(sweat, f0, (0.62, -0.78, 0.5))
key(sweat, f0 + 28, (0.68, -0.86, 0.22))
key(sweat, f0 + 30, (0.62, -0.78, 0.5), scale=0)
key(sweat, f0 + 34, scale=1)
key(sweat, f1, (0.66, -0.83, 0.33))

# Damaged10: sags lower and trembles, coins trickle out of the cracks
f0, f1 = 529, 576
bob(f0, f1, 0.03, HOVER - 0.2, 0.02)
shake(hull, f0 + 1, f1, -0.025, every=2)
for g in (dents, cracks, sweat):
    key(g, f0, scale=1)
for side in ("Left", "Right"):
    key(bpy.data.objects[f"Bag{side}"], f0, scale=0.65)  # the bags empty with its health (as in the game)
eyes_to(f0, 0.6, 0.8)
for f in range(f0, f1 + 1, 3):
    key(door, f, rot=(math.radians(-22 - 8 * ((f // 3) % 2)), 0, 0))
key(sweat, f0, (0.62, -0.78, 0.5))
key(sweat, f0 + 20, (0.68, -0.86, 0.22))
key(sweat, f0 + 22, (0.62, -0.78, 0.5), scale=0)
key(sweat, f0 + 25, scale=1)
key(sweat, f1, (0.68, -0.86, 0.22))
for i, c in enumerate(fx[:9]):
    cx, cy = CRACKS[i % 2][1 + i % 2]
    start = hull_point(cx, cy) + Vector((0, 0, -0.2))
    fly(c, f0 + 2 + i * 5, min(f1 - 1, f0 + 2 + i * 5 + 16), tuple(start),
        (0.6 * (1 if cx > 0 else -1), -0.4, 0.8), 9)

# Dispatch: the door opens, a gold MOAB pops out, arcs ahead onto the track, the door shuts
f0, f1 = 577, 624
eyes_to(f0, 1.0)
eyes_to(582, 0.55, 1.0)  # a sly squint
key(door, f0 + 2, rot=(0, 0, 0))
key(door, f0 + 8, rot=(math.radians(-105), 0, 0))
key(door, f0 + 32, rot=(math.radians(-105), 0, 0))
key(door, f0 + 37, rot=(math.radians(6), 0, 0))
key(door, f0 + 40, rot=(0, 0, 0))
key(hull, f0 + 9, scale=(1.06, 1.06, 0.9))  # recoil as it fires
key(root, f0 + 9, (0, 0.1, HOVER - 0.06))
key(hull, f0 + 15, scale=1)
key(root, f0 + 16, (0, 0, HOVER))
start = Vector((0, 0.05, HOVER + 0.85))
end = Vector((1.3, -2.3, 0.45))  # ahead and to the side, clear of its face
key(proxy, f0 + 8, tuple(start), (0, 0, 0), 0)
for k in range(0, 23, 2):
    t = k / 22
    pos = start.lerp(end, t) + Vector((0, 0, 2.6 * math.sin(math.pi * t)))
    key(proxy, f0 + 9 + k, tuple(pos), (math.radians(-35) * math.cos(math.pi * t), 0, 0), 0.35 + 0.65 * min(1, t * 2))
key(proxy, f0 + 34, tuple(end + Vector((0, 0, -0.1))), (0, 0, 0), (1.1, 1.1, 0.85))  # lands, squashes
key(proxy, f0 + 38, tuple(end), (0, 0, 0), 1)
key(proxy, f1, tuple(end + Vector((0, -0.6, 0))), (0, 0, 0), 1)  # off along the track
eyes_to(f0 + 40, 1.0)

# Escape: at the exit it grins, stuffs coins into its bags, crouches and dives out of the map
f0, f1 = 625, 696
key(mouth, f0 + 4, scale=(1.8, 1, 2.2))
eyes_to(f0 + 4, 0.45, 1.1)
for i, c in enumerate(fx[:12]):
    side = -1 if i % 2 == 0 else 1
    a = math.radians(-90) + side * 0.9 + (i // 2) * 0.12
    st = Vector((2.6 * math.cos(a), 2.6 * math.sin(a), 0.3))
    en = Vector((1.12 * side, 0.05, HOVER + 0.3))
    fs = f0 + 2 + i * 2
    key(c, fs - 1, tuple(st), scale=0)
    for k in range(0, 13, 2):
        t = k / 12
        pt = st.lerp(en, t) + Vector((0, 0, 1.2 * math.sin(math.pi * t)))
        key(c, fs + k, tuple(pt), (t * 9, t * 5, 0), 1.0 if t < 1 else 0.3)
    key(c, fs + 13, scale=0)
for side in ("Left", "Right"):
    b = bpy.data.objects[f"Bag{side}"]
    key(b, f0 + 10, scale=1.0)
    key(b, f0 + 30, scale=1.35)
key(root, f0 + 34, (0, 0, HOVER))
key(root, f0 + 42, (0, 0.2, HOVER + 0.45), (math.radians(-15), 0, 0))  # winds up
key(hull, f0 + 42, scale=(1.08, 1.08, 0.88))
key(root, f0 + 56, (0, -1.6, -0.6), (math.radians(55), 0, 0))  # dives out
key(hull, f0 + 50, scale=(0.9, 0.9, 1.15))
key(root, f0 + 62, (0, -2.0, -1.6), (math.radians(70), 0, 0))
key(hull, f0 + 62, scale=0.0)
key(shadow, f0 + 42, scale=SHADOW_SIZE)
key(shadow, f0 + 60, scale=0)
eyes_to(f0 + 44, 1.2, 0.9)

# TarnishHit: a hit that tarnishes: grey-green blotches flash on, a grimace, they fade
f0, f1 = 697, 724
key(hull, f0 + 1, scale=(1.14, 1.14, 0.8))
key(root, f0 + 1, (0, 0.14, HOVER - 0.06), (0.14, 0, 0))
key(tarnish, f0, scale=0)
key(tarnish, f0 + 2, scale=1.15)
key(tarnish, f0 + 5, scale=1)
key(tarnish, f0 + 16, scale=1)
key(tarnish, f0 + 24, scale=0)
key(gloss, f0 + 2, scale=0)  # tarnished gold doesn't shine
key(gloss, f0 + 18, scale=0)
key(gloss, f0 + 24, scale=1)
eyes_to(f0 + 2, 0.15, 0.7)
key(hull, f0 + 6, scale=(0.95, 0.95, 1.07))
key(root, f0 + 9, (0, 0.04, HOVER), (0, 0, 0))
key(hull, f0 + 11, scale=1)
eyes_to(f0 + 14, 0.5, 0.9)
eyes_to(f0 + 24, 1.0)

# Interest (Elite): a few coins drop in through the door, the Vault pulses once, bags swell a little
f0, f1 = 725, 756
for g in (elite_band, elite_spikes):
    key(g, f0, scale=1)  # only Elite has Interest
key(door, f0 + 2, rot=(0, 0, 0))
key(door, f0 + 6, rot=(math.radians(-45), 0, 0))
key(door, f0 + 18, rot=(math.radians(-45), 0, 0))
key(door, f0 + 21, rot=(0, 0, 0))
for i, c in enumerate(fx[:4]):
    st = Vector((0.4 * (i - 1.5), -0.4, HOVER + 2.6))
    key(c, f0 + 4 + i * 2 - 1, tuple(st), scale=0)
    key(c, f0 + 4 + i * 2, tuple(st), (0, 0, 0), 1)
    key(c, f0 + 12 + i * 2, (0, 0.1, HOVER + 0.85), (6, 3, 0), 0.6)
    key(c, f0 + 13 + i * 2, scale=0)
key(shell, f0 + 12, scale=0)
key(shell, f0 + 16, scale=1.05)
key(shell, f0 + 22, scale=1.0)
key(shell, f0 + 28, scale=0)
for side in ("Left", "Right"):
    b = bpy.data.objects[f"Bag{side}"]
    key(b, f0 + 14, scale=1.0)
    key(b, f0 + 20, scale=1.1)
    key(b, f0 + 28, scale=1.05)
eyes_to(f0 + 12, 0.6, 1.15)
eyes_to(f0 + 28, 1.0)

# VaultUp: coins rise out of the bags and spiral up into the shield, which grows round it
f0, f1 = 757, 792
for i, c in enumerate(fx[:16]):
    side = -1 if i % 2 == 0 else 1
    st = Vector((1.12 * side, 0.05, HOVER + 0.3))
    fs = f0 + 1 + i
    key(c, fs - 1, tuple(st), scale=0)
    for k in range(0, 15, 2):
        t = k / 14
        a = side * 0.5 + t * 4.5 + i * 0.4
        pt = Vector((1.8 * math.cos(a) * t + st.x * (1 - t), 2.0 * math.sin(a) * t + st.y * (1 - t),
                     HOVER + 0.3 + 1.0 * t))
        key(c, fs + k, tuple(pt), (t * 8, t * 4, 0), 1.0 if t < 1 else 0.2)
    key(c, fs + 15, scale=0)
key(shell, f0 + 6, scale=0)
key(shell, f0 + 14, scale=0.5)
key(shell, f0 + 24, scale=1.08)
key(shell, f0 + 30, scale=1.0)
key(shell, f1, scale=1.0)
for side in ("Left", "Right"):
    b = bpy.data.objects[f"Bag{side}"]
    key(b, f0 + 2, scale=1.0)
    key(b, f0 + 16, scale=0.85)
    key(b, f0 + 28, scale=1.0)
eyes_to(f0 + 8, 1.1, 0.8)
eyes_to(f0 + 30, 0.7, 1.0)  # pleased with its shield

# EliteSpawn: Spawn, with the red band on, then the crown of spikes shoots up and the band flares
f0, f1 = 793, 876
spawn(f0 - 1)
key(elite_band, f0, scale=1)
key(elite_spikes, f0, scale=(1, 1, 0))
key(elite_spikes, f0 + 70, scale=(1, 1, 0))
key(elite_spikes, f0 + 74, scale=(1, 1, 1.35))
key(elite_spikes, f0 + 78, scale=1)
key(elite_band, f0 + 70, scale=1)
key(elite_band, f0 + 74, scale=1.06)
key(elite_band, f0 + 80, scale=1)
eyes_to(f0 + 72, 1.0)
eyes_to(f0 + 74, 0.5, 0.8)  # a mean squint as the crown goes up
key(hull, f0 + 70, scale=1)
key(hull, f0 + 74, scale=(0.95, 0.95, 1.08))
key(hull, f0 + 80, scale=1)

# Payout: beaten; the heap it left fountains up and out and is gone. Which way the cash is on the screen depends on
# how Gildebloon faces, so the clip throws them every way and the mod flies coins on screen from here to the cash
# (Visuals/GildebloonHud.cs)
f0, f1 = 877, 924
key(root, f0, scale=0)  # its body is gone already (Death)
key(shadow, f0, scale=0)
for i, c in enumerate(fx):
    a = 2 * math.pi * i / len(fx) + rng.uniform(-0.2, 0.2)
    st = Vector((0.3 * math.cos(a), 0.3 * math.sin(a), 0.05))
    out = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.9, 1.5)
    rise = rng.uniform(2.2, 3.0)
    fs = f0 + 2 + round(i * 0.6)  # all of them done by the clip's end
    key(c, fs - 1, tuple(st), (0, 0, 0), 0)
    key(c, fs, tuple(st), (0, 0, 0), 1)
    for k in range(2, 23, 2):
        t = k / 22
        pt = st + out * t + Vector((0, 0, rise * t - 1.6 * t * t))
        key(c, fs + k, tuple(pt), (t * 14, t * 7, 0), 1.0 if t < 0.7 else 1.0 - (t - 0.7) / 0.3 * 0.9)
    key(c, fs + 23, scale=0)

# IdleCount: flips a coin out of the door, eyes follow it, catches it back in
f0, f1 = 925, 972
bob(f0, f1)
c = fx[0]
key(door, f0 + 6, rot=(0, 0, 0))
key(door, f0 + 10, rot=(math.radians(-40), 0, 0))
key(door, f0 + 34, rot=(math.radians(-40), 0, 0))
key(door, f0 + 38, rot=(0, 0, 0))
top = Vector((0, 0.1, HOVER + 0.85))
key(c, f0 + 9, tuple(top), scale=0)
for k in range(0, 25, 2):
    t = k / 24
    key(c, f0 + 10 + k, tuple(top + Vector((0, -0.2 * t, 1.4 * math.sin(math.pi * t)))), (t * 25, 0, 0), 1)
key(c, f0 + 35, scale=0)
for side, sx in (("Left", -1), ("Right", 1)):
    pu = bpy.data.objects[f"Pupil{side}"]
    key(pu, f0 + 10, (-0.03 * sx, -0.07, -0.02))
    key(pu, f0 + 20, (-0.03 * sx, -0.07, 0.07))  # up at the coin
    key(pu, f0 + 32, (-0.03 * sx, -0.07, -0.02))
eyes_to(f0 + 36, 0.55, 1.1)  # pleased
eyes_to(f1 - 4, 1.0)

# IdlePolish: a banknote rubs the shine in little circles, the shine glints, eyes closed in content
f0, f1 = 973, 1020
bob(f0, f1, 0.04)
gp, gn = on_hull(-0.62, -0.5)
rot_b = gn.to_track_quat("Z", "Y").to_euler()
key(bill, f0, tuple(gp + gn * 0.05), rot_b, 0)
key(bill, f0 + 4, tuple(gp + gn * 0.05), rot_b, 1)
for k in range(0, 33, 3):
    a = k / 32 * 2 * math.pi * 3
    off = Vector((0.12 * math.cos(a), 0.1 * math.sin(a), 0))
    q, n2 = on_hull(-0.62 + off.x, -0.5 + off.y)
    key(bill, f0 + 4 + k, tuple(q + n2 * 0.05), rot_b, 1)
key(bill, f0 + 40, scale=0)
eyes_to(f0 + 6, 0.12)
eyes_to(f0 + 36, 0.12)
key(gloss, f0 + 37, scale=1)
key(gloss, f0 + 40, scale=1.5)
key(gloss, f0 + 44, scale=1)
eyes_to(f0 + 40, 0.8, 1.1)

# IdleSmug: leans back, half-closed eyes, a wide slot, chuckles: quick little bounces, bags jiggling
f0, f1 = 1021, 1068
key(root, f0 + 6, (0, 0.1, HOVER), (math.radians(-12), 0, 0))
key(mouth, f0 + 6, scale=(1.35, 1, 1.4))
eyes_to(f0 + 6, 0.45, 0.85)
for k, f in enumerate(range(f0 + 10, f0 + 34, 3)):
    up = 0.06 if k % 2 == 0 else 0.0
    key(root, f, (0, 0.1, HOVER + up), (math.radians(-12), 0, 0))
    for side in ("Left", "Right"):
        key(bpy.data.objects[f"Bag{side}"], f, scale=1.08 if k % 2 == 0 else 0.97)
key(root, f1 - 4, (0, 0, HOVER), (0, 0, 0))
key(mouth, f1 - 4, scale=1)
eyes_to(f1 - 4, 1.0)

# the dial and coin ring turn all the time
key(ring, 1, rot=(0, 0, 0))
END = ANIMS[-1][2]
key(ring, END, rot=(0, 0, math.radians(-45 * END / FPS)))
key(dial, END, rot=(0, 0, math.radians(720 + 300 + 120 * (END - 132) / FPS)))
for o in (ring, dial):
    for fc in o.animation_data.action.fcurves if hasattr(o.animation_data.action, "fcurves") else []:
        if fc.data_path == "rotation_euler" and fc.array_index == 2:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"



def fcurves(o):
    """An object's F-curves, from a plain action or a layered one (Blender 4.4+)"""
    act = o.animation_data.action if o.animation_data else None
    if act is None:
        return []
    if hasattr(act, "fcurves") and len(act.fcurves):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, "layers", []):
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                out.extend(bag.fcurves)
    return out


# Each animation stands on its own: a key on its last frame holds what it ends on, so nothing slides towards the next
# one's rest pose (which matters once they are cut into clips for the game)
for name, a0, a1, _ in ANIMS[:-1]:
    for o in scene.objects:
        for fc in fcurves(o):
            if fc.data_path in ("hide_render", "hide_viewport"):
                continue
            inside = [kp for kp in fc.keyframe_points if a0 - 0.5 <= kp.co.x <= a1 + 0.5]
            if not inside or any(abs(kp.co.x - a1) < 0.5 for kp in inside):
                continue
            last = max(inside, key=lambda kp: kp.co.x)
            fc.keyframe_points.insert(a1, last.co.y, options={"FAST"})
    for o in scene.objects:
        for fc in fcurves(o):
            fc.update()

scene.frame_start, scene.frame_end = 1, END
scene.frame_set(100)
scene["gildebloon_anims"] = str([(n, a, b) for n, a, b, _ in ANIMS])
result = {"scene": scene.name, "objects": len(scene.objects), "anims": [(n, a, b) for n, a, b, _ in ANIMS]}
