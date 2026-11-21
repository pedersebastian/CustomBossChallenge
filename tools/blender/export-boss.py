"""
Writes Gildebloon's body and animations, as built by gildebloon-boss.py, to Bundles/gildebloon-body.gba.gz for the
mod (#25). Run after the boss script, in the open Blender or in a background build (build-boss.py):

    python tools/blender/run-in-blender.py tools/blender/export-boss.py

The game can't load a model or an animation from an asset bundle (57.4), so the mod builds the body from this file
(Visuals/GildebloonBody.cs) and plays the animations itself (Visuals/GildebloonBodyData.cs). Every part is rigid: an
animation is only a position, rotation and scale per part and frame. Like the pile (export-pile.py), the toon bands
are baked: each corner gets a UV into a ramp texture the mod makes, x how lit it is by a fixed light, y its
material's row. The outlines are the shells of the Solidify modifiers.

Format, little-endian, gzipped:
  "GBA1"
  rows: int count; per row: kind (byte: 0 toon, 1 see-through), shade, base, light (3 x RGB bytes; light = base when
        there is none), cut, light cut (2 floats), alpha (float)
  parts: int count; per part: name (byte length + UTF-8), parent (int, -1 for the base under the body), flags (byte:
        1 Elite's, 2 see-through), rest position (3 floats), rotation (4 floats, x y z w), scale (3 floats), then its
        mesh: vertex count (int, 0 for an empty), positions (3 floats each), UVs (2 floats), index count, indices
  clips: int count; per clip: name, frame count (int), frames per second (float), loops (byte), track count (int);
        per track its part (int), then per frame position (3), rotation (4), scale (3), as floats
All in Unity's axes (x, z, y of Blender's, rotations mirrored to match), in Blender's units.
"""
import ast, gzip, math, os, struct
import bpy
from mathutils import Vector, Quaternion, Euler

OUT = os.path.join(REPO, "Bundles", "gildebloon-body.gba.gz")  # REPO is set by run-in-blender.py / build-boss.py
LIGHT = Vector((-0.55, -0.35, 0.75)).normalized()  # the pile's: from the top left of the screen, towards the camera
AMBIENT = 0.06
REST_FRAME = 85  # Idle's first frame: everything at rest
LOOPS = {"Idle", "Damaged66", "Damaged33", "Damaged10"}
# Not part of the body in the game: the mod draws the reach of a Repossession itself (GildebloonSeizeLook)
LEAVE_OUT = {"Reach"}
ELITE_PARTS = ("EliteBand", "EliteSpikes")

scene = bpy.data.scenes["Gildebloon Boss"]
body = bpy.data.collections["GildebloonBody"]
anims = ast.literal_eval(scene["gildebloon_anims"])


def srgb(c):
    out = []
    for x in c[:3]:
        x = max(0.0, min(1.0, x))
        x = x * 12.92 if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055
        out.append(round(x * 255))
    return out


def row_of(mat):
    """A material as a ramp row: (kind, shade, base, light, cut, light cut, alpha)"""
    nodes = mat.node_tree.nodes if mat.use_nodes and mat.node_tree else []
    ramp = next((n for n in nodes if n.type == "VALTORGB"), None)
    if ramp is not None:
        els = list(ramp.color_ramp.elements)
        shade, base = srgb(els[0].color), srgb(els[1].color)
        light = srgb(els[2].color) if len(els) > 2 else base
        return (0, shade, base, light, els[1].position, els[2].position if len(els) > 2 else 1.0, 1.0)
    em = next((n for n in nodes if n.type == "EMISSION"), None)
    colour = srgb(em.inputs[0].default_value) if em is not None else srgb(mat.diffuse_color)
    if any(n.type == "BSDF_TRANSPARENT" for n in nodes):
        alpha = 0.45 if "Shadow" in mat.name else 0.3
        return (1, colour, colour, colour, 0.0, 1.0, alpha)
    return (0, colour, colour, colour, 0.0, 1.0, 1.0)


# ---- the parts: the body's objects, parents before children
objects = [o for o in body.objects if o.name not in LEAVE_OUT]
names = {o.name for o in objects}
order = []


def visit(o):
    if o in order:
        return
    if o.parent is not None and o.parent.name in names:
        visit(o.parent)
    order.append(o)


for o in sorted(objects, key=lambda o: o.name):
    visit(o)
index = {o.name: i for i, o in enumerate(order)}


def fcurves(o):
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


curves = {o.name: {(fc.data_path, fc.array_index): fc for fc in fcurves(o)} for o in order}


def channel(o, path, i, frame):
    fc = curves[o.name].get((path, i))
    return fc.evaluate(frame) if fc is not None else getattr(o, path)[i]


def local_trs(o, frame):
    """An object's own position, rotation and scale at a frame, read from its curves (no scene evaluation needed)"""
    loc = Vector([channel(o, "location", i, frame) for i in range(3)])
    rot = [channel(o, "rotation_euler", i, frame) for i in range(3)]
    scale = Vector([channel(o, "scale", i, frame) for i in range(3)])
    for c in o.constraints:  # the shadow follows the body over the ground
        if c.type == "COPY_LOCATION" and c.target is not None:
            t = local_trs(c.target, frame)[0]
            if c.use_x:
                loc.x = t.x
            if c.use_y:
                loc.y = t.y
            if c.use_z:
                loc.z = t.z
    quat = Euler(rot, o.rotation_mode if o.rotation_mode in {"XYZ", "XZY", "YXZ", "YZX", "ZXY", "ZYX"} else "XYZ").to_quaternion()
    return loc, quat, scale


def unity_trs(loc, quat, scale):
    """Blender's (x, y, z) is Unity's (x, z, y): a mirror, so a rotation's axis swaps and its angle turns round"""
    return ((loc.x, loc.z, loc.y), (-quat.x, -quat.z, -quat.y, quat.w), (scale.x, scale.z, scale.y))


def world_rotation(o, frame):
    q = Quaternion()
    x = o
    while x is not None:
        q = local_trs(x, frame)[1] @ q
        x = x.parent if x.parent is not None and x.parent.name in names else None
    return q


# ---- the meshes, with the toon bands baked for the rest pose
rows, row_index = [], {}


def row_for(mat):
    key = mat.name.split(".")[0] if mat else "none"
    if key not in row_index:
        row_index[key] = len(rows)
        rows.append(row_of(mat) if mat else (0, [255, 200, 30], [255, 200, 30], [255, 200, 30], 0.0, 1.0, 1.0))
    return row_index[key]


depsgraph = scene.view_layers[0].depsgraph
depsgraph.update()
meshes, flags = [], []
for o in order:
    chain, x = [], o
    while x is not None:
        chain.append(x.name)
        x = x.parent
    flag = 1 if any(n in ELITE_PARTS for n in chain) else 0
    if o.type != "MESH":
        meshes.append(None)
        flags.append(flag)
        continue
    ev = o.evaluated_get(depsgraph)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    normals = me.corner_normals
    rot = world_rotation(o, REST_FRAME)
    verts, uvs, tris, seen = [], [], [], {}
    see_through = False
    for tri in me.loop_triangles:
        mat = me.materials[tri.material_index] if me.materials else None
        r = row_for(mat)
        if rows[r][0] == 1:
            see_through = True
        ids = []
        for loop in tri.loops:
            co = me.vertices[me.loops[loop].vertex_index].co
            n = (rot @ normals[loop].vector).normalized()
            lit = 0.5 if rows[r][0] == 1 or (mat and mat.name.startswith(("Gilde_Outline", "Gilde_Shine", "Gilde_Slot", "Gilde_Pupil"))) \
                else min(0.999, max(0.001, AMBIENT + max(0.0, n.dot(LIGHT))))
            p = (co.x, co.z, co.y)
            u = (round(lit, 3), r)
            k = (round(p[0], 5), round(p[1], 5), round(p[2], 5), u)
            if k not in seen:
                seen[k] = len(verts)
                verts.append(p)
                uvs.append(u)
            ids.append(seen[k])
        # Unity's front faces turn clockwise; mirrored into Unity's axes, Blender's need turning round (as the pile's)
        a, b, c = ids
        tris.append((a, c, b))
    ev.to_mesh_clear()
    meshes.append((verts, uvs, tris))
    flags.append(flag | (2 if see_through else 0))

# the rows are known now: a UV's y is its row's middle
n_rows = len(rows)
for m in meshes:
    if m is None:
        continue
    verts, uvs, tris = m
    for i, (x, r) in enumerate(uvs):
        uvs[i] = (x, (r + 0.5) / n_rows)


# ---- the clips: per animation, the parts that move in it
def sample(o, frame):
    return unity_trs(*local_trs(o, frame))


rest = [sample(o, REST_FRAME) for o in order]


def differs(a, b):
    return any(abs(x - y) > 1e-4 for u, v in zip(a, b) for x, y in zip(u, v))


clips = []
for name, a0, a1 in anims:
    frames = list(range(a0, a1 + 1))
    tracks = []
    for i, o in enumerate(order):
        if not curves[o.name] and not o.constraints:
            continue
        samples = [sample(o, f) for f in frames]
        # a quaternion and its negative are the same turn: keep neighbours on the same side, so they blend the short way
        for k in range(1, len(samples)):
            q0, q1 = samples[k - 1][1], samples[k][1]
            if sum(x * y for x, y in zip(q0, q1)) < 0:
                samples[k] = (samples[k][0], tuple(-x for x in q1), samples[k][2])
        if any(differs(s, rest[i]) for s in samples):
            tracks.append((i, samples))
    clips.append((name, len(frames), float(scene.render.fps), name in LOOPS, tracks))


def write_name(f, text):
    raw = text.encode()
    f.write(struct.pack("<B", len(raw)) + raw)


os.makedirs(os.path.dirname(OUT), exist_ok=True)
with gzip.open(OUT, "wb", compresslevel=9) as f:
    f.write(b"GBA1")
    f.write(struct.pack("<i", n_rows))
    for kind, shade, base, light, cut, light_cut, alpha in rows:
        f.write(struct.pack("<B", kind) + bytes(shade + base + light) + struct.pack("<3f", cut, light_cut, alpha))
    f.write(struct.pack("<i", len(order)))
    for i, o in enumerate(order):
        write_name(f, o.name)
        parent = index[o.parent.name] if o.parent is not None and o.parent.name in names else -1
        f.write(struct.pack("<iB", parent, flags[i]))
        pos, rot, scale = rest[i]
        f.write(struct.pack("<10f", *pos, *rot, *scale))
        m = meshes[i]
        if m is None:
            f.write(struct.pack("<i", 0))
            continue
        verts, uvs, tris = m
        f.write(struct.pack("<i", len(verts)))
        f.write(b"".join(struct.pack("<3f", *p) for p in verts))
        f.write(b"".join(struct.pack("<2f", *u) for u in uvs))
        f.write(struct.pack("<i", len(tris) * 3))
        f.write(b"".join(struct.pack("<3i", *t) for t in tris))
    f.write(struct.pack("<i", len(clips)))
    for name, count, fps, loops, tracks in clips:
        write_name(f, name)
        f.write(struct.pack("<ifBi", count, fps, 1 if loops else 0, len(tracks)))
        for part, samples in tracks:
            f.write(struct.pack("<i", part))
            f.write(b"".join(struct.pack("<10f", *p, *r, *s) for p, r, s in samples))

result = {"file": OUT, "bytes": os.path.getsize(OUT), "parts": len(order), "rows": n_rows,
          "vertices": sum(len(m[0]) for m in meshes if m), "clips": [(c[0], c[1], len(c[4])) for c in clips]}
