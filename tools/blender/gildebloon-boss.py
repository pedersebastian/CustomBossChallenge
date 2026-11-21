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
from mathutils import Vector, Euler

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
]


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*c, 1.0)


# ---- its own scene, emptied on a rerun
scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
for o in list(scene.collection.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.actions):
    for d in list(coll):
        if d.users == 0:
            coll.remove(d)
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


GOLD = toon("Gilde_Gold", "#e58e00", "#ffcf26", "#fff6b8", cuts=(0.3, 0.82))
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
    r, t = 0.13, 0.034
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=r, radius2=r, depth=t)
    caps = [f for f in bm.faces if abs(f.normal.z) > 0.9]
    bmesh.ops.inset_individual(bm, faces=caps, thickness=0.024, depth=-0.005)
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
EYES_AT = (0, -1.02, 0.42)
eyes = empty("Eyes", hull, loc=EYES_AT)
eyes.rotation_euler = (-0.62, 0, 0)  # faces the game's camera, which looks down at 60 degrees
eyes.scale = (1.25,) * 3
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
dollar = text_mesh("$", 0.5, 0.03, GREEN)
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
    obj(f"Bag{side}Dollar", dollar, b, loc=(0.05 * sx, -0.4, 0.02), rot=(math.pi / 2 - 0.35, 0, 0), line=0.012)

# a ring of coins circling it
coin = coin_mesh()
ring = empty("CoinRing", root, loc=(0, 0, -0.15))
for i in range(12):
    a = 2 * math.pi * i / 12
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

# ---- the stage: grass, the pile it rises from, its reach on the ground, sun, camera
gbm = bmesh.new()
bmesh.ops.create_circle(gbm, cap_ends=True, segments=64, radius=8)
to_stage(obj("Ground", finish(gbm, "Gilde_Ground", [GRASS], smooth=False)))
mound = bmesh.new()
bmesh.ops.create_uvsphere(mound, u_segments=32, v_segments=16, radius=1)
bmesh.ops.delete(mound, geom=[v for v in mound.verts if v.co.z < -0.01], context="VERTS")
bmesh.ops.scale(mound, vec=(1.7, 1.7, 0.55), verts=mound.verts)
pile = obj("SpawnPile", finish(mound, "Gilde_Mound", [GOLD_DARK]), line=0.02)
pile_coins = []
for i in range(70):
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
    key(eyes, f, EYES_AT, scale=1.25)
    for side in ("Left", "Right"):
        key(bpy.data.objects[f"Eye{side}"], f, scale=1)
        key(bpy.data.objects[f"Pupil{side}"], f, scale=1)
        key(bpy.data.objects[f"Bag{side}"], f, rot=(0, 0, 0), scale=1)
        for k in range(2):
            key(bpy.data.objects[f"EyeX{side}{k}"], f, scale=0)
    key(mouth, f, scale=1)
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
key(root, 1, (0, 0, -1.3))
key(shadow, 1, scale=0)
key(shadow, 38, scale=0)
key(shadow, 50, scale=SHADOW_SIZE)
eyes_to(1, 0.05)
for f in range(1, 31, 3):
    key(pile, f, scale=(1, 1, 1 + 0.06 * math.sin(f * 1.7) * f / 30))
key(pile, 33, scale=(1.05, 1.05, 1.15))
key(pile, 40, scale=(1, 1, 0.9))
key(pile, 48, scale=1)
key(root, 30, (0, 0, -1.1))
key(root, 46, (0, 0, HOVER + 0.55))
key(hull, 46, scale=(0.9, 0.9, 1.15))
key(root, 58, (0, 0, HOVER - 0.12))
key(hull, 58, scale=(1.12, 1.12, 0.85))
key(root, 68, (0, 0, HOVER))
key(hull, 68, scale=1)
for i, c in enumerate(fx[:16]):
    a = 2 * math.pi * i / 16 + rng.uniform(-0.2, 0.2)
    fly(c, 34 + i % 4, 80, (0.4 * math.cos(a), 0.4 * math.sin(a), 0.7),
        (2.6 * math.cos(a), 2.6 * math.sin(a), rng.uniform(4.5, 6.5)), rng.uniform(8, 14))
eyes_to(62, 0.05)
eyes_to(66, 1.15)
eyes_to(70, 1.0)
key(ring, 40, scale=0.2)
key(ring, 66, scale=1.1)
key(ring, 74, scale=1)
key(dial, 1, rot=(0, 0, 0))
key(dial, 84, rot=(0, 0, math.radians(720)))

for o in [pile] + pile_coins:  # hiding a parent doesn't hide its children in a render
    show(o, 1, True)
    show(o, 85, False)

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
key(door, 432, scale=0)
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

# the dial and coin ring turn all the time
key(ring, 1, rot=(0, 0, 0))
key(ring, 432, rot=(0, 0, math.radians(-45 * 432 / FPS)))
key(dial, 432, rot=(0, 0, math.radians(720 + 300 + 120 * 300 / FPS)))
for o in (ring, dial):
    for fc in o.animation_data.action.fcurves if hasattr(o.animation_data.action, "fcurves") else []:
        if fc.data_path == "rotation_euler" and fc.array_index == 2:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"

scene.frame_start, scene.frame_end = 1, 432
scene.frame_set(100)
scene["gildebloon_anims"] = str([(n, a, b) for n, a, b, _ in ANIMS])
result = {"scene": scene.name, "objects": len(scene.objects), "anims": [(n, a, b) for n, a, b, _ in ANIMS]}
