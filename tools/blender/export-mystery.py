"""
Writes the mystery map effects, as built by mystery-fx.py in the open Blender, to Bundles/mystery-fx.mesh.gz for the
mod (#285):

    python tools/blender/run-in-blender.py tools/blender/export-mystery.py

The game can't load a model from an asset bundle (it crashes, 57.4), so the mod builds the meshes itself from this file
(Visuals/MysteryMeshData.cs reads it, Visuals/MysteryMapFx.cs shows it). Like Gildebloon's pile, the toon look is
baked: each corner gets a UV into a ramp texture the mod makes, x how lit the corner is by a fixed light, y its
material's row. The dice tumble, so a die's corners are baked for each of the 24 ways a die can lie (ORIENTATIONS):
the mod gives a die the set for how it lies once it lands. See-through parts fade out along x instead (the mesh's
"fade" attribute). The outlines are the shells of the Solidify modifiers. Nothing is animated here: the mod moves it all.

Format "CBM1", little-endian, gzipped:
  rows: byte count; per row: kind (byte: 0 toon, 1 see-through fading out along x), shade, base, light (3 x RGB bytes;
        light = base when there is none), cut, light cut, alpha (3 floats)
  meshes: int count; per mesh: name (byte length + UTF-8), flags (byte: 1 see-through), vertex count (int), positions
        (3 floats each), the row's y (a float each), steps (byte: 1, or 24 for a die), x per step and corner (byte,
        0-255; all of step 0's corners, then step 1's...), index count (int), indices (int32, Unity's front faces)
  orientations: int count; per one a rotation (4 floats, x y z w): the way to lie each step of a 24-step mesh is for
  points: int count; per point: name, position (3 floats), size (float)
All in Unity's axes (x, z, y of Blender's), in Blender's units, each mesh round its own object's origin.
"""
import gzip, itertools, os, struct
import bpy
from mathutils import Matrix, Vector

SCENE = "Mystery Fx"
OUT = os.path.join(REPO, "Bundles", "mystery-fx.mesh.gz")  # REPO is set by run-in-blender.py / build-mystery.py
ROWS = ["Mystery_Ivory", "Mystery_Pip", "Mystery_Outline", "Mystery_Haze", "Mystery_Glow", "Mystery_Twinkle"]
MESHES = ["Die", "Glow", "Haze", "Twinkle"]
TUMBLING = {"Die"}
# The light the bands are baked for, Gildebloon's: from the top left of the game's screen, towards the camera
LIGHT = Vector((-0.55, 0.75, -0.35)).normalized()  # in Unity's axes
AMBIENT = 0.06


def unity(v):
    return Vector((v[0], v[2], v[1]))


def orientations():
    """The 24 ways a die can lie, as rotations in Unity's axes: every signed permutation matrix that turns, no mirror"""
    out = [Matrix.Identity(3)]
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product((1, -1), repeat=3):
            m = Matrix([[signs[r] if c == perm[r] else 0 for c in range(3)] for r in range(3)])
            if round(m.determinant()) == 1 and m != Matrix.Identity(3):
                out.append(m)
    assert len(out) == 24, len(out)
    return out


TURNS = orientations()


def srgb(c):
    out = []
    for x in c[:3]:
        x = max(0.0, min(1.0, x))
        x = x * 12.92 if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055
        out.append(round(x * 255))
    return out


def row_of(mat):
    if mat.get("mystery_kind", 0) == 1:
        colour = srgb(mat.diffuse_color)
        return (1, colour, colour, colour, 0.0, 1.0, float(mat["mystery_alpha"]))
    ramp = next(n for n in mat.node_tree.nodes if n.type == "VALTORGB")
    els = list(ramp.color_ramp.elements)
    shade, base = srgb(els[0].color), srgb(els[1].color)
    light = srgb(els[2].color) if len(els) > 2 else base
    return (0, shade, base, light, els[1].position, els[2].position if len(els) > 2 else 1.0, 1.0)


def collect(o):
    """A mesh's corners: position, the row's y, x for each step; and its triangles in Unity's front-face order"""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    normals = me.corner_normals
    fade = me.attributes.get("fade")
    steps = len(TURNS) if o.name in TUMBLING else 1
    positions, ys, xs, index, tris, kinds = [], [], [], {}, [], set()
    for tri in me.loop_triangles:
        mat = me.materials[tri.material_index]
        name = mat.name.split(".")[0]
        row = ROWS.index(name)
        kind = mat.get("mystery_kind", 0)
        kinds.add(kind)
        ids = []
        for loop in tri.loops:
            vi = me.loops[loop].vertex_index
            p = unity(me.vertices[vi].co)
            n = unity(normals[loop].vector).normalized()
            if kind == 1:
                x = (round(fade.data[vi].value * 255),)
            elif name == "Mystery_Outline":
                x = (128,) * steps
            else:
                x = tuple(round(255 * min(0.999, max(0.001, AMBIENT + max(0.0, (TURNS[s] @ n).dot(LIGHT)))))
                          for s in range(steps))
            y = (row + 0.5) / len(ROWS)
            key = (round(p.x, 5), round(p.y, 5), round(p.z, 5), y, x)
            if key not in index:
                index[key] = len(positions)
                positions.append(p)
                ys.append(y)
                xs.append(x)
            ids.append(index[key])
        a, b, c = (positions[i] for i in ids)
        face = unity(tri.normal).normalized()
        # Unity's front faces: cross(b - a, c - a) along the face's normal in Unity's axes (as the pile's export ends up)
        tris.append(ids if (b - a).cross(c - a).dot(face) > 0 else [ids[0], ids[2], ids[1]])
    ev.to_mesh_clear()
    assert len(kinds) == 1, f"{o.name} mixes see-through and solid materials"
    return positions, ys, xs, steps, tris, kinds == {1}


def name_bytes(s):
    raw = s.encode()
    return struct.pack("<B", len(raw)) + raw


scene = bpy.data.scenes[SCENE]
objects = {o.name: o for o in scene.objects}
points = sorted((o for o in scene.objects if o.type == "EMPTY" and o.name.startswith(("DieSpot", "PipSpot"))),
                key=lambda o: (o.name.rstrip("0123456789"), int(o.name[len(o.name.rstrip("0123456789")):])))

stats = {}
with gzip.open(OUT, "wb", compresslevel=9) as f:
    f.write(b"CBM1")
    f.write(struct.pack("<B", len(ROWS)))
    for name in ROWS:
        kind, shade, base, light, cut, light_cut, alpha = row_of(bpy.data.materials[name])
        f.write(struct.pack("<B", kind) + bytes(shade + base + light) + struct.pack("<3f", cut, light_cut, alpha))
    f.write(struct.pack("<i", len(MESHES)))
    for name in MESHES:
        positions, ys, xs, steps, tris, see_through = collect(objects[name])
        f.write(name_bytes(name) + struct.pack("<B", 1 if see_through else 0))
        f.write(struct.pack("<i", len(positions)))
        for p in positions:
            f.write(struct.pack("<3f", p.x, p.y, p.z))
        f.write(struct.pack(f"<{len(ys)}f", *ys))
        f.write(struct.pack("<B", steps))
        for s in range(steps):
            f.write(bytes(x[s] for x in xs))
        f.write(struct.pack("<i", len(tris) * 3))
        for t in tris:
            f.write(struct.pack("<3i", *t))
        stats[name] = {"vertices": len(positions), "triangles": len(tris), "steps": steps, "see_through": see_through}
    f.write(struct.pack("<i", len(TURNS)))
    for m in TURNS:
        q = m.to_quaternion()
        f.write(struct.pack("<4f", q.x, q.y, q.z, q.w))
    f.write(struct.pack("<i", len(points)))
    for o in points:
        p = unity(o.location)
        f.write(name_bytes(o.name) + struct.pack("<4f", p.x, p.y, p.z, o.empty_display_size))

result = {"file": OUT, "bytes": os.path.getsize(OUT), "meshes": stats, "points": len(points)}
