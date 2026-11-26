"""
The mystery map effects for a boss Random picked that is still a secret (#285), in the style of the die the Random
tiers show (ivory with dark brown pips, toon-shaded with dark outlines). The game's own boss effects are switched off
while the boss is a secret (#283); these take their place:

- the effect round the map (in place of <Boss>AmbientMapFx): a soft ivory haze coming in from the screen's edges, with
  a few tiny pips twinkling in it. Subtle: the mod breathes the haze and twinkles the pips.
- the effect at the boss's spawn point (in place of <Boss>TrackFx): three small dice on a faint glow. The mod makes them
  hop, tumble and land on new faces every few seconds.

Builds a scene "Mystery Fx" in the open Blender, then export-mystery.py writes it for the mod:

    python tools/blender/run-in-blender.py tools/blender/mystery-fx.py
    python tools/blender/run-in-blender.py tools/blender/export-mystery.py

or without an open Blender: blender --background --factory-startup --python tools/blender/build-mystery.py

Sizes: the haze frames a screen of W x H = 18 x 13.6 units, which the mod stretches over the camera's view. A die is
1 unit across; the mod scales the dice to the game's units (MysteryMapFx.DieSize).
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

SCENE = "Mystery Fx"
W, H = 18.0, 13.6
BAND = 1.7  # how far the haze reaches in from the edge
rng = random.Random(285)


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*c, 1.0)


scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
for o in list(scene.collection.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials):
    for d in list(coll):
        if d.users == 0:
            coll.remove(d)
if bpy.context.window is not None:
    bpy.context.window.scene = scene
col = scene.collection


def toon(name, shade, base, light, cuts=(0.42, 0.9)):
    """Toon bands as Gildebloon's: diffuse light through a constant ramp; export-mystery.py bakes the ramp's colours"""
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
    m["mystery_kind"] = 0
    return m


def flat(name, colour):
    m = toon(name, colour, colour, None, cuts=(0.0, 1.0))
    return m


def fading(name, colour, alpha):
    """See-through, fading out along the mesh's "fade" attribute (0 full, 1 gone); the mod's ramp row kind 1"""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    if hasattr(m, "surface_render_method"):
        m.surface_render_method = "BLENDED"
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "fade"
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    sq = nt.nodes.new("ShaderNodeMath")
    sq.operation = "POWER"
    sq.inputs[1].default_value = 2.0
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = alpha
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs[0].default_value = lin(colour)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(attr.outputs["Fac"], inv.inputs[1])
    nt.links.new(inv.outputs[0], sq.inputs[0])
    nt.links.new(sq.outputs[0], mul.inputs[0])
    nt.links.new(mul.outputs[0], mix.inputs[0])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    m.diffuse_color = lin(colour)
    m["mystery_kind"] = 1
    m["mystery_alpha"] = alpha
    m["mystery_colour"] = colour
    return m


# The die of the Random icon: light ivory on top, cream in front, warm tan in the shade, dark brown pips
IVORY = toon("Mystery_Ivory", "#c4b49b", "#e7e0cd", "#f7f4e6", cuts=(0.45, 0.82))
PIP = toon("Mystery_Pip", "#2c1f18", "#3d2b22", None, cuts=(0.45, 1.0))
LINE = flat("Mystery_Outline", "#2a1d16")
LINE.use_backface_culling = True  # the shell's faces point inwards: only its far side shows, round the die
HAZE = fading("Mystery_Haze", "#fff5dc", 0.32)
GLOW = fading("Mystery_Glow", "#fff3d0", 0.5)
TWINKLE = fading("Mystery_Twinkle", "#fffbea", 0.9)


def outline(o, width):
    mats = o.data.materials
    n = len(mats)
    mats.append(LINE)
    mod = o.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = width
    mod.offset = 1
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = n
    mod.material_offset_rim = n


def obj(name, bm, mats, smooth=True, loc=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    o.location = loc
    col.objects.link(o)
    return o


def set_fade(o, fades):
    """The "fade" of each vertex (0 at full strength, 1 gone), which the see-through material and the export read"""
    a = o.data.attributes.new("fade", "FLOAT", "POINT")
    for i, f in enumerate(fades):
        a.data[i].value = f


# ---- the die: a rounded cube 1 across, pips as dark dots set a little into its faces (opposite faces add up to 7)
def die_mesh():
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=0.14, segments=4, affect="EDGES", profile=0.5)
    for f in bm.faces:
        f.material_index = 0
    faces = {  # normal: pips (on the face's own u, v, from -1 to 1)
        (0, 0, 1): [(0, 0)],
        (0, 0, -1): [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)],
        (1, 0, 0): [(-1, -1), (1, 1)],
        (-1, 0, 0): [(-1, -1), (1, 1), (-1, 1), (1, -1), (0, 0)],
        (0, 1, 0): [(-1, -1), (0, 0), (1, 1)],
        (0, -1, 0): [(-1, -1), (1, 1), (-1, 1), (1, -1)],
    }
    for normal, pips in faces.items():
        n = Vector(normal)
        u = Vector((0, 0, 1)) if abs(n.z) < 0.5 else Vector((1, 0, 0))
        u = (u - n * u.dot(n)).normalized()
        v = n.cross(u)
        for pu, pv in pips:
            centre = n * 0.5 + u * (pu * 0.25) + v * (pv * 0.25)
            res = bmesh.ops.create_circle(bm, cap_ends=True, segments=16, radius=0.085)
            verts = res["verts"]
            rot = Matrix((u, v, n)).transposed()
            bmesh.ops.transform(bm, matrix=rot.to_4x4(), verts=verts)
            bmesh.ops.translate(bm, vec=centre + n * 0.004, verts=verts)
            for f in {f for vert in verts for f in vert.link_faces}:
                f.material_index = 1
                f.smooth = False
    return bm


die = obj("Die", die_mesh(), [IVORY, PIP], smooth=True)
outline(die, 0.045)

# Where the three dice lie, on a faint glow: a loose triangle, about 3.2 units across
spots = [(-1.15, -0.35), (1.1, -0.5), (0.05, 0.85)]
for i, (x, y) in enumerate(spots):
    e = bpy.data.objects.new(f"DieSpot{i}", None)
    e.location = (x, y, 0.5)
    e.empty_display_type = "CUBE"
    e.empty_display_size = 0.5
    col.objects.link(e)
# Instances of the die at the spots, for the renders only (the export takes the die once and the spots)
for i, (x, y) in enumerate(spots):
    inst = bpy.data.objects.new(f"Preview_Die{i}", die.data)
    inst.location = (x, y, 0.5)
    inst.rotation_euler = (0, 0, rng.uniform(-0.6, 0.6))
    inst.modifiers.new("Outline", "SOLIDIFY")
    for k in ("thickness", "offset", "use_flip_normals", "use_rim", "material_offset", "material_offset_rim"):
        setattr(inst.modifiers["Outline"], k, getattr(die.modifiers["Outline"], k))
    col.objects.link(inst)
die.location = (0, -4, 0.5)


def disc(name, radius, rings, segments, mat, loc=(0, 0, 0)):
    """A flat disc fading out from its middle: fade 0 at the centre, 1 at the rim"""
    bm = bmesh.new()
    centre = bm.verts.new((0, 0, 0))
    fades = [0.0]
    ring_verts = []
    for r in range(1, rings + 1):
        ring = [bm.verts.new((math.cos(a) * radius * r / rings, math.sin(a) * radius * r / rings, 0))
                for a in (2 * math.pi * s / segments for s in range(segments))]
        fades += [r / rings] * segments
        ring_verts.append(ring)
    for s in range(segments):
        bm.faces.new((centre, ring_verts[0][s], ring_verts[0][(s + 1) % segments]))
    for r in range(rings - 1):
        a, b = ring_verts[r], ring_verts[r + 1]
        for s in range(segments):
            bm.faces.new((a[s], b[s], b[(s + 1) % segments], a[(s + 1) % segments]))
    o = obj(name, bm, [mat], smooth=False, loc=loc)
    set_fade(o, fades)
    return o


disc("Glow", 2.6, 5, 32, GLOW, loc=(0, 0, 0.01))

# ---- the haze round the screen: a frame from the edge (fade 0) in to BAND (fade 1), in rings for a soft fade
bm = bmesh.new()
RINGS = 6
rings, fades = [], []
for r in range(RINGS + 1):
    inset = BAND * r / RINGS
    hw, hh = W / 2 - inset, H / 2 - inset
    # 4 corners and points along each side, so the fade follows the frame evenly
    pts = []
    for (x0, y0), (x1, y1) in (((-hw, -hh), (hw, -hh)), ((hw, -hh), (hw, hh)), ((hw, hh), (-hw, hh)), ((-hw, hh), (-hw, -hh))):
        for k in range(6):
            t = k / 6
            pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, 0))
    rings.append([bm.verts.new(p) for p in pts])
    fades += [r / RINGS] * len(pts)
for r in range(RINGS):
    a, b = rings[r], rings[r + 1]
    n = len(a)
    for s in range(n):
        bm.faces.new((a[s], a[(s + 1) % n], b[(s + 1) % n], b[s]))
haze = obj("Haze", bm, [HAZE], smooth=False)
set_fade(haze, fades)

# The twinkle: a tiny pip, a soft dot facing the camera; the mod shows it at the PipSpots, a few at a time
disc("Twinkle", 0.22, 3, 16, TWINKLE, loc=(0, -9, 0))

# Where pips twinkle: inside the haze's band, spread round the frame
placed = []
while len(placed) < 14:
    side = rng.random() * 2 * (W + H)
    depth = rng.uniform(0.25, BAND * 0.7)
    if side < W:
        p = (-W / 2 + side, -H / 2 + depth)
    elif side < W + H:
        p = (W / 2 - depth, -H / 2 + side - W)
    elif side < 2 * W + H:
        p = (W / 2 - (side - W - H), H / 2 - depth)
    else:
        p = (-W / 2 + depth, H / 2 - (side - 2 * W - H))
    if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 2.5 ** 2 for q in placed):
        placed.append(p)
for i, (x, y) in enumerate(placed):
    e = bpy.data.objects.new(f"PipSpot{i}", None)
    e.location = (x, y, 0.02)
    e.empty_display_type = "SPHERE"
    e.empty_display_size = rng.uniform(0.7, 1.2)  # its size, relative to the twinkle
    col.objects.link(e)

# A camera like the game's for the dice: orthographic, looking down at 60 degrees
cam_data = bpy.data.cameras.new("MysteryCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 7
cam = bpy.data.objects.new("MysteryCam", cam_data)
cam.location = (0, -10 * math.cos(math.radians(60)), 10 * math.sin(math.radians(60)) + 0.5)
cam.rotation_euler = (math.radians(30), 0, 0)
col.objects.link(cam)
scene.camera = cam
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("MysteryWorld")
scene.world.use_nodes = True
bg = scene.world.node_tree.nodes.get("Background")
if bg:
    bg.inputs[0].default_value = lin("#6fb336")
light = bpy.data.objects.new("MysteryLight", bpy.data.lights.new("MysteryLight", "SUN"))
light.rotation_euler = Vector((-0.55, -0.35, 0.75)).to_track_quat("Z", "Y").to_euler()
col.objects.link(light)

result = {"scene": SCENE, "objects": len(scene.objects), "pips": len(placed),
          "die_vertices": len(die.data.vertices), "haze_vertices": len(haze.data.vertices)}
